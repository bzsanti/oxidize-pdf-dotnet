using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OxidizePdf.NET;

/// <summary>Provider-neutral incremental PDF signing. Private keys stay with the caller.</summary>
public static class PdfSigning
{
    /// <summary>
    /// Prepares an incremental revision. The provider must create a detached DER CMS
    /// signature over BytesToDigest, then call Complete on the returned disposable session.
    /// Cancellation prevents queued work; it does not interrupt an active native call.
    /// </summary>
    public static Task<PdfPreparedSignature> PrepareAsync(byte[] pdfBytes, PdfSigningOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);
        ArgumentNullException.ThrowIfNull(options);
        if (pdfBytes.Length == 0) throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ct.ThrowIfCancellationRequested();
        var json = JsonSerializer.Serialize(options);
        var snapshot = (byte[])pdfBytes.Clone();
        return Task.Run(() => Prepare(snapshot, json), ct);
    }

    /// <summary>Checks that all visible text fits, returning the chosen layout or throwing.</summary>
    public static PdfSignatureAppearanceLayout LayoutAppearance(double[] rectangle, PdfSignatureAppearance appearance)
    {
        ArgumentNullException.ThrowIfNull(rectangle);
        ArgumentNullException.ThrowIfNull(appearance);
        IntPtr result = IntPtr.Zero;
        try
        {
            Check(SigningNative.oxidize_signature_layout(JsonSerializer.Serialize(new { rect = rectangle, appearance }), out result));
            return Read<PdfSignatureAppearanceLayout>(result);
        }
        finally { if (result != IntPtr.Zero) NativeMethods.oxidize_free_string(result); }
    }

    private static PdfPreparedSignature Prepare(byte[] pdf, string json)
    {
        var pinned = GCHandle.Alloc(pdf, GCHandleType.Pinned);
        IntPtr raw = IntPtr.Zero, result = IntPtr.Zero;
        SignatureSafeHandle? handle = null;
        try
        {
            Check(SigningNative.oxidize_signature_prepare(pinned.AddrOfPinnedObject(), (nuint)pdf.Length, json, out raw, out result));
            handle = new SignatureSafeHandle(raw);
            raw = IntPtr.Zero;
            var response = Read<PreparedResponse>(result);
            var session = new PdfPreparedSignature(handle, response);
            handle = null;
            return session;
        }
        finally
        {
            handle?.Dispose();
            if (raw != IntPtr.Zero) SigningNative.oxidize_signature_free(raw);
            if (result != IntPtr.Zero) NativeMethods.oxidize_free_string(result);
            pinned.Free();
        }
    }

    internal static void Check(int status)
    {
        if (status != 0) throw new PdfExtractionException(NativeMethods.GetLastError() ?? "Signature operation failed");
    }
    private static T Read<T>(IntPtr value) => JsonSerializer.Deserialize<T>(Marshal.PtrToStringUTF8(value)
        ?? throw new PdfExtractionException("Missing signing response"))
        ?? throw new PdfExtractionException("Invalid signing response");

    internal sealed class PreparedResponse
    {
        [JsonPropertyName("prepared_pdf")] public byte[] PreparedPdf { get; set; } = [];
        [JsonPropertyName("bytes_to_digest")] public byte[] BytesToDigest { get; set; } = [];
        [JsonPropertyName("byte_range")] public ulong[][] ByteRange { get; set; } = [];
        [JsonPropertyName("layout")] public PdfSignatureAppearanceLayout? Layout { get; set; }
    }
}

/// <summary>Immutable native preparation with detached snapshots for an external signer.</summary>
public sealed class PdfPreparedSignature : IDisposable
{
    private readonly SignatureSafeHandle handle;
    /// <summary>Prepared PDF snapshot. Editing it does not alter the native signing session.</summary>
    public byte[] PreparedPdf { get; }
    /// <summary>Exact concatenated byte ranges to sign. Provider must create detached CMS over these bytes.</summary>
    public byte[] BytesToDigest { get; }
    /// <summary>Pairs of offset and length covered by the signature.</summary>
    public ulong[][] ByteRange { get; }
    /// <summary>Visible text layout, when a rectangle and appearance were provided.</summary>
    public PdfSignatureAppearanceLayout? Layout { get; }
    internal PdfPreparedSignature(SignatureSafeHandle handle, PdfSigning.PreparedResponse response)
    {
        this.handle = handle;
        PreparedPdf = response.PreparedPdf;
        BytesToDigest = response.BytesToDigest;
        ByteRange = response.ByteRange;
        Layout = response.Layout;
    }
    /// <summary>
    /// Embeds caller-produced DER CMS. Validates container shape and reserved size, not
    /// cryptographic authenticity. The immutable session supports retries and repeated completion.
    /// </summary>
    public byte[] Complete(byte[] cms)
    {
        ArgumentNullException.ThrowIfNull(cms);
        if (cms.Length == 0) throw new ArgumentException("CMS cannot be empty", nameof(cms));
        ObjectDisposedException.ThrowIf(handle.IsClosed, this);
        var pinned = GCHandle.Alloc(cms, GCHandleType.Pinned);
        IntPtr output = IntPtr.Zero;
        nuint length = 0;
        try
        {
            PdfSigning.Check(SigningNative.oxidize_signature_finalize(handle, pinned.AddrOfPinnedObject(), (nuint)cms.Length, out output, out length));
            var result = new byte[checked((int)length)];
            Marshal.Copy(output, result, 0, result.Length);
            return result;
        }
        finally
        {
            if (output != IntPtr.Zero) NativeMethods.oxidize_free_bytes(output, length);
            pinned.Free();
        }
    }
    /// <summary>Releases the native session. SafeHandle protects in-flight completion calls.</summary>
    public void Dispose() => handle.Dispose();
}

internal sealed class SignatureSafeHandle(IntPtr pointer) : OxidizeSafeHandle(pointer)
{
    protected override bool ReleaseHandle()
    {
        SigningNative.oxidize_signature_free(handle);
        return true;
    }
}

internal static class SigningNative
{
    private const string Library = "oxidize_pdf_ffi";
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_signature_prepare(IntPtr pdf, nuint length,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string options, out IntPtr handle, out IntPtr json);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_signature_finalize(SignatureSafeHandle handle, IntPtr cms, nuint length,
        out IntPtr bytes, out nuint outputLength);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void oxidize_signature_free(IntPtr handle);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_signature_layout([MarshalAs(UnmanagedType.LPUTF8Str)] string input, out IntPtr json);
}
