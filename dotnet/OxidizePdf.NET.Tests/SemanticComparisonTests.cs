namespace OxidizePdf.NET.Tests;

public class SemanticComparisonTests
{
    private static byte[] Pdf(string text = "Hello", string title = "Original", bool encrypt = false)
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.SetFont(StandardFont.Helvetica, 12).TextAt(50, 700, text);
        doc.AddPage(page).SetTitle(title);
        if (encrypt) doc.Encrypt("user", "owner");
        return doc.SaveToBytes();
    }

    [Fact]
    public async Task IdenticalDocumentReportsEquivalentObjectsAndOneRevision()
    {
        var pdf = Pdf();
        var r = await PdfSemanticComparison.CompareAsync(pdf, pdf);
        Assert.True(r.SemanticallyEqual);
        Assert.Empty(r.Differences);
        Assert.NotEmpty(r.EquivalentObjects);
        Assert.All(r.EquivalentObjects, pair => Assert.Equal(pair.Left, pair.Right));
        var revision = Assert.Single(r.LeftRevisions);
        Assert.Equal(0u, revision.Index);
        Assert.Equal(64, revision.SemanticFingerprint.Length);
        Assert.All(revision.ObjectChanges, change => Assert.Equal("Added", change.Kind));
        Assert.Equal(revision.SemanticFingerprint, Assert.Single(r.RightRevisions).SemanticFingerprint);
    }

    [Fact]
    public async Task TextAndMetadataChangesAreClassified()
    {
        var r = await PdfSemanticComparison.CompareAsync(Pdf(), Pdf("Changed", "New title"));
        Assert.False(r.SemanticallyEqual);
        Assert.Contains(r.Differences, d => d.Class == "Textual" && d.Path == "/Pages/Text");
        Assert.Contains(r.Differences, d => d.Class == "Metadata" && d.Path == "/Trailer/Info");
        Assert.Equal(r.Differences.Select(d => d.Path).Order(StringComparer.Ordinal), r.Differences.Select(d => d.Path));
    }

    [Fact]
    public async Task AnnotationRevisionHasAddedAndReplacedObjects()
    {
        var original = Pdf();
        var edited = await PdfOperations.EditFreeTextAnnotationsAsync(original,
            [new PdfFreeTextMutation.Add(0, [10, 10, 200, 60], "Review")]);
        var r = await PdfSemanticComparison.CompareAsync(original, edited.PdfBytes);
        Assert.False(r.SemanticallyEqual);
        Assert.Single(r.LeftRevisions);
        Assert.Equal(2, r.RightRevisions.Length);
        Assert.Equal(r.LeftRevisions[0].SemanticFingerprint, r.RightRevisions[0].SemanticFingerprint);
        var latest = r.RightRevisions[1];
        Assert.Equal(1u, latest.Index);
        Assert.True(latest.XrefOffset > r.RightRevisions[0].XrefOffset);
        Assert.Contains(latest.ObjectChanges, c => c.Kind == "Added");
        Assert.Contains(latest.ObjectChanges, c => c.Kind == "Replaced");
        Assert.Contains(r.Differences, d => d.Path == "/Revisions");
    }

    [Fact]
    public async Task TrailingWhitespaceIsOnlySerializationChange()
    {
        var original = Pdf();
        var r = await PdfSemanticComparison.CompareAsync(original, [.. original, 10, 32, 10]);
        Assert.True(r.SemanticallyEqual);
        Assert.Equal("SerializationOnly", Assert.Single(r.Differences).Class);
    }

    [Fact]
    public async Task RevisionAndTextBudgetsAreEnforced()
    {
        var original = Pdf();
        var edited = await PdfOperations.EditFreeTextAnnotationsAsync(original,
            [new PdfFreeTextMutation.Add(0, [10, 10, 200, 60], "Review")]);
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfSemanticComparison.CompareAsync(original,
            edited.PdfBytes, new() { MaxRevisions = 1 }));
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfSemanticComparison.CompareAsync(original,
            original, new() { MaxExtractedTextBytes = 1 }));
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfSemanticComparison.CompareAsync(original,
            original, new() { MaxDecodedStreamBytes = 1 }));
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfSemanticComparison.CompareAsync(original,
            original, new() { MaxDepth = 1 }));
    }

    [Fact]
    public async Task LimitsAndEncryptedInputsFailExplicitly()
    {
        var pdf = Pdf();
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfSemanticComparison.CompareAsync(pdf, pdf, new() { MaxObjects = 1 }));
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfSemanticComparison.CompareAsync(pdf, pdf, new() { MaxCanonicalBytes = 1 }));
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfSemanticComparison.CompareAsync(pdf, Pdf(encrypt: true)));
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfSemanticComparison.CompareAsync(pdf, [1, 2, 3]));
    }
}
