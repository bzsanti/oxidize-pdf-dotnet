namespace OxidizePdf.NET.Tests;

/// <summary>
/// End-to-end integration tests that exercise multiple features together
/// to verify they compose correctly and produce valid PDFs.
/// </summary>
public class EndToEndTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void FullDocument_WithTableAndList_ProducesValidPdf()
    {
        using var doc = new PdfDocument();
        doc.SetTitle("Integration Test Report")
            .SetAuthor("OxidizePdf.NET")
            .SetProducer("E2E Test Suite")
            .SetCreationDate(DateTimeOffset.UtcNow);

        using var page = PdfPage.A4();
        page.SetHeader("Test Report", StandardFont.HelveticaBold, 14)
            .SetFooter("Confidential", StandardFont.Helvetica, 8)
            .SetFont(StandardFont.Helvetica, 12)
            .TextAt(50, 750, "Summary");

        // Table
        var table = new PdfTable(["Metric", "Value", "Status"], totalWidth: 450);
        table.SetPosition(50, 700)
            .AddRow(["Tests", "220", "Passing"])
            .AddRow(["Coverage", "85%", "Good"]);
        page.AddTable(table);

        // Ordered list
        page.SetFont(StandardFont.Helvetica, 11)
            .AddOrderedList(["Review results", "Update docs", "Release"], 50, 550);

        doc.AddPage(page);
        var bytes = doc.SaveToBytes();
        Assert.True(bytes.Length > 200);
        // Verify PDF magic bytes
        Assert.Equal(0x25, bytes[0]); // %
        Assert.Equal(0x50, bytes[1]); // P
        Assert.Equal(0x44, bytes[2]); // D
        Assert.Equal(0x46, bytes[3]); // F
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void AdvancedGraphics_WithBlendAndClip_ProducesValidPdf()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();

        page.SaveGraphicsState()
            .SetLineCap(LineCap.Round)
            .SetLineJoin(LineJoin.Bevel)
            .SetDashPattern(5, 3)
            .SetStrokeColor(0, 0, 1)
            .SetLineWidth(2)
            .MoveTo(50, 700)
            .LineTo(300, 700)
            .Stroke()
            .SetLineSolid()
            .ClipRect(50, 400, 300, 200)
            .SetBlendMode(BlendMode.Multiply)
            .SetFillColor(1, 0, 0)
            .SetFillOpacity(0.5)
            .DrawRect(0, 350, 500, 300)
            .Fill()
            .ClearClipping()
            .RestoreGraphicsState();

        doc.AddPage(page);
        var bytes = doc.SaveToBytes();
        Assert.True(bytes.Length > 200);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void StyledText_WithRiseAndScaling_ProducesValidPdf()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();

        page.SetFont(StandardFont.TimesRoman, 24)
            .SetTextRenderingMode(TextRenderingMode.FillStroke)
            .SetStrokeColor(0, 0, 0)
            .SetTextColor(0.2, 0.2, 0.8)
            .SetHorizontalScaling(120)
            .TextAt(50, 700, "TITLE")
            .SetTextRenderingMode(TextRenderingMode.Fill)
            .SetHorizontalScaling(100)
            .SetFont(StandardFont.Helvetica, 12)
            .TextAt(50, 660, "Normal text with ")
            .SetTextRise(5)
            .TextAt(200, 660, "superscript")
            .SetTextRise(0);

        doc.AddPage(page);
        var bytes = doc.SaveToBytes();
        Assert.True(bytes.Length > 200);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SaveToFile_ThenReadBack_ValidPageCount()
    {
        using var doc = new PdfDocument();
        for (int i = 0; i < 3; i++)
        {
            using var page = PdfPage.A4();
            page.SetFont(StandardFont.Helvetica, 12)
                .TextAt(50, 700, $"Page {i + 1}");
            doc.AddPage(page);
        }

        var path = Path.GetTempFileName() + ".pdf";
        try
        {
            doc.SaveToFile(path);
            var fileBytes = File.ReadAllBytes(path);
            var extractor = new PdfExtractor();
            var pageCount = await extractor.GetPageCountAsync(fileBytes);
            Assert.Equal(3, (int)pageCount);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ReorderAndReverse_Roundtrip_ProducesValidPdf()
    {
        // Create 3-page PDF
        using var doc = new PdfDocument();
        for (int i = 0; i < 3; i++)
        {
            using var page = PdfPage.A4();
            page.SetFont(StandardFont.Helvetica, 12).TextAt(50, 700, $"Page {i + 1}");
            doc.AddPage(page);
        }
        var original = doc.SaveToBytes();

        // Reverse
        var reversed = await PdfOperations.ReversePagesAsync(original);
        Assert.True(reversed.Length > 100);

        // Reorder back
        var restored = await PdfOperations.ReorderPagesAsync(reversed, [2, 1, 0]);
        Assert.True(restored.Length > 100);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task OverlayPdf_TwoDocuments_ProducesValidPdf()
    {
        using var baseDoc = new PdfDocument();
        using var basePage = PdfPage.A4();
        basePage.SetFont(StandardFont.Helvetica, 24).TextAt(50, 700, "BASE");
        baseDoc.AddPage(basePage);
        var baseBytes = baseDoc.SaveToBytes();

        using var overlayDoc = new PdfDocument();
        using var overlayPage = PdfPage.A4();
        overlayPage.SetFont(StandardFont.Helvetica, 12)
            .SetTextColor(0.8, 0, 0)
            .TextAt(200, 400, "WATERMARK");
        overlayDoc.AddPage(overlayPage);
        var overlayBytes = overlayDoc.SaveToBytes();

        var result = await PdfOperations.OverlayAsync(baseBytes, overlayBytes);
        Assert.True(result.Length > 100);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void RotatedPage_ProducesValidPdf()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.SetRotation(90)
            .SetFont(StandardFont.Helvetica, 12)
            .TextAt(50, 700, "Rotated content");
        doc.AddPage(page);
        var bytes = doc.SaveToBytes();
        Assert.True(bytes.Length > 100);
    }
}
