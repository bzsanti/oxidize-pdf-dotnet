using System.Runtime.InteropServices;
using System.Text.Json;

namespace OxidizePdf.NET;

public static partial class PdfOperations
{
    /// <summary>Lists standard FreeText annotations with stable identities.</summary>
    public static Task<IReadOnlyList<PdfFreeText>> GetFreeTextAnnotationsAsync(
        byte[] pdfBytes, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0) throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ct.ThrowIfCancellationRequested();
        return Task.Run<IReadOnlyList<PdfFreeText>>(() => CallFreeText(pdfBytes, null).Annotations, ct);
    }

    /// <summary>
    /// Applies Add/Update/Remove operations atomically in one incremental revision.
    /// Updates preserve identities and unrelated keys. Encrypted inputs are rejected;
    /// upstream enforces DocMDP annotation-modification permissions.
    /// </summary>
    public static Task<PdfFreeTextUpdate> EditFreeTextAnnotationsAsync(
        byte[] pdfBytes, IEnumerable<PdfFreeTextMutation> mutations, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);
        ArgumentNullException.ThrowIfNull(mutations);
        if (pdfBytes.Length == 0) throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ct.ThrowIfCancellationRequested();
        var dtos = mutations.Select(mutation =>
        {
            switch (mutation)
            {
                case PdfFreeTextMutation.Add add:
                    ValidateFreeText(add.Rectangle, add.Contents, add.DefaultAppearance, add.Alignment);
                    return (object)new { operation = "add", page_index = add.PageIndex, rect = add.Rectangle,
                        contents = add.Contents, default_appearance = add.DefaultAppearance, alignment = (byte)add.Alignment };
                case PdfFreeTextMutation.Update update when update.Id.ObjectNumber > 0:
                    ValidateFreeText(update.Rectangle, update.Contents, update.DefaultAppearance, update.Alignment);
                    return new { operation = "update", object_number = update.Id.ObjectNumber, generation_number = update.Id.GenerationNumber,
                        rect = update.Rectangle, contents = update.Contents, default_appearance = update.DefaultAppearance, alignment = (byte)update.Alignment };
                case PdfFreeTextMutation.Remove remove when remove.Id.ObjectNumber > 0:
                    return new { operation = "remove", object_number = remove.Id.ObjectNumber, generation_number = remove.Id.GenerationNumber };
                default:
                    throw new ArgumentException("Invalid FreeText mutation or identity", nameof(mutations));
            }
        }).ToArray();
        if (dtos.Length == 0) throw new ArgumentException("At least one mutation is required", nameof(mutations));
        var json = JsonSerializer.Serialize(dtos);
        ct.ThrowIfCancellationRequested();
        return Task.Run(() => CallFreeText(pdfBytes, json), ct);
    }

    private static void ValidateFreeText(double[] rect, string contents, string appearance, PdfFreeTextAlignment alignment)
    {
        ArgumentNullException.ThrowIfNull(rect);
        ArgumentException.ThrowIfNullOrWhiteSpace(contents);
        ArgumentException.ThrowIfNullOrWhiteSpace(appearance);
        if (rect.Length != 4 || rect.Any(v => !double.IsFinite(v)) || rect[0] >= rect[2] || rect[1] >= rect[3])
            throw new ArgumentException("Rectangle must have four finite coordinates and positive width and height", nameof(rect));
        if (appearance.Any(c => c > 127 || c == 0))
            throw new ArgumentException("Default appearance must be ASCII without NUL characters", nameof(appearance));
        if (!Enum.IsDefined(alignment)) throw new ArgumentOutOfRangeException(nameof(alignment));
    }

    private static PdfFreeTextUpdate CallFreeText(byte[] pdfBytes, string? mutations)
    {
        var pinned = GCHandle.Alloc(pdfBytes, GCHandleType.Pinned);
        IntPtr result = IntPtr.Zero;
        try
        {
            var status = NativeMethods.oxidize_free_text_json(pinned.AddrOfPinnedObject(),
                (nuint)pdfBytes.Length, mutations, out result);
            ThrowIfError(status, "Failed to read or edit FreeTexts");
            var value = JsonSerializer.Deserialize<PdfFreeTextUpdate>(Marshal.PtrToStringUTF8(result)
                ?? throw new PdfExtractionException("Missing FreeText response"))
                ?? throw new PdfExtractionException("Invalid FreeText response");
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
