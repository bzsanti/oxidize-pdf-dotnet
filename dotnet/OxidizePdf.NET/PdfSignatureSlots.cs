using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OxidizePdf.NET;

/// <summary>An application signature field; handwriting completion is separate from CMS signing.</summary>
public sealed class PdfSignatureSlot
{
    /// <summary>Unique field name.</summary>
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    /// <summary>Application metadata preserved as text.</summary>
    [JsonPropertyName("metadata")] public string Metadata { get; init; } = "";
    /// <summary>Zero-based page index.</summary>
    [JsonPropertyName("page_index")] public int PageIndex { get; init; }
    /// <summary>Page rotation in degrees.</summary>
    [JsonPropertyName("rotation")] public int Rotation { get; init; }
    /// <summary>Rectangle [left, bottom, right, top], in PDF points.</summary>
    [JsonPropertyName("rect")] public double[] Rectangle { get; init; } = [];
    /// <summary>Whether application handwriting is marked complete.</summary>
    [JsonPropertyName("completed")] public bool Completed { get; init; }
    /// <summary>Whether a digital signature value exists; this is not a validity assertion.</summary>
    [JsonPropertyName("digitally_signed")] public bool DigitallySigned { get; init; }
}

/// <summary>Incremental management of signature slots without rebuilding the original PDF.</summary>
public static class PdfSignatureSlots
{
    /// <summary>Creates a field. Rectangle is [left, bottom, right, top] in PDF points.</summary>
    public static Task<byte[]> CreateAsync(byte[] pdf, string name, int pageIndex, double[] rectangle,
        string metadata = "", CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(rectangle);
        ArgumentNullException.ThrowIfNull(metadata);
        return CallAsync<byte[]>(pdf, new { action = "Create", name, page = pageIndex, rect = rectangle, metadata }, ct);
    }

    /// <summary>Lists slot state, including application and digital completion separately.</summary>
    public static Task<PdfSignatureSlot[]> ListAsync(byte[] pdf, CancellationToken ct = default)
        => CallAsync<PdfSignatureSlot[]>(pdf, new { action = "List" }, ct);

    /// <summary>Reads a named slot or throws if it is absent.</summary>
    public static Task<PdfSignatureSlot> ReadAsync(byte[] pdf, string name, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(name);
        return CallAsync<PdfSignatureSlot>(pdf, new { action = "Read", name }, ct);
    }

    /// <summary>Removes an unsigned application slot subject to core permission checks.</summary>
    public static Task<byte[]> RemoveAsync(byte[] pdf, string name, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(name);
        return CallAsync<byte[]>(pdf, new { action = "Remove", name }, ct);
    }

    /// <summary>Draws normalized [0,1] point strokes and an optional label. Does not create CMS.</summary>
    public static Task<byte[]> DrawAsync(byte[] pdf, string name, double[][][] strokes,
        bool complete = false, string? label = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(strokes);
        return CallAsync<byte[]>(pdf, new { action = "Draw", name, strokes, complete, label }, ct);
    }

    /// <summary>Updates an incomplete slot. Completed slots reject further drawing. Does not cryptographically sign.</summary>
    public static Task<byte[]> CompleteAsync(byte[] pdf, string name, double[][][] strokes,
        bool complete = true, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(strokes);
        return CallAsync<byte[]>(pdf, new { action = "Complete", name, strokes, complete }, ct);
    }

    // Snapshot inputs before queuing: caller mutations cannot change an in-flight operation.
    private static Task<T> CallAsync<T>(byte[] pdf, object request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        if (pdf.Length == 0) throw new ArgumentException("PDF bytes cannot be empty", nameof(pdf));
        ct.ThrowIfCancellationRequested();
        var snapshot = (byte[])pdf.Clone();
        var json = JsonSerializer.Serialize(request);
        return Task.Run(() => Call<T>(snapshot, json), ct);
    }

    private static T Call<T>(byte[] pdf, string request)
    {
        var pinned = GCHandle.Alloc(pdf, GCHandleType.Pinned);
        IntPtr output = IntPtr.Zero;
        try
        {
            var status = SlotsNative(pinned.AddrOfPinnedObject(), (nuint)pdf.Length, request, out output);
            if (status != 0) throw new PdfExtractionException(NativeMethods.GetLastError() ?? "Signature slot operation failed");
            return JsonSerializer.Deserialize<T>(Marshal.PtrToStringUTF8(output)
                ?? throw new PdfExtractionException("Missing signature slot response"))
                ?? throw new PdfExtractionException("Invalid signature slot response");
        }
        finally
        {
            if (output != IntPtr.Zero) NativeMethods.oxidize_free_string(output);
            pinned.Free();
        }
    }

    [DllImport("oxidize_pdf_ffi", EntryPoint = "oxidize_signature_slots", CallingConvention = CallingConvention.Cdecl)]
    private static extern int SlotsNative(IntPtr pdf, nuint length,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string request, out IntPtr output);
}
