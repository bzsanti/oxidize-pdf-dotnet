namespace OxidizePdf.NET.Tests;

/// <summary>
/// Tests for PdfPage class — creation, dimensions, text, and graphics.
/// </summary>
public class PdfPageTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void Create_WithCustomDimensions_SetsWidthAndHeight()
    {
        using var page = new PdfPage(400, 600);
        Assert.Equal(400, page.Width);
        Assert.Equal(600, page.Height);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void A4_HasStandardDimensions()
    {
        using var page = PdfPage.A4();
        // A4 = 595.28 x 841.89 points (approximately)
        Assert.InRange(page.Width, 595, 596);
        Assert.InRange(page.Height, 841, 842);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Letter_HasStandardDimensions()
    {
        using var page = PdfPage.Letter();
        // Letter = 612 x 792 points
        Assert.Equal(612, page.Width);
        Assert.Equal(792, page.Height);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void A4Landscape_HasSwappedDimensions()
    {
        using var page = PdfPage.A4Landscape();
        Assert.True(page.Width > page.Height, "Landscape width should be greater than height");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Legal_CreatesValidPage()
    {
        using var page = PdfPage.Legal();
        Assert.True(page.Width > 0);
        Assert.True(page.Height > 0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetMargins_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        var result = page.SetMargins(72, 72, 72, 72);
        Assert.Same(page, result);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetFont_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        var result = page.SetFont(StandardFont.Helvetica, 12);
        Assert.Same(page, result);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void TextOperations_FluentChaining()
    {
        using var page = PdfPage.A4();
        var result = page
            .SetFont(StandardFont.CourierBold, 14)
            .SetTextColor(0, 0, 0)
            .SetCharacterSpacing(1.0)
            .SetWordSpacing(2.0)
            .SetLeading(16)
            .TextAt(72, 700, "Hello World");
        Assert.Same(page, result);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GraphicsOperations_FluentChaining()
    {
        using var page = PdfPage.A4();
        var result = page
            .SetFillColor(1.0, 0.0, 0.0)
            .SetStrokeColor(0.0, 0.0, 1.0)
            .SetLineWidth(2.0)
            .SetFillOpacity(0.8)
            .SetStrokeOpacity(1.0)
            .DrawRect(50, 50, 200, 100)
            .FillAndStroke();
        Assert.Same(page, result);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void PathOperations_FluentChaining()
    {
        using var page = PdfPage.A4();
        var result = page
            .SetStrokeColor(0, 0, 0)
            .SetLineWidth(1.5)
            .MoveTo(100, 100)
            .LineTo(200, 200)
            .CurveTo(250, 250, 300, 200, 350, 150)
            .ClosePath()
            .Stroke();
        Assert.Same(page, result);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void DrawCircle_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        var result = page.DrawCircle(200, 400, 50);
        Assert.Same(page, result);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetTextColorGray_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        var result = page.SetTextColorGray(0.5);
        Assert.Same(page, result);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetTextColorCmyk_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        var result = page.SetTextColorCmyk(0.0, 1.0, 1.0, 0.0);
        Assert.Same(page, result);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetFillColorGray_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        var result = page.SetFillColorGray(0.3);
        Assert.Same(page, result);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Dispose_ThenAccess_ThrowsObjectDisposedException()
    {
        var page = PdfPage.A4();
        page.Dispose();
        Assert.Throws<ObjectDisposedException>(() => page.Width);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void DoubleDispose_DoesNotThrow()
    {
        var page = PdfPage.A4();
        page.Dispose();
        page.Dispose(); // Should not throw
    }
}
