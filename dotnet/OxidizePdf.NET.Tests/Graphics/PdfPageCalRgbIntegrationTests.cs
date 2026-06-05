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
        Assert.Same(page, page.SetFillColorCalRgb(0.5, 0.3, 0.8, CalRgbColorSpace.SRgb()));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetStrokeColorCalRgb_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        Assert.Same(page, page.SetStrokeColorCalRgb(0.5, 0.3, 0.8, CalRgbColorSpace.SRgb()));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetFillColorCalRgb_NullColorSpace_Throws()
    {
        using var page = PdfPage.A4();
        Assert.Throws<ArgumentNullException>(() => page.SetFillColorCalRgb(0.5, 0.3, 0.8, null!));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetFillColorCalRgb_InvalidColorSpace_Throws()
    {
        using var page = PdfPage.A4();
        // WhitePoint[1] != 1.0 is invalid per PDF spec
        var badCs = new CalRgbColorSpace { WhitePoint = new double[] { 0.9505, 0.5, 1.0890 } };
        Assert.Throws<ArgumentException>(() => page.SetFillColorCalRgb(0.5, 0.3, 0.8, badCs));
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
        // The hardcoded-name variant emits a 'cs' operator referencing a fixed resource name (/CalGray1, /CalRGB1, /Lab1). That resource is NOT registered in /Resources/ColorSpace; for a spec-valid standalone PDF use AddColorSpace + the named draw methods. The name appears in the decompressed content stream via the cs operator.
        var stream = ContentStreamHelper.DecompressFirstContentStream(pdfBytes);
        Assert.NotNull(stream);
        Assert.Contains("CalRGB", stream);
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
        Assert.NotNull(stream);
        Assert.Contains("/CalRGB1 cs", stream);
        Assert.Contains("sc\n", stream);
    }
}
