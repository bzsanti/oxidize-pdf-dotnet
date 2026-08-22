using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Models;

/// <summary>Renderer-ready font resource resolved from a parsed PDF page.</summary>
public sealed class ResolvedFontResource
{
    /// <summary>Page resource dictionary name.</summary>
    [JsonPropertyName("resource_name")] public string ResourceName { get; set; } = string.Empty;
    /// <summary>PDF BaseFont or Type 3 Name.</summary>
    [JsonPropertyName("base_font")] public string? BaseFont { get; set; }
    /// <summary>Concrete font subtype.</summary>
    [JsonPropertyName("subtype")] public string Subtype { get; set; } = string.Empty;
    /// <summary>Named PDF encoding, when present.</summary>
    [JsonPropertyName("encoding")] public string? Encoding { get; set; }
    /// <summary>Horizontal or vertical writing mode.</summary>
    [JsonPropertyName("writing_mode")] public string WritingMode { get; set; } = string.Empty;
    /// <summary>Simple-font character-code name overrides.</summary>
    [JsonPropertyName("differences")] public Dictionary<byte, string> Differences { get; set; } = new();
    /// <summary>Declared embedded font-program format.</summary>
    [JsonPropertyName("embedded_format")] public string? EmbeddedFormat { get; set; }
    /// <summary>Base64-encoded decoded embedded font program.</summary>
    [JsonPropertyName("embedded_data")] public string? EmbeddedDataBase64 { get; set; }
    /// <summary>Type 3 details, present only for Type 3 resources.</summary>
    [JsonPropertyName("type3")] public ResolvedType3Font? Type3 { get; set; }

    /// <summary>Decoded embedded font-program bytes, or null for non-embedded fonts.</summary>
    [JsonIgnore] public byte[]? EmbeddedData => EmbeddedDataBase64 is null ? null : Convert.FromBase64String(EmbeddedDataBase64);
}

/// <summary>Resolved Type 3 font geometry and glyph programs.</summary>
public sealed class ResolvedType3Font
{
    /// <summary>Glyph-space to text-space transformation.</summary>
    [JsonPropertyName("font_matrix")] public double[] FontMatrix { get; set; } = [];
    /// <summary>Font bounding box in glyph space.</summary>
    [JsonPropertyName("font_bbox")] public double[] FontBoundingBox { get; set; } = [];
    /// <summary>Resolved character procedures.</summary>
    [JsonPropertyName("glyphs")] public List<ResolvedType3Glyph> Glyphs { get; set; } = [];
}

/// <summary>Metadata for one bounded, parsed Type 3 character procedure.</summary>
public sealed class ResolvedType3Glyph
{
    /// <summary>Character code.</summary>
    [JsonPropertyName("code")] public byte Code { get; set; }
    /// <summary>Glyph name selected by the font encoding.</summary>
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    /// <summary>Advance from the Widths array.</summary>
    [JsonPropertyName("width")] public double Width { get; set; }
    /// <summary>X displacement declared by d0 or d1.</summary>
    [JsonPropertyName("procedure_width_x")] public double ProcedureWidthX { get; set; }
    /// <summary>Y displacement declared by d0 or d1.</summary>
    [JsonPropertyName("procedure_width_y")] public double ProcedureWidthY { get; set; }
    /// <summary>Optional glyph bounding box declared by d1.</summary>
    [JsonPropertyName("bbox")] public double[]? BoundingBox { get; set; }
    /// <summary>Count of bounded renderer-neutral drawing operations.</summary>
    [JsonPropertyName("operation_count")] public int OperationCount { get; set; }
}
