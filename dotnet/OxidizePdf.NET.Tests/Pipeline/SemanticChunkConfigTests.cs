using System.Text.Json;
using OxidizePdf.NET.Pipeline;

namespace OxidizePdf.NET.Tests.Pipeline;

public class SemanticChunkConfigTests
{
    [Fact]
    public void Defaults_match_rust()
    {
        var c = new SemanticChunkConfig();
        Assert.Equal(512, c.MaxTokens);
        Assert.Equal(50, c.OverlapTokens);
        Assert.True(c.RespectElementBoundaries);
    }

    [Fact]
    public void Ctor_with_max_tokens_keeps_other_defaults()
    {
        var c = new SemanticChunkConfig(256);
        Assert.Equal(256, c.MaxTokens);
        Assert.Equal(50, c.OverlapTokens);
        Assert.True(c.RespectElementBoundaries);
    }

    [Fact]
    public void Fluent_with_overlap_returns_self()
    {
        var original = new SemanticChunkConfig(256);
        var chained = original.WithOverlap(75);
        Assert.Same(original, chained);
        Assert.Equal(75, chained.OverlapTokens);
    }

    [Fact]
    public void Validate_rejects_non_positive_max_tokens()
    {
        Assert.Throws<ArgumentException>(() => new SemanticChunkConfig { MaxTokens = 0 }.Validate());
        Assert.Throws<ArgumentException>(() => new SemanticChunkConfig { MaxTokens = -1 }.Validate());
    }

    [Fact]
    public void Validate_rejects_negative_overlap()
    {
        Assert.Throws<ArgumentException>(() => new SemanticChunkConfig { OverlapTokens = -1 }.Validate());
    }

    [Fact]
    public void Validate_rejects_overlap_ge_max()
    {
        Assert.Throws<ArgumentException>(() => new SemanticChunkConfig { MaxTokens = 100, OverlapTokens = 100 }.Validate());
        Assert.Throws<ArgumentException>(() => new SemanticChunkConfig { MaxTokens = 100, OverlapTokens = 150 }.Validate());
    }

    [Fact]
    public void JSON_uses_snake_case()
    {
        var c = new SemanticChunkConfig(128).WithOverlap(10);
        var json = c.ToJson();
        Assert.Contains("\"max_tokens\":128", json);
        Assert.Contains("\"overlap_tokens\":10", json);
        Assert.Contains("\"respect_element_boundaries\":true", json);
    }

    [Fact]
    public void JSON_round_trip_preserves_every_field()
    {
        var original = new SemanticChunkConfig
        {
            MaxTokens = 384,
            OverlapTokens = 24,
            RespectElementBoundaries = false,
        };
        var json = original.ToJson();
        var back = JsonSerializer.Deserialize<SemanticChunkConfig>(json, SemanticChunkConfig.JsonOptions);

        Assert.NotNull(back);
        Assert.Equal(384, back!.MaxTokens);
        Assert.Equal(24, back.OverlapTokens);
        Assert.False(back.RespectElementBoundaries);
    }
}
