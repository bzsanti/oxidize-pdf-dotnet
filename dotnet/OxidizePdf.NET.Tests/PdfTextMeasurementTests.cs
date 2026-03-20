namespace OxidizePdf.NET.Tests;

public class PdfTextMeasurementTests
{
    private static byte[] GetSampleFontBytes() =>
        File.ReadAllBytes(Path.Combine(
            AppContext.BaseDirectory, "fixtures", "fonts", "sample.ttf"));

    [Fact]
    [Trait("Category", "Integration")]
    public void MeasureText_ValidFont_ReturnsPositiveDimensions()
    {
        var fontBytes = GetSampleFontBytes();
        var result = PdfTextMeasurement.Measure(fontBytes, "Hello World", 12.0f);
        Assert.True(result.Width > 0);
        Assert.True(result.Height > 0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void MeasureText_EmptyString_ReturnsZeroWidth()
    {
        var fontBytes = GetSampleFontBytes();
        var result = PdfTextMeasurement.Measure(fontBytes, "", 12.0f);
        Assert.Equal(0.0f, result.Width);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void MeasureText_LargerFontSize_ReturnsLargerWidth()
    {
        var fontBytes = GetSampleFontBytes();
        var small = PdfTextMeasurement.Measure(fontBytes, "Test", 10.0f);
        var large = PdfTextMeasurement.Measure(fontBytes, "Test", 24.0f);
        Assert.True(large.Width > small.Width);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void MeasureText_NullFontBytes_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PdfTextMeasurement.Measure(null!, "Hello", 12.0f));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void MeasureText_EmptyFontBytes_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            PdfTextMeasurement.Measure([], "Hello", 12.0f));
    }
}
