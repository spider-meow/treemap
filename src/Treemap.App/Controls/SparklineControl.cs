using System.Windows;
using System.Windows.Media;

namespace Treemap.App.Controls;

/// <summary>A small filled line graph of recent values, newest on the right.</summary>
public sealed class SparklineControl : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = Register<IReadOnlyList<double>?>(nameof(Values), null);

    /// <summary>Value at the top of the graph. Zero or less means "fit the data".</summary>
    public static readonly DependencyProperty MaximumProperty = Register(nameof(Maximum), 0.0);

    /// <summary>How many points a full graph holds; fewer points are drawn right-aligned.</summary>
    public static readonly DependencyProperty CapacityProperty = Register(nameof(Capacity), 120);

    public static readonly DependencyProperty StrokeProperty = Register<Brush>(nameof(Stroke), Brushes.SteelBlue);

    public static readonly DependencyProperty FillProperty = Register<Brush>(nameof(Fill), Brushes.Transparent);

    public IReadOnlyList<double>? Values
    {
        get => (IReadOnlyList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public int Capacity
    {
        get => (int)GetValue(CapacityProperty);
        set => SetValue(CapacityProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public Brush Fill
    {
        get => (Brush)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 2 || height < 2)
        {
            return;
        }

        var baseline = new Pen(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), 1);
        dc.DrawLine(baseline, new Point(0, height - 0.5), new Point(width, height - 0.5));

        var values = Values;
        if (values is null || values.Count < 2)
        {
            return;
        }

        var maximum = Maximum > 0 ? Maximum : Math.Max(values.Max() * 1.15, 1e-9);
        var step = width / Math.Max(Capacity - 1, 1);
        var points = new Point[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            var x = width - (values.Count - 1 - i) * step;
            var y = height - Math.Clamp(values[i] / maximum, 0, 1) * (height - 2) - 1;
            points[i] = new Point(x, y);
        }

        var line = new StreamGeometry();
        using (var context = line.Open())
        {
            context.BeginFigure(points[0], isFilled: false, isClosed: false);
            context.PolyLineTo(points.AsSpan(1).ToArray(), isStroked: true, isSmoothJoin: true);
        }

        var area = new StreamGeometry();
        using (var context = area.Open())
        {
            context.BeginFigure(new Point(points[0].X, height), isFilled: true, isClosed: true);
            context.PolyLineTo(points, isStroked: false, isSmoothJoin: false);
            context.LineTo(new Point(points[^1].X, height), isStroked: false, isSmoothJoin: false);
        }

        line.Freeze();
        area.Freeze();
        dc.DrawGeometry(Fill, null, area);
        dc.DrawGeometry(null, new Pen(Stroke, 1.6) { LineJoin = PenLineJoin.Round }, line);
    }

    private static DependencyProperty Register<T>(string name, T defaultValue) =>
        DependencyProperty.Register(
            name,
            typeof(T),
            typeof(SparklineControl),
            new FrameworkPropertyMetadata(defaultValue, FrameworkPropertyMetadataOptions.AffectsRender));
}
