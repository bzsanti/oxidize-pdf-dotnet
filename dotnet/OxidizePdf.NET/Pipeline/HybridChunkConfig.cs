using System.Text.Json;
using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Pipeline;

/// <summary>
/// Configuration for the hybrid chunker. Mirrors
/// <c>oxidize_pdf::pipeline::HybridChunkConfig</c>.
/// </summary>
/// <remarks>
/// Fluent <c>With*</c> methods mutate this instance in place and return <c>this</c> —
/// the config is not immutable. Two references obtained from the same fluent chain
/// will alias the same object.
/// </remarks>
public class HybridChunkConfig
{
    /// <summary>JSON serialization options used by <see cref="ToJson"/>. Uses snake_case and stringifies <see cref="Pipeline.MergePolicy"/>.</summary>
    public static readonly JsonSerializerOptions JsonOptions = BuildOptions();

    /// <summary>Maximum tokens per chunk (approximate — word-count proxy). Default <c>512</c>.</summary>
    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; } = 512;

    /// <summary>Overlap tokens between consecutive chunks. Default <c>50</c>.</summary>
    [JsonPropertyName("overlap_tokens")]
    public int OverlapTokens { get; set; } = 50;

    /// <summary>Whether to merge adjacent elements of the same type. Default <c>true</c>.</summary>
    [JsonPropertyName("merge_adjacent")]
    public bool MergeAdjacent { get; set; } = true;

    /// <summary>Whether to propagate heading context from <c>parent_heading</c> metadata. Default <c>true</c>.</summary>
    [JsonPropertyName("propagate_headings")]
    public bool PropagateHeadings { get; set; } = true;

    /// <summary>Merge policy for adjacent elements. Default <see cref="Pipeline.MergePolicy.AnyInlineContent"/>.</summary>
    [JsonPropertyName("merge_policy")]
    public MergePolicy MergePolicy { get; set; } = MergePolicy.AnyInlineContent;

    /// <summary>Set the maximum tokens per chunk.</summary>
    public HybridChunkConfig WithMaxTokens(int n) { MaxTokens = n; return this; }

    /// <summary>Set the overlap tokens between chunks.</summary>
    public HybridChunkConfig WithOverlap(int n) { OverlapTokens = n; return this; }

    /// <summary>Set the merge policy for adjacent elements.</summary>
    public HybridChunkConfig WithMergePolicy(MergePolicy p) { MergePolicy = p; return this; }

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

    private static JsonSerializerOptions BuildOptions()
    {
        var o = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        };
        o.Converters.Add(new JsonStringEnumConverter());
        return o;
    }
}
