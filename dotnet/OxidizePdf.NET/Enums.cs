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

/// <summary>
/// Numbering style for ordered lists.
/// </summary>
public enum OrderedListStyle
{
    /// <summary>Decimal numbers: 1, 2, 3, ...</summary>
    Decimal = 0,
    /// <summary>Lowercase letters: a, b, c, ...</summary>
    LowerAlpha = 1,
    /// <summary>Uppercase letters: A, B, C, ...</summary>
    UpperAlpha = 2,
    /// <summary>Lowercase Roman numerals: i, ii, iii, ...</summary>
    LowerRoman = 3,
    /// <summary>Uppercase Roman numerals: I, II, III, ...</summary>
    UpperRoman = 4,
}

/// <summary>
/// Bullet style for unordered lists.
/// </summary>
public enum BulletStyle
{
    /// <summary>Filled circle bullet</summary>
    Disc = 0,
    /// <summary>Empty circle bullet</summary>
    Circle = 1,
    /// <summary>Filled square bullet</summary>
    Square = 2,
    /// <summary>Dash bullet</summary>
    Dash = 3,
}

/// <summary>
/// Text rendering mode that controls how text glyphs are painted.
/// </summary>
public enum TextRenderingMode
{
    /// <summary>Fill text glyphs (default)</summary>
    Fill = 0,
    /// <summary>Stroke text outlines</summary>
    Stroke = 1,
    /// <summary>Fill and then stroke text</summary>
    FillStroke = 2,
    /// <summary>Neither fill nor stroke (invisible text, still selectable)</summary>
    Invisible = 3,
    /// <summary>Fill text and add to clipping path</summary>
    FillClip = 4,
    /// <summary>Stroke text and add to clipping path</summary>
    StrokeClip = 5,
    /// <summary>Fill, stroke, and add to clipping path</summary>
    FillStrokeClip = 6,
    /// <summary>Add text to clipping path only</summary>
    Clip = 7,
}

/// <summary>
/// Line cap style applied to the ends of open stroked paths.
/// </summary>
public enum LineCap
{
    /// <summary>Flat cap at the exact endpoint (default)</summary>
    Butt = 0,
    /// <summary>Semicircular cap extending beyond the endpoint</summary>
    Round = 1,
    /// <summary>Square cap extending beyond the endpoint by half the line width</summary>
    Square = 2,
}

/// <summary>
/// Line join style applied where two path segments meet.
/// </summary>
public enum LineJoin
{
    /// <summary>Sharp corner (default); subject to miter limit</summary>
    Miter = 0,
    /// <summary>Rounded corner</summary>
    Round = 1,
    /// <summary>Beveled (flat) corner</summary>
    Bevel = 2,
}

/// <summary>
/// Blend mode for compositing overlapping graphics elements.
/// </summary>
public enum BlendMode
{
    /// <summary>Normal compositing (default)</summary>
    Normal = 0,
    /// <summary>Multiplies base and blend colors</summary>
    Multiply = 1,
    /// <summary>Inverse of Multiply; lightens the image</summary>
    Screen = 2,
    /// <summary>Combines Multiply and Screen based on the base color</summary>
    Overlay = 3,
    /// <summary>Darkens or lightens depending on the blend color (gentle)</summary>
    SoftLight = 4,
    /// <summary>Combines Multiply and Screen based on the blend color</summary>
    HardLight = 5,
    /// <summary>Brightens the base color to reflect the blend color</summary>
    ColorDodge = 6,
    /// <summary>Darkens the base color to reflect the blend color</summary>
    ColorBurn = 7,
    /// <summary>Selects the darker of the base and blend colors</summary>
    Darken = 8,
    /// <summary>Selects the lighter of the base and blend colors</summary>
    Lighten = 9,
    /// <summary>Subtracts the darker from the lighter color</summary>
    Difference = 10,
    /// <summary>Similar to Difference with lower contrast</summary>
    Exclusion = 11,
    /// <summary>Uses the hue of the blend color with the saturation and luminosity of the base</summary>
    Hue = 12,
    /// <summary>Uses the saturation of the blend color with the hue and luminosity of the base</summary>
    Saturation = 13,
    /// <summary>Uses the hue and saturation of the blend color with the luminosity of the base</summary>
    Color = 14,
    /// <summary>Uses the luminosity of the blend color with the hue and saturation of the base</summary>
    Luminosity = 15,
}

/// <summary>
/// Icon for text (sticky note) annotations.
/// </summary>
public enum TextNoteIcon
{
    /// <summary>Comment icon</summary>
    Comment = 0,
    /// <summary>Key icon</summary>
    Key = 1,
    /// <summary>Note icon (default)</summary>
    Note = 2,
    /// <summary>Help icon</summary>
    Help = 3,
    /// <summary>New paragraph icon</summary>
    NewParagraph = 4,
    /// <summary>Paragraph icon</summary>
    Paragraph = 5,
    /// <summary>Insert icon</summary>
    Insert = 6,
}

