using System.Text.Json;
using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Pipeline;

/// <summary>
/// Configuration for the document partitioner. Mirrors
/// <c>oxidize_pdf::pipeline::PartitionConfig</c> field-for-field.
/// </summary>
/// <remarks>
/// Fluent <c>With*</c> methods mutate this instance in place and return <c>this</c> —
/// the config is not immutable. Two references obtained from the same fluent chain
/// will alias the same object.
/// </remarks>
public class PartitionConfig
{
    /// <summary>JSON serialization options used by <see cref="ToJson"/>. Uses snake_case to match serde's default.</summary>
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>Whether to detect table structures. Default <c>true</c>.</summary>
    [JsonPropertyName("detect_tables")]
    public bool DetectTables { get; set; } = true;

    /// <summary>Whether to detect headers and footers by position. Default <c>true</c>.</summary>
    [JsonPropertyName("detect_headers_footers")]
    public bool DetectHeadersFooters { get; set; } = true;

    /// <summary>Minimum font-size ratio vs. median that classifies a fragment as a title. Default <c>1.3</c>.</summary>
    [JsonPropertyName("title_min_font_ratio")]
    public double TitleMinFontRatio { get; set; } = 1.3;

    /// <summary>Fraction of page height from the top eligible for header detection (0–1). Default <c>0.05</c>.</summary>
    [JsonPropertyName("header_zone")]
    public double HeaderZone { get; set; } = 0.05;

    /// <summary>Fraction of page height from the bottom eligible for footer detection (0–1). Default <c>0.05</c>.</summary>
    [JsonPropertyName("footer_zone")]
    public double FooterZone { get; set; } = 0.05;

    private ReadingOrderStrategy _readingOrder = ReadingOrderStrategy.Simple;

    /// <summary>Reading-order strategy applied to fragments before classification. Default <see cref="ReadingOrderStrategy.Simple"/>.</summary>
    [JsonPropertyName("reading_order")]
    public ReadingOrderStrategy ReadingOrder
    {
        get => _readingOrder;
        set => _readingOrder = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Minimum confidence threshold (0–1) for accepting a detected table. Default <c>0.5</c>.</summary>
    [JsonPropertyName("min_table_confidence")]
    public double MinTableConfidence { get; set; } = 0.5;

    /// <summary>Disable table detection.</summary>
    public PartitionConfig WithoutTables() { DetectTables = false; return this; }

    /// <summary>Disable header/footer detection.</summary>
    public PartitionConfig WithoutHeadersFooters() { DetectHeadersFooters = false; return this; }

    /// <summary>Set the minimum font-size ratio for title detection.</summary>
    public PartitionConfig WithTitleMinFontRatio(double ratio) { TitleMinFontRatio = ratio; return this; }

    /// <summary>Set the minimum table-confidence threshold.</summary>
    public PartitionConfig WithMinTableConfidence(double threshold) { MinTableConfidence = threshold; return this; }

    /// <summary>Set the reading-order strategy.</summary>
    /// <exception cref="ArgumentNullException">If <paramref name="strategy"/> is null.</exception>
    public PartitionConfig WithReadingOrder(ReadingOrderStrategy strategy)
    {
        ReadingOrder = strategy;  // setter validates
        return this;
    }

    /// <summary>
    /// Validate this configuration. Throws <see cref="ArgumentException"/> if any field is out of range.
    /// </summary>
    /// <remarks>
    /// This is a C#-side preflight. The Rust core <c>oxidize_pdf::pipeline::PartitionConfig</c>
    /// has no validation of its own; values outside sensible ranges may produce unexpected
    /// partitioning behaviour rather than a crash.
    /// </remarks>
    public void Validate()
    {
        if (TitleMinFontRatio <= 0)
            throw new ArgumentException("TitleMinFontRatio must be positive");
        if (HeaderZone < 0 || HeaderZone > 1)
            throw new ArgumentException("HeaderZone must be in [0, 1]");
        if (FooterZone < 0 || FooterZone > 1)
            throw new ArgumentException("FooterZone must be in [0, 1]");
        if (MinTableConfidence < 0 || MinTableConfidence > 1)
            throw new ArgumentException("MinTableConfidence must be in [0, 1]");
    }

    /// <summary>Serialize this configuration to JSON using <see cref="JsonOptions"/>.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
}
