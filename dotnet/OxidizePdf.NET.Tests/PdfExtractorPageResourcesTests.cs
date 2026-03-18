using OxidizePdf.NET.Models;
using OxidizePdf.NET.Tests.TestHelpers;

namespace OxidizePdf.NET.Tests;

/// <summary>
/// Tests for page resources and content stream extraction (Phase 7 — PARSE-017).
/// </summary>
public class PdfExtractorPageResourcesTests
{
    // ── Model tests ──────────────────────────────────────────────────────────

    [Fact]
    public void PageResources_HasExpectedProperties()
    {
        var resources = new PageResources();

        Assert.NotNull(resources.FontNames);
        Assert.Empty(resources.FontNames);
        Assert.False(resources.HasXObjects);
        Assert.NotNull(resources.ResourceKeys);
        Assert.Empty(resources.ResourceKeys);
    }

    // ── GetPageResourcesAsync tests ──────────────────────────────────────────

    [Fact]
    public async Task GetPageResourcesAsync_NullBytes_ThrowsArgumentNullException()
    {
        var extractor = new PdfExtractor();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => extractor.GetPageResourcesAsync(null!, 1));
    }

    [Fact]
    public async Task GetPageResourcesAsync_EmptyBytes_ThrowsArgumentException()
    {
        var extractor = new PdfExtractor();
        await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.GetPageResourcesAsync(Array.Empty<byte>(), 1));
    }

    [Fact]
    public async Task GetPageResourcesAsync_InvalidPage_ThrowsArgumentOutOfRangeException()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => extractor.GetPageResourcesAsync(pdf, 0));
    }

    [Fact]
    public async Task GetPageResourcesAsync_Page1_ReturnsResources()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var resources = await extractor.GetPageResourcesAsync(pdf, 1);

        Assert.NotNull(resources);
        Assert.NotNull(resources.ResourceKeys);
        // A real PDF page should have at least some resources (Font is typical)
        Assert.NotEmpty(resources.ResourceKeys);
    }

    [Fact]
    public async Task GetPageResourcesAsync_Page1_HasFonts()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var resources = await extractor.GetPageResourcesAsync(pdf, 1);

        // A text-bearing PDF should have at least one font
        Assert.NotEmpty(resources.FontNames);
    }

    [Fact]
    public async Task GetPageResourcesAsync_PageOutOfRange_ThrowsPdfExtractionException()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        await Assert.ThrowsAsync<PdfExtractionException>(
            () => extractor.GetPageResourcesAsync(pdf, 9999));
    }

    // ── GetPageContentStreamAsync tests ──────────────────────────────────────

    [Fact]
    public async Task GetPageContentStreamAsync_NullBytes_ThrowsArgumentNullException()
    {
        var extractor = new PdfExtractor();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => extractor.GetPageContentStreamAsync(null!, 1));
    }

    [Fact]
    public async Task GetPageContentStreamAsync_EmptyBytes_ThrowsArgumentException()
    {
        var extractor = new PdfExtractor();
        await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.GetPageContentStreamAsync(Array.Empty<byte>(), 1));
    }

    [Fact]
    public async Task GetPageContentStreamAsync_InvalidPage_ThrowsArgumentOutOfRangeException()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => extractor.GetPageContentStreamAsync(pdf, 0));
    }

    [Fact]
    public async Task GetPageContentStreamAsync_Page1_ReturnsNonEmptyStreams()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var streams = await extractor.GetPageContentStreamAsync(pdf, 1);

        Assert.NotNull(streams);
        Assert.False(streams.IsEmpty);
        Assert.True(streams.Count > 0);
        // Each stream should have content
        Assert.All(streams.Streams, s => Assert.True(s.Length > 0, "Content stream should not be empty"));
    }

    [Fact]
    public async Task GetPageContentStreamAsync_PageOutOfRange_ThrowsPdfExtractionException()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        await Assert.ThrowsAsync<PdfExtractionException>(
            () => extractor.GetPageContentStreamAsync(pdf, 9999));
    }
}
