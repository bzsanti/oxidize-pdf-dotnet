using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OxidizePdf.NET;

/// <summary>Selective image XObject extraction. Does not scan content operators or unrelated images.</summary>
public static class PdfImages
{
    /// <summary>Extracts one exact, case-sensitive resource name from a zero-based page.
    /// Names are decoded PDF names, without the syntactic leading slash; they are not trimmed or split.
    /// formPath explicitly traverses Form resource dictionaries, not drawing operations. An absent
    /// Form Resources uses its enclosing dictionary; an empty one masks it. No implicit recursive search.
    /// Returns original bytes and bounded packed samples, or standalone JPEG bytes without JPEG decoding.
    /// No PNG conversion, color transformation, Decode mapping, mask composition or inline-image scan.
    /// Input/settings are snapshotted; cancellation prevents queued work but cannot interrupt native execution.</summary>
    public static Task<PdfNamedImage> ExtractAsync(byte[] pdf, uint pageIndex, string resourceName,
        IReadOnlyList<string>? formPath = null, PdfNamedImageOptions? options = null,
        PdfParserOptions? parserOptions = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        ArgumentNullException.ThrowIfNull(resourceName);
        if (pdf.Length == 0 || pdf.Length > 100 * 1024 * 1024)
            throw new ArgumentException("PDF must contain between 1 byte and 100 MiB", nameof(pdf));
        ct.ThrowIfCancellationRequested();
        options ??= new();
        ArgumentNullException.ThrowIfNull(options.Limits);
        options.Limits.Validate();
        var path = formPath?.ToArray() ?? [];
        long nameBytes = Encoding.UTF8.GetByteCount(resourceName);
        foreach (var name in path)
        {
            ArgumentNullException.ThrowIfNull(name);
            nameBytes += Encoding.UTF8.GetByteCount(name);
        }
        if ((ulong)nameBytes > options.MaxNameBytes || (ulong)path.Length > options.MaxFormDepth)
            throw new PdfImageException(PdfImageErrorKind.LimitExceeded, "Resource path exceeds name/depth budget");
        var request = JsonSerializer.Serialize(new
        {
            page_index = pageIndex,
            resource_name = resourceName,
            form_path = path,
            options,
            parser_options = parserOptions ?? new PdfParserOptions()
        });
        var snapshot = (byte[])pdf.Clone();
        return Task.Run(() => Call(snapshot, request), ct);
    }

    private static PdfNamedImage Call(byte[] pdf, string request)
    {
        var pinned = GCHandle.Alloc(pdf, GCHandleType.Pinned);
        IntPtr output = IntPtr.Zero;
        try
        {
            var status = NamedImageNative(pinned.AddrOfPinnedObject(), (nuint)pdf.Length, request, out output);
            if (status != 0) throw new PdfImageException(PdfImageErrorKind.InvalidData, NativeMethods.GetLastError() ?? "Image extraction failed");
            using var envelope = JsonDocument.Parse(Marshal.PtrToStringUTF8(output)
                ?? throw new PdfImageException(PdfImageErrorKind.InvalidData, "Missing image response"));
            if (envelope.RootElement.TryGetProperty("error", out var error))
            {
                var streamKind = error.GetProperty("stream_error_kind");
                throw new PdfImageException((PdfImageErrorKind)error.GetProperty("kind").GetInt32(),
                    error.GetProperty("message").GetString()!, streamKind.ValueKind == JsonValueKind.Null ? null : (PdfStreamErrorKind)streamKind.GetInt32());
            }
            return envelope.RootElement.GetProperty("result").Deserialize<PdfNamedImage>()
                ?? throw new PdfImageException(PdfImageErrorKind.InvalidData, "Invalid image response");
        }
        finally
        {
            if (output != IntPtr.Zero) NativeMethods.oxidize_free_string(output);
            pinned.Free();
        }
    }

    [DllImport("oxidize_pdf_ffi", EntryPoint = "oxidize_named_image", CallingConvention = CallingConvention.Cdecl)]
    private static extern int NamedImageNative(IntPtr pdf, nuint length,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string request, out IntPtr output);
}

/// <summary>Named-image bounds apply after core parsing, not to its total memory/time use.</summary>
public sealed class PdfNamedImageOptions
{
    /// <summary>Existing image count, original encoded-byte and declared width-times-height limits.
    /// One successful call consumes one image; per-image and total encoded limits both bound its source payload.
    /// Pixel limits check PDF dimensions, not an independent JPEG header or a rendered raster.</summary>
    public ImageExtractionOptions Limits { get; set; } = new();
    /// <summary>Maximum bytes at each sample decoding stage, or delivered JPEG bytes for passthrough.</summary>
    public ulong MaxDecodedBytes { get; set; } = 16 * 1024 * 1024;
    /// <summary>Opt into Flate recovery and inspect Diagnostics for incomplete/unverified samples.</summary>
    public bool AllowRecovery { get; set; } = false;
    /// <summary>Maximum page ancestors visited to find Resources (0–256).</summary>
    public uint MaxAncestorDepth { get; set; } = 256;
    /// <summary>Maximum explicit Form path length (0–64).</summary>
    public uint MaxFormDepth { get; set; } = 16;
    /// <summary>Maximum dereferences per selected object/dictionary chain (0–128).
    /// Initial page Resources follows the existing provenance lookup's single-reference contract.</summary>
    public uint MaxReferenceDepth { get; set; } = 32;
    /// <summary>Maximum total UTF-8 bytes in the requested Form names and image name.</summary>
    public ulong MaxNameBytes { get; set; } = 4096;
    /// <summary>Maximum nesting below each declared image dictionary value (0–16).</summary>
    public uint MaxMetadataDepth { get; set; } = 16;
    /// <summary>Maximum values in the returned image dictionary, including nested containers.</summary>
    public uint MaxMetadataNodes { get; set; } = 4096;
    /// <summary>Maximum dictionary-key/name UTF-8 bytes and string payload bytes in metadata.
    /// Excludes JSON escaping and numbers/references.</summary>
    public ulong MaxMetadataBytes { get; set; } = 64 * 1024;
}

