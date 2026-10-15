using System.Text;
using System.Text.Json;

namespace OxidizePdf.NET.Tests;

public class TaggedPreparationTests
{
    // Independent classic-xref fixture: authoritative content/ownership remains
    // intact while derived ParentTree, IDTree and page keys can be absent.
    internal static byte[] Pdf(bool indexes = false, bool language = false, bool alt = false,
        bool ambiguous = false, bool retired = true, bool streamXref = false, ushort retiredGeneration = 0)
    {
        const string content = "/P << /MCID 0 >> BDC BT /F1 12 Tf 20 100 Td (Original content) Tj ET EMC /Figure << /MCID 1 >> BDC EMC";
        var objects = new Dictionary<int, string>
        {
            [1] = $"<< /Type /Catalog /Pages 2 0 R /StructTreeRoot 5 0 R /MarkInfo << /Marked true >> {(language ? "/Lang (en-US)" : "")} /Private (keep-catalog) >>",
            [2] = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            [3] = $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] /Contents 4 0 R /Resources << /Font << /F1 9 0 R >> >> {(indexes ? "/StructParents 0" : "")} >>",
            [4] = $"<< /Length {content.Length} >>\nstream\n{content}\nendstream",
            [5] = $"<< /Type /StructTreeRoot /K [6 0 R 8 0 R {(ambiguous ? "12 0 R" : "")}] /ParentTree 7 0 R /IDTree 10 0 R /ParentTreeNextKey 1 >>",
            [6] = "<< /Type /StructElem /S /P /P 5 0 R /Pg 3 0 R /K 0 /ID (paragraph) /Private (keep-element) >>",
            [8] = $"<< /Type /StructElem /S /Figure /P 5 0 R /Pg 3 0 R /K 1 /ID (figure) {(alt ? "/Alt (Existing description)" : "")} >>",
            [9] = "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        };
        if (indexes)
        {
            objects[7] = "<< /Nums [0 [6 0 R 8 0 R]] >>";
            objects[10] = "<< /Names [(figure) 8 0 R (paragraph) 6 0 R] >>";
        }
        if (ambiguous) objects[12] = "<< /Type /StructElem /S /P /P 5 0 R /Pg 3 0 R /K 0 >>";
        using var output = new MemoryStream();
        void Write(string value) => output.Write(Encoding.ASCII.GetBytes(value));
        Write("%PDF-1.7\n");
        int max = ambiguous ? 12 : retired ? 11 : 10;
        var offsets = new long[max + 2];
        foreach (var entry in objects.OrderBy(p => p.Key))
        {
            offsets[entry.Key] = output.Position;
            Write($"{entry.Key} 0 obj\n{entry.Value}\nendobj\n");
        }
        var xref = output.Position;
        if (streamXref)
        {
            int id = max + 1;
            offsets[id] = xref;
            Write($"{id} 0 obj\n<< /Type /XRef /Size {id + 1} /Root 1 0 R /W [1 8 2] /Length {(id + 1) * 11} >>\nstream\n");
            for (int i = 0; i <= id; i++)
            {
                output.WriteByte((byte)(i == 0 ? 0 : 1));
                for (int shift = 56; shift >= 0; shift -= 8) output.WriteByte((byte)(offsets[i] >> shift));
                ushort generation = i == 0 ? ushort.MaxValue : i == 11 ? retiredGeneration : (ushort)0;
                output.WriteByte((byte)(generation >> 8));
                output.WriteByte((byte)generation);
            }
            Write($"\nendstream\nendobj\nstartxref\n{xref}\n%%EOF\n");
        }
        else
        {
            Write($"xref\n0 {max + 1}\n0000000000 65535 f \n");
            for (int i = 1; i <= max; i++) Write($"{offsets[i]:D10} {(i == 11 ? retiredGeneration : 0):D5} n \n");
            Write($"trailer\n<< /Size {max + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        }
        return output.ToArray();
    }

    private static PdfTaggedMutation[] Description(string value = "Descripción 東京") =>
        [new PdfTaggedMutation.SetElementAttribute(new(8, 0), "Alt", new("Text", value))];
    private static PdfTaggedPreparationOptions Both => new() { RecoverIndexes = true, DocumentLanguage = "es-ES" };

    [Fact]
    public async Task PreflightRecoversDerivedIndexesButReportsMissingAuthorMetadata()
    {
        var pdf = Pdf();
        var original = pdf.ToArray();
        var report = await PdfTaggedOperations.PreflightAsync(pdf);
        Assert.False(report.Valid);
        Assert.Equal(1u, report.ParentTreeEntries);
        Assert.Equal(new[] { "MissingAlternateText", "MissingDocumentLanguage" }, report.Findings.Select(f => f.Code).Order().ToArray());
        Assert.Equal(new PdfTaggedObjectId(8, 0), Assert.Single(report.Findings, f => f.Code == "MissingAlternateText").Object);
        Assert.Equal(original, pdf);
        Assert.True((await PdfTaggedOperations.PreflightAsync(Pdf(language: true, alt: true))).Valid);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompletePreparationInventoryIsSeparateFromMutationPlan(bool streamXref)
    {
        var pdf = Pdf(streamXref: streamXref);
        var planned = await PdfTaggedOperations.PlanPreparedAsync(pdf, Description(), Both);
        var result = await PdfTaggedOperations.EditPreparedAsync(pdf, Description(), Both);
        Assert.Equal(planned.PreparationObjects, result.PreparationObjects);
        uint[] expected = streamXref ? [1, 3, 5, 7, 10, 11, 13, 14] : [1, 3, 5, 7, 10, 11];
        Assert.Equal(expected, result.PreparationObjects.Select(o => o.ObjectNumber));
        Assert.All(result.PreparationObjects, o => Assert.Equal((ushort)0, o.Generation));
        Assert.Equal(JsonSerializer.Serialize(planned.MutationPlan), JsonSerializer.Serialize(result.Edit.Plan));
        Assert.Equal(streamXref ? new uint[] { 8, 15 } : [8],
            result.Edit.Plan.ChangedObjects.Select(o => o.Object.ObjectNumber));
        Assert.DoesNotContain(new PdfTaggedObjectId(8, 0), result.PreparationObjects);
        Assert.True(result.Edit.PdfBytes.AsSpan().StartsWith(pdf));
        Assert.True(result.Edit.ValidationAfter.Valid);
        Assert.Equal("MissingAlternateText", Assert.Single(result.Edit.ValidationBefore.Findings).Code);
        Assert.Equal("Descripción 東京", result.Edit.ValidationAfter.Elements.Single(e => e.Object.ObjectNumber == 8).AlternateText);
        Assert.Contains("Original content", await new PdfExtractor().ExtractTextAsync(result.Edit.PdfBytes));
        var privateValue = result.Edit.ValidationAfter.Elements.Single(e => e.Object.ObjectNumber == 6).Dictionary["Private"];
        Assert.Equal("keep-element", Encoding.ASCII.GetString(Convert.FromBase64String(privateValue.GetProperty("value").GetString()!)));
        var repeated = await PdfTaggedOperations.EditPreparedAsync(result.Edit.PdfBytes, [], new() { RecoverIndexes = true });
        Assert.DoesNotContain(new PdfTaggedObjectId(11, 0), repeated.PreparationObjects);
        Assert.True(repeated.Edit.PdfBytes.AsSpan().StartsWith(result.Edit.PdfBytes));
    }

    [Fact]
    public async Task PreparedApplyRejectsIncompleteFinalDocumentIncludingNoopMutations()
    {
        var source = Pdf();
        var plan = await PdfTaggedOperations.PlanPreparedAsync(source, [], Both);
        Assert.Empty(plan.MutationPlan.ChangedObjects);
        var error = await Assert.ThrowsAsync<PdfTaggedException>(() => PdfTaggedOperations.EditPreparedAsync(source, [], Both));
        Assert.Equal(PdfTaggedError.InvalidInputOrStructure, error.Error);
        Assert.Contains("MissingAlternateText", error.Message);
        await Assert.ThrowsAsync<PdfTaggedException>(() => PdfTaggedOperations.EditPreparedAsync(source, Description(), new() { RecoverIndexes = true }));
        // Ordinary editing deliberately retains its diagnostic behavior.
        var ordinary = await PdfTaggedOperations.EditAsync(Pdf(indexes: true, retired: false), []);
        Assert.False(ordinary.ValidationAfter.Valid);
    }

    [Fact]
    public async Task RecoveryEnablesFinalValidationEvenWhenIndexesAlreadyExist()
    {
        var completeIndexes = Pdf(indexes: true, language: true, retired: false);
        var ordinary = await PdfTaggedOperations.EditAsync(completeIndexes, []);
        Assert.False(ordinary.ValidationAfter.Valid);
        var error = await Assert.ThrowsAsync<PdfTaggedException>(() => PdfTaggedOperations.EditPreparedAsync(
            completeIndexes, [], new() { RecoverIndexes = true }));
        Assert.Contains("MissingAlternateText", error.Message);
    }

    [Fact]
    public async Task PreparationStepsAreExplicitAndLanguageCannotOverwriteExistingValues()
    {
        var source = Pdf(indexes: true, alt: true, retired: false);
        var result = await PdfTaggedOperations.EditPreparedAsync(source, [], new() { DocumentLanguage = "es-ES" });
        Assert.Equal(new[] { new PdfTaggedObjectId(1, 0) }, result.PreparationObjects);
        Assert.True(result.Edit.ValidationAfter.Valid);
        Assert.Empty(result.Edit.Plan.ChangedObjects);
        await Assert.ThrowsAsync<PdfTaggedException>(() => PdfTaggedOperations.EditPreparedAsync(result.Edit.PdfBytes, [], new() { DocumentLanguage = "fr-FR" }));
        await Assert.ThrowsAsync<PdfTaggedException>(() => PdfTaggedOperations.EditPreparedAsync(Pdf(indexes: true, language: true, alt: true), Description(), new() { RecoverIndexes = true }));
        await Assert.ThrowsAsync<ArgumentException>(() => PdfTaggedOperations.EditPreparedAsync(source, [], new()));
        await Assert.ThrowsAsync<ArgumentException>(() => PdfTaggedOperations.EditPreparedAsync(source, [], new() { DocumentLanguage = " " }));
    }

    [Fact]
    public async Task AmbiguousOwnershipAndResourceLimitsAreRejected()
    {
        await Assert.ThrowsAsync<PdfTaggedException>(() => PdfTaggedOperations.PreflightAsync(Pdf(ambiguous: true)));
        await Assert.ThrowsAsync<PdfTaggedException>(() => PdfTaggedOperations.EditPreparedAsync(Pdf(ambiguous: true), Description(), Both));
        await Assert.ThrowsAsync<PdfTaggedException>(() => PdfTaggedOperations.PreflightAsync(Pdf(), new() { MaxObjects = 1 }));
        await Assert.ThrowsAsync<PdfTaggedException>(() => PdfTaggedOperations.EditPreparedAsync(Pdf(), Description(), Both, new() { MaxDepth = 1 }));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task CertificationRejectsPreflightAndPreparation(byte permission)
    {
        using var prepared = await PdfSigning.PrepareAsync(Pdf(indexes: true, language: true, alt: true, retired: false), new() { Certification = permission });
        var source = prepared.Complete([0x30, 3, 2, 1, 1]);
        var preflight = await Assert.ThrowsAsync<PdfTaggedException>(() => PdfTaggedOperations.PreflightAsync(source));
        Assert.Equal(PdfTaggedError.PermissionDenied, preflight.Error);
        var apply = await Assert.ThrowsAsync<PdfTaggedException>(() => PdfTaggedOperations.EditPreparedAsync(source, [], new() { RecoverIndexes = true }));
        Assert.Equal(PdfTaggedError.PermissionDenied, apply.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetiredInventoryPreservesOriginalGeneration(bool streamXref)
    {
        var result = await PdfTaggedOperations.EditPreparedAsync(
            Pdf(language: true, alt: true, streamXref: streamXref, retiredGeneration: 3), [], new() { RecoverIndexes = true });
        Assert.Contains(new PdfTaggedObjectId(11, 3), result.PreparationObjects);
        Assert.DoesNotContain(new PdfTaggedObjectId(11, 0), result.PreparationObjects);
        Assert.True(result.Edit.ValidationAfter.Valid);
    }

    [Fact]
    public async Task EncryptionAndInvalidDescriptionEditsAreRejected()
    {
        using var document = new PdfDocument();
        using var page = PdfPage.A4();
        document.AddPage(page);
        document.Encrypt("user", "owner");
        var encrypted = document.SaveToBytes();
        var error = await Assert.ThrowsAsync<PdfTaggedException>(() => PdfTaggedOperations.PreflightAsync(encrypted));
        Assert.Equal(PdfTaggedError.PermissionDenied, error.Error);
        await Assert.ThrowsAsync<PdfTaggedException>(() => PdfTaggedOperations.EditPreparedAsync(encrypted, [], new() { DocumentLanguage = "en" }));
        await Assert.ThrowsAsync<PdfTaggedException>(() => PdfTaggedOperations.EditPreparedAsync(Pdf(), Description(" "), Both));
        await Assert.ThrowsAsync<PdfTaggedException>(() => PdfTaggedOperations.EditPreparedAsync(Pdf(),
            [new PdfTaggedMutation.SetElementAttribute(new(6, 0), "K", new("Null"))], Both));
    }

    [Fact]
    public async Task InputsAreSnapshottedAndFailuresDoNotPoisonLaterCalls()
    {
        var pdf = Pdf();
        var original = pdf.ToArray();
        var edits = Description();
        var limits = new PdfTaggedLimits();
        var pending = PdfTaggedOperations.EditPreparedAsync(pdf, edits, Both, limits);
        Array.Clear(pdf);
        edits[0] = new PdfTaggedMutation.SetElementAttribute(new(999, 0), "Alt", new("Text", "wrong"));
        limits.MaxObjects = 1;
        var result = await pending;
        Assert.True(result.Edit.PdfBytes.AsSpan().StartsWith(original));
        await Assert.ThrowsAsync<PdfTaggedException>(() => PdfTaggedOperations.PreflightAsync([1, 2, 3]));
        Assert.True((await PdfTaggedOperations.PreflightAsync(Pdf(language: true, alt: true))).Valid);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfTaggedOperations.PreflightAsync(original, ct: cts.Token));
    }
}
