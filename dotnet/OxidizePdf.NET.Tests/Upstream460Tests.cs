using System.Text;
using OxidizePdf.NET.Models;
using OxidizePdf.NET.Tests.TestHelpers;

namespace OxidizePdf.NET.Tests;

public class Upstream460Tests
{
    [Fact]
    public async Task MarkedContent_WithActualText_EmitsReplacementProperty()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.BeginMarkedContent("Span", "accessible text");
        page.SetFont(StandardFont.Helvetica, 12).TextAt(72, 720, "visual text");
        page.EndMarkedContent();
        doc.AddPage(page);

        var pdf = Encoding.Latin1.GetString(doc.SaveToBytes(
            new PdfSaveOptions { CompressStreams = false }));

        Assert.Contains("/ActualText", pdf);
        Assert.Contains("/Span", pdf);
        Assert.Contains("FEFF00610063006300650073007300690062006C006500200074006500780074", pdf);

        var extracted = await new PdfExtractor().ExtractTextAsync(Encoding.Latin1.GetBytes(pdf));
        Assert.Contains("accessible text", extracted);
        Assert.DoesNotContain("visual text", extracted);
    }

    [Fact]
    public async Task TextNoteEditor_AddsListsUpdatesAndRemovesIncrementally()
    {
        byte[] original;
        using (var doc = new PdfDocument())
        using (var page = PdfPage.A4())
        {
            doc.AddPage(page);
            original = doc.SaveToBytes();
        }

        var added = await PdfOperations.EditTextNotesAsync(original,
            [new PdfTextNoteMutation.Add(0, 72, 720, "first")]);
        Assert.True(added.PdfBytes.AsSpan().StartsWith(original));
        var note = Assert.Single(added.AddedNotes);

        var listed = await PdfOperations.GetTextNotesAsync(added.PdfBytes);
        Assert.Equal("first", Assert.Single(listed).Contents);

        var updated = await PdfOperations.EditTextNotesAsync(added.PdfBytes,
            [new PdfTextNoteMutation.Update(note.Id, 80, 700, "updated")]);
        Assert.Equal("updated", Assert.Single(await PdfOperations.GetTextNotesAsync(updated.PdfBytes)).Contents);

        var removed = await PdfOperations.EditTextNotesAsync(updated.PdfBytes,
            [new PdfTextNoteMutation.Remove(note.Id)]);
        Assert.Empty(await PdfOperations.GetTextNotesAsync(removed.PdfBytes));
    }

    [Fact]
    public async Task ExtractImages_WithInvalidBound_RejectsBeforeNativeCall()
    {
        var options = new ImageExtractionOptions { MaxImages = 0 };
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            PdfOperations.ExtractImagesAsync([1], options));
    }

    [Fact]
    public async Task ExtractImages_EnforcesImageCountAndDecodedPixelBounds()
    {
        var iconPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "../../../../OxidizePdf.NET/icon.png"));
        var png = await File.ReadAllBytesAsync(iconPath);
        byte[] pdf;
        using (var first = PdfImage.FromPngData(png))
        using (var second = PdfImage.FromPngData(png))
        using (var page = PdfPage.A4())
        using (var doc = new PdfDocument())
        {
            page.AddImage("One", first).AddImage("Two", second);
            doc.AddPage(page);
            pdf = doc.SaveToBytes();
        }

        await Assert.ThrowsAsync<PdfExtractionException>(() =>
            PdfOperations.ExtractImagesAsync(pdf, new ImageExtractionOptions { MaxImages = 1 }));
        await Assert.ThrowsAsync<PdfExtractionException>(() =>
            PdfOperations.ExtractImagesAsync(pdf, new ImageExtractionOptions { MaxDecodedPixelsPerImage = 1 }));
    }

    [Fact]
    public async Task ExtractionOptions_ApplyReadingOrderAndAllCarriageReturnPolicies()
    {
        var extractor = new PdfExtractor();
        var columns = BuildPdfWithContent(
            "BT /F1 10 Tf " +
            "1 0 0 1 350 700 Tm [(Right top)] TJ " +
            "1 0 0 1 350 680 Tm [(Right bottom)] TJ " +
            "1 0 0 1 50 700 Tm [(Left top)] TJ " +
            "1 0 0 1 50 680 Tm [(Left bottom)] TJ ET");

        var streamOrder = await extractor.ExtractTextAsync(columns, new ExtractionOptions());
        var readingOrder = await extractor.ExtractTextAsync(columns,
            new ExtractionOptions { ReadingOrder = true });
        Assert.Equal("Right top\nRight bottom\nLeft top\nLeft bottom", streamOrder);
        Assert.Equal("Left top\nLeft bottom\nRight top\nRight bottom", readingOrder);

        var carriageReturns = BuildPdfWithContent("BT /F1 10 Tf 50 700 Td (A\\015B\\015\\012C) Tj ET");
        var remove = await extractor.ExtractTextAsync(carriageReturns,
            new ExtractionOptions { CarriageReturnHandling = CarriageReturnHandling.Remove });
        var spaces = await extractor.ExtractTextAsync(carriageReturns,
            new ExtractionOptions { CarriageReturnHandling = CarriageReturnHandling.ReplaceWithSpace });
        var normalize = await extractor.ExtractTextAsync(carriageReturns,
            new ExtractionOptions { CarriageReturnHandling = CarriageReturnHandling.NormalizeLineEnding });
        Assert.Equal("AB\nC", remove);
        Assert.Equal("A B\nC", spaces);
        Assert.Equal("A\rB\nC", normalize);
    }

    [Fact]
    public void ExtractionOptions_InvalidCarriageReturnPolicy_IsRejected()
    {
        var options = new ExtractionOptions
        {
            CarriageReturnHandling = (CarriageReturnHandling)99
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Fact]
    public async Task TextNoteEditor_RejectsInvalidMutationsBeforeNativeCall()
    {
        var pdf = PdfTestFixtures.GetEmptyPdf();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            PdfOperations.EditTextNotesAsync(pdf, []));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            PdfOperations.EditTextNotesAsync(pdf,
                [new PdfTextNoteMutation.Add(0, 1, 2, " ")]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            PdfOperations.EditTextNotesAsync(pdf,
                [new PdfTextNoteMutation.Add(0, double.NaN, 2, "note")]));
    }

    private static byte[] BuildPdfWithContent(string content)
    {
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"
        };
        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append(index + 1).Append(" 0 obj\n").Append(objects[index]).Append("\nendobj\n");
        }
        var xref = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.Append("xref\n0 6\n0000000000 65535 f \n");
        foreach (var offset in offsets) pdf.Append(offset.ToString("D10")).Append(" 00000 n \n");
        pdf.Append("trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n")
            .Append(xref).Append("\n%%EOF\n");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }
}
