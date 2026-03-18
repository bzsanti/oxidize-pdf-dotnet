using OxidizePdf.NET.Models;
using OxidizePdf.NET.Tests.TestHelpers;

namespace OxidizePdf.NET.Tests;

/// <summary>
/// Tests for PDF annotation extraction (Phase 6 — PARSE-016).
/// </summary>
public class PdfExtractorAnnotationsTests
{
    // ── Model tests ──────────────────────────────────────────────────────────

    [Fact]
    public void PdfAnnotation_HasExpectedProperties()
    {
        var annotation = new PdfAnnotation();

        // Assert default values and correct property types
        Assert.Equal(string.Empty, annotation.Subtype);
        Assert.Null(annotation.Contents);
        Assert.Null(annotation.Title);
        Assert.Equal(0, annotation.PageNumber);
        Assert.Null(annotation.Rect);
    }

    // ── Null/empty validation ────────────────────────────────────────────────

    [Fact]
    public async Task GetAnnotationsAsync_NullBytes_ThrowsArgumentNullException()
    {
        var extractor = new PdfExtractor();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => extractor.GetAnnotationsAsync(null!));
    }

    [Fact]
    public async Task GetAnnotationsAsync_EmptyBytes_ThrowsArgumentException()
    {
        var extractor = new PdfExtractor();
        await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.GetAnnotationsAsync(Array.Empty<byte>()));
    }

    // ── Functional tests ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetAnnotationsAsync_OnSamplePdf_ReturnsValidList()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var annotations = await extractor.GetAnnotationsAsync(pdf);

        Assert.NotNull(annotations);
        // Sample PDF may or may not have annotations — the important thing is no crash
    }

    [Fact]
    public async Task GetAnnotationsAsync_WhenAnnotationsPresent_SubtypeIsNotEmpty()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var annotations = await extractor.GetAnnotationsAsync(pdf);

        // Only assert if there are annotations in the sample PDF
        if (annotations.Count > 0)
        {
            Assert.All(annotations, a => Assert.NotEmpty(a.Subtype));
        }
    }

    [Fact]
    public async Task GetAnnotationsAsync_WhenAnnotationsPresent_PageNumberIsOneBased()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var annotations = await extractor.GetAnnotationsAsync(pdf);

        Assert.All(annotations, a =>
            Assert.True(a.PageNumber >= 1, $"Page number should be >= 1, got {a.PageNumber}"));
    }

    [Fact]
    public async Task GetAnnotationsAsync_WhenAnnotationsPresent_RectIsNullOrFourElements()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var annotations = await extractor.GetAnnotationsAsync(pdf);

        Assert.All(annotations.Where(a => a.Rect != null), a =>
            Assert.Equal(4, a.Rect!.Length));
    }
}
