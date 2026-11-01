using System.Management;
using Microsoft.Win32;

namespace Treemap.Core.Specs;

public interface ISpecsProvider
{
    Task<SystemSpecs> GetAsync(CancellationToken cancellationToken = default);
}

/// <summary>Reads the machine description from WMI, the registry and <see cref="DriveInfo"/>. Read-only.</summary>
public sealed class WmiSpecsProvider : ISpecsProvider
{
    private const string DisplayAdapterClassKey =
        @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

    public Task<SystemSpecs> GetAsync(CancellationToken cancellationToken = default) =>
        Task.Run(Collect, cancellationToken);

    private static SystemSpecs Collect()
    {
        var warnings = new List<string>();

        var os = Section("Système d'exploitation", warnings, ReadOs);
        var machine = Section("Carte mère et BIOS", warnings, ReadMachine);
        var cpu = Section("Processeur", warnings, ReadCpu);
        var gpus = Section("Carte graphique", warnings, ReadGpus) ?? [];
        var memory = Section("Mémoire", warnings, ReadMemory);
        var disks = Section("Disques physiques", warnings, ReadPhysicalDisks) ?? [];
        var volumes = Section("Volumes", warnings, ReadVolumes) ?? [];

        return new SystemSpecs(os, machine, cpu, gpus, memory, disks, volumes, warnings);
    }

    private static T? Section<T>(string name, List<string> warnings, Func<T?> read)
    {
        try
        {
            return read();
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or InvalidOperationException
            or System.Runtime.InteropServices.COMException or System.Security.SecurityException)
        {
            warnings.Add($"{name} : lecture impossible ({ex.Message})");
            return default;
        }
    }

    private static OsInfo? ReadOs()
    {
        var os = QueryFirst("SELECT Caption, Version, BuildNumber, OSArchitecture, LastBootUpTime FROM Win32_OperatingSystem");
        if (os is null)
        {
            return null;
        }

        return new OsInfo(
            Text(os, "Caption") ?? "Windows",
            Text(os, "Version") ?? string.Empty,
            Text(os, "BuildNumber") ?? string.Empty,
            Text(os, "OSArchitecture") ?? string.Empty,
            Date(os, "LastBootUpTime"));
    }

    private static MachineInfo? ReadMachine()
    {
        var system = QueryFirst("SELECT Manufacturer, Model FROM Win32_ComputerSystem");
        var board = QueryFirst("SELECT Manufacturer, Product FROM Win32_BaseBoard");
        var bios = QueryFirst("SELECT Manufacturer, SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS");

        return new MachineInfo(
            system is null ? null : Text(system, "Manufacturer"),
            system is null ? null : Text(system, "Model"),
            board is null ? null : Text(board, "Manufacturer"),
            board is null ? null : Text(board, "Product"),
            bios is null ? null : Text(bios, "Manufacturer"),
            bios is null ? null : Text(bios, "SMBIOSBIOSVersion"),
            bios is null ? null : Date(bios, "ReleaseDate"));
    }

    private static CpuInfo? ReadCpu()
    {
        var cpu = QueryFirst(
            "SELECT Name, Manufacturer, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed, SocketDesignation, L2CacheSize, L3CacheSize FROM Win32_Processor");
        if (cpu is null)
        {
            return null;
        }

        return new CpuInfo(
            (Text(cpu, "Name") ?? "Processeur inconnu").Trim(),
            Text(cpu, "Manufacturer"),
            (int)Number(cpu, "NumberOfCores"),
            (int)Number(cpu, "NumberOfLogicalProcessors"),
            (int)Number(cpu, "MaxClockSpeed"),
            Text(cpu, "SocketDesignation"),
            (int)Number(cpu, "L2CacheSize"),
            (int)Number(cpu, "L3CacheSize"));
    }

    private static List<GpuInfo> ReadGpus()
    {
        var gpus = new List<GpuInfo>();
        using var searcher = new ManagementObjectSearcher("SELECT Name, DriverVersion, AdapterRAM FROM Win32_VideoController");
        foreach (ManagementBaseObject gpu in searcher.Get())
        {
            var name = Text(gpu, "Name");
            if (name is null)
            {
                continue;
            }

            // Win32_VideoController.AdapterRAM is a 32-bit value capped at 4 Go; the registry holds the real size.
            long? memory = ReadVideoMemoryFromRegistry(name);
            if (memory is null)
            {
                var wmiValue = Number(gpu, "AdapterRAM");
                memory = wmiValue > 0 ? wmiValue : null;
            }

            gpus.Add(new GpuInfo(name, Text(gpu, "DriverVersion"), memory));
        }

        return gpus;
    }

