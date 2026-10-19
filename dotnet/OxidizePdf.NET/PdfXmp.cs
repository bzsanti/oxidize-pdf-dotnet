using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OxidizePdf.NET;

/// <summary>Dedicated core PDF/A XMP model. Unknown XML properties are not represented.
/// Parsing/serialization does not synchronize the document Info dictionary.</summary>
public sealed record PdfXmpMetadata
{
    /// <summary>Document title.</summary>
    public string? Title { get; init; }
    /// <summary>Ordered authors; empty entries are preserved.</summary>
    public IReadOnlyList<string>? Creator { get; init; }
    /// <summary>Document description.</summary>
    public string? Description { get; init; }
    /// <summary>Ordered keywords.</summary>
    public IReadOnlyList<string>? Keywords { get; init; }
    /// <summary>Creation date text.</summary>
    public string? CreateDate { get; init; }
    /// <summary>Modification date text.</summary>
    public string? ModifyDate { get; init; }
    /// <summary>Creating application.</summary>
    public string? CreatorTool { get; init; }
    /// <summary>PDF/A identity.</summary>
    public PdfXmpIdentifier? PdfAIdentifier { get; init; }
    /// <summary>Document identifier.</summary>
    public string? DocumentId { get; init; }
    /// <summary>Instance identifier.</summary>
    public string? InstanceId { get; init; }
}
/// <summary>Core PDF/A identity, including amendment and corrigenda text.</summary>
public sealed record PdfXmpIdentifier(byte Part, PdfXmpConformance Conformance, string? Amendment = null, string? Corrigenda = null);
/// <summary>Core PDF/A conformance identifiers.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PdfXmpConformance>))]
public enum PdfXmpConformance
{
    /// <summary>Accessible.</summary>
    A,
    /// <summary>Basic visual conformance.</summary>
    B,
    /// <summary>Unicode.</summary>
    U,
}
/// <summary>Original decoded XML packet and parsed supported properties.</summary>
public sealed record PdfXmpReadResult(string Xml, PdfXmpMetadata Metadata);
/// <summary>Result of the limited core metadata check, which only requires an identity.
/// This is not full XML, PDF/A or document validation; use Parse and PdfAValidation separately.</summary>
public sealed record PdfXmpValidationResult(bool IsValid, string? Message);

