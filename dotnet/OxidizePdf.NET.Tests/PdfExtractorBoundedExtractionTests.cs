using System.Text;
using OxidizePdf.NET.Models;
using OxidizePdf.NET.Tests.TestHelpers;

namespace OxidizePdf.NET.Tests;

/// <summary>
/// Tests for the extraction options added when bumping the core to 4.0.0:
/// <c>ReorderColumns</c> (oxidize-pdf 3.1.0, #389) and the bounded-extraction
/// pair <c>MaxExtractedBytes</c> / <see cref="TextExtractionResult.Truncated"/>
/// (oxidize-pdf 4.0.0, #382).
///
/// The byte-budget tests assert real truncation behaviour (text is actually cut
/// and the flag flips). The reorder test verifies the option is honoured across
/// the FFI boundary without corrupting text; column-ordering semantics are
/// owned and tested upstream (see error-log #14 — FFI tests verify plumbing).
/// </summary>
public class PdfExtractorBoundedExtractionTests
{
    private static int Utf8Bytes(string s) => Encoding.UTF8.GetByteCount(s);

    [Fact]
    public async Task ExtractTextWithResultAsync_DefaultOptions_NotTruncatedAndMatchesPlainOverload()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var plain = await extractor.ExtractTextAsync(pdf);
        var result = await extractor.ExtractTextWithResultAsync(pdf, new ExtractionOptions());

        Assert.False(result.Truncated);
        Assert.Equal(plain, result.Text);
    }

    [Fact]
    public async Task ExtractTextWithResultAsync_ZeroBudget_MeansUnlimited()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var full = await extractor.ExtractTextAsync(pdf);
        var result = await extractor.ExtractTextWithResultAsync(
            pdf, new ExtractionOptions { MaxExtractedBytes = 0 });

        Assert.False(result.Truncated);
        Assert.Equal(full, result.Text);
    }

    [Fact]
    public async Task ExtractTextWithResultAsync_TinyBudget_TruncatesAndReportsFlag()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var full = await extractor.ExtractTextAsync(pdf);
        Assert.True(Utf8Bytes(full) > 5, "fixture must have enough text to truncate");

        // 5 bytes forces truncation of any page carrying real text.
        var result = await extractor.ExtractTextWithResultAsync(
            pdf, new ExtractionOptions { MaxExtractedBytes = 5 });

        Assert.True(result.Truncated);
        Assert.NotEmpty(result.Text);
        // Text is strictly shorter, and the first page's kept prefix is a
        // genuine prefix of the full extraction (never garbage or mid-char).
        Assert.True(result.Text.Length < full.Length);
        var firstSegment = result.Text.Split(new[] { "\n\n" }, StringSplitOptions.None)[0];
        Assert.StartsWith(firstSegment, full);
        // Per-page byte budget is honoured: each page's kept text is <= 5 bytes.
        foreach (var segment in result.Text.Split(new[] { "\n\n" }, StringSplitOptions.None))
            Assert.True(Utf8Bytes(segment) <= 5, $"page segment exceeded budget: {Utf8Bytes(segment)} bytes");
    }

    [Fact]
    public async Task ExtractTextWithResultAsync_BudgetAboveTotal_NotTruncated()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var full = await extractor.ExtractTextAsync(pdf);
        var budget = Utf8Bytes(full) + 1000;

        var result = await extractor.ExtractTextWithResultAsync(
            pdf, new ExtractionOptions { MaxExtractedBytes = budget });

        Assert.False(result.Truncated);
        Assert.Equal(full, result.Text);
    }

    [Fact]
    public void ExtractionOptions_NegativeMaxExtractedBytes_ThrowsOnValidate()
    {
        var options = new ExtractionOptions { MaxExtractedBytes = -1 };
        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public async Task ExtractTextAsync_ReorderColumns_HonouredAndPreservesGlyphs()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var baseline = await extractor.ExtractTextAsync(pdf, new ExtractionOptions());
        var reordered = await extractor.ExtractTextAsync(
            pdf, new ExtractionOptions { ReorderColumns = true });

        Assert.NotEmpty(reordered);
        // The option must reach the core and take effect: on this multi-column
        // fixture it separates per-column tokens that the flat path glued
        // together, so the output differs from the default (#389).
        Assert.NotEqual(baseline, reordered);
        // But reordering only rearranges and re-spaces text — it must not add or
        // drop any actual glyph. The multiset of non-whitespace characters is
        // therefore invariant.
        static char[] NonWhitespaceSorted(string s) =>
            s.Where(c => !char.IsWhiteSpace(c)).OrderBy(c => c).ToArray();
        Assert.Equal(NonWhitespaceSorted(baseline), NonWhitespaceSorted(reordered));
    }
}
