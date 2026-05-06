using System.Text.Json;
using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Pipeline;

/// <summary>
/// Configuration for the semantic chunker. Mirrors
/// <c>oxidize_pdf::pipeline::SemanticChunkConfig</c>.
/// </summary>
/// <remarks>
/// Fluent methods mutate this instance in place and return <c>this</c>.
/// </remarks>
public class SemanticChunkConfig
{
    /// <summary>JSON serialization options used by <see cref="ToJson"/>. Uses snake_case.</summary>
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>Maximum tokens per chunk. Default <c>512</c>.</summary>
    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; } = 512;

    /// <summary>Overlap tokens between consecutive chunks. Default <c>50</c>.</summary>
    [JsonPropertyName("overlap_tokens")]
    public int OverlapTokens { get; set; } = 50;

    /// <summary>Whether to keep elements whole (don't split titles, tables, etc.). Default <c>true</c>.</summary>
    [JsonPropertyName("respect_element_boundaries")]
    public bool RespectElementBoundaries { get; set; } = true;

    /// <summary>Create a configuration with all defaults.</summary>
    public SemanticChunkConfig() { }

    /// <summary>Create a configuration with a specific <see cref="MaxTokens"/> value; other fields keep defaults.</summary>
    public SemanticChunkConfig(int maxTokens) { MaxTokens = maxTokens; }

    /// <summary>Set the overlap tokens between chunks.</summary>
    public SemanticChunkConfig WithOverlap(int n) { OverlapTokens = n; return this; }

    /// <summary>
    /// Validate this configuration. Throws <see cref="ArgumentException"/> if any field is out of range.
    /// </summary>
    /// <remarks>
    /// This is a C#-side preflight. The Rust core has no validation of its own.
    /// </remarks>
    public void Validate()
    {
        if (MaxTokens <= 0)
            throw new ArgumentException("MaxTokens must be positive");
        if (OverlapTokens < 0)
            throw new ArgumentException("OverlapTokens must be non-negative");
        if (OverlapTokens >= MaxTokens)
            throw new ArgumentException("OverlapTokens must be less than MaxTokens");
    }

    /// <summary>Serialize this configuration to JSON using <see cref="JsonOptions"/>.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
}
