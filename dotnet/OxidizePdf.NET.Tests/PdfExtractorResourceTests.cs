using OxidizePdf.NET.Tests.TestHelpers;

namespace OxidizePdf.NET.Tests;

/// <summary>
/// Tests for PdfExtractor resource management and state behavior.
/// </summary>
public class PdfExtractorResourceTests
{
    [Fact]
    public async Task MultipleExtractions_DoNotShareState()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var pdf1 = PdfTestFixtures.GetSamplePdf();
        var pdf2 = PdfTestFixtures.GetSamplePdf();

        // Act - extract from same PDF twice
        var text1 = await extractor.ExtractTextAsync(pdf1);
        var text2 = await extractor.ExtractTextAsync(pdf2);

        // Assert - both extractions should succeed independently
        Assert.NotNull(text1);
        Assert.NotNull(text2);
        Assert.Equal(text1, text2); // Same PDF should produce same text
    }

    [Fact]
    public void PdfExtractor_DoesNotImplementIDisposable()
    {
        // Arrange & Act
        var extractor = new PdfExtractor();

        // Assert - PdfExtractor should NOT implement IDisposable
        // since it doesn't hold any native resources between calls
        Assert.False(extractor is IDisposable,
            "PdfExtractor should not implement IDisposable since it doesn't retain native resources");
    }

    [Fact]
    public async Task PdfExtractor_CanBeReusedMultipleTimes()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        // Act - reuse extractor multiple times
        for (int i = 0; i < 5; i++)
        {
            var text = await extractor.ExtractTextAsync(pdf);
            Assert.NotNull(text);
            Assert.NotEmpty(text);
        }
    }

    [Fact]
    public async Task MultipleExtractors_WorkIndependently()
    {
        // Arrange
        var extractor1 = new PdfExtractor();
        var extractor2 = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        // Act
        var text1 = await extractor1.ExtractTextAsync(pdf);
        var text2 = await extractor2.ExtractTextAsync(pdf);

        // Assert - both extractors work independently
        Assert.Equal(text1, text2);
    }
}
