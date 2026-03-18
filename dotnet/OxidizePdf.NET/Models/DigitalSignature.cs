using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Models;

/// <summary>
/// Information about a digital signature field in a PDF document.
/// </summary>
public class DigitalSignature
{
    /// <summary>Field name (from /T entry).</summary>
    [JsonPropertyName("field_name")]
    public string? FieldName { get; set; }

    /// <summary>Signature filter (e.g., "Adobe.PPKLite").</summary>
    [JsonPropertyName("filter")]
    public string Filter { get; set; } = string.Empty;

    /// <summary>Signature sub-filter (e.g., "adbe.pkcs7.detached", "ETSI.CAdES.detached").</summary>
    [JsonPropertyName("sub_filter")]
    public string? SubFilter { get; set; }

    /// <summary>Signing reason (from /Reason entry).</summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    /// <summary>Signing location (from /Location entry).</summary>
    [JsonPropertyName("location")]
    public string? Location { get; set; }

    /// <summary>Contact info (from /ContactInfo entry).</summary>
    [JsonPropertyName("contact_info")]
    public string? ContactInfo { get; set; }

    /// <summary>Signing time (from /M entry, PDF date format).</summary>
    [JsonPropertyName("signing_time")]
    public string? SigningTime { get; set; }

    /// <summary>Signer's common name extracted from the certificate (requires signatures feature).</summary>
    [JsonPropertyName("signer_name")]
    public string? SignerName { get; set; }

    /// <summary>Size of the raw signature contents in bytes.</summary>
    [JsonPropertyName("contents_size")]
    public long ContentsSize { get; set; }

    /// <summary>Whether this is a PAdES (CAdES-based) signature.</summary>
    [JsonPropertyName("is_pades")]
    public bool IsPades { get; set; }

    /// <summary>Whether this is a PKCS#7 detached signature.</summary>
    [JsonPropertyName("is_pkcs7_detached")]
    public bool IsPkcs7Detached { get; set; }
}
