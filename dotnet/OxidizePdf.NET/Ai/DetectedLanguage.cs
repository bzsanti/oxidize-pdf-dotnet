using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Ai;

/// <summary>
/// A detected language. Mirrors <c>oxidize_pdf::ai::DetectedLanguage</c>
/// (added upstream in 2.13.0). Produced by per-chunk language detection
/// (<see cref="DocumentChunker.WithLanguageDetection(bool)"/>) and aggregated by
/// <see cref="DocumentChunker.DocumentLanguage(System.Collections.Generic.IEnumerable{DocumentChunk})"/>.
/// </summary>
public class DetectedLanguage
{
    /// <summary>ISO 639-3 language code (e.g. <c>"eng"</c>, <c>"spa"</c>).</summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>Detector confidence in <c>[0.0, 1.0]</c>.</summary>
    [JsonPropertyName("confidence")]
    public float Confidence { get; set; }

    /// <summary>Whether the detector considers this detection reliable.</summary>
    [JsonPropertyName("reliable")]
    public bool Reliable { get; set; }
}