/// <summary>
/// Standard stamp names for stamp annotations.
/// </summary>
public enum StampType
{
    /// <summary>Approved</summary>
    Approved = 0,
    /// <summary>Draft</summary>
    Draft = 1,
    /// <summary>Confidential</summary>
    Confidential = 2,
    /// <summary>Final</summary>
    Final = 3,
    /// <summary>Not Approved</summary>
    NotApproved = 4,
    /// <summary>Experimental</summary>
    Experimental = 5,
    /// <summary>As Is</summary>
    AsIs = 6,
    /// <summary>Expired</summary>
    Expired = 7,
    /// <summary>Not For Public Release</summary>
    NotForPublicRelease = 8,
    /// <summary>Sold</summary>
    Sold = 9,
    /// <summary>Departmental</summary>
    Departmental = 10,
    /// <summary>For Comment</summary>
    ForComment = 11,
    /// <summary>Top Secret</summary>
    TopSecret = 12,
    /// <summary>For Public Release</summary>
    ForPublicRelease = 13,
}

/// <summary>
/// Page layout preference used when opening the document.
/// </summary>
public enum PdfPageLayout
{
    /// <summary>Display one page at a time.</summary>
    SinglePage = 0,
    /// <summary>Display pages in a continuous single column.</summary>
    OneColumn = 1,
    /// <summary>Two-column layout, odd pages on the left.</summary>
    TwoColumnLeft = 2,
    /// <summary>Two-column layout, odd pages on the right.</summary>
    TwoColumnRight = 3,
    /// <summary>Two-up layout with cover alone, odd pages on the left.</summary>
    TwoPageLeft = 4,
    /// <summary>Two-up layout with cover alone, odd pages on the right.</summary>
    TwoPageRight = 5,
}

/// <summary>
/// Page mode — which side panel is visible when the document opens.
/// </summary>
public enum PdfPageMode
{
    /// <summary>No outline or thumbnail panel visible (default).</summary>
    UseNone = 0,
    /// <summary>Document outline (bookmarks) visible.</summary>
    UseOutlines = 1,
    /// <summary>Thumbnail images panel visible.</summary>
    UseThumbs = 2,
    /// <summary>Full-screen mode — hide all UI chrome.</summary>
    FullScreen = 3,
    /// <summary>Optional-content (layer) panel visible.</summary>
    UseOC = 4,
    /// <summary>Attachments panel visible.</summary>
    UseAttachments = 5,
}

/// <summary>
/// Print-scaling preference.
/// </summary>
public enum PdfPrintScaling
{
    /// <summary>Use viewer default (usually shrink-to-fit).</summary>
    AppDefault = 0,
    /// <summary>No scaling — print at 100% even if clipping occurs.</summary>
    None = 1,
}

/// <summary>
/// Duplex printing preference.
/// </summary>
public enum PdfDuplex
{
    /// <summary>Single-sided printing.</summary>
    Simplex = 0,
    /// <summary>Double-sided — flip on short edge (landscape-style).</summary>
    DuplexFlipShortEdge = 1,
    /// <summary>Double-sided — flip on long edge (book-style).</summary>
    DuplexFlipLongEdge = 2,
}

/// <summary>
/// PDF destination fit mode for open actions and named destinations.
/// </summary>
public enum PdfDestinationFit
{
    /// <summary>Position at specific coordinates with optional zoom (XYZ).</summary>
    Xyz = 0,
    /// <summary>Fit entire page in window.</summary>
    Fit = 1,
    /// <summary>Fit page width, optional top coordinate.</summary>
    FitH = 2,
    /// <summary>Fit page height, optional left coordinate.</summary>
    FitV = 3,
    /// <summary>Fit rectangle.</summary>
    FitR = 4,
    /// <summary>Fit bounding box of page contents.</summary>
    FitB = 5,
}

/// <summary>
/// Page-label numbering style (custom page numbering). Discriminants match the
/// FFI contract consumed by <c>oxidize_document_set_page_labels_json</c>.
/// </summary>
public enum PdfPageLabelStyle
{
    /// <summary>Decimal arabic numerals (1, 2, 3, …). Emits <c>/S /D</c>.</summary>
    DecimalArabic = 0,
    /// <summary>Lowercase roman numerals (i, ii, iii, …). Emits <c>/S /r</c>.</summary>
    LowercaseRoman = 1,
    /// <summary>Uppercase roman numerals (I, II, III, …). Emits <c>/S /R</c>.</summary>
    UppercaseRoman = 2,
    /// <summary>Lowercase letters (a, b, c, …). Emits <c>/S /a</c>.</summary>
    LowercaseLetters = 3,
    /// <summary>Uppercase letters (A, B, C, …). Emits <c>/S /A</c>.</summary>
    UppercaseLetters = 4,
    /// <summary>No numeric portion (prefix only, or blank). Omits <c>/S</c>.</summary>
    None = 5,
}
