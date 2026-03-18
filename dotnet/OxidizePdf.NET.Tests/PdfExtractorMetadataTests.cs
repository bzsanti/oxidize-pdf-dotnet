using OxidizePdf.NET.Models;
using OxidizePdf.NET.Tests.TestHelpers;

namespace OxidizePdf.NET.Tests;

/// <summary>
/// Tests for PDF metadata extraction from existing PDFs (Phase 2 — PARSE-011).
/// </summary>
public class PdfExtractorMetadataTests
{
    [Fact]
    public async Task ExtractMetadataAsync_ReturnsModel_WithExpectedProperties()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetValidSinglePagePdf();

        // Act
        var metadata = await extractor.ExtractMetadataAsync(pdf);

        // Assert — return type is PdfMetadata with all expected properties
        Assert.NotNull(metadata);
        Assert.NotNull(metadata.Version);
        Assert.NotEmpty(metadata.Version);
    }

    [Fact]
    public async Task ExtractMetadataAsync_Version_MatchesGetPdfVersion()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetValidSinglePagePdf();

        // Act
        var metadata = await extractor.ExtractMetadataAsync(pdf);
        var version = await extractor.GetPdfVersionAsync(pdf);

        // Assert — metadata version should match the standalone version API
        Assert.Equal(version, metadata.Version);
    }

    [Fact]
    public async Task ExtractMetadataAsync_PageCount_IsPopulated()
    {
        // Arrange — use sample.pdf (real PDF with correct structure)
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        // Act
        var metadata = await extractor.ExtractMetadataAsync(pdf);
        var pageCount = await extractor.GetPageCountAsync(pdf);

        // Assert — page count from metadata matches standalone API
        Assert.NotNull(metadata.PageCount);
        Assert.Equal(pageCount, metadata.PageCount);
    }

    [Fact]
    public async Task ExtractMetadataAsync_PdfWithoutInfoDict_ReturnsNullOptionalFields()
    {
        // Arrange — minimal PDF has no /Info dictionary
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetValidSinglePagePdf();

        // Act
        var metadata = await extractor.ExtractMetadataAsync(pdf);

        // Assert — optional fields should be null when no Info dict exists
        Assert.Null(metadata.Title);
        Assert.Null(metadata.Author);
        Assert.Null(metadata.Subject);
        Assert.Null(metadata.Keywords);
        Assert.Null(metadata.Creator);
        Assert.Null(metadata.Producer);
        Assert.Null(metadata.CreationDate);
        Assert.Null(metadata.ModificationDate);
    }

    [Fact]
    public async Task ExtractMetadataAsync_VersionIsAlwaysPopulated()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetValidSinglePagePdf();

        // Act
        var metadata = await extractor.ExtractMetadataAsync(pdf);

        // Assert — every valid PDF has a version
        Assert.NotNull(metadata.Version);
        Assert.Matches(@"^\d+\.\d+$", metadata.Version);
    }

    [Fact]
    public async Task ExtractMetadataAsync_NullBytes_ThrowsArgumentNullException()
    {
        var extractor = new PdfExtractor();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => extractor.ExtractMetadataAsync(null!));
    }

    [Fact]
    public async Task ExtractMetadataAsync_EmptyBytes_ThrowsArgumentException()
    {
        var extractor = new PdfExtractor();
        await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.ExtractMetadataAsync(Array.Empty<byte>()));
    }
}