    private static long? ReadVideoMemoryFromRegistry(string adapterName)
    {
        using var classKey = Registry.LocalMachine.OpenSubKey(DisplayAdapterClassKey);
        if (classKey is null)
        {
            return null;
        }

        foreach (var subKeyName in classKey.GetSubKeyNames())
        {
            using var adapterKey = classKey.OpenSubKey(subKeyName);
            if (adapterKey?.GetValue("DriverDesc") as string != adapterName)
            {
                continue;
            }

            return adapterKey.GetValue("HardwareInformation.qwMemorySize") switch
            {
                long l when l > 0 => l,
                int i when i > 0 => i,
                _ => null,
            };
        }

        return null;
    }

    private static MemoryInfo? ReadMemory()
    {
        var modules = new List<MemoryModule>();
        using (var searcher = new ManagementObjectSearcher(
            "SELECT DeviceLocator, Capacity, Speed, ConfiguredClockSpeed, Manufacturer, PartNumber, SMBIOSMemoryType FROM Win32_PhysicalMemory"))
        {
            foreach (ManagementBaseObject module in searcher.Get())
            {
                var configured = (int)Number(module, "ConfiguredClockSpeed");
                var speed = configured > 0 ? configured : (int)Number(module, "Speed");
                modules.Add(new MemoryModule(
                    Text(module, "DeviceLocator"),
                    Number(module, "Capacity"),
                    speed,
                    Text(module, "Manufacturer"),
                    Text(module, "PartNumber"),
                    SpecsMapping.MemoryType((int)Number(module, "SMBIOSMemoryType"))));
            }
        }

        var system = QueryFirst("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
        var total = system is null ? 0 : Number(system, "TotalPhysicalMemory");
        if (total == 0)
        {
            total = modules.Sum(m => m.CapacityBytes);
        }

        return total == 0 && modules.Count == 0 ? null : new MemoryInfo(total, modules);
    }

    private static List<PhysicalDiskInfo> ReadPhysicalDisks()
    {
        var disks = new List<PhysicalDiskInfo>();
        var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
        using var searcher = new ManagementObjectSearcher(
            scope,
            new ObjectQuery("SELECT FriendlyName, MediaType, BusType, Size, HealthStatus FROM MSFT_PhysicalDisk"));
        foreach (ManagementBaseObject disk in searcher.Get())
        {
            var busType = (int)Number(disk, "BusType");
            disks.Add(new PhysicalDiskInfo(
                Text(disk, "FriendlyName") ?? "Disque",
                SpecsMapping.DiskKind((int)Number(disk, "MediaType"), busType),
                SpecsMapping.BusType(busType),
                Number(disk, "Size"),
                HealthName((int)Number(disk, "HealthStatus"))));
        }

        return disks;
    }

    private static List<VolumeInfo> ReadVolumes()
    {
        var volumes = new List<VolumeInfo>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType != DriveType.Fixed || !drive.IsReady)
            {
                continue;
            }

            var label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? null : drive.VolumeLabel;
            volumes.Add(new VolumeInfo(drive.Name, label, drive.DriveFormat, drive.TotalSize, drive.AvailableFreeSpace));
        }

        return volumes;
    }

    private static string? HealthName(int status) => status switch
    {
        0 => "Sain",
        1 => "Avertissement",
        2 => "Défaillant",
        _ => null,
    };

    private static ManagementBaseObject? QueryFirst(string query)
    {
        using var searcher = new ManagementObjectSearcher(query);
        foreach (ManagementBaseObject item in searcher.Get())
        {
            return item;
        }

        return null;
    }

    private static string? Text(ManagementBaseObject item, string property)
    {
        var value = item[property]?.ToString()?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static long Number(ManagementBaseObject item, string property)
    {
        var value = item[property];
        return value is null ? 0 : Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static DateTime? Date(ManagementBaseObject item, string property)
    {
        var value = item[property] as string;
        return string.IsNullOrEmpty(value) ? null : ManagementDateTimeConverter.ToDateTime(value);
    }
}
