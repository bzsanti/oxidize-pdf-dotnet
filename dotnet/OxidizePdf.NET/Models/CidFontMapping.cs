namespace OxidizePdf.NET.Models;

/// <summary>
/// Maps the CIDs of a CID-keyed font (CID = glyph id under
/// <c>CIDToGIDMap = Identity</c>) to glyph ids and back to Unicode, for use with
/// <see cref="PdfDocument.AddCidKeyedFont"/> (upstream issue #358).
/// </summary>
/// <remarks>
/// <see cref="CidToGid"/> is required and tells the writer which glyph each CID
/// renders. <see cref="CidToUnicode"/> and <see cref="CidToUnicodeStr"/> feed the
/// emitted <c>ToUnicode</c> CMap so the drawn run stays extractable; supply
/// whichever fits each CID (a multi-character string, e.g. an <c>fi</c> ligature,
/// takes precedence over the single code point for the same CID). The maximum CID
/// is derived automatically from the supplied keys.
/// </remarks>
public sealed class CidFontMapping
{
    /// <summary>CID → glyph id in the embedded font. Required.</summary>
    public IDictionary<ushort, ushort> CidToGid { get; } = new Dictionary<ushort, ushort>();

    /// <summary>CID → single Unicode code point (for the <c>ToUnicode</c> CMap).</summary>
    public IDictionary<ushort, uint> CidToUnicode { get; } = new Dictionary<ushort, uint>();

    /// <summary>
    /// CID → multi-character Unicode string (for the <c>ToUnicode</c> CMap). Takes
    /// precedence over <see cref="CidToUnicode"/> for any CID present in both.
    /// </summary>
    public IDictionary<ushort, string> CidToUnicodeStr { get; } = new Dictionary<ushort, string>();
}
