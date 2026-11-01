using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Treemap.Core.Formatting;
using Treemap.Core.Layout;
using Treemap.Core.Storage;
using CoreRect = Treemap.Core.Layout.Rect;
using WpfRect = System.Windows.Rect;

namespace Treemap.App.Controls;

public sealed class TreemapNodeEventArgs(FolderNode? node) : EventArgs
{
    public FolderNode? Node { get; } = node;
}

/// <summary>
/// Draws the children of a folder as a squarified treemap: one rectangle per entry, area proportional to size.
/// Hovering reports the entry under the mouse, clicking reports the entry to open.
/// </summary>
public sealed class TreemapControl : FrameworkElement
{
    public static readonly DependencyProperty FolderProperty = DependencyProperty.Register(
        nameof(Folder),
        typeof(FolderNode),
        typeof(TreemapControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((TreemapControl)d).Invalidate()));

    /// <summary>More cells than this are invisible slivers anyway; the largest ones are kept.</summary>
    private const int MaxCells = 600;

    private const double CellGap = 1.5;

    private static readonly Typeface LabelFace = new("Segoe UI Variable Text");
    private static readonly Typeface LabelFaceBold = new(new FontFamily("Segoe UI Variable Text"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    private static readonly Brush TextBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xF2, 0xF4, 0xF8)));
    private static readonly Brush DimTextBrush = Freeze(new SolidColorBrush(Color.FromArgb(0xB8, 0xF2, 0xF4, 0xF8)));
    private static readonly Pen HoverPen = new(Freeze(new SolidColorBrush(Color.FromRgb(0xF2, 0xF4, 0xF8))), 2);

    private readonly Dictionary<FolderNode, Brush> _brushCache = [];
    private IReadOnlyList<TreemapCell<FolderNode>> _cells = [];
    private Size _laidOutFor;
    private int _hover = -1;

    public FolderNode? Folder
    {
        get => (FolderNode?)GetValue(FolderProperty);
        set => SetValue(FolderProperty, value);
    }

    public event EventHandler<TreemapNodeEventArgs>? HoverChanged;

    public event EventHandler<TreemapNodeEventArgs>? NodeClicked;

    private void Invalidate()
    {
        _cells = [];
        _laidOutFor = default;
        _brushCache.Clear();
        SetHover(-1);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var size = RenderSize;
        if (size.Width < 2 || size.Height < 2)
        {
            return;
        }

        // A transparent fill makes the whole area hit-testable, so MouseLeave/Move behave predictably.
        dc.DrawRectangle(Brushes.Transparent, null, new WpfRect(size));

        EnsureLayout(size);
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        for (var i = 0; i < _cells.Count; i++)
        {
            var cell = _cells[i];
            var rect = Inset(cell.Bounds);
            if (rect.Width < 1 || rect.Height < 1)
            {
                continue;
            }

            var hovered = i == _hover;
            dc.DrawRoundedRectangle(BrushFor(cell.Value, i, hovered), hovered ? HoverPen : null, rect, 3, 3);
            DrawLabel(dc, cell.Value, rect, dpi);
        }
    }

    private void EnsureLayout(Size size)
    {
        if (size == _laidOutFor)
        {
            return;
        }

        _laidOutFor = size;
        _hover = -1;
        if (Folder is null)
        {
            _cells = [];
            return;
        }

        var items = Folder.Children
            .Where(c => c.SizeBytes > 0)
            .Take(MaxCells)
            .Select(c => new TreemapItem<FolderNode>(c, c.SizeBytes));
        _cells = SquarifiedTreemap.Layout(items, new CoreRect(0, 0, size.Width, size.Height));
    }

    private static WpfRect Inset(CoreRect bounds) =>
        new(bounds.X + CellGap / 2, bounds.Y + CellGap / 2, Math.Max(0, bounds.Width - CellGap), Math.Max(0, bounds.Height - CellGap));

    private void DrawLabel(DrawingContext dc, FolderNode node, WpfRect rect, double dpi)
    {
        const double padding = 6;
        if (rect.Width < 56 || rect.Height < 24)
        {
            return;
        }

        var maxWidth = rect.Width - padding * 2;
        var name = new FormattedText(
            node.Name,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            LabelFaceBold,
            12,
            TextBrush,
            dpi)
        {
            MaxTextWidth = maxWidth,
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis,
        };
        dc.DrawText(name, new Point(rect.X + padding, rect.Y + padding - 1));

        if (rect.Height >= 42)
        {
            var size = new FormattedText(
                ByteSize.Format(node.SizeBytes),
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                LabelFace,
                11,
                DimTextBrush,
                dpi)
            {
                MaxTextWidth = maxWidth,
                MaxLineCount = 1,
                Trimming = TextTrimming.CharacterEllipsis,
            };
            dc.DrawText(size, new Point(rect.X + padding, rect.Y + padding + 15));
        }
    }

    private Brush BrushFor(FolderNode node, int rank, bool hovered)
    {
        if (!hovered && _brushCache.TryGetValue(node, out var cached))
        {
            return cached;
        }

        var (saturation, lightness) = node.Kind switch
        {
            NodeKind.Folder => (0.42, 0.36),
            NodeKind.File => (0.16, 0.31),
            _ => (0.06, 0.23),
        };
        if (hovered)
        {
            lightness += 0.08;
        }

        // Folders step around the colour wheel by the golden angle, so neighbouring (similarly sized) cells never share a hue.
        var hue = node.Kind == NodeKind.Folder ? rank * 137.508 % 360 : 215;
        var brush = Freeze(new SolidColorBrush(FromHsl(hue, saturation, lightness)));
        if (!hovered)
        {
            _brushCache[node] = brush;
        }

        return brush;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var p = e.GetPosition(this);
        var index = -1;
        for (var i = 0; i < _cells.Count; i++)
        {
            if (_cells[i].Bounds.Contains(p.X, p.Y))
            {
                index = i;
                break;
            }
        }

        SetHover(index);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        SetHover(-1);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_hover >= 0 && _hover < _cells.Count)
        {
            NodeClicked?.Invoke(this, new TreemapNodeEventArgs(_cells[_hover].Value));
        }
    }

    private void SetHover(int index)
    {
        if (index == _hover)
        {
            return;
        }

        _hover = index;
        Cursor = index >= 0 && _cells[index].Value.IsFolder ? Cursors.Hand : Cursors.Arrow;
        InvalidateVisual();
        HoverChanged?.Invoke(this, new TreemapNodeEventArgs(index >= 0 ? _cells[index].Value : null));
    }

    private static T Freeze<T>(T freezable)
        where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }

    private static Color FromHsl(double hue, double saturation, double lightness)
    {
        var c = (1 - Math.Abs(2 * lightness - 1)) * saturation;
        var h = hue / 60;
        var x = c * (1 - Math.Abs(h % 2 - 1));
        var (r, g, b) = (int)h switch
        {
            0 => (c, x, 0.0),
            1 => (x, c, 0.0),
            2 => (0.0, c, x),
            3 => (0.0, x, c),
            4 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        var m = lightness - c / 2;
        return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }
}
