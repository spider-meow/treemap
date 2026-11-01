using Treemap.Core.Layout;

namespace Treemap.Core.Tests;

public class SquarifiedTreemapTests
{
    private static readonly Rect Bounds = new(10, 20, 800, 500);

    private static IReadOnlyList<TreemapCell<string>> Run(params double[] weights) =>
        SquarifiedTreemap.Layout(
            weights.Select((w, i) => new TreemapItem<string>($"item{i}", w)),
            Bounds);

    [Fact]
    public void EmptyInput_ReturnsNoCells()
    {
        Assert.Empty(Run());
    }

    [Fact]
    public void EmptyBounds_ReturnsNoCells()
    {
        var cells = SquarifiedTreemap.Layout([new TreemapItem<string>("a", 1)], new Rect(0, 0, 0, 100));
        Assert.Empty(cells);
    }

    [Fact]
    public void SingleItem_FillsBounds()
    {
        var cell = Assert.Single(Run(42));
        Assert.Equal(Bounds, cell.Bounds);
    }

    [Fact]
    public void NonPositiveWeights_AreIgnored()
    {
        var cells = Run(5, 0, -3, double.NaN);
        var cell = Assert.Single(cells);
        Assert.Equal("item0", cell.Value);
    }

    [Fact]
    public void Areas_AreProportionalToWeights()
    {
        double[] weights = [60, 30, 6, 3, 1];
        var cells = Run(weights);

        var total = weights.Sum();
        Assert.Equal(weights.Length, cells.Count);
        foreach (var cell in cells)
        {
            var weight = weights[int.Parse(cell.Value["item".Length..])];
            Assert.Equal(Bounds.Area * weight / total, cell.Bounds.Area, precision: 3);
        }
    }

    [Fact]
    public void Cells_StayInsideBoundsAndCoverThem()
    {
        var cells = Run(500, 300, 120, 80, 40, 20, 10, 5, 3, 1);

        const double tolerance = 1e-6;
        foreach (var c in cells)
        {
            Assert.True(c.Bounds.X >= Bounds.X - tolerance);
            Assert.True(c.Bounds.Y >= Bounds.Y - tolerance);
            Assert.True(c.Bounds.Right <= Bounds.Right + tolerance);
            Assert.True(c.Bounds.Bottom <= Bounds.Bottom + tolerance);
        }

        Assert.Equal(Bounds.Area, cells.Sum(c => c.Bounds.Area), precision: 3);
    }

    [Fact]
    public void Cells_DoNotOverlap()
    {
        var cells = Run(500, 300, 120, 80, 40, 20, 10, 5, 3, 1);

        for (var i = 0; i < cells.Count; i++)
        {
            for (var j = i + 1; j < cells.Count; j++)
            {
                var a = cells[i].Bounds;
                var b = cells[j].Bounds;
                var overlapWidth = Math.Min(a.Right, b.Right) - Math.Max(a.X, b.X);
                var overlapHeight = Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Y, b.Y);
                Assert.False(overlapWidth > 1e-6 && overlapHeight > 1e-6, $"cells {i} and {j} overlap");
            }
        }
    }

    [Fact]
    public void LargestItemComesFirst()
    {
        var cells = Run(1, 100, 10);
        Assert.Equal("item1", cells[0].Value);
    }
}
