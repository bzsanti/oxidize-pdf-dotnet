using System.Text.Json;
using OxidizePdf.NET.Models;

namespace OxidizePdf.NET.Tests;

public class OcrLayerTests
{
    private static byte[] Pdf(bool encrypt = false)
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.SetFont(StandardFont.Helvetica, 12).TextAt(50, 700, "Original");
        doc.AddPage(page);
        if (encrypt) doc.Encrypt("user", "owner");
        return doc.SaveToBytes();
    }
    private static PdfOcrPage Page() => new()
    {
        Language = "es", Fragments =
        [
            new() { Text = "Reconocido", Region = [50, 500, 100, 20], Confidence = 0.99, ReadingOrder = 0 },
            new() { Text = "日本語", Region = [160, 500, 100, 20], Confidence = 0.8, ReadingOrder = 1 }
        ]
    };

    [Fact]
    public async Task UnicodeLayerIsInvisibleAndPreservesOriginalPdfAndText()
    {
        var pdf = Pdf();
        var plan = await PdfOcrLayer.PlanAsync(pdf, [Page()]);
        var result = await PdfOcrLayer.ApplyAsync(pdf, [Page()]);
        Assert.True(result.PdfBytes.AsSpan().StartsWith(pdf));
        Assert.Equal(JsonSerializer.Serialize(plan), JsonSerializer.Serialize(result.Plan));
        Assert.Equal(new uint[] { 0 }, plan.Pages);
        Assert.Equal(1u, plan.StreamsAdded);
        Assert.Equal(1u, plan.ResourcesAdded);
        var fragments = await new PdfExtractor().ExtractTextFragmentsAsync(result.PdfBytes, 1);
        Assert.Contains(fragments.Fragments, f => f.Text.Contains("Original"));
        Assert.Contains(fragments.Fragments, f => f.Text.Contains("Reconocido") && (int)f.RenderMode == 3);
        Assert.Contains(fragments.Fragments, f => f.Text.Contains("日本語") && (int)f.RenderMode == 3);
    }

    [Fact]
    public async Task ExistingLayersAreSkippedWithoutAppendingARevision()
    {
        var first = await PdfOcrLayer.ApplyAsync(Pdf(), [Page()]);
        var second = await PdfOcrLayer.ApplyAsync(first.PdfBytes, [Page()]);
        Assert.Equal(first.PdfBytes, second.PdfBytes);
        Assert.Empty(second.Plan.Pages);
        Assert.Equal(new uint[] { 0 }, second.Plan.PagesSkippedExistingOcr);
        Assert.Equal(0u, second.Plan.StreamsAdded);
    }

    [Fact]
    public async Task EmptyFragmentsLeaveDocumentUnchanged()
    {
        var pdf = Pdf();
        var result = await PdfOcrLayer.ApplyAsync(pdf, [new() { Language = "en" }]);
        Assert.Equal(pdf, result.PdfBytes);
        Assert.Empty(result.Plan.Pages);
    }

    [Fact]
    public async Task InvalidRequestsFailInPlanningAndExecution()
    {
        var pdf = Pdf();
        var outside = Page(); outside.PageIndex = 2;
        var region = Page(); region.Fragments[0].Region = [0, 0, -1, 20];
        var confidence = Page(); confidence.Fragments[0].Confidence = 1.1;
        var order = Page(); order.Fragments[1].ReadingOrder = 0;
        var language = Page(); language.Language = "";
        foreach (var page in new[] { outside, region, confidence, order, language })
        {
            await Assert.ThrowsAsync<PdfExtractionException>(() => PdfOcrLayer.PlanAsync(pdf, [page]));
            await Assert.ThrowsAsync<PdfExtractionException>(() => PdfOcrLayer.ApplyAsync(pdf, [page]));
        }
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfOcrLayer.PlanAsync(pdf, [Page(), Page()]));
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfOcrLayer.ApplyAsync(Pdf(encrypt: true), [Page()]));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task CertificationRejectsOcrContentChanges(byte permission)
    {
        using var prepared = await PdfSigning.PrepareAsync(Pdf(), new() { Certification = permission });
        // Synthetic DER tests policy parsing, not signature authenticity.
        var pdf = prepared.Complete([0x30, 3, 2, 1, 1]);
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfOcrLayer.PlanAsync(pdf, [Page()]));
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfOcrLayer.ApplyAsync(pdf, [Page()]));
    }
}
