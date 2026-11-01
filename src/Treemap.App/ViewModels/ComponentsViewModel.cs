using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using Treemap.App.Infrastructure;
using Treemap.Core.Formatting;
using Treemap.Core.Specs;

namespace Treemap.App.ViewModels;

public sealed record SpecRow(string Label, string Value);

public sealed record SpecSection(string Title, IReadOnlyList<SpecRow> Rows);

public sealed record VolumeView(string Name, string Caption, string Detail, double UsedFraction, string PercentText);

public sealed class ComponentsViewModel : ObservableObject
{
    private readonly ISpecsProvider _provider;
    private SystemSpecs? _specs;
    private bool _isLoading;
    private string _statusText = string.Empty;

    public ComponentsViewModel(ISpecsProvider provider)
    {
        _provider = provider;
        RefreshCommand = new AsyncRelayCommand(_ => LoadAsync());
        CopySummaryCommand = new RelayCommand(_ => CopySummary(), _ => _specs is not null);
    }

    public ObservableCollection<SpecSection> Sections { get; } = [];

    public ObservableCollection<VolumeView> Volumes { get; } = [];

    public ObservableCollection<string> Warnings { get; } = [];

    public ICommand RefreshCommand { get; }

    public ICommand CopySummaryCommand { get; }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        StatusText = "Lecture des composants…";
        try
        {
            var specs = await _provider.GetAsync();
            Apply(specs);
        }
        catch (Exception ex)
        {
            // Last line of defence at the UI boundary: a failed refresh must not take the window down.
            StatusText = $"Lecture impossible : {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void Apply(SystemSpecs specs)
    {
        _specs = specs;

        Sections.Clear();
        foreach (var section in SpecsPresenter.Sections(specs))
        {
            Sections.Add(section);
        }

        Volumes.Clear();
        foreach (var volume in SpecsPresenter.Volumes(specs))
        {
            Volumes.Add(volume);
        }

        Warnings.Clear();
        foreach (var warning in specs.Warnings)
        {
            Warnings.Add(warning);
        }

        StatusText = $"Mis à jour à {DateTime.Now:HH:mm:ss}";
    }

    private void CopySummary()
    {
        if (_specs is null)
        {
            return;
        }

        Clipboard.SetText(SpecsSummary.ToPlainText(_specs));
        StatusText = "Résumé copié dans le presse-papiers";
    }
}

/// <summary>Turns the raw specs into labelled rows ready to display.</summary>
public static partial class SpecsPresenter
{
    [GeneratedRegex("^[0-9A-Fa-f]{4}$")]
    private static partial Regex HexId();

