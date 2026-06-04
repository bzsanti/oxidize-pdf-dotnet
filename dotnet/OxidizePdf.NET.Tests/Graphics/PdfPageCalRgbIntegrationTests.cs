using FluentAssertions;
using OxidizePdf.NET.Graphics;
using OxidizePdf.NET.Tests.TestHelpers;
using Xunit;

namespace OxidizePdf.NET.Tests.Graphics;

public class PdfPageCalRgbIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void SetFillColorCalRgb_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        page.SetFillColorCalRgb(0.5, 0.3, 0.8, CalRgbColorSpace.SRgb()).Should().BeSameAs(page);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetStrokeColorCalRgb_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        page.SetStrokeColorCalRgb(0.5, 0.3, 0.8, CalRgbColorSpace.SRgb()).Should().BeSameAs(page);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetFillColorCalRgb_NullColorSpace_Throws()
    {
        using var page = PdfPage.A4();
        page.Invoking(p => p.SetFillColorCalRgb(0.5, 0.3, 0.8, null!)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetFillColorCalRgb_InvalidColorSpace_Throws()
    {
        using var page = PdfPage.A4();
        // WhitePoint[1] != 1.0 is invalid per PDF spec
        var badCs = new CalRgbColorSpace { WhitePoint = new double[] { 0.9505, 0.5, 1.0890 } };
        page.Invoking(p => p.SetFillColorCalRgb(0.5, 0.3, 0.8, badCs)).Should().Throw<ArgumentException>();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void CalRgb_FilledRect_ContentStreamContainsCalRGBColorSpaceName()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.SetFillColorCalRgb(0.5, 0.3, 0.8, CalRgbColorSpace.SRgb())
            .DrawRect(50, 50, 200, 200)
            .Fill();
        doc.AddPage(page);
        var pdfBytes = doc.SaveToBytes();
        // The hardcoded-name variant embeds the CalRGB color space inline in the content
        // stream (not as a named /Resources/ColorSpace entry). The color space name appears
        // inside the compressed stream, so we assert on the decompressed content.
        var stream = ContentStreamHelper.DecompressFirstContentStream(pdfBytes);
        stream.Should().NotBeNull("a content stream must be present");
        stream!.Should().Contain("CalRGB",
            "the decompressed content stream must reference the CalRGB color space");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void CalRgb_FilledRect_ContentStreamContainsCsAndScOperators()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.SetFillColorCalRgb(0.5, 0.3, 0.8, CalRgbColorSpace.SRgb())
            .DrawRect(50, 50, 200, 200)
            .Fill();
        doc.AddPage(page);
        var stream = ContentStreamHelper.DecompressFirstContentStream(doc.SaveToBytes());
        stream.Should().NotBeNull("a content stream must be present");
        stream!.Should().Contain("cs", "content stream must contain the 'cs' set-colorspace operator");
        stream.Should().Contain("sc", "content stream must contain the 'sc' set-color-components operator");
    }
}
