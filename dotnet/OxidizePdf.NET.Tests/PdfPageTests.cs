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

    // ── Rotation ──────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public void SetRotation_90_GetRotation_Returns90()
    {
        using var page = PdfPage.A4();
        page.SetRotation(90);
        Assert.Equal(90, page.Rotation);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void DefaultRotation_IsZero()
    {
        using var page = PdfPage.A4();
        Assert.Equal(0, page.Rotation);
    }

    // ── Text operations (advanced) ─────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public void SetHorizontalScaling_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        Assert.Same(page, page.SetHorizontalScaling(120.0));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetTextRise_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        Assert.Same(page, page.SetTextRise(3.0));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetTextRenderingMode_Stroke_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        Assert.Same(page, page.SetTextRenderingMode(TextRenderingMode.Stroke));
    }

    // ── Line style (advanced) ────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public void SetLineCap_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        var result = page.SetLineCap(LineCap.Round);
        Assert.Same(page, result);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetLineJoin_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        Assert.Same(page, page.SetLineJoin(LineJoin.Round));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetMiterLimit_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        Assert.Same(page, page.SetMiterLimit(4.0));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetDashPattern_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        Assert.Same(page, page.SetDashPattern(6.0, 3.0));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetLineSolid_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        Assert.Same(page, page.SetLineSolid());
    }

    // ── Graphics state ───────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public void SaveAndRestoreGraphicsState_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        var result = page
            .SaveGraphicsState()
            .SetFillColor(1.0, 0.0, 0.0)
            .RestoreGraphicsState();
        Assert.Same(page, result);
    }

    // ── Clipping ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public void ClipRect_ThenFill_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        var result = page
            .ClipRect(50, 50, 200, 200)
            .SetFillColor(1.0, 0.0, 0.0)
            .DrawRect(0, 0, 400, 400)
            .Fill();
        Assert.Same(page, result);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void ClipCircle_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        Assert.Same(page, page.ClipCircle(200, 400, 100));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void ClearClipping_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        Assert.Same(page, page.ClipRect(10, 10, 100, 100).ClearClipping());
    }

    // ── Blend mode ───────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public void SetBlendMode_Multiply_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        Assert.Same(page, page.SetBlendMode(BlendMode.Multiply));
    }

    // ── Advanced graphics integration ────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public void AdvancedGraphics_FluentChaining_ProducesValidPdf()
    {
        using var page = PdfPage.A4();
        using var doc = new PdfDocument();

        page
            .SaveGraphicsState()
            .SetLineCap(LineCap.Round)
            .SetLineJoin(LineJoin.Bevel)
            .SetMiterLimit(8.0)
            .SetDashPattern(5.0, 2.0)
            .SetStrokeColor(0.0, 0.0, 1.0)
            .SetLineWidth(3.0)
            .MoveTo(50, 500)
            .LineTo(200, 600)
            .LineTo(350, 500)
            .Stroke()
            .SetLineSolid()
            .ClipRect(50, 50, 400, 300)
            .SetBlendMode(BlendMode.Multiply)
            .SetFillColor(1.0, 0.0, 0.0)
            .DrawRect(0, 0, 500, 400)
            .Fill()
            .ClearClipping()
            .RestoreGraphicsState();

        doc.AddPage(page);
        var bytes = doc.SaveToBytes();
        Assert.True(bytes.Length > 100);
    }
}
