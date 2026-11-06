using System.Runtime.InteropServices;

namespace Treemap.Core.Storage.Cleanup;

public sealed record RecycleBinInfo(long SizeBytes, long ItemCount);

/// <summary>The Windows Recycle Bin as a whole: how full it is, and emptying it (which is permanent).</summary>
public interface IRecycleBin
{
    RecycleBinInfo Query();

    /// <summary>Empties the bin for every drive. Returns an error message, or null on success.</summary>
    string? Empty();
}

public sealed class ShellRecycleBin : IRecycleBin
{
    private const uint NoConfirmation = 0x1;
    private const uint NoProgressUi = 0x2;
    private const uint NoSound = 0x4;
    private const int ErrorEmpty = unchecked((int)0x80270002); // E_... "already empty" is not a failure

    public RecycleBinInfo Query()
    {
        var info = new ShQueryRbInfo { Size = Marshal.SizeOf<ShQueryRbInfo>() };
        return SHQueryRecycleBinW(null, ref info) == 0
            ? new RecycleBinInfo(info.SizeBytes, info.ItemCount)
            : new RecycleBinInfo(0, 0);
    }

    public string? Empty()
    {
        var code = SHEmptyRecycleBinW(IntPtr.Zero, null, NoConfirmation | NoProgressUi | NoSound);
        return code == 0 || code == ErrorEmpty ? null : $"Windows n'a pas pu vider la Corbeille (code 0x{code:X}).";
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ShQueryRbInfo
    {
        public int Size;
        public long SizeBytes;
        public long ItemCount;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBinW(string? rootPath, ref ShQueryRbInfo info);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBinW(IntPtr hwnd, string? rootPath, uint flags);
}
