using System.Text.Json;
using OxidizePdf.NET.Tests.TestHelpers;

namespace OxidizePdf.NET.Tests;

/// <summary>
/// Tests for structured export (Phase 4 — PARSE-013).
/// Covers ToMarkdownAsync, ToContextualAsync, ToJsonAsync.
/// </summary>
public class PdfExtractorStructuredExportTests
{
    [Fact]
    public async Task ToMarkdownAsync_ReturnsNonEmptyString()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var markdown = await extractor.ToMarkdownAsync(pdf);

        Assert.NotNull(markdown);
        Assert.NotEmpty(markdown);
    }

    [Fact]
    public async Task ToContextualAsync_ReturnsNonEmptyString()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var contextual = await extractor.ToContextualAsync(pdf);

        Assert.NotNull(contextual);
        Assert.NotEmpty(contextual);
    }

    [Fact]
    public async Task ToJsonAsync_ReturnsValidJson()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var json = await extractor.ToJsonAsync(pdf);

        Assert.NotNull(json);
        Assert.NotEmpty(json);
        // Must parse as valid JSON
        var doc = JsonDocument.Parse(json);
        Assert.NotNull(doc);
    }

    [Fact]
    public async Task ToMarkdownAsync_NullBytes_ThrowsArgumentNullException()
    {
        var extractor = new PdfExtractor();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => extractor.ToMarkdownAsync(null!));
    }

    [Fact]
    public async Task ToContextualAsync_EmptyBytes_ThrowsArgumentException()
    {
        var extractor = new PdfExtractor();
        await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.ToContextualAsync(Array.Empty<byte>()));
    }

    [Fact]
    public async Task ToJsonAsync_EmptyBytes_ThrowsArgumentException()
    {
        var extractor = new PdfExtractor();
        await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.ToJsonAsync(Array.Empty<byte>()));
    }

    [Fact]
    public async Task AllExportFormats_ReturnContent_ForSamePdf()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var markdown = await extractor.ToMarkdownAsync(pdf);
        var contextual = await extractor.ToContextualAsync(pdf);
        var json = await extractor.ToJsonAsync(pdf);

        // All formats should return content for the same PDF
        Assert.NotEmpty(markdown);
        Assert.NotEmpty(contextual);
        Assert.NotEmpty(json);
    }
}
