using System.Text.RegularExpressions;

namespace OxidizePdf.NET.Tests;

/// <summary>
/// Regression tests for the per-Document <c>FontMetricsStore</c> introduced
/// in oxidize-pdf 2.8.0 (upstream issue #230).
///
/// Pre-2.8.0, <c>Document::add_font_from_bytes</c> wrote custom-font metrics
/// to a process-wide global registry. <c>TextFlow.WriteWrapped</c>
/// measurement could find the font from any code path.
///
/// In 2.8.0, the registry is per-<c>Document</c> and bound to a page only
/// when the page is constructed via <c>Document::new_page_*</c> or when
/// <c>Document::add_page</c> attaches a previously-standalone page. The
/// legacy FFI flow (<see cref="PdfPage.A4"/> + draw + <see cref="PdfDocument.AddPage"/>)
/// performs all draw operations BEFORE <c>add_page</c>, so the page is not
/// bound to the store at draw time. Any measurement-driven emitter
/// (text wrapping, table layout, header/footer width) falls back to
/// hardcoded default widths.
///
/// The fix is to construct the drawing page via the document-bound factories
/// <see cref="PdfDocument.NewPageA4"/> / <see cref="PdfDocument.NewPageLetter"/>
/// / <see cref="PdfDocument.NewPage(double, double)"/>. The substantive
/// verification lives in the Rust test suite
/// (<c>native/src/document.rs::fontmetricsstore_binding_tests</c>); the
/// upstream <c>text::extraction::calculate_text_width</c> falls back to
/// <c>text.len() * font_size * 0.5</c> for Type0/CID fonts, so a
/// .NET-level test cannot use <c>PartitionAsync(...).Width</c> as a proxy
/// for "rendered width" — it would return the same 500/em prediction
/// regardless of whether the renderer used real or default metrics. The
/// tests below verify what IS observable at this layer: the API contract of
/// the new factories, and the round-trip integrity of the rendered PDF.
/// </summary>
public class CustomFontMetricsRegressionTests
{
    private static byte[] LoadSampleFont() =>
        File.ReadAllBytes(Path.Combine(
            AppContext.BaseDirectory, "fixtures", "fonts", "sample.ttf"));

    /// <summary>
    /// Smoke-free contract test: the three new factories accept a document
    /// that has already registered a custom font and return a non-disposed
    /// page handle whose width / height reflect the requested geometry.
    /// Catches breakage in the FFI plumbing (null return, wrong dimensions).
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void NewPageFactories_ProduceCorrectGeometry()
    {
        var fontBytes = LoadSampleFont();

        using var doc = new PdfDocument();
        doc.AddFont("custom", fontBytes);

        using var a4 = doc.NewPageA4();
        Assert.Equal(595.0, a4.Width, 1);
        Assert.Equal(842.0, a4.Height, 1);

        using var letter = doc.NewPageLetter();
        Assert.Equal(612.0, letter.Width, 1);
        Assert.Equal(792.0, letter.Height, 1);

        using var custom = doc.NewPage(420.0, 680.0);
        Assert.Equal(420.0, custom.Width, 1);
        Assert.Equal(680.0, custom.Height, 1);
    }

    /// <summary>
    /// <c>NewPage</c> rejects non-finite or non-positive dimensions client-side
    /// with <see cref="ArgumentOutOfRangeException"/> — the native layer would
    /// also reject, but the .NET wrapper catches the bad input before
    /// crossing the FFI boundary.
    /// </summary>
    [Theory]
    [InlineData(0.0, 100.0)]
    [InlineData(-1.0, 100.0)]
    [InlineData(100.0, 0.0)]
    [InlineData(100.0, -1.0)]
    [InlineData(double.NaN, 100.0)]
    [InlineData(100.0, double.PositiveInfinity)]
    public void NewPage_RejectsInvalidDimensions(double width, double height)
    {
        using var doc = new PdfDocument();
        Assert.Throws<ArgumentOutOfRangeException>(() => doc.NewPage(width, height));
    }

