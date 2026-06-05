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
        Assert.Same(page, page.SetFillColorCalGray(0.5, CalGrayColorSpace.D65()));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetStrokeColorCalGray_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        Assert.Same(page, page.SetStrokeColorCalGray(0.5, CalGrayColorSpace.D65()));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetFillColorCalGray_NullColorSpace_Throws()
    {
        using var page = PdfPage.A4();
        Assert.Throws<ArgumentNullException>(() => page.SetFillColorCalGray(0.5, null!));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetFillColorCalGray_InvalidColorSpace_Throws()
    {
        using var page = PdfPage.A4();
        var badCs = new CalGrayColorSpace { WhitePoint = new double[] { 0.9505, 0.5, 1.089 } };
        Assert.Throws<ArgumentException>(() => page.SetFillColorCalGray(0.5, badCs));
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
        // The hardcoded-name variant emits a 'cs' operator referencing a fixed resource name (/CalGray1, /CalRGB1, /Lab1). That resource is NOT registered in /Resources/ColorSpace; for a spec-valid standalone PDF use AddColorSpace + the named draw methods. The name appears in the decompressed content stream via the cs operator.
        var stream = ContentStreamHelper.DecompressFirstContentStream(pdfBytes);
        Assert.NotNull(stream);
        Assert.Contains("CalGray", stream);
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
        Assert.NotNull(stream);
        Assert.Contains("/CalGray1 cs", stream);
        Assert.Contains("sc\n", stream);
    }
}
