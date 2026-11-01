namespace Treemap.Core.Layout;

public readonly record struct Rect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;

    public double Area => Width * Height;

    public bool Contains(double x, double y) => x >= X && x < Right && y >= Y && y < Bottom;
}

public readonly record struct TreemapItem<T>(T Value, double Weight);

public readonly record struct TreemapCell<T>(T Value, Rect Bounds);

/// <summary>
/// Squarified treemap layout (Bruls, Huizing, van Wijk): lays out weighted items inside a rectangle
/// so that each cell's area is proportional to its weight while keeping aspect ratios close to 1.
/// </summary>
public static class SquarifiedTreemap
{
    public static IReadOnlyList<TreemapCell<T>> Layout<T>(IEnumerable<TreemapItem<T>> items, Rect bounds)
    {
        var sorted = items
            .Where(i => i.Weight > 0 && double.IsFinite(i.Weight))
            .OrderByDescending(i => i.Weight)
            .ToList();

        if (sorted.Count == 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return [];
        }

        var totalWeight = sorted.Sum(i => i.Weight);
        var areaPerWeight = bounds.Area / totalWeight;
        var areas = sorted.Select(i => i.Weight * areaPerWeight).ToArray();

        var cells = new List<TreemapCell<T>>(sorted.Count);
        var remaining = bounds;
        var index = 0;

        while (index < sorted.Count)
        {
            var side = Math.Min(remaining.Width, remaining.Height);
            var rowStart = index;
            var rowEnd = index + 1;
            var worst = WorstRatio(areas, rowStart, rowEnd, side);

            while (rowEnd < sorted.Count)
            {
                var candidate = WorstRatio(areas, rowStart, rowEnd + 1, side);
                if (candidate > worst)
                {
                    break;
                }

                worst = candidate;
                rowEnd++;
            }

            remaining = PlaceRow(sorted, areas, rowStart, rowEnd, remaining, cells);
            index = rowEnd;
        }

        return cells;
    }

    private static double WorstRatio(double[] areas, int start, int end, double side)
    {
        double sum = 0;
        var min = double.MaxValue;
        double max = 0;
        for (var i = start; i < end; i++)
        {
            sum += areas[i];
            min = Math.Min(min, areas[i]);
            max = Math.Max(max, areas[i]);
        }

        var sideSquared = side * side;
        return Math.Max(sideSquared * max / (sum * sum), sum * sum / (sideSquared * min));
    }

    private static Rect PlaceRow<T>(
        List<TreemapItem<T>> items,
        double[] areas,
        int start,
        int end,
        Rect free,
        List<TreemapCell<T>> output)
    {
        double rowArea = 0;
        for (var i = start; i < end; i++)
        {
            rowArea += areas[i];
        }

        // The last row takes exactly what is left, so rounding never leaves a sliver uncovered.
        var isLastRow = end == items.Count;

        if (free.Width >= free.Height)
        {
            // Row is a column on the left edge, items stacked top to bottom.
            var rowWidth = isLastRow ? free.Width : Math.Min(rowArea / free.Height, free.Width);
            var y = free.Y;
            for (var i = start; i < end; i++)
            {
                var height = i == end - 1 ? free.Bottom - y : areas[i] / rowWidth;
                output.Add(new TreemapCell<T>(items[i].Value, new Rect(free.X, y, rowWidth, height)));
                y += height;
            }

            return new Rect(free.X + rowWidth, free.Y, free.Width - rowWidth, free.Height);
        }
        else
        {
            // Row is a strip along the top edge, items placed left to right.
            var rowHeight = isLastRow ? free.Height : Math.Min(rowArea / free.Width, free.Height);
            var x = free.X;
            for (var i = start; i < end; i++)
            {
                var width = i == end - 1 ? free.Right - x : areas[i] / rowHeight;
                output.Add(new TreemapCell<T>(items[i].Value, new Rect(x, free.Y, width, rowHeight)));
                x += width;
            }

            return new Rect(free.X, free.Y + rowHeight, free.Width, free.Height - rowHeight);
        }
    }
}
