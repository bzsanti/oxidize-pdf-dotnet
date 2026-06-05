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
        Assert.Same(page, page.AddColorSpace("ICCRGB1", PageColorSpace.IccBased(3, "DeviceRGB"))
            .SetFillColorIcc("ICCRGB1", new double[] { 0.5, 0.3, 0.8 }));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetFillColorIcc_EmptyComponents_Throws()
    {
        using var page = PdfPage.A4();
        page.AddColorSpace("ICCRGB1", PageColorSpace.IccBased(3, "DeviceRGB"));
        var ex = Assert.Throws<ArgumentException>(() => page.SetFillColorIcc("ICCRGB1", Array.Empty<double>()));
        Assert.Contains("empty", ex.Message);
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
        Assert.Contains("ICCBased", pdfText);
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
        Assert.NotNull(stream);
        Assert.Contains("/ICCRGB1 cs", stream);
        Assert.Contains("sc\n", stream);
    }
}
