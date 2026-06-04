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
        Assert.Same(page, page.SetFillColorLab(new LabColor(50.0, 0.0, 0.0, LabColorSpace.D50())));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetStrokeColorLab_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        Assert.Same(page, page.SetStrokeColorLab(new LabColor(50.0, 0.0, 0.0, LabColorSpace.D50())));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetFillColorLab_NullColor_Throws()
    {
        using var page = PdfPage.A4();
        Assert.Throws<ArgumentNullException>(() => page.SetFillColorLab(null!));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetFillColorLab_InvalidColorSpace_Throws()
    {
        using var page = PdfPage.A4();
        // WhitePoint[1] != 1.0 is invalid per PDF spec
        var badCs = new LabColorSpace { WhitePoint = new double[] { 0.9642, 0.5, 0.8251 } };
        var color = new LabColor(50.0, 0.0, 0.0, badCs);
        Assert.Throws<ArgumentException>(() => page.SetFillColorLab(color));
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
        // The hardcoded-name variant emits a 'cs' operator referencing a fixed resource name (/CalGray1, /CalRGB1, /Lab1). That resource is NOT registered in /Resources/ColorSpace; for a spec-valid standalone PDF use AddColorSpace + the named draw methods. The name appears in the decompressed content stream via the cs operator.
        var stream = ContentStreamHelper.DecompressFirstContentStream(pdfBytes);
        Assert.NotNull(stream);
        Assert.Contains("Lab", stream);
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
        Assert.NotNull(stream);
        Assert.Contains("/Lab1 cs", stream);
        Assert.Contains("sc\n", stream);
    }
}
