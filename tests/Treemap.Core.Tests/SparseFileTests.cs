using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Treemap.Core.Storage;

namespace Treemap.Core.Tests;

public sealed class SparseFileTests : IDisposable
{
    private const uint FsctlSetSparse = 0x000900C4;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "treemap-sparse-" + Guid.NewGuid().ToString("N"));

    public SparseFileTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle device,
        uint controlCode,
        IntPtr inBuffer,
        int inBufferSize,
        IntPtr outBuffer,
        int outBufferSize,
        out int bytesReturned,
        IntPtr overlapped);

    /// <summary>Creates a file that claims <paramref name="logicalLength"/> bytes but has (almost) nothing allocated.</summary>
    private string CreateSparseFile(string name, long logicalLength)
    {
        var path = Path.Combine(_root, name);
        using var stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite);
        Assert.True(
            DeviceIoControl(stream.SafeFileHandle, FsctlSetSparse, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero),
            "Could not mark the file as sparse (is the temp folder on NTFS?)");
        stream.SetLength(logicalLength);
        return path;
    }

    [Fact]
    public async Task Scan_CountsSparseFilesBySizeOnDisk()
    {
        const long logical = 256L * 1024 * 1024;
        var path = CreateSparseFile("disk.img", logical);
        Assert.Equal(logical, new FileInfo(path).Length);
        Assert.True(new FileInfo(path).Attributes.HasFlag(FileAttributes.SparseFile));

        var result = await new DiskScanner().ScanAsync(_root);

        Assert.True(result.Root.SizeBytes < logical / 10, $"counted {result.Root.SizeBytes} bytes for a mostly empty sparse file");
        Assert.True(result.SavedBySparseOrCompressedFiles > logical * 9 / 10);
    }

    [Fact]
    public async Task Scan_RegularFilesKeepTheirLength()
    {
        File.WriteAllBytes(Path.Combine(_root, "plain.bin"), new byte[4096]);

        var result = await new DiskScanner().ScanAsync(_root);

        Assert.Equal(4096, result.Root.SizeBytes);
        Assert.Equal(0, result.SavedBySparseOrCompressedFiles);
    }
}
