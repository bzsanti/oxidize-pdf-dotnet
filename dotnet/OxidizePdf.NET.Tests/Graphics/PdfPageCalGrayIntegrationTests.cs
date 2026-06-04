using FluentAssertions;
using OxidizePdf.NET.Graphics;
using OxidizePdf.NET.Tests.TestHelpers;
using Xunit;

namespace OxidizePdf.NET.Tests.Graphics;

public class PdfPageCalGrayIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void SetFillColorCalGray_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        page.SetFillColorCalGray(0.5, CalGrayColorSpace.D65()).Should().BeSameAs(page);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetStrokeColorCalGray_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        page.SetStrokeColorCalGray(0.5, CalGrayColorSpace.D65()).Should().BeSameAs(page);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetFillColorCalGray_NullColorSpace_Throws()
    {
        using var page = PdfPage.A4();
        page.Invoking(p => p.SetFillColorCalGray(0.5, null!)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetFillColorCalGray_InvalidColorSpace_Throws()
    {
        using var page = PdfPage.A4();
        var badCs = new CalGrayColorSpace { WhitePoint = new double[] { 0.9505, 0.5, 1.089 } };
        page.Invoking(p => p.SetFillColorCalGray(0.5, badCs)).Should().Throw<ArgumentException>();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void CalGray_FilledRect_ContentStreamContainsCalGrayColorSpaceName()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.SetFillColorCalGray(0.5, CalGrayColorSpace.D65())
            .DrawRect(50, 50, 200, 200)
            .Fill();
        doc.AddPage(page);
        var pdfBytes = doc.SaveToBytes();
        // The hardcoded-name variant embeds the CalGray color space inline in the content
        // stream (not as a named /Resources/ColorSpace entry). The color space name appears
        // inside the compressed stream, so we assert on the decompressed content.
        var stream = ContentStreamHelper.DecompressFirstContentStream(pdfBytes);
        stream.Should().NotBeNull("a content stream must be present");
        stream!.Should().Contain("CalGray",
            "the decompressed content stream must reference the CalGray color space");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void CalGray_FilledRect_ContentStreamContainsCsAndScOperators()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.SetFillColorCalGray(0.7, CalGrayColorSpace.D65())
            .DrawRect(50, 50, 200, 200)
            .Fill();
        doc.AddPage(page);
        var stream = ContentStreamHelper.DecompressFirstContentStream(doc.SaveToBytes());
        stream.Should().NotBeNull("a content stream must be present");
        stream!.Should().Contain("cs", "content stream must contain the 'cs' set-colorspace operator");
        stream.Should().Contain("sc", "content stream must contain the 'sc' set-color-components operator");
    }
}
