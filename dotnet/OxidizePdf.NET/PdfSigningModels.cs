using System.Text.Json;
using System.Text.Json.Serialization;

namespace OxidizePdf.NET;

/// <summary>Options for preparing a PDF for an external CMS signer.</summary>
public sealed class PdfSigningOptions
{
    /// <summary>Signature field name.</summary>
    [JsonPropertyName("field_name")] public string FieldName { get; set; } = "Signature1";
    /// <summary>Select an existing empty signature field.</summary>
    [JsonPropertyName("existing")] public bool Existing { get; set; }
    /// <summary>Zero-based page for a new field; leave zero for existing fields.</summary>
    [JsonPropertyName("page_index")] public uint PageIndex { get; set; }
    /// <summary>Optional widget index for an existing field.</summary>
    [JsonPropertyName("widget_index")] public uint? WidgetIndex { get; set; }
    /// <summary>Optional visible rectangle: left, bottom, right, top.</summary>
    [JsonPropertyName("rect")] public double[]? Rectangle { get; set; }
    /// <summary>Reserved CMS bytes; choose enough space before signing.</summary>
    [JsonPropertyName("placeholder_bytes")] public uint PlaceholderBytes { get; set; } = 16384;
    /// <summary>PDF signature handler.</summary>
    [JsonPropertyName("filter")] public string Filter { get; set; } = "Adobe.PPKLite";
    /// <summary>CMS encoding convention.</summary>
    [JsonPropertyName("sub_filter")] public string SubFilter { get; set; } = "adbe.pkcs7.detached";
    /// <summary>Signing reason.</summary>
    [JsonPropertyName("reason")] public string? Reason { get; set; }
    /// <summary>Signing location.</summary>
    [JsonPropertyName("location")] public string? Location { get; set; }
    /// <summary>Signer contact.</summary>
    [JsonPropertyName("contact_info")] public string? ContactInfo { get; set; }
    /// <summary>Optional PDF date string.</summary>
    [JsonPropertyName("signing_time")] public string? SigningTime { get; set; }
    /// <summary>DocMDP permission: 1 no changes, 2 form filling/signing, 3 annotations too.</summary>
    [JsonPropertyName("certification")] public byte? Certification { get; set; }
    /// <summary>Optional field locking policy.</summary>
    [JsonPropertyName("field_lock")] public PdfSignatureFieldLock? FieldLock { get; set; }
    /// <summary>Additional PDF signature dictionary values; reserved keys are rejected.</summary>
    [JsonPropertyName("additional_entries")] public Dictionary<string, PdfObjectValue> AdditionalEntries { get; set; } = [];
    /// <summary>Optional visible artwork.</summary>
    [JsonPropertyName("appearance")] public PdfSignatureAppearance? Appearance { get; set; }
}

/// <summary>FieldMDP policy for the signature.</summary>
public sealed class PdfSignatureFieldLock
{
    /// <summary>All, Include, or Exclude.</summary>
    [JsonPropertyName("mode")] public string Mode { get; set; } = "All";
    /// <summary>Field names for Include or Exclude; null for All.</summary>
    [JsonPropertyName("fields")] public string[]? Fields { get; set; }
}

/// <summary>Text and optional watermark for visible signing.</summary>
public sealed class PdfSignatureAppearance
{
    /// <summary>Optional signer name.</summary>
    [JsonPropertyName("signer_name")] public string? SignerName { get; set; }
    /// <summary>Optional displayed signing date.</summary>
    [JsonPropertyName("signing_date")] public string? SigningDate { get; set; }
    /// <summary>Additional displayed lines.</summary>
    [JsonPropertyName("text")] public string[] Text { get; set; } = [];
    /// <summary>Optional RGB watermark.</summary>
    [JsonPropertyName("watermark")] public PdfSignatureWatermark? Watermark { get; set; }
}

/// <summary>RGB image drawn behind the signature text.</summary>
public sealed class PdfSignatureWatermark
{
    /// <summary>Pixel width.</summary>
    [JsonPropertyName("width")] public uint Width { get; set; }
    /// <summary>Pixel height.</summary>
    [JsonPropertyName("height")] public uint Height { get; set; }
    /// <summary>Row-major RGB bytes, three per pixel.</summary>
    [JsonPropertyName("rgb")] public byte[] Rgb { get; set; } = [];
    /// <summary>Opacity between zero and one.</summary>
    [JsonPropertyName("opacity")] public float Opacity { get; set; } = 0.15f;
}

/// <summary>Validated text placement in the signature rectangle.</summary>
public sealed class PdfSignatureAppearanceLayout
{
    /// <summary>Fitted lines.</summary>
    [JsonPropertyName("lines")] public string[] Lines { get; set; } = [];
    /// <summary>Font size in points.</summary>
    [JsonPropertyName("font_size")] public double FontSize { get; set; }
    /// <summary>Inset in points.</summary>
    [JsonPropertyName("margin")] public double Margin { get; set; }
    /// <summary>First text baseline.</summary>
    [JsonPropertyName("first_baseline")] public double FirstBaseline { get; set; }
    /// <summary>Distance between baselines.</summary>
    [JsonPropertyName("line_height")] public double LineHeight { get; set; }
}

