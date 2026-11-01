using System.Diagnostics;
using System.IO;
using Treemap.Core.Bridge;

namespace Treemap.App.Services;

/// <summary>Explorer and launcher actions for the storage screen. Neither deletes anything.</summary>
public sealed class ShellLauncher : IShellLauncher
{
    public void Reveal(string path)
    {
        var arguments = File.Exists(path) ? $"/select,\"{path}\"" : $"\"{path}\"";
        Process.Start(new ProcessStartInfo("explorer.exe", arguments) { UseShellExecute = false });
    }

    public void OpenUri(string uri) =>
        Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
}