    public static IReadOnlyList<SpecSection> Sections(SystemSpecs specs, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        var sections = new List<SpecSection>();

        if (specs.Cpu is { } cpu)
        {
            var rows = new List<SpecRow>
            {
                new("Modèle", cpu.Name),
                new("Cœurs / threads", $"{cpu.Cores} / {cpu.LogicalProcessors}"),
                new("Fréquence max", $"{(cpu.MaxClockMHz / 1000.0).ToString("N2", culture)} GHz"),
            };
            if (cpu.L2CacheKb > 0 || cpu.L3CacheKb > 0)
            {
                rows.Add(new("Cache L2 / L3",
                    $"{ByteSize.Format(cpu.L2CacheKb * 1024L, culture)} / {ByteSize.Format(cpu.L3CacheKb * 1024L, culture)}"));
            }

            sections.Add(new SpecSection("Processeur", rows));
        }

        for (var i = 0; i < specs.Gpus.Count; i++)
        {
            var gpu = specs.Gpus[i];
            var rows = new List<SpecRow> { new("Modèle", gpu.Name) };
            if (gpu.VideoMemoryBytes is { } vram)
            {
                rows.Add(new("Mémoire vidéo", ByteSize.Format(vram, culture)));
            }

            AddIf(rows, "Pilote", gpu.DriverVersion);
            sections.Add(new SpecSection(specs.Gpus.Count > 1 ? $"Carte graphique {i + 1}" : "Carte graphique", rows));
        }

        if (specs.Memory is { } memory)
        {
            var rows = new List<SpecRow> { new("Total", ByteSize.Format(memory.TotalBytes, culture)) };
            foreach (var module in memory.Modules)
            {
                var detail = $"{ByteSize.Format(module.CapacityBytes, culture)} {module.Type}";
                if (module.SpeedMHz > 0)
                {
                    detail += $" {module.SpeedMHz} MHz";
                }

                // Some modules report their JEDEC manufacturer id ("01BA") instead of a name: not worth showing.
                var manufacturer = module.Manufacturer is { } m && HexId().IsMatch(m) ? null : module.Manufacturer;
                var maker = string.Join(' ', new[] { manufacturer, module.PartNumber }.Where(s => !string.IsNullOrWhiteSpace(s)));
                if (maker.Length > 0)
                {
                    detail += $" · {maker}";
                }

                rows.Add(new(module.Slot ?? "Barrette", detail));
            }

            sections.Add(new SpecSection("Mémoire", rows));
        }

        if (specs.Machine is { } machine)
        {
            var rows = new List<SpecRow>();
            AddIf(rows, "Machine", Join(machine.Manufacturer, machine.Model));
            AddIf(rows, "Carte mère", Join(machine.BoardManufacturer, machine.BoardProduct));
            AddIf(rows, "BIOS", Join(machine.BiosVendor, machine.BiosVersion));
            if (machine.BiosDate is { } date)
            {
                rows.Add(new("Date du BIOS", date.ToString("d", culture)));
            }

            if (rows.Count > 0)
            {
                sections.Add(new SpecSection("Carte mère et BIOS", rows));
            }
        }

        if (specs.Disks.Count > 0)
        {
            var rows = specs.Disks
                .Select(d => new SpecRow(
                    d.Name,
                    string.Join(" · ", new[] { d.Kind, ByteSize.Format(d.SizeBytes, culture), d.Health }.Where(s => !string.IsNullOrEmpty(s)))))
                .ToList();
            sections.Add(new SpecSection("Disques physiques", rows));
        }

        if (specs.Os is { } os)
        {
            var rows = new List<SpecRow>
            {
                new("Windows", os.Name.Replace("Microsoft ", string.Empty, StringComparison.Ordinal)),
                new("Version", $"{os.Version} (build {os.Build})"),
                new("Architecture", os.Architecture),
            };
            if (os.LastBoot is { } boot)
            {
                rows.Add(new("Allumé depuis", Uptime(DateTime.Now - boot)));
            }

            sections.Add(new SpecSection("Système", rows));
        }

        return sections;
    }

    public static IReadOnlyList<VolumeView> Volumes(SystemSpecs specs, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        return specs.Volumes
            .Select(v => new VolumeView(
                v.Name.TrimEnd('\\'),
                v.Label is null ? v.FileSystem : $"{v.Label} · {v.FileSystem}",
                $"{ByteSize.Format(v.FreeBytes, culture)} libres sur {ByteSize.Format(v.TotalBytes, culture)}",
                v.UsedFraction,
                v.UsedFraction.ToString("P0", culture)))
            .ToList();
    }

    private static void AddIf(List<SpecRow> rows, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            rows.Add(new SpecRow(label, value));
        }
    }

    private static string Join(params string?[] parts) =>
        string.Join(' ', parts.Where(p => !string.IsNullOrWhiteSpace(p)));

    private static string Uptime(TimeSpan span) =>
        span.TotalDays >= 1
            ? $"{(int)span.TotalDays} j {span.Hours} h"
            : span.TotalHours >= 1 ? $"{(int)span.TotalHours} h {span.Minutes} min" : $"{span.Minutes} min";
}
