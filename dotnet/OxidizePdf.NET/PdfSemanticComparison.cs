using System.Runtime.InteropServices;
using System.Text.Json;

namespace OxidizePdf.NET;

/// <summary>Bounded comparison of PDF object graphs, text, metadata, and revision history.</summary>
public static class PdfSemanticComparison
{
    /// <summary>
    /// Compares supported semantic domains after normalizing object numbering, dictionary
    /// order, stream encoding, and volatile timestamps. This is not pixel rendering or a
    /// signature-trust decision. Malformed, encrypted, undecodable, or over-limit inputs fail.
    /// Cancellation prevents queued work but does not interrupt an active native call.
    /// </summary>
    public static Task<PdfSemanticComparisonResult> CompareAsync(byte[] left, byte[] right,
        PdfSemanticComparisonLimits? limits = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (left.Length == 0 || right.Length == 0) throw new ArgumentException("PDF inputs cannot be empty");
        ct.ThrowIfCancellationRequested();
        var json = JsonSerializer.Serialize(limits ?? new());
        return Task.Run(() => Compare(left, right, json), ct);
    }

    private static PdfSemanticComparisonResult Compare(byte[] left, byte[] right, string options)
    {
        var a = GCHandle.Alloc(left, GCHandleType.Pinned);
        GCHandle b = default;
        IntPtr output = IntPtr.Zero;
        try
        {
            b = GCHandle.Alloc(right, GCHandleType.Pinned);
            var status = CompareNative(a.AddrOfPinnedObject(), (nuint)left.Length, b.AddrOfPinnedObject(), (nuint)right.Length, options, out output);
            if (status != 0) throw new PdfExtractionException(NativeMethods.GetLastError() ?? "Semantic comparison failed");
            return JsonSerializer.Deserialize<PdfSemanticComparisonResult>(Marshal.PtrToStringUTF8(output)
                ?? throw new PdfExtractionException("Missing semantic comparison response"))
                ?? throw new PdfExtractionException("Invalid semantic comparison response");
        }
        finally
        {
            if (output != IntPtr.Zero) NativeMethods.oxidize_free_string(output);
            if (b.IsAllocated) b.Free();
            a.Free();
        }
    }

    [DllImport("oxidize_pdf_ffi", EntryPoint = "oxidize_compare_semantically", CallingConvention = CallingConvention.Cdecl)]
    private static extern int CompareNative(IntPtr left, nuint leftLength, IntPtr right, nuint rightLength,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string options, out IntPtr result);
}
