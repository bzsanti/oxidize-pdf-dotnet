using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OxidizePdf.NET;

/// <summary>Recognition data for one word in PDF user-space coordinates.</summary>
public sealed class PdfOcrFragment
{
    /// <summary>Recognized Unicode text.</summary>
    [JsonPropertyName("text")] public string Text { get; set; } = "";
    /// <summary>Mapped page region [x, y, width, height], with positive dimensions.</summary>
    [JsonPropertyName("region")] public double[] Region { get; set; } = [];
    /// <summary>Recognition confidence between zero and one.</summary>
    [JsonPropertyName("confidence")] public double Confidence { get; set; }
    /// <summary>Strictly increasing logical order within the page.</summary>
    [JsonPropertyName("reading_order")] public uint ReadingOrder { get; set; }
}

/// <summary>Provider-produced recognition data for one existing page.</summary>
public sealed class PdfOcrPage
{
    /// <summary>Zero-based page index.</summary>
    [JsonPropertyName("page_index")] public uint PageIndex { get; set; }
    /// <summary>Non-empty ASCII BCP 47 language tag.</summary>
    [JsonPropertyName("language")] public string Language { get; set; } = "und";
    /// <summary>Positioned fragments in strictly increasing reading order.</summary>
    [JsonPropertyName("fragments")] public PdfOcrFragment[] Fragments { get; set; } = [];
}

/// <summary>Validated plan for adding OCR layers.</summary>
public sealed class PdfOcrLayerPlan
{
    /// <summary>Page dictionaries that will change, sorted by page index.</summary>
    [JsonPropertyName("pages")] public uint[] Pages { get; set; } = [];
    /// <summary>New content streams.</summary>
    [JsonPropertyName("streams_added")] public uint StreamsAdded { get; set; }
    /// <summary>Isolated font resources added.</summary>
    [JsonPropertyName("resources_added")] public uint ResourcesAdded { get; set; }
    /// <summary>Pages skipped because an oxidize-pdf OCR layer already exists.</summary>
    [JsonPropertyName("pages_skipped_existing_ocr")] public uint[] PagesSkippedExistingOcr { get; set; } = [];
}

/// <summary>Incremental OCR output and its applied plan.</summary>
public sealed class PdfOcrLayerResult
{
    /// <summary>Complete PDF, preserving the input as an exact prefix.</summary>
    [JsonPropertyName("pdf_bytes")] public byte[] PdfBytes { get; set; } = [];
    /// <summary>Plan applied to the input.</summary>
    [JsonPropertyName("plan")] public PdfOcrLayerPlan Plan { get; set; } = new();
}

/// <summary>Adds invisible searchable text supplied by an external OCR provider.</summary>
public static class PdfOcrLayer
{
    /// <summary>Checks permissions, resources and recognition data without changing the PDF.</summary>
    public static Task<PdfOcrLayerPlan> PlanAsync(byte[] pdf, IEnumerable<PdfOcrPage> pages, CancellationToken ct = default)
        => CallAsync<PdfOcrLayerPlan>(pdf, pages, true, ct);
    /// <summary>
    /// Appends an isolated text layer. Does not perform image recognition. Existing
    /// oxidize-pdf OCR layers are skipped; empty page fragments add no objects.
    /// Encrypted and certified documents forbidding content changes are rejected.
    /// Cancellation does not interrupt an active native call.
    /// </summary>
    public static Task<PdfOcrLayerResult> ApplyAsync(byte[] pdf, IEnumerable<PdfOcrPage> pages, CancellationToken ct = default)
        => CallAsync<PdfOcrLayerResult>(pdf, pages, false, ct);
    private static Task<T> CallAsync<T>(byte[] pdf, IEnumerable<PdfOcrPage> pages, bool planOnly, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        ArgumentNullException.ThrowIfNull(pages);
        if (pdf.Length == 0) throw new ArgumentException("PDF bytes cannot be empty", nameof(pdf));
        ct.ThrowIfCancellationRequested();
        var request = JsonSerializer.Serialize(new { plan_only = planOnly, pages = pages.ToArray() });
        return Task.Run(() => Call<T>(pdf, request), ct);
    }
    private static T Call<T>(byte[] pdf, string request)
    {
        var pinned = GCHandle.Alloc(pdf, GCHandleType.Pinned);
        IntPtr output = IntPtr.Zero;
        try
        {
            var status = OcrNative(pinned.AddrOfPinnedObject(), (nuint)pdf.Length, request, out output);
            if (status != 0) throw new PdfExtractionException(NativeMethods.GetLastError() ?? "OCR layer operation failed");
            return JsonSerializer.Deserialize<T>(Marshal.PtrToStringUTF8(output)
                ?? throw new PdfExtractionException("Missing OCR response"))
                ?? throw new PdfExtractionException("Invalid OCR response");
        }
        finally
        {
            if (output != IntPtr.Zero) NativeMethods.oxidize_free_string(output);
            pinned.Free();
        }
    }
    [DllImport("oxidize_pdf_ffi", EntryPoint = "oxidize_ocr_layer", CallingConvention = CallingConvention.Cdecl)]
    private static extern int OcrNative(IntPtr pdf, nuint length,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string request, out IntPtr output);
}
