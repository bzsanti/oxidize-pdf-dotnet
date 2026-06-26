namespace OxidizePdf.NET.Models;

/// <summary>
/// One element of a positioned glyph run drawn with <see cref="PdfPage.ShowCidArray"/>
/// (upstream issue #358). The caller supplies an already-shaped run; the core
/// performs no shaping.
/// </summary>
/// <param name="Cid">
/// 2-byte code (glyph id under <c>CIDToGIDMap = Identity</c>) in the active
/// CID-keyed font.
/// </param>
/// <param name="Adjust">
/// Advance adjustment applied after this glyph (the <c>TJ</c> kern, in thousandths
/// of a text-space unit; positive moves the next glyph left). <c>0</c> = none.
/// </param>
/// <param name="XOffset">
/// Per-glyph horizontal offset (thousandths of a text-space unit; positive = right)
/// that displaces this glyph without changing the advance — for mark/diacritic
/// attachment. <c>0</c> = drawn at the pen position.
/// </param>
public readonly record struct CidGlyph(ushort Cid, float Adjust = 0f, float XOffset = 0f);
