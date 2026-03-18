using OxidizePdf.NET.Models;
using OxidizePdf.NET.Tests.TestHelpers;

namespace OxidizePdf.NET.Tests;

/// <summary>
/// Tests for text extraction with ExtractionOptions (Phase 3 — PARSE-014).
/// </summary>
public class PdfExtractorExtractionOptionsTests
{
    [Fact]
    public async Task ExtractTextAsync_WithOptions_ReturnsNonEmptyText()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();
        var options = new ExtractionOptions();

        var text = await extractor.ExtractTextAsync(pdf, options);

        Assert.NotNull(text);
        Assert.NotEmpty(text);
    }

    [Fact]
    public async Task ExtractTextAsync_WithDefaultOptions_MatchesNoOptionsOverload()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();
        var options = new ExtractionOptions();

        var textWithOptions = await extractor.ExtractTextAsync(pdf, options);
        var textWithout = await extractor.ExtractTextAsync(pdf);

        Assert.Equal(textWithout, textWithOptions);
    }

    [Fact]
    public async Task ExtractTextAsync_WithPreserveLayout_ReturnsText()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();
        var options = new ExtractionOptions { PreserveLayout = true };

        var text = await extractor.ExtractTextAsync(pdf, options);

        Assert.NotNull(text);
        Assert.NotEmpty(text);
    }

    [Fact]
    public async Task ExtractTextAsync_WithMergeHyphenatedFalse_ReturnsText()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();
        var options = new ExtractionOptions { MergeHyphenated = false };

        var text = await extractor.ExtractTextAsync(pdf, options);

        Assert.NotNull(text);
        Assert.NotEmpty(text);
    }

    [Fact]
    public async Task ExtractTextAsync_WithDetectColumns_ReturnsText()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();
        var options = new ExtractionOptions
        {
            DetectColumns = true,
            ColumnThreshold = 30.0
        };

        var text = await extractor.ExtractTextAsync(pdf, options);

        Assert.NotNull(text);
        Assert.NotEmpty(text);
    }

    [Fact]
    public async Task ExtractTextAsync_WithNullOptions_UsesDefaults()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var textWithNull = await extractor.ExtractTextAsync(pdf, (ExtractionOptions)null!);
        var textDefault = await extractor.ExtractTextAsync(pdf);

        Assert.NotEmpty(textWithNull);
        Assert.Equal(textDefault, textWithNull);
    }

    [Fact]
    public void ExtractionOptions_DefaultValues_MatchCoreDefaults()
    {
        var options = new ExtractionOptions();

        Assert.False(options.PreserveLayout);
        Assert.Equal(0.3, options.SpaceThreshold);
        Assert.Equal(10.0, options.NewlineThreshold);
        Assert.True(options.SortByPosition);
        Assert.False(options.DetectColumns);
        Assert.Equal(50.0, options.ColumnThreshold);
        Assert.True(options.MergeHyphenated);
    }

    // ── Validation tests ─────────────────────────────────────────────────────

    [Fact]
    public void ExtractionOptions_Validate_DoesNotThrowForDefaults()
    {
        var options = new ExtractionOptions();
        var ex = Record.Exception(() => options.Validate());
        Assert.Null(ex);
    }

    [Fact]
    public void ExtractionOptions_Validate_ThrowsWhenSpaceThresholdIsNegative()
    {
        var options = new ExtractionOptions { SpaceThreshold = -0.1 };
        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void ExtractionOptions_Validate_ThrowsWhenNewlineThresholdIsNegative()
    {
        var options = new ExtractionOptions { NewlineThreshold = -1.0 };
        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void ExtractionOptions_Validate_ThrowsWhenColumnThresholdIsNegative()
    {
        var options = new ExtractionOptions { ColumnThreshold = -5.0 };
        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public async Task ExtractTextAsync_WithInvalidOptions_ThrowsArgumentException()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.ExtractTextAsync(pdf, new ExtractionOptions { SpaceThreshold = -1 }));
    }
}
