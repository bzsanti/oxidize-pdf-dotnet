using System.Text.Json;
using OxidizePdf.NET.Ai;

namespace OxidizePdf.NET.Tests.Ai;

public class MarkdownOptionsTests
{
    [Fact]
    public void Defaults_match_rust_MarkdownOptions_default()
    {
        var o = new MarkdownOptions();
        Assert.True(o.IncludeMetadata);
        Assert.True(o.IncludePageNumbers);
    }

    [Fact]
    public void JSON_uses_snake_case()
    {
        var o = new MarkdownOptions { IncludeMetadata = false, IncludePageNumbers = true };
        var json = o.ToJson();
        Assert.Contains("\"include_metadata\":false", json);
        Assert.Contains("\"include_page_numbers\":true", json);
    }

    [Fact]
    public void JSON_round_trip_preserves_every_field()
    {
        var original = new MarkdownOptions { IncludeMetadata = false, IncludePageNumbers = false };
        var json = original.ToJson();
        var back = JsonSerializer.Deserialize<MarkdownOptions>(json, MarkdownOptions.JsonOptions);
        Assert.NotNull(back);
        Assert.False(back!.IncludeMetadata);
        Assert.False(back.IncludePageNumbers);
    }
}
