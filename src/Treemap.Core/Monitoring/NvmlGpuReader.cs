using System.Runtime.InteropServices;
using System.Text;

namespace Treemap.Core.Monitoring;

/// <summary>
/// Reads the first NVIDIA GPU through NVML, the library installed with the NVIDIA driver (the same source
/// <c>nvidia-smi</c> uses). Needs no administrator rights and no extra package.
/// </summary>
public sealed class NvmlGpuReader : IDisposable
{
    private const int Success = 0;

    private readonly IntPtr _device;
    private bool _disposed;

    private NvmlGpuReader(IntPtr device, string name)
    {
        _device = device;
        Name = name;
    }

    public string Name { get; }

    [StructLayout(LayoutKind.Sequential)]
    private struct Utilization
    {
        public uint Gpu;
        public uint Memory;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryInfo
    {
        public ulong Total;
        public ulong Free;
        public ulong Used;
    }

    [DllImport("nvml.dll", EntryPoint = "nvmlInit_v2")]
    private static extern int Init();

    [DllImport("nvml.dll", EntryPoint = "nvmlShutdown")]
    private static extern int Shutdown();

    [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetHandleByIndex_v2")]
    private static extern int GetHandleByIndex(uint index, out IntPtr device);

    [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetName", CharSet = CharSet.Ansi)]
    private static extern int GetName(IntPtr device, StringBuilder name, uint length);

    [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetUtilizationRates")]
    private static extern int GetUtilizationRates(IntPtr device, out Utilization utilization);

    [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetMemoryInfo")]
    private static extern int GetMemoryInfo(IntPtr device, out MemoryInfo memory);

    [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetTemperature")]
    private static extern int GetTemperature(IntPtr device, int sensor, out uint temperature);

    [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetPowerUsage")]
    private static extern int GetPowerUsage(IntPtr device, out uint milliwatts);

    [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetClockInfo")]
    private static extern int GetClockInfo(IntPtr device, int clockType, out uint megahertz);

    [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetFanSpeed")]
    private static extern int GetFanSpeed(IntPtr device, out uint percent);

    /// <summary>Returns null when there is no NVIDIA driver or no NVIDIA GPU (AMD, Intel, or a VM).</summary>
    public static NvmlGpuReader? TryCreate()
    {
        try
        {
            if (Init() != Success)
            {
                return null;
            }

            if (GetHandleByIndex(0, out var device) != Success)
            {
                Shutdown();
                return null;
            }

            var name = new StringBuilder(96);
            return new NvmlGpuReader(device, GetName(device, name, (uint)name.Capacity) == Success ? name.ToString() : "GPU NVIDIA");
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            return null;
        }
    }

    public GpuMetrics? Read()
    {
        if (_disposed || GetUtilizationRates(_device, out var utilization) != Success)
        {
            return null;
        }

        long used = 0;
        long total = 0;
        if (GetMemoryInfo(_device, out var memory) == Success)
        {
            used = (long)memory.Used;
            total = (long)memory.Total;
        }

        return new GpuMetrics(
            Name,
            utilization.Gpu,
            used,
            total,
            Optional(GetTemperature(_device, 0, out var temperature), temperature) is { } t ? (int)t : null,
            Optional(GetPowerUsage(_device, out var power), power) is { } p ? p / 1000.0 : null,
            Optional(GetClockInfo(_device, 0, out var clock), clock) is { } c ? (int)c : null,
            Optional(GetFanSpeed(_device, out var fan), fan) is { } f ? (int)f : null);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _ = Shutdown();
    }

    /// <summary>Not every card reports every sensor (no fan on some laptops, for instance).</summary>
    private static uint? Optional(int result, uint value) => result == Success ? value : null;
}
