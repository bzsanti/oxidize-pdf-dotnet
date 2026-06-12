using System.Text;
using OxidizePdf.NET;
using OxidizePdf.NET.Models;

namespace OxidizePdf.NET.Tests;

/// <summary>
/// Behavioral tests for PAGE-010 (convert a parsed page of an existing PDF into
/// an editable page). Each test opens a real source PDF, overlays new content,
/// saves, and re-parses the output — asserting that BOTH the preserved original
/// content and the overlay survive the round-trip.
/// </summary>
public class PdfPageEditTests
{
    private static readonly PdfSaveOptions Uncompressed = new() { CompressStreams = false };

    /// <summary>Builds a one-page source PDF carrying an extractable marker string.</summary>
    private static byte[] BuildSourcePdf(string marker)
    {
        using var doc = new PdfDocument();
        using var page = new PdfPage(400, 500);
        page.DrawTextAt(StandardFont.Helvetica, 12, 72, 420, marker);
        doc.AddPage(page);
        return doc.SaveToBytes();
    }

    [Fact]
    public void FromParsedBytes_NullBytes_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PdfPage.FromParsedBytes(null!, 0));
    }

    [Fact]
    public void FromParsedBytes_NegativeIndex_Throws()
    {
        var src = BuildSourcePdf("ORIGINAL_MARKER");
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfPage.FromParsedBytes(src, -1));
    }

    [Fact]
    public void FromParsedBytes_OutOfRangeIndex_Throws()
    {
        var src = BuildSourcePdf("ORIGINAL_MARKER");
        // Source has exactly one page; index 5 must fail.
        Assert.Throws<PdfExtractionException>(() => PdfPage.FromParsedBytes(src, 5));
    }

    [Fact]
    public void FromParsedBytes_PreservesPageGeometry()
    {
        var src = BuildSourcePdf("ORIGINAL_MARKER");
        using var page = PdfPage.FromParsedBytes(src, 0);
        // Distinctive 400x500 source dimensions prove geometry came from the
        // parsed page, not a blank default.
        Assert.Equal(400.0, page.Width, 2);
        Assert.Equal(500.0, page.Height, 2);
    }

    [Fact]
    public async Task FromParsedBytes_OverlayText_BothMarkersExtractable()
    {
        var src = BuildSourcePdf("ORIGINAL_MARKER");

        byte[] saved;
        using (var doc = new PdfDocument())
        using (var page = PdfPage.FromParsedBytes(src, 0))
        {
            page.DrawTextAt(StandardFont.Helvetica, 14, 50, 50, "OVERLAY_MARKER");
            doc.AddPage(page);
            saved = doc.SaveToBytes(Uncompressed);
        }

        // Re-parse: the text must actually be extractable, not just present as bytes.
        var extracted = await new PdfExtractor().ExtractTextAsync(saved);
        Assert.Contains("ORIGINAL_MARKER", extracted);
        Assert.Contains("OVERLAY_MARKER", extracted);
    }

    [Fact]
    public void FromParsedBytes_ContentStream_ContainsBothTextBlocks()
    {
        var src = BuildSourcePdf("ORIGINAL_MARKER");

        byte[] saved;
        using (var doc = new PdfDocument())
        using (var page = PdfPage.FromParsedBytes(src, 0))
        {
            page.DrawTextAt(StandardFont.Helvetica, 14, 50, 50, "OVERLAY_MARKER");
            doc.AddPage(page);
            saved = doc.SaveToBytes(Uncompressed);
        }

        // PDF tokens are ASCII; Latin1 round-trips every byte without loss.
        var pdf = Encoding.Latin1.GetString(saved);

        // Both literal strings present in the (uncompressed) content stream.
        Assert.Contains("ORIGINAL_MARKER", pdf);
        Assert.Contains("OVERLAY_MARKER", pdf);

        // Two distinct BT...ET text blocks: the preserved original and the overlay
        // are not merged or dropped.
        var btCount = pdf.Split("BT").Length - 1;
        Assert.True(btCount >= 2, $"expected >= 2 BT text blocks, found {btCount}");
    }
}
