namespace Treemap.Core.Settings;

public enum OverlayCorner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

public sealed record OverlaySettings
{
    public const double MinOpacity = 0.3;

    public bool Enabled { get; init; }

    public bool ShowFps { get; init; } = true;

    public bool ShowCpu { get; init; } = true;

    public bool ShowGpu { get; init; } = true;

    public bool ShowGpuTemperature { get; init; } = true;

    public bool ShowMemory { get; init; } = true;

    public bool ShowVideoMemory { get; init; }

    public OverlayCorner Corner { get; init; } = OverlayCorner.TopLeft;

    public double Opacity { get; init; } = 0.85;

    /// <summary>Pulls hand-edited or outdated values back into a usable range.</summary>
    public OverlaySettings Normalized() =>
        this with
        {
            Opacity = double.IsFinite(Opacity) ? Math.Clamp(Opacity, MinOpacity, 1.0) : 0.85,
            Corner = Enum.IsDefined(Corner) ? Corner : OverlayCorner.TopLeft,
        };
}

/// <summary>What the game booster may do, and the lists the user built. Nothing here is on by default except the safe actions.</summary>
public sealed record BoosterSettings
{
    /// <summary>Politely ask the applications of <see cref="CloseList"/> to close when a game starts.</summary>
    public bool CloseApps { get; init; } = true;

    /// <summary>Give the game's process a higher CPU priority.</summary>
    public bool HighPriority { get; init; } = true;

    /// <summary>Switch to the High performance power plan while the game runs.</summary>
    public bool HighPerformancePlan { get; init; } = true;

    /// <summary>Start a session by itself when a known game opens. Off until the user asks for it.</summary>
    public bool Auto { get; init; }

    /// <summary>Executable names (without .exe) the user chose to close before a game.</summary>
    public string[] CloseList { get; init; } = [];

    /// <summary>Executable names (without .exe) of games the user added by hand.</summary>
    public string[] KnownGames { get; init; } = [];

    /// <summary>Power plan to put back if the app stopped in the middle of a session (so the PC never stays boosted).</summary>
    public string? PendingPowerPlan { get; init; }

    public BoosterSettings Normalized() =>
        this with
        {
            CloseList = Clean(CloseList),
            KnownGames = Clean(KnownGames),
            PendingPowerPlan = Guid.TryParse(PendingPowerPlan, out var guid) ? guid.ToString() : null,
        };

    private static string[] Clean(string[]? names) =>
        (names ?? []).Select(n => n?.Trim() ?? string.Empty)
            .Where(n => n.Length > 0 && n.Length <= 128)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}

public sealed record AppSettings
{
    public OverlaySettings Overlay { get; init; } = new();

    public BoosterSettings Booster { get; init; } = new();
}
