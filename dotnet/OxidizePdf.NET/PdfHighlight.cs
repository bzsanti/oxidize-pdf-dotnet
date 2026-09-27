using System.Text.Json.Serialization;

namespace OxidizePdf.NET;

/// <summary>Stable indirect-object identity of a highlight.</summary>
public readonly record struct PdfHighlightId(
    [property: JsonPropertyName("object_number")] uint ObjectNumber,
    [property: JsonPropertyName("generation_number")] ushort GenerationNumber);

/// <summary>A standard Highlight annotation in an existing PDF.</summary>
public sealed class PdfHighlight
{
    /// <summary>Stable object identity.</summary>
    [JsonPropertyName("id")] public PdfHighlightId Id { get; set; }
    /// <summary>Zero-based page index.</summary>
    [JsonPropertyName("page_index")] public uint PageIndex { get; set; }
    /// <summary>Bounding rectangle: left, bottom, right, top.</summary>
    [JsonPropertyName("rect")] public double[] Rectangle { get; set; } = [];
    /// <summary>Each quad contains eight coordinates in PDF QuadPoints order: first edge start/end, then opposite edge start/end.</summary>
    [JsonPropertyName("quadrilaterals")] public double[][] Quadrilaterals { get; set; } = [];
    /// <summary>Three RGB components in the inclusive range zero to one.</summary>
    [JsonPropertyName("color")] public double[] Color { get; set; } = [];
    /// <summary>Optional opacity, from zero to one.</summary>
    [JsonPropertyName("opacity")] public double? Opacity { get; set; }
    /// <summary>Optional annotation text.</summary>
    [JsonPropertyName("contents")] public string? Contents { get; set; }
}

/// <summary>Highlight changes available in upstream 5.1.4. Replace a highlight with an atomic Remove/Add batch; the replacement receives a new identity.</summary>
public abstract record PdfHighlightMutation
{
    private PdfHighlightMutation() { }
    /// <summary>Adds a highlight with one or more convex quadrilaterals to a zero-based page.</summary>
    public sealed record Add(uint PageIndex, double[][] Quadrilaterals, double[] Color,
        double? Opacity = null, string? Contents = null) : PdfHighlightMutation;
    /// <summary>Removes the highlight identified by its indirect reference.</summary>
    public sealed record Remove(PdfHighlightId Id) : PdfHighlightMutation;
}

/// <summary>Updated PDF and complete current highlight set after an atomic revision.</summary>
public sealed class PdfHighlightUpdate
{
    /// <summary>Complete PDF bytes; the input remains an exact prefix.</summary>
    [JsonPropertyName("pdf_bytes")] public byte[] PdfBytes { get; set; } = [];
    /// <summary>All highlights remaining after the batch, including existing ones.</summary>
    [JsonPropertyName("highlights")] public List<PdfHighlight> Highlights { get; set; } = [];
}
