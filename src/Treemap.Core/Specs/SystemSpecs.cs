namespace Treemap.Core.Specs;

public sealed record OsInfo(string Name, string Version, string Build, string Architecture, DateTime? LastBoot);

public sealed record MachineInfo(
    string? Manufacturer,
    string? Model,
    string? BoardManufacturer,
    string? BoardProduct,
    string? BiosVendor,
    string? BiosVersion,
    DateTime? BiosDate);

public sealed record CpuInfo(
    string Name,
    string? Manufacturer,
    int Cores,
    int LogicalProcessors,
    int MaxClockMHz,
    string? Socket,
    int L2CacheKb,
    int L3CacheKb);

public sealed record GpuInfo(string Name, string? DriverVersion, long? VideoMemoryBytes);

public sealed record MemoryModule(
    string? Slot,
    long CapacityBytes,
    int SpeedMHz,
    string? Manufacturer,
    string? PartNumber,
    string Type);

public sealed record MemoryInfo(long TotalBytes, IReadOnlyList<MemoryModule> Modules);

public sealed record PhysicalDiskInfo(string Name, string Kind, string? Bus, long SizeBytes, string? Health);

public sealed record VolumeInfo(string Name, string? Label, string FileSystem, long TotalBytes, long FreeBytes)
{
    public long UsedBytes => TotalBytes - FreeBytes;

    public double UsedFraction => TotalBytes <= 0 ? 0 : (double)UsedBytes / TotalBytes;
}

/// <summary>
/// Snapshot of the machine. Every section is independent: if one query fails the rest is still filled in
/// and the reason is listed in <see cref="Warnings"/>.
/// </summary>
public sealed record SystemSpecs(
    OsInfo? Os,
    MachineInfo? Machine,
    CpuInfo? Cpu,
    IReadOnlyList<GpuInfo> Gpus,
    MemoryInfo? Memory,
    IReadOnlyList<PhysicalDiskInfo> Disks,
    IReadOnlyList<VolumeInfo> Volumes,
    IReadOnlyList<string> Warnings);