/// <summary>Explicit XMP model operations and incremental metadata integration.
/// Model input is limited to 1 MiB JSON and output to 8 MiB. PDF input is limited to
/// 256 MiB; catalog metadata decoding and written XML are limited to 1 MiB.</summary>
public static class PdfXmp
{
    /// <summary>Parse namespace-aware XML with the core. DTD/external entities are disabled.</summary>
    public static PdfXmpMetadata Parse(string xml)
    {
        ArgumentNullException.ThrowIfNull(xml);
        return Model<PdfXmpMetadata>(new { Operation = "Parse", Xml = xml });
    }
    /// <summary>Serialize every supported property. Like core, this method does not discard
    /// invalid XML characters; Parse/Write reject them. Unknown source properties are not retained.</summary>
    public static string Serialize(PdfXmpMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return Model<string>(new { Operation = "Serialize", Metadata = metadata });
    }
    /// <summary>Serialize the identity RDF fragment through the core.</summary>
    public static string SerializeIdentifier(PdfXmpIdentifier identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        return Model<string>(new { Operation = "IdentifierRdf", Metadata = new PdfXmpMetadata { PdfAIdentifier = identifier } });
    }
    /// <summary>Invoke core validate_for_pdfa; currently only checks identity presence.</summary>
    public static PdfXmpValidationResult ValidateForPdfA(PdfXmpMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return Model<PdfXmpValidationResult>(new { Operation = "Validate", Metadata = metadata });
    }
    /// <summary>Read catalog metadata, returning null when absent. Uses strict parsing,
    /// bounded decoding and no password support. Invalid metadata fails rather than looking absent.</summary>
    public static PdfXmpReadResult? Read(byte[] pdfBytes)
    {
        CheckPdf(pdfBytes);
        IntPtr output = IntPtr.Zero;
        try
        {
            Check(NativeMethods.oxidize_xmp_read(pdfBytes, (nuint)pdfBytes.Length, out output));
            return JsonSerializer.Deserialize<PdfXmpReadResult>(Marshal.PtrToStringUTF8(output)!);
        }
        finally { if (output != IntPtr.Zero) NativeMethods.oxidize_free_string(output); }
    }
    /// <summary>Append a replacement catalog XMP stream, preserving original bytes and Info.
    /// Info may intentionally disagree with XMP; there is no implicit synchronization.
    /// Uses the core generic object editor, which rejects encryption, DocMDP certification
    /// at every level, FieldMDP locks and malformed policies. No signature-validity claim.</summary>
    /// <remarks>This writes the supplied supported model, replacing any unknown XML properties.
    /// It does not silently merge arbitrary namespaces or perform a PDF/A conversion.</remarks>
    public static byte[] Write(byte[] pdfBytes, PdfXmpMetadata metadata)
    {
        CheckPdf(pdfBytes); ArgumentNullException.ThrowIfNull(metadata);
        var input = Input(metadata);
        IntPtr output = IntPtr.Zero; nuint length = 0;
        try
        {
            Check(NativeMethods.oxidize_xmp_write(pdfBytes, (nuint)pdfBytes.Length, input, (nuint)input.Length, out output, out length));
            var result = new byte[checked((int)length)];
            Marshal.Copy(output, result, 0, result.Length);
            return result;
        }
        finally { if (output != IntPtr.Zero) NativeMethods.oxidize_free_bytes(output, length); }
    }
    private static void CheckPdf(byte[] pdf)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        if (pdf.Length == 0 || pdf.Length > 268_435_456) throw new ArgumentOutOfRangeException(nameof(pdf));
    }
    private static byte[] Input(object request)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(request);
        if (bytes.Length > 1_048_576) throw new ArgumentOutOfRangeException(nameof(request), "XMP request exceeds 1 MiB JSON.");
        return bytes;
    }
    private static T Model<T>(object request)
    {
        var input = Input(request); IntPtr output = IntPtr.Zero;
        try
        {
            Check(NativeMethods.oxidize_xmp_model(input, (nuint)input.Length, out output));
            return JsonSerializer.Deserialize<T>(Marshal.PtrToStringUTF8(output)!)!;
        }
        finally { if (output != IntPtr.Zero) NativeMethods.oxidize_free_string(output); }
    }
    private static void Check(int code)
    {
        if (code != 0) throw new PdfXmpException((PdfXmpErrorKind)code, NativeMethods.GetLastError());
    }
}
/// <summary>Typed XMP boundary failure codes.</summary>
public enum PdfXmpErrorKind
{
    /// <summary>Invalid native pointer.</summary>
    NullPointer = 1,
    /// <summary>Metadata is not valid UTF-8.</summary>
    InvalidUtf8 = 2,
    /// <summary>Invalid PDF structure or metadata stream.</summary>
    InvalidPdf = 3,
    /// <summary>Native allocation error.</summary>
    Allocation = 4,
    /// <summary>Serialization failure.</summary>
    Serialization = 5,
    /// <summary>I/O failure.</summary>
    Io = 6,
    /// <summary>Encryption failure.</summary>
    Encryption = 7,
    /// <summary>Core modification/encryption policy rejects the operation.</summary>
    PermissionDenied = 8,
    /// <summary>Invalid XML, model, request or input limit.</summary>
    InvalidInput = 9,
    /// <summary>Caught native panic.</summary>
    NativeFailure = 10,
}
/// <summary>XMP operation failure; no partial updated PDF is returned.</summary>
public sealed class PdfXmpException : Exception
{
    /// <summary>Typed failure kind.</summary>
    public PdfXmpErrorKind Kind { get; }
    internal PdfXmpException(PdfXmpErrorKind kind, string? message) : base(message) => Kind = kind;
}
