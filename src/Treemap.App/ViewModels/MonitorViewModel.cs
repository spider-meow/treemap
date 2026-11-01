using System.Collections.ObjectModel;
using Treemap.App.Infrastructure;
using Treemap.App.Services;
using Treemap.Core.Formatting;

namespace Treemap.App.ViewModels;

public sealed class MetricCardViewModel : ObservableObject
{
    private string _valueText = "—";
    private string _detailText = string.Empty;
    private IReadOnlyList<double> _values = [];

    public MetricCardViewModel(string title, double maximum = 0)
    {
        Title = title;
        Maximum = maximum;
    }

    public string Title { get; }

    /// <summary>Top of the graph; 0 lets the graph fit its data (used for rates).</summary>
    public double Maximum { get; }

    public string ValueText
    {
        get => _valueText;
        set => SetProperty(ref _valueText, value);
    }

    public string DetailText
    {
        get => _detailText;
        set => SetProperty(ref _detailText, value);
    }

    public IReadOnlyList<double> Values
    {
        get => _values;
        set => SetProperty(ref _values, value);
    }
}

public sealed class MonitorViewModel : ObservableObject
{
    private readonly MonitoringService _monitoring;

    public MonitorViewModel(MonitoringService monitoring, OverlaySettingsViewModel overlay)
    {
        _monitoring = monitoring;
        Overlay = overlay;
        Cards = [Cpu, Memory, Gpu, VideoMemory, Disk, Network];
        _monitoring.Updated += (_, _) => Refresh();
    }

    public OverlaySettingsViewModel Overlay { get; }

    public MetricCardViewModel Cpu { get; } = new("Processeur", maximum: 100);

    public MetricCardViewModel Memory { get; } = new("Mémoire", maximum: 100);

    public MetricCardViewModel Gpu { get; } = new("Carte graphique", maximum: 100);

    public MetricCardViewModel VideoMemory { get; } = new("Mémoire vidéo", maximum: 100);

    public MetricCardViewModel Disk { get; } = new("Disque");

    public MetricCardViewModel Network { get; } = new("Réseau");

    public ObservableCollection<MetricCardViewModel> Cards { get; }

    private void Refresh()
    {
        if (_monitoring.Latest is not { } s)
        {
            return;
        }

        Cpu.ValueText = $"{s.CpuLoadPercent:N0} %";
        Cpu.DetailText = "Température : nécessite un pilote noyau, non activé.";
        Cpu.Values = _monitoring.Cpu.ToList();

        Memory.ValueText = $"{s.MemoryPercent:N0} %";
        Memory.DetailText = $"{ByteSize.Format(s.MemoryUsedBytes)} utilisés sur {ByteSize.Format(s.MemoryTotalBytes)}";
        Memory.Values = _monitoring.Memory.ToList();

        if (s.Gpu is { } gpu)
        {
            Gpu.ValueText = $"{gpu.LoadPercent:N0} %";
            Gpu.DetailText = GpuDetail(gpu);
            Gpu.Values = _monitoring.GpuLoad.ToList();

            VideoMemory.ValueText = $"{gpu.MemoryPercent:N0} %";
            VideoMemory.DetailText = $"{ByteSize.Format(gpu.MemoryUsedBytes)} utilisés sur {ByteSize.Format(gpu.MemoryTotalBytes)}";
            VideoMemory.Values = _monitoring.VideoMemory.ToList();
        }
        else
        {
            Gpu.DetailText = "Aucune carte NVIDIA détectée.";
            VideoMemory.DetailText = "Aucune carte NVIDIA détectée.";
        }

        Disk.ValueText = $"{s.DiskActivePercent:N0} %";
        Disk.DetailText = $"Lecture {ByteSize.Format((long)s.DiskReadBytesPerSecond)}/s · Écriture {ByteSize.Format((long)s.DiskWriteBytesPerSecond)}/s";
        Disk.Values = _monitoring.DiskRead.ToList().Zip(_monitoring.DiskWrite.ToList(), (r, w) => r + w).ToList();

        Network.ValueText = $"↓ {ByteSize.Format((long)s.NetworkDownBytesPerSecond)}/s";
        Network.DetailText = $"↑ {ByteSize.Format((long)s.NetworkUpBytesPerSecond)}/s en envoi";
        Network.Values = _monitoring.NetworkDown.ToList();
    }

    private static string GpuDetail(Core.Monitoring.GpuMetrics gpu)
    {
        var parts = new List<string> { gpu.Name };
        if (gpu.TemperatureCelsius is { } temperature)
        {
            parts.Add($"{temperature} °C");
        }

        if (gpu.PowerWatts is { } power)
        {
            parts.Add($"{power:N0} W");
        }

        if (gpu.ClockMHz is { } clock)
        {
            parts.Add($"{clock} MHz");
        }

        if (gpu.FanPercent is { } fan)
        {
            parts.Add($"ventilo {fan} %");
        }

        return string.Join(" · ", parts);
    }
}
