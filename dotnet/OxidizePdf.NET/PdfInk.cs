using System.Text.Json.Serialization;

namespace OxidizePdf.NET;

/// <summary>Stable indirect-object identity of an Ink annotation.</summary>
public readonly record struct PdfInkId(
    [property: JsonPropertyName("object_number")] uint ObjectNumber,
    [property: JsonPropertyName("generation_number")] ushort GenerationNumber);

/// <summary>A standard Ink annotation, including geometry and appearance.</summary>
public sealed class PdfInk
{
    /// <summary>Stable object identity.</summary>
    [JsonPropertyName("id")] public PdfInkId Id { get; set; }
    /// <summary>Zero-based page index.</summary>
    [JsonPropertyName("page_index")] public uint PageIndex { get; set; }
    /// <summary>Bounding rectangle (left, bottom, right, top), including stroke width.</summary>
    [JsonPropertyName("rect")] public double[] Rectangle { get; set; } = [];
    /// <summary>Each stroke is a flat sequence of x/y pairs in page coordinates.</summary>
    [JsonPropertyName("strokes")] public double[][] Strokes { get; set; } = [];
    /// <summary>DeviceGray (one), RGB (three), or CMYK (four) components in [0, 1].</summary>
    [JsonPropertyName("color")] public double[] Color { get; set; } = [];
    /// <summary>Positive stroke width in page units.</summary>
    [JsonPropertyName("width")] public double Width { get; set; }
    /// <summary>Optional opacity in [0, 1].</summary>
    [JsonPropertyName("opacity")] public double? Opacity { get; set; }
}

/// <summary>An atomic Ink annotation mutation. Unrelated metadata keys are preserved on update.</summary>
public abstract record PdfInkMutation
{
    private PdfInkMutation() { }
    /// <summary>Adds strokes to a zero-based page. Each stroke contains x/y pairs.</summary>
    public sealed record Add(uint PageIndex, double[][] Strokes, double[] Color,
        double Width = 1, double? Opacity = null) : PdfInkMutation;
    /// <summary>Updates strokes and appearance while preserving identity.</summary>
    public sealed record Update(PdfInkId Id, double[][] Strokes, double[] Color,
        double Width = 1, double? Opacity = null) : PdfInkMutation;
    /// <summary>Removes an existing annotation reference.</summary>
    public sealed record Remove(PdfInkId Id) : PdfInkMutation;
}

/// <summary>Updated PDF and all current Ink annotations.</summary>
public sealed class PdfInkUpdate
{
    /// <summary>Complete PDF bytes; the input remains an exact prefix.</summary>
    [JsonPropertyName("pdf_bytes")] public byte[] PdfBytes { get; set; } = [];
    /// <summary>Complete current Ink annotation set.</summary>
    [JsonPropertyName("annotations")] public List<PdfInk> Annotations { get; set; } = [];
}
