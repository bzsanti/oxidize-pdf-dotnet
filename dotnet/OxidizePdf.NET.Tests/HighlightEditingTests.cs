namespace OxidizePdf.NET.Tests;

public class HighlightEditingTests
{
    private static byte[] BlankPdf()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        doc.AddPage(page);
        return doc.SaveToBytes();
    }

    private static double[][] Quads() => [[10, 40, 80, 40, 10, 20, 80, 20]];

    [Fact]
    public async Task HighlightEdits_PreserveBytesAndRoundTripAllAppearanceProperties()
    {
        var original = BlankPdf();
        Assert.Empty(await PdfOperations.GetHighlightsAsync(original));
        var added = await PdfOperations.EditHighlightsAsync(original,
            [new PdfHighlightMutation.Add(0, Quads(), [1, 0.5, 0], 0.4, "área resaltada")]);
        Assert.True(added.PdfBytes.AsSpan().StartsWith(original));
        var highlight = Assert.Single(added.Highlights);
        Assert.True(highlight.Id.ObjectNumber > 0);
        Assert.Equal(0u, highlight.PageIndex);
        Assert.Equal(new double[] { 1, 0.5, 0 }, highlight.Color);
        Assert.Equal(0.4, highlight.Opacity);
        Assert.Equal("área resaltada", highlight.Contents);
        Assert.Equal(Quads()[0], Assert.Single(highlight.Quadrilaterals));
        Assert.Equal(new double[] { 10, 20, 80, 40 }, highlight.Rectangle);
        var listed = Assert.Single(await PdfOperations.GetHighlightsAsync(added.PdfBytes));
        Assert.Equal(highlight.Id, listed.Id);
        Assert.Equal(highlight.Contents, listed.Contents);
        var removed = await PdfOperations.EditHighlightsAsync(added.PdfBytes,
            [new PdfHighlightMutation.Remove(highlight.Id)]);
        Assert.True(removed.PdfBytes.AsSpan().StartsWith(added.PdfBytes));
        Assert.Empty(removed.Highlights);
        Assert.Empty(await PdfOperations.GetHighlightsAsync(removed.PdfBytes));
    }

    [Fact]
    public async Task AtomicReplacement_GetsNewIdAndRetainsOtherHighlights()
    {
        var added = await PdfOperations.EditHighlightsAsync(BlankPdf(),
            [new PdfHighlightMutation.Add(0, Quads(), [1, 1, 0], Contents: "first"),
             new PdfHighlightMutation.Add(0, Quads(), [0, 1, 0], Contents: "second")]);
        var first = Assert.Single(added.Highlights, h => h.Contents == "first");
        var second = Assert.Single(added.Highlights, h => h.Contents == "second");
        var replacement = await PdfOperations.EditHighlightsAsync(added.PdfBytes,
            [new PdfHighlightMutation.Remove(first.Id),
             new PdfHighlightMutation.Add(0, Quads(), [0, 0, 1], Contents: "replacement")]);
        Assert.True(replacement.PdfBytes.AsSpan().StartsWith(added.PdfBytes));
        Assert.Equal(2, replacement.Highlights.Count);
        Assert.Contains(replacement.Highlights, h => h.Id == second.Id);
        Assert.DoesNotContain(replacement.Highlights, h => h.Id == first.Id);
        Assert.NotEqual(first.Id, Assert.Single(replacement.Highlights, h => h.Contents == "replacement").Id);
    }

    [Fact]
    public async Task EncryptedDocumentsAreRejected()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        doc.AddPage(page);
        doc.Encrypt("user", "owner");
        var pdf = doc.SaveToBytes();
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfOperations.GetHighlightsAsync(pdf));
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfOperations.EditHighlightsAsync(pdf,
            [new PdfHighlightMutation.Add(0, Quads(), [1, 1, 0])]));
    }

    [Fact]
    public async Task InvalidGeometryAndPageAreRejectedAndOriginalRemainsReadable()
    {
        var pdf = BlankPdf();
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfOperations.EditHighlightsAsync(pdf,
            [new PdfHighlightMutation.Add(0, [[0, 0, 0, 0, 0, 0, 0, 0]], [1, 1, 0])]));
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfOperations.EditHighlightsAsync(pdf,
            [new PdfHighlightMutation.Add(0, Quads(), [1, 1, 0]),
             new PdfHighlightMutation.Add(99, Quads(), [1, 1, 0])]));
        await Assert.ThrowsAsync<ArgumentException>(() => PdfOperations.EditHighlightsAsync(pdf,
            [new PdfHighlightMutation.Add(0, Quads(), [2, 1, 0])]));
        await Assert.ThrowsAsync<ArgumentException>(() => PdfOperations.EditHighlightsAsync(pdf, []));
        Assert.Empty(await PdfOperations.GetHighlightsAsync(pdf));
    }
}
