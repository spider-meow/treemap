using System.Runtime.InteropServices;

namespace Treemap.Core.Storage.Cleanup;

/// <param name="Success">The item is in the Recycle Bin.</param>
/// <param name="Declined">Windows asked (for instance because the item is too big for the bin and would be destroyed) and the user said no.</param>
public sealed record RecycleResult(bool Success, bool Declined = false, string? Error = null);

/// <summary>Sends one file or folder to the Recycle Bin. The only way this app ever deletes anything.</summary>
public interface IRecycler
{
    RecycleResult Recycle(string path);
}

/// <summary>
/// Recycle Bin through the shell, so Windows keeps the original location and "Restore" works.
/// Windows is asked to warn instead of silently destroying an item it cannot recycle (too big for the bin, bin disabled).
/// </summary>
public sealed class ShellRecycler : IRecycler
{
    private const uint FoWDelete = 0x0003;
    private const ushort FofSilent = 0x0004;
    private const ushort FofNoErrorUi = 0x0400;
    private const ushort FofAllowUndo = 0x0040;
    private const ushort FofWantNukeWarning = 0x4000;

    public RecycleResult Recycle(string path)
    {
        // The shell wants a double-null-terminated list and does not support long paths.
        if (path.Length >= 260)
        {
            return new RecycleResult(false, Error: "Chemin trop long pour la corbeille de Windows.");
        }

        var operation = new ShFileOpStruct
        {
            Func = FoWDelete,
            From = path + "\0\0",
            Flags = (ushort)(FofAllowUndo | FofWantNukeWarning | FofSilent | FofNoErrorUi),
        };

        var code = SHFileOperationW(ref operation);
        if (code != 0)
        {
            return new RecycleResult(false, Error: $"Windows n'a pas pu l'envoyer à la corbeille (code 0x{code:X}).");
        }

        return operation.AnyOperationsAborted
            ? new RecycleResult(false, Declined: true)
            : new RecycleResult(true);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileOpStruct
    {
        public nint Hwnd;
        public uint Func;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string From;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? To;

        public ushort Flags;

        [MarshalAs(UnmanagedType.Bool)]
        public bool AnyOperationsAborted;

        public nint NameMappings;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? ProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperationW(ref ShFileOpStruct operation);
}
