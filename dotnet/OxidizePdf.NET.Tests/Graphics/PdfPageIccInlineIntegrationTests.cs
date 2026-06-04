using FluentAssertions;
using OxidizePdf.NET.Graphics;
using OxidizePdf.NET.Tests.TestHelpers;
using Xunit;

namespace OxidizePdf.NET.Tests.Graphics;

/// <summary>
/// Tests for the inline ICCBased path: AddColorSpace(name, PageColorSpace.IccBased(n, alternate))
/// followed by SetFillColorIcc / SetStrokeColorIcc.
/// Mirrors python's PageColorSpace.icc_based(n, alternate) pattern.
/// </summary>
public class PdfPageIccInlineIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void AddColorSpace_IccBased_ThenSetFillColorIcc_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        page.AddColorSpace("ICCRGB1", PageColorSpace.IccBased(3, "DeviceRGB"))
            .SetFillColorIcc("ICCRGB1", new double[] { 0.5, 0.3, 0.8 })
            .Should().BeSameAs(page);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetFillColorIcc_EmptyComponents_Throws()
    {
        using var page = PdfPage.A4();
        page.AddColorSpace("ICCRGB1", PageColorSpace.IccBased(3, "DeviceRGB"));
        page.Invoking(p => p.SetFillColorIcc("ICCRGB1", Array.Empty<double>()))
            .Should().Throw<ArgumentException>().WithMessage("*empty*");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void IccInline_FilledRect_PdfContainsICCBasedResource()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.AddColorSpace("ICCRGB1", PageColorSpace.IccBased(3, "DeviceRGB"))
            .SetFillColorIcc("ICCRGB1", new double[] { 0.5, 0.3, 0.8 })
            .DrawRect(50, 50, 200, 200)
            .Fill();
        doc.AddPage(page);
        var pdfText = ContentStreamHelper.ToLatin1(doc.SaveToBytes());
        pdfText.Should().Contain("ICCBased",
            "the PDF resource dictionary must register an ICCBased color space");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void IccInline_ContentStream_ContainsCsAndScOperators()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.AddColorSpace("ICCRGB1", PageColorSpace.IccBased(3, "DeviceRGB"))
            .SetFillColorIcc("ICCRGB1", new double[] { 0.5, 0.3, 0.8 })
            .DrawRect(50, 50, 200, 200)
            .Fill();
        doc.AddPage(page);
        var stream = ContentStreamHelper.DecompressFirstContentStream(doc.SaveToBytes());
        stream.Should().NotBeNull();
        stream!.Should().Contain("cs");
        stream.Should().Contain("sc");
    }
}
