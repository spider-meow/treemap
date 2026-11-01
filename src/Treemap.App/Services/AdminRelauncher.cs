using System.ComponentModel;
using System.Diagnostics;
using System.Windows;

namespace Treemap.App.Services;

/// <summary>Restarts the app with administrator rights. Windows shows its own consent prompt; nothing is bypassed.</summary>
public static class AdminRelauncher
{
    private const int ErrorCancelled = 1223;

    /// <summary>Returns false when the user declined the prompt (the app then keeps running as it was).</summary>
    public static bool Relaunch(string arguments)
    {
        var path = Environment.ProcessPath;
        if (path is null)
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo(path, arguments) { UseShellExecute = true, Verb = "runas" });
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            return false;
        }

        Application.Current.Shutdown();
        return true;
    }
}
