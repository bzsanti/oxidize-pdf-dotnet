using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Models;

/// <summary>
/// Metadata extracted from an existing PDF document's Info dictionary.
/// </summary>
public class PdfMetadata
{
    /// <summary>Document title from the Info dictionary.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>Document author from the Info dictionary.</summary>
    [JsonPropertyName("author")]
    public string? Author { get; set; }

    /// <summary>Document subject from the Info dictionary.</summary>
    [JsonPropertyName("subject")]
    public string? Subject { get; set; }

    /// <summary>Document keywords from the Info dictionary.</summary>
    [JsonPropertyName("keywords")]
    public string? Keywords { get; set; }

    /// <summary>Application that created the original document.</summary>
    [JsonPropertyName("creator")]
    public string? Creator { get; set; }

    /// <summary>Application that produced the PDF.</summary>
    [JsonPropertyName("producer")]
    public string? Producer { get; set; }

    /// <summary>Document creation date as a string (PDF date format).</summary>
    [JsonPropertyName("creation_date")]
    public string? CreationDate { get; set; }

    /// <summary>Document last modification date as a string (PDF date format).</summary>
    [JsonPropertyName("modification_date")]
    public string? ModificationDate { get; set; }

    /// <summary>PDF version string (e.g., "1.4", "1.7").</summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>Number of pages in the document.</summary>
    [JsonPropertyName("page_count")]
    public int? PageCount { get; set; }
}
