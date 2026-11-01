namespace Treemap.Core.Specs;

/// <summary>Turns the numeric codes returned by WMI into readable names.</summary>
public static class SpecsMapping
{
    /// <summary>SMBIOS memory type (Win32_PhysicalMemory.SMBIOSMemoryType).</summary>
    public static string MemoryType(int smbiosType) => smbiosType switch
    {
        18 => "DDR",
        19 => "DDR2",
        24 => "DDR3",
        26 => "DDR4",
        27 => "LPDDR",
        28 => "LPDDR2",
        29 => "LPDDR3",
        30 => "LPDDR4",
        34 => "DDR5",
        35 => "LPDDR5",
        _ => "Inconnu",
    };

    /// <summary>MSFT_PhysicalDisk.BusType.</summary>
    public static string? BusType(int busType) => busType switch
    {
        3 => "ATA",
        7 => "USB",
        8 => "RAID",
        10 => "SAS",
        11 => "SATA",
        17 => "NVMe",
        _ => null,
    };

    /// <summary>Combines MSFT_PhysicalDisk.MediaType (3 = HDD, 4 = SSD, 5 = SCM) and the bus type.</summary>
    public static string DiskKind(int mediaType, int busType) => (mediaType, busType) switch
    {
        (_, 17) => "SSD NVMe",
        (4, _) => "SSD",
        (3, _) => "HDD",
        (5, _) => "Mémoire persistante",
        _ => "Disque",
    };
}
