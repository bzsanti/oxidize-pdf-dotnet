using OxidizePdf.NET.Models;

namespace OxidizePdf.NET.Tests;

/// <summary>
/// Tests for the CID-keyed positioned-glyph-run API (upstream issue #358,
/// oxidize-pdf 3.0.0). A caller registers a TrueType font drawn by glyph id
/// (CID = GID) and draws a pre-shaped run via <see cref="PdfPage.ShowCidArray"/>.
/// Verified against the emitted PDF bytes (font dictionary + ToUnicode CMap),
/// not a smoke check.
/// </summary>
public class CidFontTests
{
    private static byte[] LoadSampleFont() =>
        File.ReadAllBytes(Path.Combine(
            AppContext.BaseDirectory, "fixtures", "fonts", "sample.ttf"));

    // Glyph ids resolved from sample.ttf's cmap: 'A' = 36, 'B' = 37.
    private const ushort GidA = 36;
    private const ushort GidB = 37;

    [Fact]
    [Trait("Category", "Integration")]
    public void AddCidKeyedFont_ShowCidArray_EmbedsType0CidFontWithToUnicode()
    {
        var fontBytes = LoadSampleFont();

        var mapping = new CidFontMapping();
        mapping.CidToGid[GidA] = GidA;
        mapping.CidToGid[GidB] = GidB;
        mapping.CidToUnicode[GidA] = 'A';
        mapping.CidToUnicode[GidB] = 'B';

        using var doc = new PdfDocument();
        doc.AddCidKeyedFont("ShapedSample", fontBytes, mapping);
        using var page = doc.NewPageA4();
        page.ShowCidArray(
            "ShapedSample", 24.0,
            new[] { new CidGlyph(GidA), new CidGlyph(GidB, -15f) },
            100.0, 700.0);
        doc.AddPage(page);

        // Disable compression so the ToUnicode CMap stream is inspectable.
        byte[] pdf = doc.SaveToBytes(new PdfSaveOptions { CompressStreams = false });
        string text = System.Text.Encoding.Latin1.GetString(pdf);

        Assert.Contains("/Subtype /Type0", text);
        Assert.Contains("/Subtype /CIDFontType2", text);
        // ToUnicode CMap maps each CID back to its Unicode code point (4-hex each).
        Assert.Contains($"<{GidA:X4}> <{(int)'A':X4}>", text);
        Assert.Contains($"<{GidB:X4}> <{(int)'B':X4}>", text);
    }

    [Fact]
    public void AddCidKeyedFont_NullArguments_Throw()
    {
        using var doc = new PdfDocument();
        Assert.Throws<ArgumentNullException>(
            () => doc.AddCidKeyedFont(null!, new byte[] { 1 }, new CidFontMapping()));
        Assert.Throws<ArgumentNullException>(
            () => doc.AddCidKeyedFont("x", null!, new CidFontMapping()));
        Assert.Throws<ArgumentNullException>(
            () => doc.AddCidKeyedFont("x", new byte[] { 1 }, null!));
    }

    [Fact]
    public void ShowCidArray_NullArguments_Throw()
    {
        using var doc = new PdfDocument();
        using var page = doc.NewPageA4();
        Assert.Throws<ArgumentNullException>(
            () => page.ShowCidArray(null!, 12.0, new[] { new CidGlyph(GidA) }, 0, 0));
        Assert.Throws<ArgumentNullException>(
            () => page.ShowCidArray("x", 12.0, null!, 0, 0));
    }
}
