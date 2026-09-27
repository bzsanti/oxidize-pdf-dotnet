using System.Text;
using OxidizePdf.NET.Models;
namespace OxidizePdf.NET.Tests;

public class RedactionTests
{
    private static byte[] Pdf(bool marked = false, bool duplicate = false)
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        doc.SetTitle("SECRET");
        page.SetFont(StandardFont.Helvetica, 12);
        if (marked) page.BeginMarkedContent("P");
        page.TextAt(50, 700, "SECRET");
        if (marked) page.EndMarkedContent();
        page.TextAt(50, 650, "PUBLIC");
        if (duplicate) page.TextAt(50, 600, "SECRET");
        doc.AddPage(page);
        return doc.SaveToBytes(new PdfSaveOptions { CompressStreams = false });
    }
    private static PdfRedactionEntity Target(string content = "SECRET") => new()
    {
        Id = "target", Type = "customSensitive", Content = content,
        Bounds = new() { X = 45, Y = 695, Width = 100, Height = 20 }
    };

    [Fact]
    public async Task IrreversibleRemovalDropsTargetAndMetadataButPreservesOtherText()
    {
        var original = Pdf();
        var result = await PdfRedaction.RemoveIrreversiblyAsync(original, [Target()], ["customSensitive"]);
        Assert.True(result.Report.IsIrreversible);
        Assert.Equal("Irreversible", result.Report.Mode);
        Assert.Empty(result.Report.ResidualRisks);
        Assert.Equal(1u, result.Report.RedactedCount);
        Assert.Equal("target", Assert.Single(result.Report.Entries).EntityId);
        Assert.Equal(new uint[] { 1 }, result.Report.PagesAffected);
        var text = await new PdfExtractor().ExtractTextFromPageAsync(result.PdfBytes, 1);
        Assert.DoesNotContain("SECRET", text);
        Assert.Contains("PUBLIC", text);
        Assert.DoesNotContain("SECRET", Encoding.Latin1.GetString(result.PdfBytes));
        Assert.Null((await new PdfExtractor().ExtractMetadataAsync(result.PdfBytes)).Title);
        Assert.False(result.PdfBytes.AsSpan().StartsWith(original));
    }

    [Fact]
    public async Task VisualMaskReportsRecoverabilityAndRetainsTargetText()
    {
        var result = await PdfRedaction.MaskVisuallyAsync(Pdf(), [Target()], ["customSensitive"], "REMOVED");
        Assert.False(result.Report.IsIrreversible);
        Assert.Equal("VisualMask", result.Report.Mode);
        Assert.NotEmpty(result.Report.ResidualRisks);
        var text = await new PdfExtractor().ExtractTextFromPageAsync(result.PdfBytes, 1);
        Assert.Contains("SECRET", text);
        Assert.Contains("REMOVED", text);
    }

    [Fact]
    public async Task UnsupportedInputsFailWithoutVisualFallback()
    {
        foreach (var pdf in new[] { Pdf(marked: true), Pdf(duplicate: true) })
            await Assert.ThrowsAsync<PdfExtractionException>(() => PdfRedaction.RemoveIrreversiblyAsync(pdf, [Target()], ["customSensitive"]));
        foreach (var content in new[] { "SEC", "missing", "秘密", "" })
            await Assert.ThrowsAsync<PdfExtractionException>(() => PdfRedaction.RemoveIrreversiblyAsync(Pdf(), [Target(content)], ["customSensitive"]));
        var annotated = await PdfOperations.EditFreeTextAnnotationsAsync(Pdf(), [new PdfFreeTextMutation.Add(0, [10, 10, 100, 40], "note")]);
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfRedaction.RemoveIrreversiblyAsync(annotated.PdfBytes, [Target()], ["customSensitive"]));
    }

    [Fact]
    public async Task EmptyTypeSelectionIsOnlyANoopForVisualMasking()
    {
        var original = Pdf();
        var masked = await PdfRedaction.MaskVisuallyAsync(original, [Target()], []);
        Assert.Equal(original, masked.PdfBytes);
        Assert.Equal(0u, masked.Report.RedactedCount);
        Assert.False(masked.Report.IsIrreversible);
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfRedaction.RemoveIrreversiblyAsync(original, [Target()], []));
    }
}
