using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Models;

/// <summary>A bookmark in a preorder list; parent indexes retain the full hierarchy.</summary>
public sealed class PdfBookmark
{
    /// <summary>Visible bookmark title.</summary>
    [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
    /// <summary>Index of the parent in the returned list, or null for root items.</summary>
    [JsonPropertyName("parent_index")] public int? ParentIndex { get; set; }
    /// <summary>Resolved local destination, or null when absent or not a local GoTo action.</summary>
    [JsonPropertyName("destination")] public BookmarkDestination? Destination { get; set; }
    /// <summary>Optional RGB components, in the range zero to one.</summary>
    [JsonPropertyName("color")] public double[]? Color { get; set; }
    /// <summary>Whether the title is bold.</summary>
    [JsonPropertyName("is_bold")] public bool IsBold { get; set; }
    /// <summary>Whether the title is italic.</summary>
    [JsonPropertyName("is_italic")] public bool IsItalic { get; set; }
    /// <summary>Whether children are initially expanded.</summary>
    [JsonPropertyName("is_open")] public bool IsOpen { get; set; }
}

/// <summary>A resolved destination preserving all standard PDF view modes.</summary>
public sealed class BookmarkDestination
{
    /// <summary>Zero-based target page index.</summary>
    [JsonPropertyName("page_index")] public uint PageIndex { get; set; }
    /// <summary>PDF destination view mode.</summary>
    [JsonPropertyName("view_mode")] public BookmarkViewMode ViewMode { get; set; }
    /// <summary>Optional left parameter; null means unspecified or inapplicable.</summary>
    [JsonPropertyName("left")] public double? Left { get; set; }
    /// <summary>Optional top parameter; null means unspecified or inapplicable.</summary>
    [JsonPropertyName("top")] public double? Top { get; set; }
    /// <summary>Optional right parameter; null means unspecified or inapplicable.</summary>
    [JsonPropertyName("right")] public double? Right { get; set; }
    /// <summary>Optional bottom parameter; null means unspecified or inapplicable.</summary>
    [JsonPropertyName("bottom")] public double? Bottom { get; set; }
    /// <summary>Optional zoom parameter; null means unspecified or inapplicable.</summary>
    [JsonPropertyName("zoom")] public double? Zoom { get; set; }
}

/// <summary>Standard PDF destination view modes.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<BookmarkViewMode>))]
public enum BookmarkViewMode
{
    /// <summary>Position at left and top with optional zoom.</summary>
    XYZ,
    /// <summary>Fit the entire page.</summary>
    Fit,
    /// <summary>Fit page width at top.</summary>
    FitH,
    /// <summary>Fit page height at left.</summary>
    FitV,
    /// <summary>Fit the specified rectangle.</summary>
    FitR,
    /// <summary>Fit the page content bounding box.</summary>
    FitB,
    /// <summary>Fit bounding box width at top.</summary>
    FitBH,
    /// <summary>Fit bounding box height at left.</summary>
    FitBV,
}
