using System.Runtime.InteropServices;
using System.Text.Json;

namespace OxidizePdf.NET;

/// <summary>Complete text extraction and explicit bounded Flate recovery with diagnostics.</summary>
public static class PdfTextExtraction
{
    /// <summary>Extracts all pages, or one zero-based page, without permitting incomplete Flate data.</summary>
    public static Task<PdfExtractedPage[]> ExtractAsync(byte[] pdf, PdfTextExtractionOptions? options = null,
        PdfParserOptions? parserOptions = null, uint? pageIndex = null, CancellationToken ct = default)
        => CallAsync<PdfExtractedPage[]>(pdf, "Extract", options, parserOptions, pageIndex, null, ct);

    /// <summary>
    /// Extracts with explicit recovery. Inspect every diagnostic even when text is nonempty.
    /// maxStreamBytes bounds decoded bytes per content stream/Form, including operators;
    /// it is independent of the emitted-text budget. Resource/predictor errors remain errors.
    /// Cancellation prevents queued work but does not interrupt an active native call.
    /// </summary>
    public static Task<PdfExtractedPage[]> RecoverAsync(byte[] pdf, ulong maxStreamBytes,
        PdfTextExtractionOptions? options = null, PdfParserOptions? parserOptions = null,
        uint? pageIndex = null, CancellationToken ct = default)
        => CallAsync<PdfExtractedPage[]>(pdf, "Extract", options, parserOptions, pageIndex, maxStreamBytes, ct);

    /// <summary>Returns the effective PDF version, accounting for catalog Version overrides.</summary>
    public static Task<string> GetEffectiveVersionAsync(byte[] pdf, PdfParserOptions? parserOptions = null,
        CancellationToken ct = default)
        => CallAsync<string>(pdf, "Version", null, parserOptions, null, null, ct);

    private static Task<T> CallAsync<T>(byte[] pdf, string action, PdfTextExtractionOptions? options,
        PdfParserOptions? parserOptions, uint? pageIndex, ulong? maxStreamBytes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        if (pdf.Length == 0) throw new ArgumentException("PDF bytes cannot be empty", nameof(pdf));
        ct.ThrowIfCancellationRequested();
        var request = JsonSerializer.Serialize(new
        {
            action,
            options = options ?? new PdfTextExtractionOptions(),
            parser_options = parserOptions ?? new PdfParserOptions(),
            page_index = pageIndex,
            max_stream_bytes = maxStreamBytes
        });
        var snapshot = (byte[])pdf.Clone();
        return Task.Run(() => Call<T>(snapshot, request), ct);
    }

    private static T Call<T>(byte[] pdf, string request)
    {
        var pinned = GCHandle.Alloc(pdf, GCHandleType.Pinned);
        IntPtr output = IntPtr.Zero;
        try
        {
            var status = ExtractionNative(pinned.AddrOfPinnedObject(), (nuint)pdf.Length, request, out output);
            if (status != 0) throw new PdfExtractionException(NativeMethods.GetLastError() ?? "Text extraction failed");
            return JsonSerializer.Deserialize<T>(Marshal.PtrToStringUTF8(output)
                ?? throw new PdfExtractionException("Missing extraction response"))
                ?? throw new PdfExtractionException("Invalid extraction response");
        }
        finally
        {
            if (output != IntPtr.Zero) NativeMethods.oxidize_free_string(output);
            pinned.Free();
        }
    }

    [DllImport("oxidize_pdf_ffi", EntryPoint = "oxidize_text_extraction", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ExtractionNative(IntPtr pdf, nuint length,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string request, out IntPtr output);
}
