using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OxidizePdf.NET;

/// <summary>Runs the limited PDF/A checks implemented by core 5.4.2. This is not
/// comprehensive certification and does not convert documents to PDF/A.</summary>
public static class PdfAValidation
{
    /// <summary>Validate owned snapshots of the PDF and settings. Findings return
    /// IsValid=false; parsing/operation failures throw PdfAException. Results are
    /// caller-owned. Cancellation prevents queued work but cannot interrupt a native call.</summary>
    public static Task<PdfAValidationResult> ValidateAsync(byte[] pdf,
        PdfAValidationOptions? options = null, PdfParserOptions? parserOptions = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        if (pdf.Length == 0 || pdf.Length > 100 * 1024 * 1024)
            throw new ArgumentException("PDF must contain 1 byte to 100 MiB", nameof(pdf));
        ct.ThrowIfCancellationRequested();
        options ??= new();
        if (!Enum.IsDefined(options.Level))
            throw new PdfAException(PdfAErrorKind.InvalidLevel, "Unsupported PDF/A level");
        var request = JsonSerializer.Serialize(new { options, parser_options = parserOptions ?? new PdfParserOptions() });
        var snapshot = (byte[])pdf.Clone();
        return Task.Run(() => Validate(snapshot, request), ct);
    }

    private static PdfAValidationResult Validate(byte[] pdf, string request)
    {
        int code = NativeMethods.oxidize_pdfa_validate(pdf, (nuint)pdf.Length, request, out var output);
        try
        {
            if (code != 0) throw new PdfAException(PdfAErrorKind.NativeFailure, NativeMethods.GetLastError() ?? "PDF/A validation failed");
            using var envelope = JsonDocument.Parse(Marshal.PtrToStringUTF8(output)
                ?? throw new PdfAException(PdfAErrorKind.NativeFailure, "Missing PDF/A response"));
            if (envelope.RootElement.TryGetProperty("error", out var error))
                throw new PdfAException(Enum.Parse<PdfAErrorKind>(error.GetProperty("kind").GetString()!),
                    error.GetProperty("message").GetString()!,
                    error.TryGetProperty("finding", out var finding) ? finding.Deserialize<PdfAValidationError>() : null);
            return envelope.RootElement.GetProperty("result").Deserialize<PdfAValidationResult>()
                ?? throw new PdfAException(PdfAErrorKind.NativeFailure, "Invalid PDF/A response");
        }
        finally
        {
            if (output != IntPtr.Zero) NativeMethods.oxidize_free_string(output);
        }
    }
}

/// <summary>The eight PDF/A levels exposed by core 5.4.2.</summary>
public enum PdfALevel
{
    /// <summary>Part 1, accessible.</summary>
    A1a,
    /// <summary>Part 1, basic.</summary>
    A1b,
    /// <summary>Part 2, accessible.</summary>
    A2a,
    /// <summary>Part 2, basic.</summary>
    A2b,
    /// <summary>Part 2, Unicode.</summary>
    A2u,
    /// <summary>Part 3, accessible.</summary>
    A3a,
    /// <summary>Part 3, basic.</summary>
    A3b,
    /// <summary>Part 3, Unicode.</summary>
    A3u
}

/// <summary>Core validator configuration.</summary>
public sealed class PdfAValidationOptions
{
    /// <summary>Target level; defaults to PDF/A-1b.</summary>
    public PdfALevel Level { get; set; } = PdfALevel.A1b;
    /// <summary>Forwarded to the core. In 5.4.2 both values collect all findings:
    /// the stored setting is not consulted. False does not stop parsing early.</summary>
    public bool CollectAllErrors { get; set; } = true;
    /// <summary>Optional password for unlocking the reader. Null leaves it locked;
    /// empty is an explicit empty password. Unlocking does not remove the encryption
    /// violation. Wrong passwords are ParseError operation failures.</summary>
    public string? Password { get; set; }
}

/// <summary>Core validation findings, not independent PDF/A certification.</summary>
public sealed class PdfAValidationResult
{
    /// <summary>Requested level actually validated.</summary>
    public PdfALevel Level { get; init; }
    /// <summary>True when the core reported no conformance errors.</summary>
    public bool IsValid { get; init; }
    /// <summary>All reported conformance violations, with native payloads.</summary>
    public PdfAValidationError[] Errors { get; init; } = [];
    /// <summary>Informational warnings. Core 5.4.2 currently emits none.</summary>
    public PdfAValidationWarning[] Warnings { get; init; } = [];
    /// <summary>Number of conformance errors.</summary>
    public int ErrorCount => Errors.Length;
    /// <summary>Number of informational warnings.</summary>
    public int WarningCount => Warnings.Length;
}