/// <summary>Representation of image payload bytes; no rendered-image guarantee.</summary>
public enum PdfImagePayloadFormat
{
    /// <summary>PDF-filter-encoded source stream; inspect Dictionary Filter/DecodeParms.</summary>
    FilterEncoded,
    /// <summary>Packed source samples after stream decoding. Colors, Decode mapping and masks remain unapplied.</summary>
    PackedSamples,
    /// <summary>Standalone DCT stream passed through as JPEG, without JPEG decompression/validation.</summary>
    Jpeg
}

/// <summary>An owned image resource extraction, with declared metadata and exact lookup identity.</summary>
public sealed class PdfNamedImage
{
    /// <summary>Zero-based requested page.</summary>
    [JsonPropertyName("page_index")] public uint PageIndex { get; init; }
    /// <summary>Page object identity.</summary>
    [JsonPropertyName("page_object_id")] public PdfIndirectObjectId PageObjectId { get; init; }
    /// <summary>Exact image resource name.</summary>
    [JsonPropertyName("resource_name")] public string ResourceName { get; init; } = "";
    /// <summary>Explicit Form names traversed in order.</summary>
    [JsonPropertyName("form_path")] public string[] FormPath { get; init; } = [];
    /// <summary>Identity for each Form path entry, or null for a direct object.</summary>
    [JsonPropertyName("form_object_ids")] public PdfIndirectObjectId?[] FormObjectIds { get; init; } = [];
    /// <summary>Selected image identity, or null for a direct stream.</summary>
    [JsonPropertyName("object_id")] public PdfIndirectObjectId? ObjectId { get; init; }
    /// <summary>Page, ancestor or Form that declares the final effective Resources.</summary>
    [JsonPropertyName("resource_owner_id")] public PdfIndirectObjectId? ResourceOwnerId { get; init; }
    /// <summary>Indirect identity of final Resources, or null for an inline dictionary.</summary>
    [JsonPropertyName("resource_dictionary_id")] public PdfIndirectObjectId? ResourceDictionaryId { get; init; }
    /// <summary>Declared width in pixels.</summary>
    [JsonPropertyName("width")] public uint Width { get; init; }
    /// <summary>Declared height in pixels.</summary>
    [JsonPropertyName("height")] public uint Height { get; init; }
    /// <summary>Declared bits per component, defaulting to one only for an image mask.</summary>
    [JsonPropertyName("bits_per_component")] public uint BitsPerComponent { get; init; }
    /// <summary>Whether the resource is a stencil image mask.</summary>
    [JsonPropertyName("image_mask")] public bool ImageMask { get; init; }
    /// <summary>Representation of EncodedData.</summary>
    [JsonPropertyName("encoded_format")] public PdfImagePayloadFormat EncodedFormat { get; init; }
    /// <summary>Representation of Data; JPEG is never mislabeled as decoded samples.</summary>
    [JsonPropertyName("output_format")] public PdfImagePayloadFormat OutputFormat { get; init; }
    /// <summary>Original encoded payload from the core parser, not a physical file slice.</summary>
    [JsonPropertyName("encoded_data")] public byte[] EncodedData { get; init; } = [];
    /// <summary>Decoded packed samples, or JPEG passthrough bytes. Inspect OutputFormat and Diagnostics.</summary>
    [JsonPropertyName("data")] public byte[] Data { get; init; } = [];
    /// <summary>Complete declared stream dictionary, retaining ordered Filter/DecodeParms, ColorSpace,
    /// Decode, Mask, SMask and other entries. References stay unresolved and can be read via PdfStreams.</summary>
    [JsonPropertyName("dictionary")] public Dictionary<string, PdfStreamValue> Dictionary { get; init; } = [];
    /// <summary>All Flate recovery limitations. Empty does not validate sample geometry or JPEG integrity.</summary>
    [JsonPropertyName("diagnostics")] public PdfStreamRecoveryDiagnostic[] Diagnostics { get; init; } = [];
}

/// <summary>Typed image selection/extraction failures.</summary>
public enum PdfImageErrorKind
{
    /// <summary>The requested resource or its containing resource dictionary is absent.</summary>
    MissingResource,
    /// <summary>The final resource is not an image XObject.</summary>
    NotImage,
    /// <summary>An intermediate path resource is not a Form XObject.</summary>
    NotForm,
    /// <summary>No bounded decoder/passthrough supports the selected filter chain.</summary>
    UnsupportedFilter,
    /// <summary>A configured resource bound was exceeded.</summary>
    LimitExceeded,
    /// <summary>Malformed document, selector, reference chain or image metadata.</summary>
    InvalidData,
    /// <summary>Stream decoding failed; StreamErrorKind preserves the core/wrapper classification.</summary>
    DecodeError
}

/// <summary>Image extraction failure, with stream error detail when decoding fails.</summary>
public sealed class PdfImageException : Exception
{
    /// <summary>Selection/extraction error category.</summary>
    public PdfImageErrorKind Kind { get; }
    /// <summary>Stream failure classification when available, including strict decoding limits.</summary>
    public PdfStreamErrorKind? StreamErrorKind { get; }
    /// <summary>Creates a typed image error.</summary>
    public PdfImageException(PdfImageErrorKind kind, string message, PdfStreamErrorKind? streamErrorKind = null) : base(message)
        => (Kind, StreamErrorKind) = (kind, streamErrorKind);
}
