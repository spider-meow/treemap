using System.Globalization;

namespace Treemap.Core.Formatting;

/// <summary>
/// Human-readable sizes using 1024-based steps, the same convention Windows Explorer uses
/// (so "930 Go" matches what the user sees in the Explorer drive list).
/// </summary>
public static class ByteSize
{
    private static readonly string[] Units = ["o", "Ko", "Mo", "Go", "To", "Po"];

    public static string Format(long bytes, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;

        if (bytes < 0)
        {
            return "-" + Format(-bytes, culture);
        }

        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        // Bytes are whole numbers; everything else keeps one decimal below 100 for readability.
        var format = unit == 0 ? "N0" : value >= 100 ? "N0" : "N1";
        return value.ToString(format, culture) + " " + Units[unit];
    }
}
