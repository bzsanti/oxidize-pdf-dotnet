using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Models;

/// <summary>
/// Analysis of a PDF page's content composition (text vs scanned/image).
/// </summary>
public class ContentAnalysis
{
    /// <summary>Page content type classification.</summary>
    [JsonPropertyName("page_type")]
    public ContentPageType PageType { get; set; } = ContentPageType.Unknown;

    /// <summary>Total number of text characters on the page.</summary>
    [JsonPropertyName("character_count")]
    public int CharacterCount { get; set; }

    /// <summary>Whether the page has at least one content stream.</summary>
    [JsonPropertyName("has_content_stream")]
    public bool HasContentStream { get; set; }

    /// <summary>Number of XObjects on the page.</summary>
    [JsonPropertyName("image_count")]
    public int ImageCount { get; set; }

    /// <summary>Whether this page is classified as scanned.</summary>
    [JsonIgnore]
    public bool IsScanned => PageType == ContentPageType.Scanned;

    /// <summary>Whether this page is classified as primarily text.</summary>
    [JsonIgnore]
    public bool IsText => PageType == ContentPageType.Text;

    /// <summary>Whether this page is classified as mixed content.</summary>
    [JsonIgnore]
    public bool IsMixed => PageType == ContentPageType.Mixed;
}
