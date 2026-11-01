using System.Runtime.InteropServices;

namespace Treemap.Core.Storage;

/// <summary>
/// Sparse and NTFS-compressed files report a logical length far above what they really occupy
/// (an Android emulator disk image can claim 512 Go and use 3 Go). For those, the allocated size is what
/// matters when the question is "why is my disk full".
/// </summary>
internal static class SizeOnDisk
{
    private const FileAttributes NeedsAllocatedSize = FileAttributes.SparseFile | FileAttributes.Compressed;
    private const uint InvalidFileSize = 0xFFFFFFFF;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetCompressedFileSizeW(string fileName, out uint fileSizeHigh);

    /// <summary>
    /// Returns the space the file really takes. Regular files are not queried (one extra system call per file
    /// would slow a whole-drive scan for no gain); <paramref name="logicalLength"/> is returned as is.
    /// </summary>
    public static long Of(FileInfo file, long logicalLength)
    {
        if ((file.Attributes & NeedsAllocatedSize) == 0)
        {
            return logicalLength;
        }

        var low = GetCompressedFileSizeW(file.FullName, out var high);
        if (low == InvalidFileSize && Marshal.GetLastWin32Error() != 0)
        {
            return logicalLength;
        }

        var allocated = ((long)high << 32) | low;
        return Math.Min(allocated, logicalLength);
    }
}
