using System.Text.Json;
using OxidizePdf.NET.Pipeline;

namespace OxidizePdf.NET.Tests.Pipeline;

public class HybridChunkConfigTests
{
    [Fact]
    public void Defaults_match_rust()
    {
        var c = new HybridChunkConfig();
        Assert.Equal(512, c.MaxTokens);
        Assert.Equal(50, c.OverlapTokens);
        Assert.True(c.MergeAdjacent);
        Assert.True(c.PropagateHeadings);
        Assert.Equal(MergePolicy.AnyInlineContent, c.MergePolicy);
    }

    [Fact]
    public void Fluent_mutators_chain_and_return_self()
    {
        var original = new HybridChunkConfig();
        var chained = original
            .WithMaxTokens(256)
            .WithOverlap(30)
            .WithMergePolicy(MergePolicy.SameTypeOnly);

        Assert.Same(original, chained);
        Assert.Equal(256, chained.MaxTokens);
        Assert.Equal(30, chained.OverlapTokens);
        Assert.Equal(MergePolicy.SameTypeOnly, chained.MergePolicy);
    }

    [Fact]
    public void Validate_rejects_non_positive_max_tokens()
    {
        Assert.Throws<ArgumentException>(() => new HybridChunkConfig { MaxTokens = 0 }.Validate());
        Assert.Throws<ArgumentException>(() => new HybridChunkConfig { MaxTokens = -1 }.Validate());
    }

    [Fact]
    public void Validate_rejects_negative_overlap()
    {
        Assert.Throws<ArgumentException>(() => new HybridChunkConfig { OverlapTokens = -1 }.Validate());
    }

    [Fact]
    public void Validate_rejects_overlap_ge_max()
    {
        Assert.Throws<ArgumentException>(() => new HybridChunkConfig { MaxTokens = 100, OverlapTokens = 100 }.Validate());
        Assert.Throws<ArgumentException>(() => new HybridChunkConfig { MaxTokens = 100, OverlapTokens = 150 }.Validate());
    }

    [Fact]
    public void Validate_accepts_zero_overlap_and_small_max()
    {
        new HybridChunkConfig { MaxTokens = 1, OverlapTokens = 0 }.Validate();
    }

    [Fact]
    public void JSON_uses_snake_case_and_emits_merge_policy_as_string()
    {
        var c = new HybridChunkConfig().WithMergePolicy(MergePolicy.SameTypeOnly);
        var json = c.ToJson();
        Assert.Contains("\"max_tokens\":512", json);
        Assert.Contains("\"overlap_tokens\":50", json);
        Assert.Contains("\"merge_adjacent\":true", json);
        Assert.Contains("\"propagate_headings\":true", json);
        Assert.Contains("\"merge_policy\":\"SameTypeOnly\"", json);
    }

    [Fact]
    public void JSON_round_trip_preserves_every_field()
    {
        var original = new HybridChunkConfig
        {
            MaxTokens = 384,
            OverlapTokens = 24,
            MergeAdjacent = false,
            PropagateHeadings = false,
            MergePolicy = MergePolicy.SameTypeOnly,
        };

        var json = original.ToJson();
        var back = JsonSerializer.Deserialize<HybridChunkConfig>(json, HybridChunkConfig.JsonOptions);

        Assert.NotNull(back);
        Assert.Equal(384, back!.MaxTokens);
        Assert.Equal(24, back.OverlapTokens);
        Assert.False(back.MergeAdjacent);
        Assert.False(back.PropagateHeadings);
        Assert.Equal(MergePolicy.SameTypeOnly, back.MergePolicy);
    }
}
