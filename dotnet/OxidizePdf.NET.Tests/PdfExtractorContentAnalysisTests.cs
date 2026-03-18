using OxidizePdf.NET.Models;
using OxidizePdf.NET.Tests.TestHelpers;

namespace OxidizePdf.NET.Tests;

/// <summary>
/// Tests for page content analysis (Phase 8 — PARSE-015).
/// </summary>
public class PdfExtractorContentAnalysisTests
{
    // ── Model + enum tests ───────────────────────────────────────────────────

    [Fact]
    public void ContentPageType_EnumHasExpectedValues()
    {
        Assert.True(Enum.IsDefined(typeof(ContentPageType), ContentPageType.Unknown));
        Assert.True(Enum.IsDefined(typeof(ContentPageType), ContentPageType.Text));
        Assert.True(Enum.IsDefined(typeof(ContentPageType), ContentPageType.Scanned));
        Assert.True(Enum.IsDefined(typeof(ContentPageType), ContentPageType.Mixed));
    }

    [Fact]
    public void ContentAnalysis_HasExpectedDefaults()
    {
        var analysis = new ContentAnalysis();

        Assert.Equal(ContentPageType.Unknown, analysis.PageType);
        Assert.Equal(0, analysis.CharacterCount);
        Assert.False(analysis.HasContentStream);
        Assert.Equal(0, analysis.ImageCount);
        Assert.False(analysis.IsScanned);
        Assert.False(analysis.IsText);
        Assert.False(analysis.IsMixed);
    }

    [Fact]
    public void ContentAnalysis_IsScanned_TrueWhenEnumIsScanned()
    {
        var analysis = new ContentAnalysis { PageType = ContentPageType.Scanned };
        Assert.True(analysis.IsScanned);
        Assert.False(analysis.IsText);
        Assert.False(analysis.IsMixed);
    }

    [Fact]
    public void ContentAnalysis_IsText_TrueWhenEnumIsText()
    {
        var analysis = new ContentAnalysis { PageType = ContentPageType.Text };
        Assert.True(analysis.IsText);
        Assert.False(analysis.IsScanned);
        Assert.False(analysis.IsMixed);
    }

    [Fact]
    public void ContentAnalysis_IsMixed_TrueWhenEnumIsMixed()
    {
        var analysis = new ContentAnalysis { PageType = ContentPageType.Mixed };
        Assert.True(analysis.IsMixed);
        Assert.False(analysis.IsScanned);
        Assert.False(analysis.IsText);
    }

    // ── Null/empty validation ────────────────────────────────────────────────

    [Fact]
    public async Task AnalyzePageContentAsync_NullBytes_ThrowsArgumentNullException()
    {
        var extractor = new PdfExtractor();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => extractor.AnalyzePageContentAsync(null!, 1));
    }

    [Fact]
    public async Task AnalyzePageContentAsync_EmptyBytes_ThrowsArgumentException()
    {
        var extractor = new PdfExtractor();
        await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.AnalyzePageContentAsync(Array.Empty<byte>(), 1));
    }

    [Fact]
    public async Task AnalyzePageContentAsync_InvalidPage_ThrowsArgumentOutOfRangeException()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => extractor.AnalyzePageContentAsync(pdf, 0));
    }

    // ── Functional tests ─────────────────────────────────────────────────────

    [Fact]
    public async Task AnalyzePageContentAsync_TextPage_ReturnsTextType()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var analysis = await extractor.AnalyzePageContentAsync(pdf, 1);

        Assert.NotNull(analysis);
        Assert.True(analysis.CharacterCount > 0, "Text PDF should have characters");
        Assert.True(analysis.IsText || analysis.IsMixed,
            $"Text PDF page should be Text or Mixed, got {analysis.PageType}");
    }

    [Fact]
    public async Task AnalyzePageContentAsync_TextPage_HasContentStream()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var analysis = await extractor.AnalyzePageContentAsync(pdf, 1);

        Assert.True(analysis.HasContentStream, "Page should have a content stream");
    }

    [Fact]
    public async Task AnalyzePageContentAsync_TextPage_IsNotScanned()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var analysis = await extractor.AnalyzePageContentAsync(pdf, 1);

        Assert.False(analysis.IsScanned, "Text-based PDF should not be classified as scanned");
    }

    [Fact]
    public async Task AnalyzePageContentAsync_TextPage_PageTypeNeverUnknown()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var analysis = await extractor.AnalyzePageContentAsync(pdf, 1);

        Assert.NotEqual(ContentPageType.Unknown, analysis.PageType);
    }

    [Fact]
    public async Task AnalyzePageContentAsync_PageOutOfRange_ThrowsPdfExtractionException()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        await Assert.ThrowsAsync<PdfExtractionException>(
            () => extractor.AnalyzePageContentAsync(pdf, 9999));
    }

    [Fact]
    public async Task AnalyzePageContentAsync_PageTypeIsKnownEnumValue()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var analysis = await extractor.AnalyzePageContentAsync(pdf, 1);

        Assert.True(Enum.IsDefined(typeof(ContentPageType), analysis.PageType));
    }
}
