using System.Text;
using OxidizePdf.NET.Graphics;
using OxidizePdf.NET.Tests.TestHelpers;
using Xunit;

namespace OxidizePdf.NET.Tests.Graphics;

/// <summary>
/// GFX-016 tiling patterns. Verifies the pattern is registered in the page
/// /Resources/Pattern dictionary and that the fill/stroke pattern-selection
/// operators (scn/SCN with the /Pattern color space) reach the content stream.
/// </summary>
public class PdfPageTilingPatternIntegrationTests
{
    // A minimal pattern cell: a 10x10 red square. Raw PDF content-stream operators.
    private static byte[] TileContent() =>
        Encoding.ASCII.GetBytes("1.0 0.0 0.0 rg\n0 0 10 10 re\nf\n");

    private static PdfTilingPattern RedTile(string name) => new(
        name,
        PaintType.Colored,
        TilingType.ConstantSpacing,
        bboxX: 0, bboxY: 0, bboxWidth: 10, bboxHeight: 10,
        xStep: 10, yStep: 10,
        contentStream: TileContent());

    [Fact]
    [Trait("Category", "Integration")]
    public void TilingPattern_AddedToPage_ResourceDictContainsPatternEntry()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.AddTilingPattern(RedTile("P1"))
            .SetFillPattern("P1")
            .DrawRect(50, 50, 200, 200)
            .Fill();
        doc.AddPage(page);

        var pdfText = ContentStreamHelper.ToLatin1(doc.SaveToBytes());
        Assert.Contains("/Pattern", pdfText);
        Assert.Contains("/PatternType", pdfText);
        Assert.Contains("P1", pdfText);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void TilingPattern_FillVariant_ContentStreamContainsScnOperator()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.AddTilingPattern(RedTile("P1"))
            .SetFillPattern("P1")
            .DrawRect(50, 50, 200, 200)
            .Fill();
        doc.AddPage(page);

        // The pattern's own tile content stream appears before the page content
        // stream, so scan all decompressed streams for the page-level operators.
        var stream = ContentStreamHelper.DecompressAllContentStreams(doc.SaveToBytes());
        Assert.Contains("/Pattern cs", stream);
        Assert.Contains("/P1 scn", stream);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void TilingPattern_StrokeVariant_ContentStreamContainsSCNOperator()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.AddTilingPattern(RedTile("P1"))
            .SetStrokePattern("P1")
            .DrawRect(50, 50, 200, 200)
            .Stroke();
        doc.AddPage(page);

        var stream = ContentStreamHelper.DecompressAllContentStreams(doc.SaveToBytes());
        Assert.Contains("/Pattern CS", stream);
        Assert.Contains("/P1 SCN", stream);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void TilingPattern_TileContent_EmittedAsSeparatePatternStream()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.AddTilingPattern(RedTile("P1"))
            .SetFillPattern("P1")
            .DrawRect(50, 50, 200, 200)
            .Fill();
        doc.AddPage(page);

        // The tile's own content (the red 10x10 square) must be emitted as a
        // pattern stream distinct from the page content stream. The BBox and
        // XStep/YStep of the registered pattern prove it is a real Type-1 pattern.
        var pdfText = ContentStreamHelper.ToLatin1(doc.SaveToBytes());
        Assert.Contains("/BBox", pdfText);
        Assert.Contains("/XStep", pdfText);
        Assert.Contains("/YStep", pdfText);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void AddTilingPattern_NullPattern_Throws()
    {
        using var page = PdfPage.A4();
        Assert.Throws<ArgumentNullException>(() => page.AddTilingPattern(null!));
    }
}
