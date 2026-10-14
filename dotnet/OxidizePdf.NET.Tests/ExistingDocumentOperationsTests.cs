using System.Text.Json;

namespace OxidizePdf.NET.Tests;

public class ExistingDocumentOperationsTests
{
    private static byte[] Pdf(string[] labels, bool outline = false, bool encrypt = false)
    {
        using var doc = new PdfDocument();
        doc.SetTitle("original title");
        foreach (var label in labels)
        {
            using var page = PdfPage.A4();
            page.SetFont(StandardFont.Helvetica, 12).TextAt(60, 700, label);
            doc.AddPage(page);
        }
        if (outline)
        {
            var tree = new PdfOutline(); tree.AddItem(new PdfOutlineItem("bookmark", 0)); doc.SetOutline(tree);
        }
        if (encrypt) doc.Encrypt("user", "owner");
        return doc.SaveToBytes();
    }

    [Fact]
    public async Task PreservingMerge_PlanMatchesExecutionAndKeepsBookmarks()
    {
        var first = Pdf(["One", "Two"], outline: true);
        PdfExistingMergeInput[] inputs = [new(first), new(Pdf(["Three"]))];
        var plan = await PdfExistingDocumentOperations.PlanMergeAsync(inputs, PdfExistingDocumentPolicy.PreserveBase);
        Assert.Empty(plan.Outputs);
        var result = await PdfExistingDocumentOperations.MergeAsync(inputs, PdfExistingDocumentPolicy.PreserveBase);
        var output = Assert.Single(result.Outputs);
        Assert.True(output.AsSpan().StartsWith(first));
        Assert.Equal(JsonSerializer.Serialize(plan.Reports), JsonSerializer.Serialize(result.Reports));
        var report = Assert.Single(result.Reports);
        Assert.Equal("PreserveBase", report.Engine);
        Assert.Equal(3, report.PageCount);
        Assert.Equal(new[] { 0, 1 }, report.Inputs.Select(i => i.InputIndex));
        Assert.Contains(report.Inputs[0].Structures, s => s.Structure == "Outlines" && s.Disposition == "Preserved");
        Assert.Single(await new PdfExtractor().GetBookmarksAsync(output));
        Assert.Equal(3, await new PdfExtractor().GetPageCountAsync(output));
    }

