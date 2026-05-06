using System.Text.Json;
using OxidizePdf.NET.Pipeline;

namespace OxidizePdf.NET.Tests.Pipeline;

public class PartitionConfigTests
{
    [Fact]
    public void Defaults_match_rust_PartitionConfig_default()
    {
        var c = new PartitionConfig();
        Assert.True(c.DetectTables);
        Assert.True(c.DetectHeadersFooters);
        Assert.Equal(1.3, c.TitleMinFontRatio);
        Assert.Equal(0.05, c.HeaderZone);
        Assert.Equal(0.05, c.FooterZone);
        Assert.Equal(0.5, c.MinTableConfidence);
        Assert.Same(ReadingOrderStrategy.Simple, c.ReadingOrder);
    }

    [Fact]
    public void Fluent_builders_mutate_and_return_self()
    {
        var c = new PartitionConfig()
            .WithoutTables()
            .WithoutHeadersFooters()
            .WithTitleMinFontRatio(1.5)
            .WithMinTableConfidence(0.7)
            .WithReadingOrder(ReadingOrderStrategy.XyCut(20.0));

        Assert.False(c.DetectTables);
        Assert.False(c.DetectHeadersFooters);
        Assert.Equal(1.5, c.TitleMinFontRatio);
        Assert.Equal(0.7, c.MinTableConfidence);
        Assert.Equal(ReadingOrderKind.XyCut, c.ReadingOrder.Kind);
        Assert.Equal(20.0, c.ReadingOrder.MinGap);
    }

    [Fact]
    public void WithReadingOrder_null_throws()
    {
        Assert.Throws<ArgumentNullException>(() => new PartitionConfig().WithReadingOrder(null!));
    }

    [Fact]
    public void ReadingOrder_property_setter_rejects_null()
    {
        Assert.Throws<ArgumentNullException>(() => new PartitionConfig { ReadingOrder = null! });
    }

    [Fact]
    public void Validate_rejects_non_positive_title_ratio()
    {
        Assert.Throws<ArgumentException>(() => new PartitionConfig { TitleMinFontRatio = 0.0 }.Validate());
        Assert.Throws<ArgumentException>(() => new PartitionConfig { TitleMinFontRatio = -0.1 }.Validate());
    }

    [Fact]
    public void Validate_rejects_out_of_range_zones()
    {
        Assert.Throws<ArgumentException>(() => new PartitionConfig { HeaderZone = -0.01 }.Validate());
        Assert.Throws<ArgumentException>(() => new PartitionConfig { HeaderZone = 1.01 }.Validate());
        Assert.Throws<ArgumentException>(() => new PartitionConfig { FooterZone = -0.01 }.Validate());
        Assert.Throws<ArgumentException>(() => new PartitionConfig { FooterZone = 1.01 }.Validate());
    }

    [Fact]
    public void Validate_rejects_out_of_range_confidence()
    {
        Assert.Throws<ArgumentException>(() => new PartitionConfig { MinTableConfidence = -0.01 }.Validate());
        Assert.Throws<ArgumentException>(() => new PartitionConfig { MinTableConfidence = 1.01 }.Validate());
    }

    [Fact]
    public void Validate_accepts_boundary_values()
    {
        new PartitionConfig { HeaderZone = 0.0, FooterZone = 1.0, MinTableConfidence = 0.0 }.Validate();
        new PartitionConfig { HeaderZone = 1.0, FooterZone = 0.0, MinTableConfidence = 1.0 }.Validate();
    }

    [Fact]
    public void JSON_round_trip_preserves_all_fields()
    {
        var original = new PartitionConfig()
            .WithoutTables()
            .WithReadingOrder(ReadingOrderStrategy.XyCut(15.0))
            .WithTitleMinFontRatio(1.4);

        var json = original.ToJson();
        var round = JsonSerializer.Deserialize<PartitionConfig>(json, PartitionConfig.JsonOptions);

        Assert.NotNull(round);
        Assert.False(round!.DetectTables);
        Assert.Equal(1.4, round.TitleMinFontRatio);
        Assert.Equal(ReadingOrderKind.XyCut, round.ReadingOrder.Kind);
        Assert.Equal(15.0, round.ReadingOrder.MinGap);
    }

    [Fact]
    public void JSON_round_trip_preserves_every_field()
    {
        var original = new PartitionConfig
        {
            DetectTables = false,
            DetectHeadersFooters = false,
            TitleMinFontRatio = 1.7,
            HeaderZone = 0.12,
            FooterZone = 0.08,
            MinTableConfidence = 0.85,
            ReadingOrder = ReadingOrderStrategy.XyCut(22.5),
        };

        var json = original.ToJson();
        var back = JsonSerializer.Deserialize<PartitionConfig>(json, PartitionConfig.JsonOptions);

        Assert.NotNull(back);
        Assert.False(back!.DetectTables);
        Assert.False(back.DetectHeadersFooters);
        Assert.Equal(1.7, back.TitleMinFontRatio);
        Assert.Equal(0.12, back.HeaderZone);
        Assert.Equal(0.08, back.FooterZone);
        Assert.Equal(0.85, back.MinTableConfidence);
        Assert.Equal(ReadingOrderKind.XyCut, back.ReadingOrder.Kind);
        Assert.Equal(22.5, back.ReadingOrder.MinGap);
    }

    [Fact]
    public void JSON_uses_snake_case_property_names()
    {
        var c = new PartitionConfig();
        var json = c.ToJson();
        Assert.Contains("\"detect_tables\"", json);
        Assert.Contains("\"detect_headers_footers\"", json);
        Assert.Contains("\"title_min_font_ratio\"", json);
        Assert.Contains("\"header_zone\"", json);
        Assert.Contains("\"footer_zone\"", json);
        Assert.Contains("\"reading_order\"", json);
        Assert.Contains("\"min_table_confidence\"", json);
    }
}
