using System.Globalization;
using Treemap.Core.Specs;

namespace Treemap.Core.Tests;

public class SpecsTests
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    [Theory]
    [InlineData(24, "DDR3")]
    [InlineData(26, "DDR4")]
    [InlineData(34, "DDR5")]
    [InlineData(30, "LPDDR4")]
    [InlineData(0, "Inconnu")]
    [InlineData(999, "Inconnu")]
    public void MemoryType_MapsSmbiosCodes(int code, string expected)
    {
        Assert.Equal(expected, SpecsMapping.MemoryType(code));
    }

    [Theory]
    [InlineData(4, 17, "SSD NVMe")]
    [InlineData(0, 17, "SSD NVMe")]
    [InlineData(4, 11, "SSD")]
    [InlineData(3, 11, "HDD")]
    [InlineData(0, 0, "Disque")]
    public void DiskKind_CombinesMediaAndBus(int media, int bus, string expected)
    {
        Assert.Equal(expected, SpecsMapping.DiskKind(media, bus));
    }

    [Fact]
    public void BusType_ReturnsNullWhenUnknown()
    {
        Assert.Equal("NVMe", SpecsMapping.BusType(17));
        Assert.Null(SpecsMapping.BusType(255));
    }

    [Fact]
    public void Volume_ComputesUsedSpace()
    {
        var volume = new VolumeInfo("C:\\", null, "NTFS", TotalBytes: 1000, FreeBytes: 250);

        Assert.Equal(750, volume.UsedBytes);
        Assert.Equal(0.75, volume.UsedFraction, precision: 6);
    }

    [Fact]
    public void Volume_WithZeroTotal_HasZeroUsedFraction()
    {
        Assert.Equal(0, new VolumeInfo("X:\\", null, "NTFS", 0, 0).UsedFraction);
    }

    [Fact]
    public void Summary_ListsEverySectionPresent()
    {
        var specs = new SystemSpecs(
            new OsInfo("Microsoft Windows 11 Famille", "10.0.26200", "26200", "64 bits", null),
            new MachineInfo("ASUS", "Prime", "ASUSTeK", "PRIME B460M", "AMI", "1.2", new DateTime(2021, 3, 4)),
            new CpuInfo("Intel Core i5-10400F", "Intel", 6, 12, 2904, "LGA1200", 1536, 12288),
            [new GpuInfo("NVIDIA GeForce RTX 3060", "32.0.15.6", 12L * 1024 * 1024 * 1024)],
            new MemoryInfo(16L * 1024 * 1024 * 1024, [new MemoryModule("DIMM A1", 8L * 1024 * 1024 * 1024, 2666, "Kingston", "KF426", "DDR4")]),
            [new PhysicalDiskInfo("Samsung SSD 980", "SSD NVMe", "NVMe", 1000L * 1024 * 1024 * 1024, "Sain")],
            [new VolumeInfo("C:\\", null, "NTFS", 930L * 1024 * 1024 * 1024, 105L * 1024 * 1024 * 1024)],
            []);

        var text = SpecsSummary.ToPlainText(specs, Fr);

        Assert.Contains("Windows 11", text);
        Assert.Contains("i5-10400F", text);
        Assert.Contains("RTX 3060", text);
        Assert.Contains("12,0 Go", text);
        Assert.Contains("2,90 GHz", text);
        Assert.Contains("DDR4", text);
        Assert.Contains("SSD NVMe", text);
        Assert.Contains("105 Go libres sur 930 Go", text);
    }

    [Fact]
    public void Summary_SkipsMissingSections()
    {
        var specs = new SystemSpecs(null, null, null, [], null, [], [], []);

        Assert.Equal(string.Empty, SpecsSummary.ToPlainText(specs, Fr));
    }

    [Fact]
    public async Task WmiProvider_ReadsThisMachine()
    {
        var specs = await new WmiSpecsProvider().GetAsync();

        Assert.NotNull(specs.Os);
        Assert.NotNull(specs.Cpu);
        Assert.True(specs.Cpu.LogicalProcessors > 0);
        Assert.NotNull(specs.Memory);
        Assert.True(specs.Memory.TotalBytes > 0);
        Assert.NotEmpty(specs.Volumes);
        Assert.All(specs.Volumes, v => Assert.True(v.TotalBytes > 0));
    }
}
