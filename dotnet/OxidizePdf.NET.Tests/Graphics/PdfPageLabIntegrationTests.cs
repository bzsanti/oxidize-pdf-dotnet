using FluentAssertions;
using OxidizePdf.NET.Graphics;
using OxidizePdf.NET.Tests.TestHelpers;
using Xunit;

namespace OxidizePdf.NET.Tests.Graphics;

public class PdfPageLabIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void SetFillColorLab_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        page.SetFillColorLab(new LabColor(50.0, 0.0, 0.0, LabColorSpace.D50())).Should().BeSameAs(page);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetStrokeColorLab_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        page.SetStrokeColorLab(new LabColor(50.0, 0.0, 0.0, LabColorSpace.D50())).Should().BeSameAs(page);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetFillColorLab_NullColor_Throws()
    {
        using var page = PdfPage.A4();
        page.Invoking(p => p.SetFillColorLab(null!)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetFillColorLab_InvalidColorSpace_Throws()
    {
        using var page = PdfPage.A4();
        // WhitePoint[1] != 1.0 is invalid per PDF spec
        var badCs = new LabColorSpace { WhitePoint = new double[] { 0.9642, 0.5, 0.8251 } };
        var color = new LabColor(50.0, 0.0, 0.0, badCs);
        page.Invoking(p => p.SetFillColorLab(color)).Should().Throw<ArgumentException>();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Lab_FilledRect_ContentStreamContainsLabColorSpaceName()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.SetFillColorLab(new LabColor(50.0, 0.0, 0.0, LabColorSpace.D50()))
            .DrawRect(50, 50, 200, 200)
            .Fill();
        doc.AddPage(page);
        var pdfBytes = doc.SaveToBytes();
        // The hardcoded-name variant embeds the Lab color space inline in the content
        // stream (not as a named /Resources/ColorSpace entry). The color space name appears
        // inside the compressed stream, so we assert on the decompressed content.
        var stream = ContentStreamHelper.DecompressFirstContentStream(pdfBytes);
        stream.Should().NotBeNull("a content stream must be present");
        stream!.Should().Contain("Lab",
            "the decompressed content stream must reference the Lab color space");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Lab_FilledRect_ContentStreamContainsCsAndScOperators()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.SetFillColorLab(new LabColor(50.0, 0.0, 0.0, LabColorSpace.D50()))
            .DrawRect(50, 50, 200, 200)
            .Fill();
        doc.AddPage(page);
        var stream = ContentStreamHelper.DecompressFirstContentStream(doc.SaveToBytes());
        stream.Should().NotBeNull("a content stream must be present");
        stream!.Should().Contain("cs", "content stream must contain the 'cs' set-colorspace operator");
        stream.Should().Contain("sc", "content stream must contain the 'sc' set-color-components operator");
    }
}
