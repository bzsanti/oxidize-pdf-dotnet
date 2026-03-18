using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Models;

/// <summary>
/// Certificate validation information for a digital signature.
/// </summary>
public class CertificateInfo
{
    /// <summary>Certificate subject (e.g., "CN=John Doe").</summary>
    [JsonPropertyName("subject")]
    public string Subject { get; set; } = string.Empty;

    /// <summary>Certificate issuer (e.g., "CN=Root CA").</summary>
    [JsonPropertyName("issuer")]
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Certificate validity start date.</summary>
    [JsonPropertyName("valid_from")]
    public string ValidFrom { get; set; } = string.Empty;

    /// <summary>Certificate validity end date.</summary>
    [JsonPropertyName("valid_to")]
    public string ValidTo { get; set; } = string.Empty;

    /// <summary>Whether the certificate is within its validity period.</summary>
    [JsonPropertyName("is_time_valid")]
    public bool IsTimeValid { get; set; }

    /// <summary>Whether the certificate chain is trusted.</summary>
    [JsonPropertyName("is_trusted")]
    public bool IsTrusted { get; set; }

    /// <summary>Whether the certificate has the digital signature key usage.</summary>
    [JsonPropertyName("is_signature_capable")]
    public bool IsSignatureCapable { get; set; }

    /// <summary>Certificate validation warnings.</summary>
    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();
}
