using System.Text.Json.Serialization;

namespace OxidizePdf.NET;

/// <summary>Stable identity of a geometric annotation.</summary>
public readonly record struct PdfGeometricId(
    [property: JsonPropertyName("object_number")] uint ObjectNumber,
    [property: JsonPropertyName("generation_number")] ushort GenerationNumber);

/// <summary>Standard geometric annotation subtype.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PdfGeometricKind>))]
public enum PdfGeometricKind
{
    /// <summary>Line segment.</summary>
    Line,
    /// <summary>Rectangle.</summary>
    Square,
    /// <summary>Ellipse inside a rectangle.</summary>
    Circle,
    /// <summary>Closed polygon.</summary>
    Polygon,
    /// <summary>Open sequence of line segments.</summary>
    PolyLine
}

/// <summary>Standard line-end decoration.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PdfAnnotationLineEnding>))]
public enum PdfAnnotationLineEnding
{
    /// <summary>No decoration.</summary>
    None,
    /// <summary>Square.</summary>
    Square,
    /// <summary>Circle.</summary>
    Circle,
    /// <summary>Diamond.</summary>
    Diamond,
    /// <summary>Open arrow.</summary>
    OpenArrow,
    /// <summary>Closed arrow.</summary>
    ClosedArrow,
    /// <summary>Butt.</summary>
    Butt,
    /// <summary>Reverse open arrow.</summary>
    ROpenArrow,
    /// <summary>Reverse closed arrow.</summary>
    RClosedArrow,
    /// <summary>Slash.</summary>
    Slash
}

/// <summary>Geometry in page coordinates. Populate only fields applicable to Kind.</summary>
public sealed class PdfGeometricGeometry
{
    /// <summary>Annotation subtype.</summary>
    [JsonPropertyName("kind")] public PdfGeometricKind Kind { get; set; }
    /// <summary>Line start (x, y), required for Line only.</summary>
    [JsonPropertyName("start"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double[]? Start { get; set; }
    /// <summary>Line end (x, y), required for Line only.</summary>
    [JsonPropertyName("end"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double[]? End { get; set; }
    /// <summary>Left, bottom, right, top; required for Square and Circle.</summary>
    [JsonPropertyName("rect"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double[]? Rectangle { get; set; }
    /// <summary>Ordered x/y pairs; required for Polygon and PolyLine.</summary>
    [JsonPropertyName("vertices"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double[][]? Vertices { get; set; }
    /// <summary>Required for Line and PolyLine; use None for no decoration.</summary>
    [JsonPropertyName("start_ending"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public PdfAnnotationLineEnding? StartEnding { get; set; }
    /// <summary>Required for Line and PolyLine; use None for no decoration.</summary>
    [JsonPropertyName("end_ending"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public PdfAnnotationLineEnding? EndEnding { get; set; }
}

/// <summary>Stroke, fill and transparency for geometric annotations.</summary>
public sealed class PdfGeometricStyle
{
    /// <summary>Gray (one), RGB (three), or CMYK (four) components in [0, 1].</summary>
    [JsonPropertyName("stroke_color")] public double[] StrokeColor { get; set; } = [0];
    /// <summary>Optional fill/interior color with one, three or four components.</summary>
    [JsonPropertyName("fill_color")] public double[]? FillColor { get; set; }
    /// <summary>Positive stroke width in page units.</summary>
    [JsonPropertyName("width")] public double Width { get; set; } = 1;
    /// <summary>Optional nonnegative dash lengths, not all zero (up to 256 values).</summary>
    [JsonPropertyName("dash_pattern")] public double[]? DashPattern { get; set; }
    /// <summary>Optional opacity in [0, 1].</summary>
    [JsonPropertyName("opacity")] public double? Opacity { get; set; }
}

/// <summary>A geometric annotation read from an existing PDF.</summary>
public sealed class PdfGeometricAnnotation
{
    /// <summary>Stable object identity.</summary>
    [JsonPropertyName("id")] public PdfGeometricId Id { get; set; }
    /// <summary>Zero-based page index.</summary>
    [JsonPropertyName("page_index")] public uint PageIndex { get; set; }
    /// <summary>Annotation geometry.</summary>
    [JsonPropertyName("geometry")] public PdfGeometricGeometry Geometry { get; set; } = new();
    /// <summary>Stroke and fill style.</summary>
    [JsonPropertyName("style")] public PdfGeometricStyle Style { get; set; } = new();
}

/// <summary>An atomic geometric annotation change.</summary>
public abstract record PdfGeometricMutation
{
    private PdfGeometricMutation() { }
    /// <summary>Adds an annotation to a zero-based page.</summary>
    public sealed record Add(uint PageIndex, PdfGeometricGeometry Geometry, PdfGeometricStyle Style) : PdfGeometricMutation;
    /// <summary>Updates an annotation while preserving identity and unrelated keys.</summary>
    public sealed record Update(PdfGeometricId Id, PdfGeometricGeometry Geometry, PdfGeometricStyle Style) : PdfGeometricMutation;
    /// <summary>Removes an annotation reference.</summary>
    public sealed record Remove(PdfGeometricId Id) : PdfGeometricMutation;
}

/// <summary>Updated PDF and all current geometric annotations.</summary>
public sealed class PdfGeometricUpdate
{
    /// <summary>Complete PDF bytes with the input preserved as an exact prefix.</summary>
    [JsonPropertyName("pdf_bytes")] public byte[] PdfBytes { get; set; } = [];
    /// <summary>Complete current geometric annotation set.</summary>
    [JsonPropertyName("annotations")] public List<PdfGeometricAnnotation> Annotations { get; set; } = [];
}