    [Fact]
    public async Task SecondaryOutlinesAreRejectedUnlessReconstructionIsExplicit()
    {
        PdfExistingMergeInput[] inputs = [new(Pdf(["base"])), new(Pdf(["secondary"], outline: true))];
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfExistingDocumentOperations.PlanMergeAsync(inputs, PdfExistingDocumentPolicy.PreserveBase));
        var result = await PdfExistingDocumentOperations.MergeAsync(inputs, PdfExistingDocumentPolicy.Reconstruct);
        var report = Assert.Single(result.Reports);
        Assert.Equal("Reconstruct", report.Engine);
        Assert.Null(report.Mutation);
        Assert.Contains(report.Inputs[1].Structures, s => s.Structure == "Outlines" && s.Disposition == "Discarded");
        Assert.Empty(await new PdfExtractor().GetBookmarksAsync(Assert.Single(result.Outputs)));
    }

    [Theory]
    [InlineData(PdfExistingDocumentPolicy.PreserveBase)]
    [InlineData(PdfExistingDocumentPolicy.Reconstruct)]
    public async Task ExtractionAndSplit_ReturnReportsAndRequestedPageOrder(PdfExistingDocumentPolicy policy)
    {
        var pdf = Pdf(["One", "Two", "Three"]);
        var plan = await PdfExistingDocumentOperations.PlanExtractAsync(pdf, [2, 0], policy);
        var extracted = await PdfExistingDocumentOperations.ExtractAsync(pdf, [2, 0], policy);
        Assert.Equal(JsonSerializer.Serialize(plan.Reports), JsonSerializer.Serialize(extracted.Reports));
        var bytes = Assert.Single(extracted.Outputs);
        if (policy == PdfExistingDocumentPolicy.PreserveBase) Assert.True(bytes.AsSpan().StartsWith(pdf));
        var extractor = new PdfExtractor();
        Assert.Contains("Three", await extractor.ExtractTextFromPageAsync(bytes, 1));
        Assert.Contains("One", await extractor.ExtractTextFromPageAsync(bytes, 2));
        int[][] groups = [[0, 1], [2]];
        var splitPlan = await PdfExistingDocumentOperations.PlanSplitAsync(pdf, groups, policy);
        var split = await PdfExistingDocumentOperations.SplitAsync(pdf, groups, policy);
        Assert.Equal(JsonSerializer.Serialize(splitPlan.Reports), JsonSerializer.Serialize(split.Reports));
        Assert.Equal(2, split.Outputs.Length);
        Assert.Equal(2, await extractor.GetPageCountAsync(split.Outputs[0]));
        Assert.Contains("Three", await extractor.ExtractTextFromPageAsync(split.Outputs[1], 1));
    }

    [Fact]
    public async Task AtomicPageBatch_PlansAndExecutesEveryMutationType()
    {
        var pdf = Pdf(["One", "Two", "Three"]);
        PdfPageMutation[] mutations = [new PdfPageMutation.Move(0, 2), new PdfPageMutation.Rotate(0, 90),
            new PdfPageMutation.Duplicate(1, 3), new PdfPageMutation.Insert(Pdf(["Four"]), 0, 1), new PdfPageMutation.Delete(2)];
        var plan = await PdfExistingDocumentOperations.PlanMutationsAsync(pdf, mutations);
        Assert.Empty(plan.Outputs);
        var result = await PdfExistingDocumentOperations.MutateAsync(pdf, mutations);
        Assert.Equal(JsonSerializer.Serialize(plan.Mutation), JsonSerializer.Serialize(result.Mutation));
        Assert.Equal(4, result.Mutation!.PageCount);
        Assert.NotEmpty(result.Mutation.AddedObjects);
        Assert.NotEmpty(result.Mutation.ReplacedObjects);
        var output = Assert.Single(result.Outputs);
        Assert.True(output.AsSpan().StartsWith(pdf));
        var extractor = new PdfExtractor();
        var expected = new[] { "Two", "Four", "One", "Three" };
        for (var i = 0; i < expected.Length; i++) Assert.Contains(expected[i], await extractor.ExtractTextFromPageAsync(output, i + 1));
    }

    [Fact]
    public async Task ReorderRequiresExactPermutationAndPreservesPrefix()
    {
        var pdf = Pdf(["One", "Two", "Three"]);
        var plan = await PdfExistingDocumentOperations.PlanReorderAsync(pdf, [2, 0, 1]);
        var result = await PdfExistingDocumentOperations.ReorderAsync(pdf, [2, 0, 1]);
        Assert.Equal(JsonSerializer.Serialize(plan.Mutation), JsonSerializer.Serialize(result.Mutation));
        Assert.True(result.Outputs[0].AsSpan().StartsWith(pdf));
        Assert.Contains("Three", await new PdfExtractor().ExtractTextFromPageAsync(result.Outputs[0], 1));
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfExistingDocumentOperations.ReorderAsync(pdf, [0, 0, 2]));
    }

    [Fact]
    public async Task IdentityReorderIsValid()
    {
        var pdf = Pdf(["One", "Two"]);
        var plan = await PdfExistingDocumentOperations.PlanReorderAsync(pdf, [0, 1]);
        var result = await PdfExistingDocumentOperations.ReorderAsync(pdf, [0, 1]);
        Assert.Equal(2, plan.Mutation!.PageCount);
        Assert.True(result.Outputs[0].AsSpan().StartsWith(pdf));
        Assert.Contains("One", await new PdfExtractor().ExtractTextFromPageAsync(result.Outputs[0], 1));
    }

    [Theory]
    [InlineData("merge")]
    [InlineData("extract")]
    [InlineData("split")]
    public async Task ReconstructionMetadataPolicyMatchesOutput(string operation)
    {
        var pdf = Pdf(["One"]);
        foreach (var policy in new[] { PdfExistingDocumentPolicy.Reconstruct, PdfExistingDocumentPolicy.ReconstructWithFirstInputMetadata })
        {
            var result = operation switch
            {
                "merge" => await PdfExistingDocumentOperations.MergeAsync([new PdfExistingMergeInput(pdf)], policy),
                "extract" => await PdfExistingDocumentOperations.ExtractAsync(pdf, [0], policy),
                _ => await PdfExistingDocumentOperations.SplitAsync(pdf, new int[][] { [0] }, policy)
            };
            var metadata = await new PdfExtractor().ExtractMetadataAsync(Assert.Single(result.Outputs));
            if (policy == PdfExistingDocumentPolicy.ReconstructWithFirstInputMetadata) Assert.Equal("original title", metadata.Title);
            else Assert.Null(metadata.Title);
            var disposition = policy == PdfExistingDocumentPolicy.Reconstruct ? "Discarded" : "FirstInputWins";
            Assert.Contains(Assert.Single(result.Reports).Inputs[0].Structures,
                s => s.Structure == "DocumentInfo" && s.Disposition == disposition);
        }
    }

    [Fact]
    public async Task SecondaryPageLabelsRequireExplicitFirstInputPolicy()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4(); doc.AddPage(page);
        doc.SetPageLabels(PdfPageLabels.Create().AddRange(0, PdfPageLabelStyle.LowercaseRoman));
        PdfExistingMergeInput[] inputs = [new(Pdf(["base"])), new(doc.SaveToBytes())];
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfExistingDocumentOperations.PlanMergeAsync(inputs, PdfExistingDocumentPolicy.PreserveBase));
        var result = await PdfExistingDocumentOperations.MergeAsync(inputs, PdfExistingDocumentPolicy.PreserveBaseWithFirstInputPageLabels);
        Assert.Contains(Assert.Single(result.Reports).Inputs[1].Structures,
            s => s.Structure == "PageLabels" && s.Disposition == "FirstInputWins");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task CertifiedDocumentsRejectStructuralMutations(int permission)
    {
        var pdf = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "fixtures", "signatures", "docmdp_p2_rsa.pdf"));
        // Synthetic policy variants, not cryptographic signature validation.
        var offset = pdf.AsSpan().IndexOf(System.Text.Encoding.ASCII.GetBytes("/P 2"));
        Assert.True(offset >= 0);
        pdf[offset + 3] = (byte)('0' + permission);
        var error = await Assert.ThrowsAsync<PdfExtractionException>(() => PdfExistingDocumentOperations.PlanMutationsAsync(pdf,
            [new PdfPageMutation.Rotate(0, 90)]));
        Assert.Contains("DocMDP", error.Message);
    }

    [Fact]
    public async Task EncryptedInputAndInvalidPageMutationsAreRejected()
    {
        var encrypted = Pdf(["secret"], encrypt: true);
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfExistingDocumentOperations.PlanExtractAsync(encrypted, [0], PdfExistingDocumentPolicy.PreserveBase));
        var pdf = Pdf(["page"]);
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfExistingDocumentOperations.MutateAsync(pdf, [new PdfPageMutation.Delete(9)]));
        await Assert.ThrowsAsync<ArgumentException>(() => PdfExistingDocumentOperations.MutateAsync(pdf, [new PdfPageMutation.Rotate(0, 45)]));
    }
}