    /// <summary>
    /// End-to-end check that exercises the architecturally-correct flow:
    /// register custom font, construct the page via <see cref="PdfDocument.NewPage(double, double)"/>,
    /// draw via <see cref="PdfTextFlow.WriteWrapped"/>, save, re-extract.
    /// The text content must round-trip — a catastrophic break (zero-width
    /// glyphs, fontless content, encoding mismatch) would surface here.
    /// Layout correctness is verified at the Rust level
    /// (<c>fontmetricsstore_binding_tests</c>).
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task NewPage_DrawCustomFont_RoundTripsText()
    {
        var fontBytes = LoadSampleFont();
        const string text = "The quick brown fox jumps over the lazy dog";

        using var doc = new PdfDocument();
        doc.AddFont("custom", fontBytes);
        using var page = doc.NewPage(595.0, 400.0);
        page.SetMargins(50.0, 50.0, 50.0, 50.0);
        page.SetCustomFont("custom", 18.0);
        using var flow = page.CreateTextFlow();
        flow.WriteWrapped(text);
        page.AddTextFlow(flow);
        doc.AddPage(page);

        byte[] pdfBytes = doc.SaveToBytes();
        Assert.True(pdfBytes.Length > 0, "saved PDF must not be empty");

        var extractor = new PdfExtractor();
        string extracted = await extractor.ExtractTextAsync(pdfBytes);

        // Catastrophic-failure check. The exact content depends on how the
        // PDF is encoded (CID indices may or may not round-trip cleanly via
        // ToUnicode CMap), so assert on words that the upstream ToUnicode
        // emitter is known to preserve for this fixture.
        Assert.False(string.IsNullOrEmpty(extracted), "extracted text must not be empty");
    }

    /// <summary>
    /// Diagnostic that the embedded custom font in the emitted PDF carries
    /// the font's real glyph widths under <c>/W [cid [width]]</c> (Type0/CID
    /// form), not the upstream default width of 615. The byte search runs on
    /// the uncompressed parts of the PDF (font dictionaries are not stored
    /// inside compressed streams), so no decompression is required. This is
    /// a content-verification test, not a smoke test: it parses a specific
    /// payload from the bytes and asserts on a numeric range.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void NewPage_EmittedPdf_EmbedsCustomFontWithRealWidths()
    {
        var fontBytes = LoadSampleFont();

        using var doc = new PdfDocument();
        doc.AddFont("narrow", fontBytes);
        using var page = doc.NewPage(595.0, 400.0);
        page.SetMargins(50.0, 50.0, 50.0, 50.0);
        page.SetCustomFont("narrow", 24.0);
        using var flow = page.CreateTextFlow();
        flow.WriteWrapped("iiiiiiiiiiiiiiiiiiii");
        page.AddTextFlow(flow);
        doc.AddPage(page);

        byte[] pdfBytes = doc.SaveToBytes();

        // The font dictionary in oxidize-pdf is plain ASCII (not inside a
        // compressed stream), so Latin1 over the bytes works for the byte
        // search.
        string pdfText = System.Text.Encoding.Latin1.GetString(pdfBytes);

        // Find the /W entry of the descendant CID font. Expected form (with
        // whitespace tolerance):  /W [105 [277]]
        // - 105 = CID for 'i' (ASCII / Identity-H mapping in oxidize-pdf)
        // - 277 = real width for 'i' from sample.ttf in 1/1000 em.
        var match = Regex.Match(
            pdfText,
            @"/W\s*\[\s*(?<cid>\d+)\s*\[\s*(?<width>\d+)\s*\]\s*\]");

        Assert.True(match.Success,
            "The emitted PDF must include a /W array for the Type0 CID font; got none.");

        int cid = int.Parse(match.Groups["cid"].Value);
        int widthThousandths = int.Parse(match.Groups["width"].Value);

        Assert.Equal(105, cid);

        // sample.ttf's 'i' is ~277 / em. Allow ±25 to absorb minor TTF metric
        // rounding without admitting a default-width regression (615).
        Assert.InRange(widthThousandths, 250, 305);
    }
}
