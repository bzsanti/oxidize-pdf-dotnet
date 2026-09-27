using System.Text;
using OxidizePdf.NET.Models;

namespace OxidizePdf.NET.Tests;

public class Upstream514Tests
{
    [Fact]
    public async Task LinkTargets_AreOptInAndNonUriActionsAreExcluded()
    {
        var pdf = BuildPdf("BT /F1 12 Tf 10 70 Td (Body) Tj ET",
            "/WinAnsiEncoding",
            " /Annots [<< /Type /Annot /Subtype /Link /Rect [0 0 10 10] /A << /S /URI /URI (https://example.org/document) >> >> " +
            "<< /Type /Annot /Subtype /Link /Rect [0 0 10 10] /A << /S /JavaScript /JS (javascript:alert) >> >>]");
        var extractor = new PdfExtractor();
        var normal = await extractor.ExtractTextAsync(pdf, new ExtractionOptions());
        var links = await extractor.ExtractTextAsync(pdf,
            new ExtractionOptions { IncludeLinkAnnotations = true });
        Assert.DoesNotContain("https://", normal);
        Assert.Contains("Body", links);
        Assert.Contains("https://example.org/document", links);
        Assert.DoesNotContain("javascript:", links);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnreliableFigureText_IsFilteredUnlessExplicitlyRequested(bool preserveLayout)
    {
        var pdf = BuildPdf(
            "BT /F1 12 Tf 10 70 Td (B) Tj ET /Figure BMC BT /F1 12 Tf 10 50 Td (A) Tj ET EMC",
            "<< /Type /Encoding /BaseEncoding /WinAnsiEncoding /Differences [65 /B] >>");
        var extractor = new PdfExtractor();
        var filtered = await extractor.ExtractTextAsync(pdf,
            new ExtractionOptions { PreserveLayout = preserveLayout });
        var retained = await extractor.ExtractTextAsync(pdf,
            new ExtractionOptions { PreserveLayout = preserveLayout, IncludeUnreliableFigureText = true });
        Assert.Equal("B", filtered.Trim());
        Assert.Equal(2, retained.Count(c => c == 'B'));
    }

    [Fact]
    public async Task Fragments_RetainAllRenderingModesAndInvisibleText()
    {
        var content = string.Join(" ", Enumerable.Range(0, 8).Select(mode =>
            $"BT /F1 12 Tf {mode} Tr 10 {90 - mode * 10} Td (mode{mode}) Tj ET"));
        var result = await new PdfExtractor().ExtractTextFragmentsAsync(
            BuildPdf(content, "/WinAnsiEncoding"), 1);
        Assert.False(result.Truncated);
        Assert.Equal(8, result.Fragments.Count);
        for (var mode = 0; mode < 8; mode++)
        {
            var fragment = Assert.Single(result.Fragments, f => f.Text == $"mode{mode}");
            Assert.Equal((TextRenderingMode)mode, fragment.RenderMode);
            Assert.Equal(12, fragment.FontSize);
            Assert.True(fragment.Width > 0);
        }
    }

    [Fact]
    public async Task Fragments_PreserveModeAcrossGraphicsStateAndActualText()
    {
        var pdf = BuildPdf(
            "BT /F1 12 Tf 1 Tr 10 80 Td (before) Tj ET " +
            "q BT /F1 12 Tf 3 Tr 10 60 Td /Span << /ActualText (replacement) >> BDC (visual) Tj EMC ET Q " +
            "BT /F1 12 Tf 10 40 Td (after) Tj ET", "/WinAnsiEncoding");
        var result = await new PdfExtractor().ExtractTextFragmentsAsync(pdf, 1);
        Assert.Equal(TextRenderingMode.Stroke, Assert.Single(result.Fragments, f => f.Text == "before").RenderMode);
        Assert.Equal(TextRenderingMode.Invisible, Assert.Single(result.Fragments, f => f.Text == "replacement").RenderMode);
        Assert.Equal(TextRenderingMode.Stroke, Assert.Single(result.Fragments, f => f.Text == "after").RenderMode);
        Assert.DoesNotContain(result.Fragments, f => f.Text == "visual");
    }

    [Fact]
    public async Task Fragments_ReportLimitsAndRejectMissingPages()
    {
        var pdf = BuildPdf("BT /F1 12 Tf 10 70 Td (first) Tj 0 -20 Td (second) Tj ET", "/WinAnsiEncoding");
        var extractor = new PdfExtractor();
        var limited = await extractor.ExtractTextFragmentsAsync(pdf, 1, 5);
        Assert.True(limited.Truncated);
        Assert.DoesNotContain("second", limited.Text);
        await Assert.ThrowsAsync<PdfExtractionException>(() => extractor.ExtractTextFragmentsAsync(pdf, 2));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => extractor.ExtractTextFragmentsAsync(pdf, 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => extractor.ExtractTextFragmentsAsync(pdf, 1, -1));
    }

    private static byte[] BuildPdf(string content, string encoding, string pageExtra = "")
    {
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R {pageExtra} >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream",
            $"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding {encoding} >>"
        ];
        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append(i + 1).Append(" 0 obj\n").Append(objects[i]).Append("\nendobj\n");
        }
        var xref = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.Append("xref\n0 6\n0000000000 65535 f \n");
        foreach (var offset in offsets) pdf.Append(offset.ToString("D10")).Append(" 00000 n \n");
        pdf.Append("trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n").Append(xref).Append("\n%%EOF\n");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }
}
