namespace OxidizePdf.NET.Tests;

public class PdfPageGraphicsTransformTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void Translate_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        Assert.Same(page, page.Translate(10.0, 20.0));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Scale_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        Assert.Same(page, page.Scale(2.0, 2.0));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void RotateRadians_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        Assert.Same(page, page.RotateRadians(Math.PI / 4));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void RotateDegrees_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        Assert.Same(page, page.RotateDegrees(45.0));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Transform_Matrix_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        Assert.Same(page, page.Transform(1, 0, 0, 1, 10, 20));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void TransformPipeline_SaveTranslateDrawRestore_ProducesValidPdf()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.SaveGraphicsState()
            .Translate(100, 100)
            .Scale(2.0, 2.0)
            .SetFillColor(1, 0, 0)
            .DrawRect(0, 0, 50, 50)
            .Fill()
            .RestoreGraphicsState();
        doc.AddPage(page);
        var bytes = doc.SaveToBytes();
        Assert.True(bytes.Length > 0);
    }
}
