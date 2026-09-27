using System.Text.Json.Serialization;

namespace OxidizePdf.NET;

/// <summary>Stable indirect-object identity of a FreeText annotation.</summary>
public readonly record struct PdfFreeTextId(
    [property: JsonPropertyName("object_number")] uint ObjectNumber,
    [property: JsonPropertyName("generation_number")] ushort GenerationNumber);

/// <summary>Horizontal alignment of FreeText contents.</summary>
public enum PdfFreeTextAlignment
{
    /// <summary>Left justified.</summary>
    Left = 0,
    /// <summary>Centered.</summary>
    Center = 1,
    /// <summary>Right justified.</summary>
    Right = 2
}

/// <summary>A standard FreeText annotation in an existing PDF.</summary>
public sealed class PdfFreeText
{
    /// <summary>Stable object identity.</summary>
    [JsonPropertyName("id")] public PdfFreeTextId Id { get; set; }
    /// <summary>Zero-based page index.</summary>
    [JsonPropertyName("page_index")] public uint PageIndex { get; set; }
    /// <summary>Rectangle: left, bottom, right, top.</summary>
    [JsonPropertyName("rect")] public double[] Rectangle { get; set; } = [];
    /// <summary>Unicode annotation contents.</summary>
    [JsonPropertyName("contents")] public string Contents { get; set; } = string.Empty;
    /// <summary>PDF default appearance string, such as /Helv 12 Tf 0 g.</summary>
    [JsonPropertyName("default_appearance")] public string DefaultAppearance { get; set; } = string.Empty;
    /// <summary>Horizontal alignment.</summary>
    [JsonPropertyName("alignment")] public PdfFreeTextAlignment Alignment { get; set; }
}

/// <summary>An atomic FreeText annotation change.</summary>
public abstract record PdfFreeTextMutation
{
    private PdfFreeTextMutation() { }
    /// <summary>Adds an annotation to a zero-based page with a left/bottom/right/top rectangle.</summary>
    public sealed record Add(uint PageIndex, double[] Rectangle, string Contents,
        string DefaultAppearance = "/Helv 12 Tf 0 g", PdfFreeTextAlignment Alignment = PdfFreeTextAlignment.Left) : PdfFreeTextMutation;
    /// <summary>Updates geometry, contents and appearance while preserving identity and unrelated dictionary keys.</summary>
    public sealed record Update(PdfFreeTextId Id, double[] Rectangle, string Contents,
        string DefaultAppearance = "/Helv 12 Tf 0 g", PdfFreeTextAlignment Alignment = PdfFreeTextAlignment.Left) : PdfFreeTextMutation;
    /// <summary>Removes an existing annotation reference.</summary>
    public sealed record Remove(PdfFreeTextId Id) : PdfFreeTextMutation;
}

/// <summary>Updated PDF and all current FreeText annotations.</summary>
public sealed class PdfFreeTextUpdate
{
    /// <summary>Complete PDF bytes with the input as an exact prefix.</summary>
    [JsonPropertyName("pdf_bytes")] public byte[] PdfBytes { get; set; } = [];
    /// <summary>Complete current FreeText set after the batch.</summary>
    [JsonPropertyName("annotations")] public List<PdfFreeText> Annotations { get; set; } = [];
}
