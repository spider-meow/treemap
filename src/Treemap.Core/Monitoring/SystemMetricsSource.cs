using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace Treemap.Core.Monitoring;

public interface IMetricsSource : IDisposable
{
    MetricsSnapshot Sample();
}

/// <summary>
/// Reads CPU, memory, disk, network and (when an NVIDIA GPU is present) GPU figures. Everything here works without
/// administrator rights. CPU temperature is deliberately absent: Windows exposes it only through a kernel driver.
/// </summary>
public sealed class SystemMetricsSource : IMetricsSource
{
    private readonly NvmlGpuReader? _gpu = NvmlGpuReader.TryCreate();
    private readonly PerformanceCounter? _diskRead = TryCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total");
    private readonly PerformanceCounter? _diskWrite = TryCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total");
    private readonly PerformanceCounter? _diskActive = TryCounter("PhysicalDisk", "% Disk Time", "_Total");
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private (ulong Idle, ulong Kernel, ulong User)? _previousCpu;
    private (long Received, long Sent)? _previousNetwork;
    private TimeSpan _previousNetworkTime;

    public SystemMetricsSource()
    {
        // Rate counters need one read to establish a baseline; the first real value comes with the next sample.
        _ = SafeNext(_diskRead);
        _ = SafeNext(_diskWrite);
        _ = SafeNext(_diskActive);
        _previousCpu = ReadCpuTimes();
        _previousNetwork = ReadNetworkTotals();
        _previousNetworkTime = _clock.Elapsed;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low;
        public uint High;

        public readonly ulong Value => ((ulong)High << 32) | Low;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx status);

    public MetricsSnapshot Sample()
    {
        var cpu = ReadCpuLoad();
        var (memoryUsed, memoryTotal) = ReadMemory();
        var (down, up) = ReadNetworkRates();

        return new MetricsSnapshot(
            DateTime.Now,
            cpu,
            memoryUsed,
            memoryTotal,
            SafeNext(_diskRead),
            SafeNext(_diskWrite),
            Math.Clamp(SafeNext(_diskActive), 0, 100),
            down,
            up,
            _gpu?.Read());
    }

    public void Dispose()
    {
        _gpu?.Dispose();
        _diskRead?.Dispose();
        _diskWrite?.Dispose();
        _diskActive?.Dispose();
    }

    private double ReadCpuLoad()
    {
        var current = ReadCpuTimes();
        var previous = _previousCpu;
        _previousCpu = current;
        if (previous is null || current is null)
        {
            return 0;
        }

        // The kernel figure already includes idle time.
        var total = (current.Value.Kernel - previous.Value.Kernel) + (current.Value.User - previous.Value.User);
        var idle = current.Value.Idle - previous.Value.Idle;
        return total == 0 ? 0 : Math.Clamp(100.0 * (total - idle) / total, 0, 100);
    }

    private static (ulong Idle, ulong Kernel, ulong User)? ReadCpuTimes() =>
        GetSystemTimes(out var idle, out var kernel, out var user) ? (idle.Value, kernel.Value, user.Value) : null;

    private static (long Used, long Total) ReadMemory()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status))
        {
            return (0, 0);
        }

        return ((long)(status.TotalPhys - status.AvailPhys), (long)status.TotalPhys);
    }

    private (double Down, double Up) ReadNetworkRates()
    {
        var current = ReadNetworkTotals();
        var now = _clock.Elapsed;
        var previous = _previousNetwork;
        var seconds = (now - _previousNetworkTime).TotalSeconds;
        _previousNetwork = current;
        _previousNetworkTime = now;

        if (previous is null || seconds <= 0)
        {
            return (0, 0);
        }

        // A counter going backwards means an adapter appeared or vanished: report nothing rather than a huge spike.
        var received = current.Received - previous.Value.Received;
        var sent = current.Sent - previous.Value.Sent;
        return (received < 0 ? 0 : received / seconds, sent < 0 ? 0 : sent / seconds);
    }

    private static (long Received, long Sent) ReadNetworkTotals()
    {
        long received = 0;
        long sent = 0;
        foreach (var network in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (network.OperationalStatus != OperationalStatus.Up
                || network.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            try
            {
                var statistics = network.GetIPv4Statistics();
                received += statistics.BytesReceived;
                sent += statistics.BytesSent;
            }
            catch (NetworkInformationException)
            {
                // Adapter without IPv4 statistics (virtual or disconnected): nothing to add.
            }
        }

        return (received, sent);
    }

    private static PerformanceCounter? TryCounter(string category, string counter, string instance)
    {
        try
        {
            return new PerformanceCounter(category, counter, instance, readOnly: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static double SafeNext(PerformanceCounter? counter)
    {
        try
        {
            return counter?.NextValue() ?? 0;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return 0;
        }
    }
}
