using System.Text.Json;
using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Ai;

/// <summary>
/// Options for Markdown export. Mirrors <c>oxidize_pdf::ai::MarkdownOptions</c>.
/// </summary>
/// <remarks>
/// This is a plain mutable POCO; there is no fluent builder because it has only two fields.
/// Both fields default to <c>true</c>, matching the Rust <c>Default</c> impl.
/// </remarks>
public class MarkdownOptions
{
    /// <summary>JSON serialization options used by <see cref="ToJson"/>. Uses snake_case.</summary>
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>Whether to include a YAML metadata frontmatter block. Default <c>true</c>.</summary>
    [JsonPropertyName("include_metadata")]
    public bool IncludeMetadata { get; set; } = true;

    /// <summary>Whether to include per-page number markers. Default <c>true</c>.</summary>
    [JsonPropertyName("include_page_numbers")]
    public bool IncludePageNumbers { get; set; } = true;

    /// <summary>Serialize these options to JSON using <see cref="JsonOptions"/>.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
}