/// <summary>Every conformance error variant declared by the core.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PdfAValidationErrorKind>))]
public enum PdfAValidationErrorKind
{
    /// <summary>Encryption forbidden.</summary>
    EncryptionForbidden,
    /// <summary>Font not embedded.</summary>
    FontNotEmbedded,
    /// <summary>Font missing Unicode mapping.</summary>
    FontMissingToUnicode,
    /// <summary>JavaScript forbidden.</summary>
    JavaScriptForbidden,
    /// <summary>XMP missing or unreadable.</summary>
    XmpMetadataMissing,
    /// <summary>XMP PDF/A identifier absent.</summary>
    XmpMissingPdfAIdentifier,
    /// <summary>XMP PDF/A identifier mismatched.</summary>
    XmpInvalidPdfAIdentifier,
    /// <summary>Uncalibrated or invalid color space.</summary>
    InvalidColorSpace,
    /// <summary>Output intent absent.</summary>
    MissingOutputIntent,
    /// <summary>Transparency forbidden for the target level.</summary>
    TransparencyForbidden,
    /// <summary>External reference forbidden.</summary>
    ExternalReferenceForbidden,
    /// <summary>LZW filter forbidden for the target level.</summary>
    LzwCompressionForbidden,
    /// <summary>PDF version incompatible with target.</summary>
    IncompatiblePdfVersion,
    /// <summary>Embedded file forbidden for the target level.</summary>
    EmbeddedFileForbidden,
    /// <summary>Embedded file metadata absent.</summary>
    EmbeddedFileMissingMetadata,
    /// <summary>Action forbidden.</summary>
    ActionForbidden
}

/// <summary>Discriminated conformance finding. Only payload fields belonging to
/// Kind are populated; all other fields remain null. Message is the core display text.</summary>
public sealed class PdfAValidationError
{
    /// <summary>Core variant.</summary>
    public PdfAValidationErrorKind Kind { get; init; }
    /// <summary>Human-readable native message.</summary>
    public string Message { get; init; } = "";
    /// <summary>Font resource name for font errors.</summary>
    public string? FontName { get; init; }
    /// <summary>Native location, retaining its original page numbering.</summary>
    public string? Location { get; init; }
    /// <summary>Invalid identifier details.</summary>
    public string? Details { get; init; }
    /// <summary>Invalid color-space name.</summary>
    public string? ColorSpace { get; init; }
    /// <summary>External action type.</summary>
    public string? ReferenceType { get; init; }
    /// <summary>Native object description, including page/filter context.</summary>
    public string? ObjectId { get; init; }
    /// <summary>Actual PDF version.</summary>
    public string? Actual { get; init; }
    /// <summary>Required PDF version label.</summary>
    public string? Required { get; init; }
    /// <summary>Embedded file name.</summary>
    public string? FileName { get; init; }
    /// <summary>Missing embedded-file field.</summary>
    public string? MissingField { get; init; }
    /// <summary>Forbidden action type.</summary>
    public string? ActionType { get; init; }
}

/// <summary>Every informational warning variant declared by the core.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PdfAValidationWarningKind>))]
public enum PdfAValidationWarningKind
{
    /// <summary>Font subset concern.</summary>
    FontSubsetWarning,
    /// <summary>Optional metadata absent.</summary>
    OptionalMetadataMissing,
    /// <summary>Color profile concern.</summary>
    ColorProfileWarning,
    /// <summary>Large file size.</summary>
    LargeFileWarning
}

/// <summary>Discriminated warning; fields unrelated to Kind are null.</summary>
public sealed class PdfAValidationWarning
{
    /// <summary>Core variant.</summary>
    public PdfAValidationWarningKind Kind { get; init; }
    /// <summary>Native display text.</summary>
    public string Message { get; init; } = "";
    /// <summary>Subset font name.</summary>
    public string? FontName { get; init; }
    /// <summary>Font or color-profile details.</summary>
    public string? Details { get; init; }
    /// <summary>Missing optional metadata field.</summary>
    public string? Field { get; init; }
    /// <summary>File size in bytes, retaining the full unsigned 64-bit value.</summary>
    public ulong? SizeBytes { get; init; }
}

/// <summary>Operation failures, distinct from a successfully returned invalid result.</summary>
public enum PdfAErrorKind
{
    /// <summary>Invalid request or parser setting.</summary>
    InvalidInput,
    /// <summary>PDF parsing failed before or during validation.</summary>
    ParseError,
    /// <summary>XMP parser operation failed.</summary>
    XmpParseError,
    /// <summary>Unsupported target level.</summary>
    InvalidLevel,
    /// <summary>Core I/O failure.</summary>
    IoError,
    /// <summary>Core returned a single validation operation error.</summary>
    Validation,
    /// <summary>Native ABI or internal failure.</summary>
    NativeFailure
}

/// <summary>Typed failure of the validation operation.</summary>
public sealed class PdfAException : PdfExtractionException
{
    /// <summary>Failure category.</summary>
    public PdfAErrorKind Kind { get; }
    /// <summary>Payload of a core Validation operation error, otherwise null.</summary>
    public PdfAValidationError? Finding { get; }
    /// <summary>Create an operation failure.</summary>
    public PdfAException(PdfAErrorKind kind, string message, PdfAValidationError? finding = null) : base(message)
    {
        Kind = kind;
        Finding = finding;
    }
}
