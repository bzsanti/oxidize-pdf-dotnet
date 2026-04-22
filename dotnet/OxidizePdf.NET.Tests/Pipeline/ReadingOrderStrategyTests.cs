using System.Text.Json;
using OxidizePdf.NET.Pipeline;

namespace OxidizePdf.NET.Tests.Pipeline;

public class ReadingOrderStrategyTests
{
    [Fact]
    public void Simple_and_None_are_singletons()
    {
        Assert.Same(ReadingOrderStrategy.Simple, ReadingOrderStrategy.Simple);
        Assert.Same(ReadingOrderStrategy.None, ReadingOrderStrategy.None);
    }

    [Fact]
    public void XyCut_carries_min_gap()
    {
        var s = ReadingOrderStrategy.XyCut(15.0);
        Assert.Equal(ReadingOrderKind.XyCut, s.Kind);
        Assert.Equal(15.0, s.MinGap);
    }

    [Fact]
    public void XyCut_rejects_negative_min_gap()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ReadingOrderStrategy.XyCut(-1.0));
    }

    [Fact]
    public void XyCut_rejects_NaN_and_infinity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ReadingOrderStrategy.XyCut(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => ReadingOrderStrategy.XyCut(double.PositiveInfinity));
    }

    [Fact]
    public void JSON_shape_Simple()
    {
        Assert.Equal("\"Simple\"", JsonSerializer.Serialize(ReadingOrderStrategy.Simple));
    }

    [Fact]
    public void JSON_shape_None()
    {
        Assert.Equal("\"None\"", JsonSerializer.Serialize(ReadingOrderStrategy.None));
    }

    [Fact]
    public void JSON_shape_XyCut()
    {
        Assert.Equal("{\"XYCut\":{\"min_gap\":20}}", JsonSerializer.Serialize(ReadingOrderStrategy.XyCut(20.0)));
    }

    [Fact]
    public void JSON_round_trip_Simple()
    {
        var back = JsonSerializer.Deserialize<ReadingOrderStrategy>("\"Simple\"");
        Assert.Same(ReadingOrderStrategy.Simple, back);
    }

    [Fact]
    public void JSON_round_trip_XyCut()
    {
        var back = JsonSerializer.Deserialize<ReadingOrderStrategy>("{\"XYCut\":{\"min_gap\":15.5}}");
        Assert.NotNull(back);
        Assert.Equal(ReadingOrderKind.XyCut, back!.Kind);
        Assert.Equal(15.5, back.MinGap);
    }

    [Fact]
    public void Equality_respects_Kind_and_MinGap()
    {
        Assert.Equal(ReadingOrderStrategy.XyCut(10.0), ReadingOrderStrategy.XyCut(10.0));
        Assert.NotEqual(ReadingOrderStrategy.XyCut(10.0), ReadingOrderStrategy.XyCut(11.0));
        Assert.NotEqual(ReadingOrderStrategy.Simple, ReadingOrderStrategy.None);
    }

    [Fact]
    public void JSON_unknown_string_tag_throws()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ReadingOrderStrategy>("\"Foo\""));
    }

    [Fact]
    public void JSON_missing_min_gap_throws()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ReadingOrderStrategy>("{\"XYCut\":{}}"));
    }

    [Fact]
    public void JSON_non_numeric_min_gap_throws()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ReadingOrderStrategy>("{\"XYCut\":{\"min_gap\":\"not a number\"}}"));
    }

    [Fact]
    public void JSON_negative_min_gap_throws_JsonException_not_ArgumentException()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ReadingOrderStrategy>("{\"XYCut\":{\"min_gap\":-1.0}}"));
    }

    [Fact]
    public void JSON_tolerates_trailing_fields_in_XyCut()
    {
        var result = JsonSerializer.Deserialize<ReadingOrderStrategy>("{\"XYCut\":{\"min_gap\":20,\"extra\":1}}");
        Assert.NotNull(result);
        Assert.Equal(ReadingOrderKind.XyCut, result!.Kind);
        Assert.Equal(20.0, result.MinGap);
    }

    [Fact]
    public void JSON_round_trip_None()
    {
        var back = JsonSerializer.Deserialize<ReadingOrderStrategy>("\"None\"");
        Assert.Same(ReadingOrderStrategy.None, back);
    }

    [Fact]
    public void XyCut_zero_min_gap_is_accepted()
    {
        var result = ReadingOrderStrategy.XyCut(0.0);
        Assert.NotNull(result);
        Assert.Equal(0.0, result.MinGap);
    }
}
