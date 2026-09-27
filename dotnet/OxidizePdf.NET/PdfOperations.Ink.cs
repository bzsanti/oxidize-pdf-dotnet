using System.Runtime.InteropServices;
using System.Text.Json;

namespace OxidizePdf.NET;

public static partial class PdfOperations
{
    /// <summary>Lists standard Ink annotations with stable identities.</summary>
    public static Task<IReadOnlyList<PdfInk>> GetInkAnnotationsAsync(
        byte[] pdfBytes, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0) throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ct.ThrowIfCancellationRequested();
        return Task.Run<IReadOnlyList<PdfInk>>(() => CallInk(pdfBytes, null).Annotations, ct);
    }

    /// <summary>
    /// Applies Add/Update/Remove operations atomically in one incremental revision.
    /// Updates preserve identities and unrelated keys. Encrypted inputs are rejected;
    /// upstream enforces DocMDP annotation-modification permissions.
    /// </summary>
    public static Task<PdfInkUpdate> EditInkAnnotationsAsync(
        byte[] pdfBytes, IEnumerable<PdfInkMutation> mutations, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);
        ArgumentNullException.ThrowIfNull(mutations);
        if (pdfBytes.Length == 0) throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ct.ThrowIfCancellationRequested();
        var dtos = mutations.Select(mutation =>
        {
            switch (mutation)
            {
                case PdfInkMutation.Add add:
                    ValidateInk(add.Strokes, add.Color, add.Width, add.Opacity);
                    return (object)new { operation = "add", page_index = add.PageIndex, strokes = add.Strokes,
                        color = add.Color, width = add.Width, opacity = add.Opacity };
                case PdfInkMutation.Update update when update.Id.ObjectNumber > 0:
                    ValidateInk(update.Strokes, update.Color, update.Width, update.Opacity);
                    return new { operation = "update", object_number = update.Id.ObjectNumber, generation_number = update.Id.GenerationNumber,
                        strokes = update.Strokes, color = update.Color, width = update.Width, opacity = update.Opacity };
                case PdfInkMutation.Remove remove when remove.Id.ObjectNumber > 0:
                    return new { operation = "remove", object_number = remove.Id.ObjectNumber, generation_number = remove.Id.GenerationNumber };
                default:
                    throw new ArgumentException("Invalid Ink mutation or identity", nameof(mutations));
            }
        }).ToArray();
        if (dtos.Length == 0) throw new ArgumentException("At least one mutation is required", nameof(mutations));
        var json = JsonSerializer.Serialize(dtos);
        ct.ThrowIfCancellationRequested();
        return Task.Run(() => CallInk(pdfBytes, json), ct);
    }

    private static void ValidateInk(double[][] strokes, double[] color, double width, double? opacity)
    {
        ArgumentNullException.ThrowIfNull(strokes);
        ArgumentNullException.ThrowIfNull(color);
        if (strokes.Length is < 1 or > 4096)
            throw new ArgumentException("Ink requires between 1 and 4096 strokes", nameof(strokes));
        long points = 0;
        foreach (var stroke in strokes)
        {
            if (stroke is null || stroke.Length == 0 || stroke.Length % 2 != 0 || stroke.Any(v => !double.IsFinite(v)))
                throw new ArgumentException("Strokes require finite x/y coordinate pairs", nameof(strokes));
            points += stroke.Length / 2;
        }
        if (points > 1_000_000) throw new ArgumentException("Ink is limited to 1000000 points", nameof(strokes));
        if (color.Length is not (1 or 3 or 4) || color.Any(v => !double.IsFinite(v) || v < 0 || v > 1))
            throw new ArgumentException("Color requires 1, 3 or 4 components in [0, 1]", nameof(color));
        if (!double.IsFinite(width) || width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (opacity is double value && (!double.IsFinite(value) || value < 0 || value > 1))
            throw new ArgumentOutOfRangeException(nameof(opacity));
    }

    private static PdfInkUpdate CallInk(byte[] pdfBytes, string? mutations)
    {
        var pinned = GCHandle.Alloc(pdfBytes, GCHandleType.Pinned);
        IntPtr result = IntPtr.Zero;
        try
        {
            var status = NativeMethods.oxidize_ink_json(pinned.AddrOfPinnedObject(),
                (nuint)pdfBytes.Length, mutations, out result);
            ThrowIfError(status, "Failed to read or edit Inks");
            var value = JsonSerializer.Deserialize<PdfInkUpdate>(Marshal.PtrToStringUTF8(result)
                ?? throw new PdfExtractionException("Missing Ink response"))
                ?? throw new PdfExtractionException("Invalid Ink response");
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
