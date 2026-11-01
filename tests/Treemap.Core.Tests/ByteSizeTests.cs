using System.Globalization;
using Treemap.Core.Formatting;

namespace Treemap.Core.Tests;

public class ByteSizeTests
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    [Theory]
    [InlineData(0, "0 o")]
    [InlineData(512, "512 o")]
    [InlineData(1024, "1,0 Ko")]
    [InlineData(1536, "1,5 Ko")]
    [InlineData(1024L * 1024, "1,0 Mo")]
    [InlineData(105L * 1024 * 1024 * 1024, "105 Go")]
    [InlineData(930L * 1024 * 1024 * 1024, "930 Go")]
    [InlineData(1024L * 1024 * 1024 * 1024, "1,0 To")]
    public void Format_UsesBinaryUnits(long bytes, string expected)
    {
        Assert.Equal(expected, ByteSize.Format(bytes, Fr).Replace(' ', ' ').Replace(' ', ' '));
    }

    [Fact]
    public void Format_KeepsOneDecimalBelowOneHundred()
    {
        Assert.Equal("99,5 Go", ByteSize.Format((long)(99.5 * 1024 * 1024 * 1024), Fr));
    }

    [Fact]
    public void Format_HandlesNegativeValues()
    {
        Assert.Equal("-1,0 Ko", ByteSize.Format(-1024, Fr));
    }
}
