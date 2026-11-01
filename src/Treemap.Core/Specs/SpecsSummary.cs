using System.Globalization;
using System.Text;
using Treemap.Core.Formatting;

namespace Treemap.Core.Specs;

/// <summary>Plain-text rendering of the specs, meant to be copied into a message or a listing.</summary>
public static class SpecsSummary
{
    public static string ToPlainText(SystemSpecs specs, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        var text = new StringBuilder();

        if (specs.Os is { } os)
        {
            text.AppendLine($"Système : {os.Name} {os.Version} (build {os.Build}, {os.Architecture})");
        }

        if (specs.Machine is { } machine)
        {
            text.AppendLine($"Machine : {Join(machine.Manufacturer, machine.Model)}");
            text.AppendLine($"Carte mère : {Join(machine.BoardManufacturer, machine.BoardProduct)}");
            text.AppendLine($"BIOS : {Join(machine.BiosVendor, machine.BiosVersion)}"
                + (machine.BiosDate is { } date ? $" ({date:yyyy-MM-dd})" : string.Empty));
        }

        if (specs.Cpu is { } cpu)
        {
            var ghz = (cpu.MaxClockMHz / 1000.0).ToString("N2", culture);
            text.AppendLine($"Processeur : {cpu.Name} — {cpu.Cores} cœurs / {cpu.LogicalProcessors} threads, {ghz} GHz");
        }

        foreach (var gpu in specs.Gpus)
        {
            var memory = gpu.VideoMemoryBytes is { } bytes ? $", {ByteSize.Format(bytes, culture)}" : string.Empty;
            text.AppendLine($"Carte graphique : {gpu.Name}{memory}");
        }

        if (specs.Memory is { } ram)
        {
            text.AppendLine($"Mémoire : {ByteSize.Format(ram.TotalBytes, culture)}");
            foreach (var module in ram.Modules)
            {
                text.AppendLine(
                    $"  - {module.Slot}: {ByteSize.Format(module.CapacityBytes, culture)} {module.Type} {module.SpeedMHz} MHz {Join(module.Manufacturer, module.PartNumber)}".TrimEnd());
            }
        }

        foreach (var disk in specs.Disks)
        {
            text.AppendLine($"Disque : {disk.Name} — {disk.Kind}, {ByteSize.Format(disk.SizeBytes, culture)}");
        }

        foreach (var volume in specs.Volumes)
        {
            text.AppendLine(
                $"Volume {volume.Name} : {ByteSize.Format(volume.FreeBytes, culture)} libres sur {ByteSize.Format(volume.TotalBytes, culture)} ({volume.FileSystem})");
        }

        return text.ToString().TrimEnd();
    }

    private static string Join(params string?[] parts) =>
        string.Join(' ', parts.Where(p => !string.IsNullOrWhiteSpace(p)));
}
