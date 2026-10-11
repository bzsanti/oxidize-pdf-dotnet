using System.Text.Json.Serialization;
using OxidizePdf.NET.Models;

namespace OxidizePdf.NET;

/// <summary>Preferred source character encoding.</summary>
public enum PdfPreferredEncoding
{
    /// <summary>UTF-8.</summary>
    Utf8,
    /// <summary>ISO 8859-1.</summary>
    Latin1,
    /// <summary>Windows-1252.</summary>
    Windows1252,
    /// <summary>Legacy Mac Roman.</summary>
    MacRoman,
    /// <summary>PDF document encoding.</summary>
    PdfDocEncoding,
    /// <summary>Mixed/unknown source encoding.</summary>
    Mixed
}
/// <summary>All Rust parser options. Defaults match ParseOptions::default() in core 5.4.2.
/// Legacy recovery preferences do not enable incomplete Flate decoding; use RecoverAsync explicitly.</summary>
public sealed class PdfParserOptions
{
    /// <summary>Enforce strict PDF syntax.</summary>
    public bool StrictMode { get; set; } = true;
    /// <summary>Legacy structural recovery preference; does not enable partial Flate output.</summary>
    public bool RecoverFromStreamErrors { get; set; } = false;
    /// <summary>Legacy preference; normal Flate errors are still returned.</summary>
    public bool IgnoreCorruptStreams { get; set; } = false;
    /// <summary>Legacy partial-content preference; no implicit Flate recovery.</summary>
    public bool PartialContentAllowed { get; set; } = false;
    /// <summary>Maximum structural recovery attempts.</summary>
    public ulong MaxRecoveryAttempts { get; set; } = 3;
    /// <summary>Request recovery logging where supported by core.</summary>
    public bool LogRecoveryDetails { get; set; } = false;
    /// <summary>Recover incorrect stream lengths.</summary>
    public bool LenientStreams { get; set; } = false;
    /// <summary>Maximum structural stream-length search distance.</summary>
    public ulong MaxRecoveryBytes { get; set; } = 1000;
    /// <summary>Collect recoverable parser warnings.</summary>
    public bool CollectWarnings { get; set; } = false;
    /// <summary>Use replacement characters for invalid source encoding.</summary>
    public bool LenientEncoding { get; set; } = true;
    /// <summary>Preferred source encoding; null selects automatic detection.</summary>
    public PdfPreferredEncoding? PreferredEncoding { get; set; } = null;
    /// <summary>Enable syntax recovery.</summary>
    public bool LenientSyntax { get; set; } = false;
    /// <summary>Rust strict preset, disabling structural recovery and lenient encoding.</summary>
    public static PdfParserOptions Strict() => new() { MaxRecoveryAttempts = 0, MaxRecoveryBytes = 0, LenientEncoding = false };
    /// <summary>Rust tolerant/lenient preset; Flate decoding remains strict.</summary>
    public static PdfParserOptions Tolerant() => new()
    {
        StrictMode = false,
        RecoverFromStreamErrors = true,
        PartialContentAllowed = true,
        MaxRecoveryAttempts = 5,
        LogRecoveryDetails = true,
        LenientStreams = true,
        MaxRecoveryBytes = 5000,
        CollectWarnings = true,
        LenientSyntax = true
    };
    /// <summary>Rust legacy skip-errors preset; does not suppress Flate errors.</summary>
    public static PdfParserOptions SkipErrors() => new()
    {
        StrictMode = false,
        RecoverFromStreamErrors = true,
        IgnoreCorruptStreams = true,
        PartialContentAllowed = true,
        MaxRecoveryAttempts = 1,
        LenientStreams = true,
        MaxRecoveryBytes = 5000,
        LenientSyntax = true
    };
}
/// <summary>Complete extraction settings, including diagnostic spacing decisions and NBSP normalization.</summary>
public sealed class PdfTextExtractionOptions
{
    /// <summary>Preserve text layout.</summary>
    public bool PreserveLayout { get; set; } = false;
    /// <summary>Space insertion threshold in text units.</summary>
    public double SpaceThreshold { get; set; } = 0.3;
    /// <summary>Vertical distance threshold in page units.</summary>
    public double NewlineThreshold { get; set; } = 10;
    /// <summary>Sort fragments by position.</summary>
    public bool SortByPosition { get; set; } = true;
    /// <summary>Detect columns.</summary>
    public bool DetectColumns { get; set; } = false;
    /// <summary>Column separation threshold.</summary>
    public double ColumnThreshold { get; set; } = 50;
    /// <summary>Merge hyphenated line endings.</summary>
    public bool MergeHyphenated { get; set; } = true;
    /// <summary>TJ kerning threshold relative to font size.</summary>
    public double TjSpaceThreshold { get; set; } = 0.2;
    /// <summary>Group text into lines and paragraphs.</summary>
    public bool ReconstructParagraphs { get; set; } = false;
    /// <summary>Include marked artifact text.</summary>
    public bool IncludeArtifacts { get; set; } = false;
    /// <summary>Reorder flat output by column.</summary>
    public bool ReorderColumns { get; set; } = false;
    /// <summary>Apply scale-relative reading order.</summary>
    public bool ReadingOrder { get; set; } = false;
    /// <summary>Standalone carriage-return policy.</summary>
    public CarriageReturnHandling CarriageReturnHandling { get; set; } = CarriageReturnHandling.Remove;
    /// <summary>Per-page emitted-text budget; null is unlimited, zero permits no emitted bytes.</summary>
    public ulong? MaxExtractedBytes { get; set; } = null;
    /// <summary>Append URI targets as text without following them.</summary>
    public bool IncludeLinkAnnotations { get; set; } = false;
    /// <summary>Retain unreliable figure-font text.</summary>
    public bool IncludeUnreliableFigureText { get; set; } = false;
    /// <summary>Normalize NBSP to ordinary spaces.</summary>
    public bool NormalizeNonBreakingSpaces { get; set; } = false;
    /// <summary>Retain per-fragment space-insertion diagnostics.</summary>
    public bool TrackSpaceDecisions { get; set; } = false;
}
/// <summary>PdfRecoveryLocation values reported by core.</summary>
public enum PdfRecoveryLocation
{
    /// <summary>Page content stream.</summary>
    PageContents,
    /// <summary>Invoked Form XObject.</summary>
    FormXObject,
}
/// <summary>PdfRecoveryAction values reported by core.</summary>
public enum PdfRecoveryAction
{
    /// <summary>Decoded output with an integrity/completion limitation.</summary>
    Recovered,
    /// <summary>Stream could not be decoded and was omitted.</summary>
    Omitted,
}
/// <summary>PdfFlateRecoveryKind values reported by core.</summary>
public enum PdfFlateRecoveryKind
{
    /// <summary>Integrity could not be verified.</summary>
    Unverified,
    /// <summary>Decoded content is incomplete.</summary>
    Incomplete,
}
/// <summary>PdfExtractedColorSpace values reported by core.</summary>
public enum PdfExtractedColorSpace
{
    /// <summary>RGB.</summary>
    Rgb,
    /// <summary>Grayscale.</summary>
    Gray,
    /// <summary>CMYK.</summary>
    Cmyk,
}
/// <summary>PdfExtractedColor returned by text extraction.</summary>
public sealed class PdfExtractedColor
{
    /// <summary>Original color space.</summary>
    [JsonPropertyName("space")] public PdfExtractedColorSpace Space { get; init; } = default;
    /// <summary>Color components without RGB conversion.</summary>
    [JsonPropertyName("components")] public double[] Components { get; init; } = [];
}
/// <summary>PdfSpaceDecision returned by text extraction.</summary>
public sealed class PdfSpaceDecision
{
    /// <summary>Character offset.</summary>
    [JsonPropertyName("offset")] public ulong Offset { get; init; } = 0;
    /// <summary>Horizontal gap.</summary>
    [JsonPropertyName("dx")] public double Dx { get; init; } = 0;
    /// <summary>Applied threshold.</summary>
    [JsonPropertyName("threshold")] public double Threshold { get; init; } = 0;
    /// <summary>Decision confidence.</summary>
    [JsonPropertyName("confidence")] public double Confidence { get; init; } = 0;
    /// <summary>Whether a space was inserted.</summary>
    [JsonPropertyName("inserted")] public bool Inserted { get; init; } = false;
}
/// <summary>PdfTextRecoveryDiagnostic returned by text extraction.</summary>
public sealed class PdfTextRecoveryDiagnostic
{
    /// <summary>Content or Form location.</summary>
    [JsonPropertyName("location")] public PdfRecoveryLocation Location { get; init; } = default;
    /// <summary>Zero-based Contents index, when applicable.</summary>
    [JsonPropertyName("contents_index")] public ulong? ContentsIndex { get; init; } = null;
    /// <summary>Invoked Form resource name.</summary>
    [JsonPropertyName("form_name")] public string? FormName { get; init; } = null;
    /// <summary>Form object number and generation.</summary>
    [JsonPropertyName("object_id")] public uint[]? ObjectId { get; init; } = null;
    /// <summary>Recovery or omission.</summary>
    [JsonPropertyName("action")] public PdfRecoveryAction Action { get; init; } = default;
    /// <summary>Filter position in the decoding chain.</summary>
    [JsonPropertyName("filter_index")] public ulong? FilterIndex { get; init; } = null;
    /// <summary>Recovery limitation, absent for omitted content.</summary>
    [JsonPropertyName("kind")] public PdfFlateRecoveryKind? Kind { get; init; } = null;
    /// <summary>Original decoder error.</summary>
    [JsonPropertyName("error")] public string Error { get; init; } = "";
}
/// <summary>PdfExtractedPage returned by text extraction.</summary>
public sealed class PdfExtractedPage
{
    /// <summary>Zero-based requested page index.</summary>
    [JsonPropertyName("page_index")] public uint PageIndex { get; init; } = 0;
    /// <summary>Extracted text, fragments and truncation flag.</summary>
    [JsonPropertyName("text")] public PageTextFragments Text { get; init; } = new();
    /// <summary>All recovered/omitted Flate streams; empty does not certify the PDF.</summary>
    [JsonPropertyName("diagnostics")] public PdfTextRecoveryDiagnostic[] Diagnostics { get; init; } = [];
}
