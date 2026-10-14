namespace OxidizePdf.NET.Tests;

public class FreeTextEditingTests
{
    private static byte[] BlankPdf(bool encrypt = false)
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        doc.AddPage(page);
        if (encrypt) doc.Encrypt("user", "owner");
        return doc.SaveToBytes();
    }

    [Theory]
    [InlineData(PdfFreeTextAlignment.Left)]
    [InlineData(PdfFreeTextAlignment.Center)]
    [InlineData(PdfFreeTextAlignment.Right)]
    public async Task AddUpdateRemove_PreservePrefixAndIdentity(PdfFreeTextAlignment alignment)
    {
        var pdf = BlankPdf();
        Assert.Empty(await PdfOperations.GetFreeTextAnnotationsAsync(pdf));
        var added = await PdfOperations.EditFreeTextAnnotationsAsync(pdf,
            [new PdfFreeTextMutation.Add(0, [10, 20, 180, 90], "primera revisión", Alignment: alignment)]);
        Assert.True(added.PdfBytes.AsSpan().StartsWith(pdf));
        var note = Assert.Single(added.Annotations);
        Assert.Equal(alignment, note.Alignment);
        Assert.Equal("primera revisión", note.Contents);
        Assert.Equal("/Helv 12 Tf 0 g", note.DefaultAppearance);
        Assert.Equal(new double[] { 10, 20, 180, 90 }, note.Rectangle);
        var updated = await PdfOperations.EditFreeTextAnnotationsAsync(added.PdfBytes,
            [new PdfFreeTextMutation.Update(note.Id, [20, 30, 190, 100], "updated", "/Helv 10 Tf 1 0 0 rg", PdfFreeTextAlignment.Right)]);
        Assert.True(updated.PdfBytes.AsSpan().StartsWith(added.PdfBytes));
        var listed = Assert.Single(await PdfOperations.GetFreeTextAnnotationsAsync(updated.PdfBytes));
        Assert.Equal(note.Id, listed.Id);
        Assert.Equal("updated", listed.Contents);
        Assert.Equal("/Helv 10 Tf 1 0 0 rg", listed.DefaultAppearance);
        Assert.Equal(PdfFreeTextAlignment.Right, listed.Alignment);
        Assert.Equal(new double[] { 20, 30, 190, 100 }, listed.Rectangle);
        var removed = await PdfOperations.EditFreeTextAnnotationsAsync(updated.PdfBytes,
            [new PdfFreeTextMutation.Remove(note.Id)]);
        Assert.True(removed.PdfBytes.AsSpan().StartsWith(updated.PdfBytes));
        Assert.Empty(removed.Annotations);
        Assert.Empty(await PdfOperations.GetFreeTextAnnotationsAsync(removed.PdfBytes));
    }

    [Fact]
    public async Task FreeTextEdits_RetainOtherAnnotationTypes()
    {
        var highlighted = await PdfOperations.EditHighlightsAsync(BlankPdf(),
            [new PdfHighlightMutation.Add(0, [[10, 40, 80, 40, 10, 20, 80, 20]], [1, 1, 0])]);
        var result = await PdfOperations.EditFreeTextAnnotationsAsync(highlighted.PdfBytes,
            [new PdfFreeTextMutation.Add(0, [10, 60, 180, 110], "note")]);
        Assert.Single(result.Annotations);
        Assert.Equal(Assert.Single(highlighted.Highlights).Id,
            Assert.Single(await PdfOperations.GetHighlightsAsync(result.PdfBytes)).Id);
    }

    [Fact]
    public async Task InvalidFieldsAndBatchConflictsAreRejected()
    {
        var pdf = BlankPdf();
        await Assert.ThrowsAsync<ArgumentException>(() => PdfOperations.EditFreeTextAnnotationsAsync(pdf,
            [new PdfFreeTextMutation.Add(0, [0, 0, 0, 0], "text")]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => PdfOperations.EditFreeTextAnnotationsAsync(pdf,
            [new PdfFreeTextMutation.Add(0, [0, 0, 100, 100], "text", Alignment: (PdfFreeTextAlignment)99)]));
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfOperations.EditFreeTextAnnotationsAsync(pdf,
            [new PdfFreeTextMutation.Add(0, [0, 0, 100, 100], "text", "not a PDF appearance")]));
        var added = await PdfOperations.EditFreeTextAnnotationsAsync(pdf,
            [new PdfFreeTextMutation.Add(0, [0, 0, 100, 100], "text")]);
        var id = Assert.Single(added.Annotations).Id;
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfOperations.EditFreeTextAnnotationsAsync(added.PdfBytes,
            [new PdfFreeTextMutation.Remove(id), new PdfFreeTextMutation.Remove(id)]));
        Assert.Single(await PdfOperations.GetFreeTextAnnotationsAsync(added.PdfBytes));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public async Task DocMdpPolicy_OnlyLevelThreeAllowsAnnotationChanges(int permission, bool allowed)
    {
        var source = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory,
            "fixtures", "signatures", "docmdp_p2_rsa.pdf"));
        // Synthetic policy variants: changing P invalidates the original signature.
        // This test checks permission parsing, not cryptographic signature validity.
        var marker = System.Text.Encoding.ASCII.GetBytes("/P 2");
        var offset = source.AsSpan().IndexOf(marker);
        Assert.True(offset >= 0);
        source[offset + 3] = (byte)('0' + permission);
        var mutation = new PdfFreeTextMutation.Add(0, [10, 20, 180, 90], "policy test");
        if (allowed)
        {
            var result = await PdfOperations.EditFreeTextAnnotationsAsync(source, [mutation]);
            Assert.True(result.PdfBytes.AsSpan().StartsWith(source));
            Assert.Contains(result.Annotations, annotation => annotation.Contents == "policy test");
        }
        else
        {
            var error = await Assert.ThrowsAsync<PdfExtractionException>(() =>
                PdfOperations.EditFreeTextAnnotationsAsync(source, [mutation]));
            Assert.Contains("DocMDP", error.Message);
        }
    }

    [Fact]
    public async Task EncryptedDocumentsAreRejected()
    {
        var pdf = BlankPdf(encrypt: true);
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfOperations.GetFreeTextAnnotationsAsync(pdf));
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfOperations.EditFreeTextAnnotationsAsync(pdf,
            [new PdfFreeTextMutation.Add(0, [0, 0, 100, 100], "text")]));
    }
}
