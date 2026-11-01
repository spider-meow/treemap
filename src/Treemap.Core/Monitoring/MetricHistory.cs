namespace Treemap.Core.Monitoring;

/// <summary>Fixed-size rolling window of the most recent values of one metric, for graphs.</summary>
public sealed class MetricHistory
{
    private readonly double[] _values;
    private int _next;
    private int _count;

    public MetricHistory(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _values = new double[capacity];
    }

    public int Capacity => _values.Length;

    public int Count => _count;

    public void Add(double value)
    {
        _values[_next] = value;
        _next = (_next + 1) % _values.Length;
        _count = Math.Min(_count + 1, _values.Length);
    }

    /// <summary>The stored values, oldest first.</summary>
    public IReadOnlyList<double> ToList()
    {
        var result = new double[_count];
        var start = _count < _values.Length ? 0 : _next;
        for (var i = 0; i < _count; i++)
        {
            result[i] = _values[(start + i) % _values.Length];
        }

        return result;
    }
}
