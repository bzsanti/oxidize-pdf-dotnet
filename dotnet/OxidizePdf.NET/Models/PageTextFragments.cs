using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Models;

/// <summary>Positioned extraction results for a single page.</summary>
public sealed class PageTextFragments
{
    /// <summary>Layout-preserving plain text.</summary>
    [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
    /// <summary>Whether the decoded text byte limit stopped extraction early.</summary>
    [JsonPropertyName("truncated")] public bool Truncated { get; set; }
    /// <summary>Extracted runs with position, font and rendering metadata.</summary>
    [JsonPropertyName("fragments")] public List<ExtractedTextFragment> Fragments { get; set; } = [];
}

/// <summary>A positioned text run extracted from a PDF page.</summary>
public sealed class ExtractedTextFragment
{
    /// <summary>Text content.</summary>
    [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
    /// <summary>Horizontal position in page coordinates.</summary>
    [JsonPropertyName("x")] public double X { get; set; }
    /// <summary>Vertical position in page coordinates.</summary>
    [JsonPropertyName("y")] public double Y { get; set; }
    /// <summary>Fragment width in page units.</summary>
    [JsonPropertyName("width")] public double Width { get; set; }
    /// <summary>Fragment height in page units.</summary>
    [JsonPropertyName("height")] public double Height { get; set; }
    /// <summary>Font size in points.</summary>
    [JsonPropertyName("font_size")] public double FontSize { get; set; }
    /// <summary>Resolved font name, if known.</summary>
    [JsonPropertyName("font_name")] public string? FontName { get; set; }
    /// <summary>Whether the font is bold.</summary>
    [JsonPropertyName("is_bold")] public bool IsBold { get; set; }
    /// <summary>Whether the font is italic.</summary>
    [JsonPropertyName("is_italic")] public bool IsItalic { get; set; }
    /// <summary>PDF Tr mode; invisible text is retained.</summary>
    [JsonPropertyName("render_mode")] public TextRenderingMode RenderMode { get; set; }
    /// <summary>Innermost marked-content identifier, if any.</summary>
    [JsonPropertyName("mcid")] public uint? MarkedContentId { get; set; }
    /// <summary>Owning structural marked-content tag, if any.</summary>
    [JsonPropertyName("struct_tag")] public string? StructureTag { get; set; }
}
