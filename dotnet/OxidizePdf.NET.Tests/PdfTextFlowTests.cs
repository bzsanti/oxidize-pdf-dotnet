namespace OxidizePdf.NET.Tests;

/// <summary>
/// Tests for PdfTextFlow — text flow creation, alignment, and wrapped text.
/// </summary>
public class PdfTextFlowTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void CreateTextFlow_FromPage_Succeeds()
    {
        using var page = PdfPage.A4();
        page.SetMargins(50, 50, 50, 50);
        using var flow = page.CreateTextFlow();
        Assert.NotNull(flow);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void TextFlow_WithLeftAlignment_CreatesValidPdf()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.SetMargins(50, 50, 50, 50);
        using var flow = page.CreateTextFlow();
        flow.SetFont(StandardFont.Helvetica, 12)
            .SetAlignment(TextAlign.Left)
            .WriteWrapped("This is left-aligned text for testing purposes.");
        page.AddTextFlow(flow);
        doc.AddPage(page);
        var bytes = doc.SaveToBytes();
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void TextFlow_WithCenterAlignment_CreatesValidPdf()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.SetMargins(50, 50, 50, 50);
        using var flow = page.CreateTextFlow();
        flow.SetFont(StandardFont.Helvetica, 12)
            .SetAlignment(TextAlign.Center)
            .WriteWrapped("This is center-aligned text.");
        page.AddTextFlow(flow);
        doc.AddPage(page);
        var bytes = doc.SaveToBytes();
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void TextFlow_WithRightAlignment_CreatesValidPdf()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.SetMargins(50, 50, 50, 50);
        using var flow = page.CreateTextFlow();
        flow.SetFont(StandardFont.Helvetica, 12)
            .SetAlignment(TextAlign.Right)
            .WriteWrapped("This is right-aligned text.");
        page.AddTextFlow(flow);
        doc.AddPage(page);
        var bytes = doc.SaveToBytes();
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void TextFlow_WithJustifiedAlignment_CreatesValidPdf()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.SetMargins(50, 50, 50, 50);
        using var flow = page.CreateTextFlow();
        flow.SetFont(StandardFont.Helvetica, 12)
            .SetAlignment(TextAlign.Justified)
            .WriteWrapped("This is justified text that should span the full width between margins when the line is long enough to wrap.");
        page.AddTextFlow(flow);
        doc.AddPage(page);
        var bytes = doc.SaveToBytes();
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void TextFlow_FluentChaining_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        page.SetMargins(50, 50, 50, 50);
        using var flow = page.CreateTextFlow();
        var result = flow.SetFont(StandardFont.Helvetica, 12);
        Assert.Same(flow, result);

        result = flow.SetAlignment(TextAlign.Left);
        Assert.Same(flow, result);

        result = flow.WriteWrapped("test");
        Assert.Same(flow, result);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void AddTextFlow_ReturnsPage_ForFluentChaining()
    {
        using var page = PdfPage.A4();
        page.SetMargins(50, 50, 50, 50);
        using var flow = page.CreateTextFlow();
        flow.SetFont(StandardFont.Helvetica, 12)
            .WriteWrapped("test");
        var result = page.AddTextFlow(flow);
        Assert.Same(page, result);
    }

    [Fact]
    public void WriteWrapped_WithNullText_ThrowsArgumentNullException()
    {
        using var page = PdfPage.A4();
        using var flow = page.CreateTextFlow();
        Assert.Throws<ArgumentNullException>(() => flow.WriteWrapped(null!));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void AddTextFlow_WithNullFlow_ThrowsArgumentNullException()
    {
        using var page = PdfPage.A4();
        Assert.Throws<ArgumentNullException>(() => page.AddTextFlow(null!));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void TextFlow_DisposedFlow_ThrowsObjectDisposedException()
    {
        using var page = PdfPage.A4();
        var flow = page.CreateTextFlow();
        flow.Dispose();
        Assert.Throws<ObjectDisposedException>(() => flow.SetFont(StandardFont.Helvetica, 12));
    }
}
