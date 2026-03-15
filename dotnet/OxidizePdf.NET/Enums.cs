namespace OxidizePdf.NET;

/// <summary>
/// The 14 standard PDF fonts available in all PDF viewers without embedding.
/// </summary>
public enum StandardFont
{
    /// <summary>Helvetica (sans-serif)</summary>
    Helvetica = 0,
    /// <summary>Helvetica Bold</summary>
    HelveticaBold = 1,
    /// <summary>Helvetica Oblique (italic)</summary>
    HelveticaOblique = 2,
    /// <summary>Helvetica Bold Oblique</summary>
    HelveticaBoldOblique = 3,
    /// <summary>Times Roman (serif)</summary>
    TimesRoman = 4,
    /// <summary>Times Bold</summary>
    TimesBold = 5,
    /// <summary>Times Italic</summary>
    TimesItalic = 6,
    /// <summary>Times Bold Italic</summary>
    TimesBoldItalic = 7,
    /// <summary>Courier (monospace)</summary>
    Courier = 8,
    /// <summary>Courier Bold</summary>
    CourierBold = 9,
    /// <summary>Courier Oblique</summary>
    CourierOblique = 10,
    /// <summary>Courier Bold Oblique</summary>
    CourierBoldOblique = 11,
    /// <summary>Symbol (Greek and mathematical symbols)</summary>
    Symbol = 12,
    /// <summary>ZapfDingbats (decorative symbols)</summary>
    ZapfDingbats = 13,
}

/// <summary>
/// Permission flags for encrypted PDF documents.
/// Multiple permissions can be combined using bitwise OR.
/// </summary>
[Flags]
public enum PdfPermissions : uint
{
    /// <summary>No permissions granted</summary>
    None = 0,
    /// <summary>Allow printing at any quality</summary>
    Print = 0x01,
    /// <summary>Allow copying text and graphics</summary>
    Copy = 0x02,
    /// <summary>Allow modifying document contents</summary>
    ModifyContents = 0x04,
    /// <summary>Allow adding or modifying text annotations</summary>
    ModifyAnnotations = 0x08,
    /// <summary>Allow filling in form fields</summary>
    FillForms = 0x10,
    /// <summary>Allow content extraction for accessibility purposes</summary>
    Accessibility = 0x20,
    /// <summary>Allow assembling the document (insert, rotate, delete pages)</summary>
    Assemble = 0x40,
    /// <summary>Allow printing in high quality</summary>
    PrintHighQuality = 0x80,
    /// <summary>All permissions granted</summary>
    All = 0xFF,
}

/// <summary>
/// Text alignment options for text flow operations.
/// </summary>
public enum TextAlign
{
    /// <summary>Align text to the left margin (default)</summary>
    Left = 0,
    /// <summary>Align text to the right margin</summary>
    Right = 1,
    /// <summary>Center text between margins</summary>
    Center = 2,
    /// <summary>Justify text to fill the full width between margins</summary>
    Justified = 3,
}
