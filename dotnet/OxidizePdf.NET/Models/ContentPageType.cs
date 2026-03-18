using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Models;

/// <summary>
/// Classification of a PDF page's primary content type.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ContentPageType
{
    /// <summary>Page has no classifiable content.</summary>
    Unknown,

    /// <summary>Page contains primarily vector text.</summary>
    Text,

    /// <summary>Page contains primarily scanned images with little or no text.</summary>
    Scanned,

    /// <summary>Page contains a mix of text and images.</summary>
    Mixed,
}
