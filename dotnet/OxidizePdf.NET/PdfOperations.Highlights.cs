using System.Runtime.InteropServices;
using System.Text.Json;

namespace OxidizePdf.NET;

public static partial class PdfOperations
{
    /// <summary>Lists standard Highlight annotations with stable IDs and appearance properties.</summary>
    public static Task<IReadOnlyList<PdfHighlight>> GetHighlightsAsync(
        byte[] pdfBytes, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0) throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ct.ThrowIfCancellationRequested();
        return Task.Run<IReadOnlyList<PdfHighlight>>(() => CallHighlights(pdfBytes, null).Highlights, ct);
    }

    /// <summary>
    /// Applies additions and removals in one atomic incremental revision, preserving
    /// the original PDF bytes. A Remove/Add batch replaces a highlight with a new ID.
    /// Encrypted inputs are rejected and geometry is validated. This upstream
    /// editor does not enforce DocMDP certification permissions.
    /// </summary>
    public static Task<PdfHighlightUpdate> EditHighlightsAsync(
        byte[] pdfBytes, IEnumerable<PdfHighlightMutation> mutations, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);
        ArgumentNullException.ThrowIfNull(mutations);
        if (pdfBytes.Length == 0) throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ct.ThrowIfCancellationRequested();
        var dtos = mutations.Select(mutation =>
        {
            switch (mutation)
            {
                case PdfHighlightMutation.Add add:
                    ArgumentNullException.ThrowIfNull(add.Quadrilaterals);
                    ArgumentNullException.ThrowIfNull(add.Color);
                    if (add.Quadrilaterals.Length == 0 || add.Quadrilaterals.Any(q => q is null || q.Length != 8 || q.Any(v => !double.IsFinite(v))))
                        throw new ArgumentException("Each highlight requires at least one quad of eight finite coordinates", nameof(mutations));
                    if (add.Color.Length != 3 || add.Color.Any(v => !double.IsFinite(v) || v < 0 || v > 1))
                        throw new ArgumentException("Highlight RGB components must be in [0, 1]", nameof(mutations));
                    if (add.Opacity is double opacity && (!double.IsFinite(opacity) || opacity < 0 || opacity > 1))
                        throw new ArgumentException("Highlight opacity must be in [0, 1]", nameof(mutations));
                    return (object)new { operation = "add", page_index = add.PageIndex, quadrilaterals = add.Quadrilaterals,
                        color = add.Color, opacity = add.Opacity, contents = add.Contents };
                case PdfHighlightMutation.Remove remove when remove.Id.ObjectNumber > 0:
                    return new { operation = "remove", object_number = remove.Id.ObjectNumber, generation_number = remove.Id.GenerationNumber };
                default:
                    throw new ArgumentException("Invalid highlight mutation or identity", nameof(mutations));
            }
        }).ToArray();
        if (dtos.Length == 0) throw new ArgumentException("At least one mutation is required", nameof(mutations));
        var json = JsonSerializer.Serialize(dtos);
        ct.ThrowIfCancellationRequested();
        return Task.Run(() => CallHighlights(pdfBytes, json), ct);
    }

    private static PdfHighlightUpdate CallHighlights(byte[] pdfBytes, string? mutations)
    {
        var pinned = GCHandle.Alloc(pdfBytes, GCHandleType.Pinned);
        IntPtr result = IntPtr.Zero;
        try
        {
            var status = NativeMethods.oxidize_highlights_json(pinned.AddrOfPinnedObject(),
                (nuint)pdfBytes.Length, mutations, out result);
            ThrowIfError(status, "Failed to read or edit highlights");
            var value = JsonSerializer.Deserialize<PdfHighlightUpdate>(Marshal.PtrToStringUTF8(result)
                ?? throw new PdfExtractionException("Missing highlight response"))
                ?? throw new PdfExtractionException("Invalid highlight response");
            if (mutations is not null && value.PdfBytes.Length == 0)
                throw new PdfExtractionException("Missing updated PDF bytes");
            return value;
        }
        finally
        {
            if (result != IntPtr.Zero) NativeMethods.oxidize_free_string(result);
            pinned.Free();
        }
    }
}
