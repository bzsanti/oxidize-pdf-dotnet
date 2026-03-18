using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Models;

/// <summary>
/// Complete verification result for a digital signature in a PDF document.
/// </summary>
public class SignatureVerificationResult
{
    /// <summary>Signature field name.</summary>
    [JsonPropertyName("field_name")]
    public string? FieldName { get; set; }

    /// <summary>Signer's common name (from certificate).</summary>
    [JsonPropertyName("signer_name")]
    public string? SignerName { get; set; }

    /// <summary>Signing time.</summary>
    [JsonPropertyName("signing_time")]
    public string? SigningTime { get; set; }

    /// <summary>Whether the document hash matches the signed hash.</summary>
    [JsonPropertyName("hash_valid")]
    public bool HashValid { get; set; }

    /// <summary>Whether the cryptographic signature is valid.</summary>
    [JsonPropertyName("signature_valid")]
    public bool SignatureValid { get; set; }

    /// <summary>Whether the signature passes all validation checks.</summary>
    [JsonPropertyName("is_valid")]
    public bool IsValid { get; set; }

    /// <summary>Whether the document was modified after signing.</summary>
    [JsonPropertyName("has_modifications_after_signing")]
    public bool HasModificationsAfterSigning { get; set; }

    /// <summary>Validation errors encountered.</summary>
    [JsonPropertyName("errors")]
    public List<string> Errors { get; set; } = new();

    /// <summary>Validation warnings.</summary>
    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();

    /// <summary>Digest algorithm used (e.g., "SHA-256").</summary>
    [JsonPropertyName("digest_algorithm")]
    public string? DigestAlgorithm { get; set; }

    /// <summary>Signature algorithm used (e.g., "RSA-SHA256").</summary>
    [JsonPropertyName("signature_algorithm")]
    public string? SignatureAlgorithm { get; set; }

    /// <summary>Certificate validation details (null if not available).</summary>
    [JsonPropertyName("certificate")]
    public CertificateInfo? Certificate { get; set; }
}
