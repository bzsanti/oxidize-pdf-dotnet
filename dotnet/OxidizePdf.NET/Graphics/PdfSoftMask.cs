namespace OxidizePdf.NET.Graphics;

/// <summary>
/// Soft-mask subtype (ISO 32000-1 §11.6.4.3, Table 144).
/// </summary>
public enum SoftMaskKind
{
    /// <summary>No mask — disables a previously set soft mask (<c>/S /None</c>).</summary>
    None = 0,

    /// <summary>Alpha mask: the group's alpha channel is the mask (<c>/S /Alpha</c>).</summary>
    Alpha = 1,

    /// <summary>Luminosity mask: the group's luminosity is the mask (<c>/S /Luminosity</c>).</summary>
    Luminosity = 2,
}

/// <summary>
/// A soft mask (GFX-021) applied through an ExtGState <c>/SMask</c> entry. For
/// <see cref="SoftMaskKind.Alpha"/> and <see cref="SoftMaskKind.Luminosity"/> the
/// mask source is a Form XObject registered on the page (its name is resolved to an
/// indirect <c>/G</c> reference at save time). Apply with <see cref="PdfPage.ApplySoftMask"/>.
/// </summary>
public sealed class PdfSoftMask
{
    /// <summary>The mask subtype.</summary>
    public SoftMaskKind Kind { get; }

    /// <summary>
    /// Name of the Form XObject acting as the mask source; null for
    /// <see cref="SoftMaskKind.None"/>. Must match a form registered on the page via
    /// <see cref="PdfPage.AddFormXObject"/> before the document is saved.
    /// </summary>
    public string? GroupReference { get; }

    private PdfSoftMask(SoftMaskKind kind, string? groupReference)
    {
        Kind = kind;
        GroupReference = groupReference;
    }

    /// <summary>Creates a soft mask that disables masking (<c>/S /None</c>).</summary>
    public static PdfSoftMask None() => new(SoftMaskKind.None, null);

    /// <summary>Creates an alpha soft mask sourced from the Form XObject named <paramref name="groupReference"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="groupReference"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="groupReference"/> is empty.</exception>
    public static PdfSoftMask Alpha(string groupReference) =>
        new(SoftMaskKind.Alpha, Validate(groupReference));

    /// <summary>Creates a luminosity soft mask sourced from the Form XObject named <paramref name="groupReference"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="groupReference"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="groupReference"/> is empty.</exception>
    public static PdfSoftMask Luminosity(string groupReference) =>
        new(SoftMaskKind.Luminosity, Validate(groupReference));

    private static string Validate(string groupReference)
    {
        ArgumentNullException.ThrowIfNull(groupReference);
        if (groupReference.Length == 0)
            throw new ArgumentException("Soft mask group reference must not be empty.", nameof(groupReference));
        return groupReference;
    }
}
