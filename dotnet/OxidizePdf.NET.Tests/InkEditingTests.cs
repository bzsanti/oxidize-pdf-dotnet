namespace OxidizePdf.NET.Tests;

public class InkEditingTests
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
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task Lifecycle_PreservesColorSpaceStrokesAndIdentity(int components)
    {
        var pdf = BlankPdf();
        var color = Enumerable.Repeat(0.5, components).ToArray();
        Assert.Empty(await PdfOperations.GetInkAnnotationsAsync(pdf));
        var added = await PdfOperations.EditInkAnnotationsAsync(pdf,
            [new PdfInkMutation.Add(0, [[10, 20, 30, 40], [50, 60, 70, 80]], color, 2, 0.6)]);
        Assert.True(added.PdfBytes.AsSpan().StartsWith(pdf));
        var ink = Assert.Single(added.Annotations);
        Assert.Equal(color, ink.Color);
        Assert.Equal(2, ink.Width);
        Assert.Equal(0.6, ink.Opacity);
        Assert.Equal(2, ink.Strokes.Length);
        Assert.Equal(new double[] { 10, 20, 30, 40 }, ink.Strokes[0]);
        Assert.Equal(new double[] { 9, 19, 71, 81 }, ink.Rectangle);
        var updated = await PdfOperations.EditInkAnnotationsAsync(added.PdfBytes,
            [new PdfInkMutation.Update(ink.Id, [[20, 30, 40, 50]], [0, 0, 1], 3)]);
        Assert.True(updated.PdfBytes.AsSpan().StartsWith(added.PdfBytes));
        var current = Assert.Single(await PdfOperations.GetInkAnnotationsAsync(updated.PdfBytes));
        Assert.Equal(ink.Id, current.Id);
        Assert.Equal(new double[] { 0, 0, 1 }, current.Color);
        Assert.Equal(3, current.Width);
        Assert.Null(current.Opacity);
        Assert.Equal(new double[] { 20, 30, 40, 50 }, Assert.Single(current.Strokes));
        var removed = await PdfOperations.EditInkAnnotationsAsync(updated.PdfBytes,
            [new PdfInkMutation.Remove(ink.Id)]);
        Assert.True(removed.PdfBytes.AsSpan().StartsWith(updated.PdfBytes));
        Assert.Empty(removed.Annotations);
        Assert.Empty(await PdfOperations.GetInkAnnotationsAsync(removed.PdfBytes));
    }

    [Fact]
    public async Task InvalidInputAndConflictingBatchesAreRejected()
    {
        var pdf = BlankPdf();
        await Assert.ThrowsAsync<ArgumentException>(() => PdfOperations.EditInkAnnotationsAsync(pdf,
            [new PdfInkMutation.Add(0, [[10, 20, 30]], [0])]));
        await Assert.ThrowsAsync<ArgumentException>(() => PdfOperations.EditInkAnnotationsAsync(pdf,
            [new PdfInkMutation.Add(0, [[double.NaN, 20]], [0])]));
        await Assert.ThrowsAsync<ArgumentException>(() => PdfOperations.EditInkAnnotationsAsync(pdf,
            [new PdfInkMutation.Add(0, Enumerable.Range(0, 4097).Select(_ => new double[] { 10, 20 }).ToArray(), [0])]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => PdfOperations.EditInkAnnotationsAsync(pdf,
            [new PdfInkMutation.Add(0, [[10, 20]], [0], 0)]));
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfOperations.EditInkAnnotationsAsync(pdf,
            [new PdfInkMutation.Add(9, [[10, 20]], [0])]));
        var added = await PdfOperations.EditInkAnnotationsAsync(pdf, [new PdfInkMutation.Add(0, [[10, 20]], [0])]);
        var id = Assert.Single(added.Annotations).Id;
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfOperations.EditInkAnnotationsAsync(added.PdfBytes,
            [new PdfInkMutation.Remove(id), new PdfInkMutation.Remove(id)]));
        Assert.Single(await PdfOperations.GetInkAnnotationsAsync(added.PdfBytes));
    }

    [Fact]
    public async Task RetainsFreeTextAndRejectsEncryption()
    {
        var notes = await PdfOperations.EditFreeTextAnnotationsAsync(BlankPdf(),
            [new PdfFreeTextMutation.Add(0, [10, 20, 150, 70], "keep")]);
        var result = await PdfOperations.EditInkAnnotationsAsync(notes.PdfBytes,
            [new PdfInkMutation.Add(0, [[10, 80, 50, 90]], [0])]);
        Assert.Equal(Assert.Single(notes.Annotations).Id,
            Assert.Single(await PdfOperations.GetFreeTextAnnotationsAsync(result.PdfBytes)).Id);
        var encrypted = BlankPdf(encrypt: true);
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfOperations.GetInkAnnotationsAsync(encrypted));
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfOperations.EditInkAnnotationsAsync(encrypted,
            [new PdfInkMutation.Add(0, [[10, 20]], [0])]));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public async Task DocMdpPolicy_IsEnforced(int permission, bool allowed)
    {
        var pdf = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory,
            "fixtures", "signatures", "docmdp_p2_rsa.pdf"));
        // Synthetic policy variants only; modified signatures are not asserted valid.
        var offset = pdf.AsSpan().IndexOf(System.Text.Encoding.ASCII.GetBytes("/P 2"));
        Assert.True(offset >= 0);
        pdf[offset + 3] = (byte)('0' + permission);
        var mutation = new PdfInkMutation.Add(0, [[10, 20, 30, 40]], [0]);
        if (allowed)
        {
            var result = await PdfOperations.EditInkAnnotationsAsync(pdf, [mutation]);
            Assert.True(result.PdfBytes.AsSpan().StartsWith(pdf));
            Assert.Single(result.Annotations);
        }
        else
        {
            var error = await Assert.ThrowsAsync<PdfExtractionException>(() =>
                PdfOperations.EditInkAnnotationsAsync(pdf, [mutation]));
            Assert.Contains("DocMDP", error.Message);
        }
    }
}
