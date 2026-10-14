namespace OxidizePdf.NET.Tests;

public class SignatureSlotTests
{
    private static byte[] Pdf()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        doc.AddPage(page);
        return doc.SaveToBytes();
    }

    [Fact]
    public async Task SlotLifecyclePreservesRevisionsAndSeparatesHandwritingFromCms()
    {
        var original = Pdf();
        var created = await PdfSignatureSlots.CreateAsync(original, "approval", 0, [10, 20, 200, 100], "área legal");
        Assert.True(created.AsSpan().StartsWith(original));
        var slot = Assert.Single(await PdfSignatureSlots.ListAsync(created));
        Assert.Equal("approval", slot.Name);
        Assert.Equal("área legal", slot.Metadata);
        Assert.Equal(0, slot.PageIndex);
        Assert.Equal(0, slot.Rotation);
        Assert.Equal(new double[] { 10, 20, 200, 100 }, slot.Rectangle);
        Assert.False(slot.Completed);
        Assert.False(slot.DigitallySigned);

        double[][][] strokes = [[[0.1, 0.2], [0.4, 0.8], [0.9, 0.2]]];
        var drawn = await PdfSignatureSlots.DrawAsync(created, "approval", strokes, label: "Approved");
        Assert.True(drawn.AsSpan().StartsWith(created));
        Assert.False((await PdfSignatureSlots.ReadAsync(drawn, "approval")).Completed);
        var draft = await PdfSignatureSlots.CompleteAsync(drawn, "approval", strokes, complete: false);
        Assert.False((await PdfSignatureSlots.ReadAsync(draft, "approval")).Completed);
        var labelOnly = await PdfSignatureSlots.DrawAsync(created, "approval", [], complete: true, label: "Approved");
        Assert.True((await PdfSignatureSlots.ReadAsync(labelOnly, "approval")).Completed);
        var completed = await PdfSignatureSlots.CompleteAsync(draft, "approval", strokes);
        Assert.True(completed.AsSpan().StartsWith(drawn));
        slot = await PdfSignatureSlots.ReadAsync(completed, "approval");
        Assert.True(slot.Completed);
        Assert.False(slot.DigitallySigned);
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfSignatureSlots.CompleteAsync(completed, "approval", strokes, complete: false));
        var removed = await PdfSignatureSlots.RemoveAsync(draft, "approval");
        Assert.True(removed.AsSpan().StartsWith(draft));
        Assert.Empty(await PdfSignatureSlots.ListAsync(removed));
    }

    [Fact]
    public async Task InvalidTargetsAndStrokesAreRejectedWithoutChangingInput()
    {
        var original = Pdf();
        var snapshot = (byte[])original.Clone();
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfSignatureSlots.CreateAsync(original, "bad", -1, [10, 20, 200, 100]));
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfSignatureSlots.CreateAsync(original, "bad", 0, [10, 20, 0, 0]));
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfSignatureSlots.ReadAsync(original, "absent"));
        var created = await PdfSignatureSlots.CreateAsync(original, "approval", 0, [10, 20, 200, 100]);
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfSignatureSlots.CreateAsync(created, "approval", 0, [10, 20, 200, 100]));
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfSignatureSlots.DrawAsync(created, "approval", [[[1.1, 0.2], [0.4, 0.8]]]));
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfSignatureSlots.DrawAsync(created, "approval", [[[0.1], [0.4, 0.8]]]));
        Assert.Equal(snapshot, original);
        Assert.False((await PdfSignatureSlots.ReadAsync(created, "approval")).Completed);
    }

    [Fact]
    public void PreCancelledOperationAndEmptyPdfFailBeforeNativeCall()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => { _ = PdfSignatureSlots.ListAsync(Pdf(), cts.Token); });
        Assert.Throws<ArgumentException>(() => { _ = PdfSignatureSlots.ListAsync([]); });
    }

    [Fact]
    public async Task DigitalSignatureValueCompletesSlotAndPreventsRemoval()
    {
        var created = await PdfSignatureSlots.CreateAsync(Pdf(), "approval", 0, [10, 20, 200, 100]);
        // Core application slots require an appearance before digital completion.
        var drawn = await PdfSignatureSlots.DrawAsync(created, "approval", [[[0.1, 0.2], [0.9, 0.8]]]);
        using var prepared = await PdfSigning.PrepareAsync(drawn, new() { FieldName = "approval", Existing = true });
        // Structural DER fixture only: DigitallySigned reports a stored signature
        // value, not cryptographic authenticity or certificate trust.
        var filled = prepared.Complete([0x30, 3, 2, 1, 1]);
        var slot = await PdfSignatureSlots.ReadAsync(filled, "approval");
        Assert.True(slot.DigitallySigned);
        Assert.True(slot.Completed);
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfSignatureSlots.RemoveAsync(filled, "approval"));
    }
}
