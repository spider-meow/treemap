using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Treemap.Core.Booster;

/// <summary>What the booster needs from the operating system's processes. Faked in tests.</summary>
public interface IProcessSystem
{
    int CurrentProcessId { get; }

    string WindowsDirectory { get; }

    IReadOnlyList<RunningProcess> List();

    bool IsRunning(int processId);

    /// <summary>Asks the main window to close and waits; false when the application is still running afterwards. Nothing is killed.</summary>
    bool CloseGracefully(int processId, TimeSpan wait);

    bool RaisePriority(int processId);

    bool Start(string path);
}

public sealed record PowerPlan(string Guid, string Name);

public interface IPowerPlans
{
    PowerPlan? Active();

    bool Exists(string guid);

    bool Activate(string guid);
}

public sealed class WindowsProcessSystem : IProcessSystem
{
    public int CurrentProcessId => Environment.ProcessId;

    public string WindowsDirectory => Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    public IReadOnlyList<RunningProcess> List()
    {
        var list = new List<RunningProcess>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    list.Add(new RunningProcess(process.Id, process.ProcessName, PathOf(process), TitleOf(process), process.WorkingSet64));
                }
                catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
                {
                    // Exited while listing, or not readable: it stays out of the list.
                }
            }
        }

        return list;
    }

    public bool IsRunning(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
        {
            return false;
        }
    }

    public bool CloseGracefully(int processId, TimeSpan wait)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            process.CloseMainWindow();
            return process.WaitForExit(wait);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
        {
            return !IsRunning(processId);
        }
    }

    public bool RaisePriority(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            process.PriorityClass = ProcessPriorityClass.High;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
        {
            return false;
        }
    }

    public bool Start(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            return false;
        }
    }

    private static string? PathOf(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            return null; // Protected process, or another bitness: unknown path means the booster leaves it alone.
        }
    }

    private static string? TitleOf(Process process)
    {
        try
        {
            return process.MainWindowTitle;
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            return null;
        }
    }
}

/// <summary>The Windows power plans, through <c>powercfg</c> (no administrator rights needed to switch the plan).</summary>
public sealed partial class PowerCfg : IPowerPlans
{
    public const string HighPerformanceGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

    public PowerPlan? Active() => ParsePlan(Run("/getactivescheme"));

    public bool Exists(string guid) => Run("/list").Contains(guid, StringComparison.OrdinalIgnoreCase);

    public bool Activate(string guid) =>
        Guid.TryParse(guid, out var parsed) && RunExit($"/setactive {parsed}") == 0;

    /// <summary>"Power Scheme GUID: 381b4222-... (Balanced)" or the localised equivalent.</summary>
    public static PowerPlan? ParsePlan(string output)
    {
        var match = PlanLine().Match(output);
        return match.Success ? new PowerPlan(match.Groups[1].Value.ToLowerInvariant(), match.Groups[2].Value.Trim()) : null;
    }

    private static string Run(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("powercfg", arguments)
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (process is null)
            {
                return string.Empty;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            return output;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return string.Empty;
        }
    }

    private static int RunExit(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("powercfg", arguments) { UseShellExecute = false, CreateNoWindow = true });
            if (process is null)
            {
                return -1;
            }

            return process.WaitForExit(5000) ? process.ExitCode : -1;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return -1;
        }
    }

    [GeneratedRegex(@"([0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12})\s*\(([^)]*)\)")]
    private static partial Regex PlanLine();
}
