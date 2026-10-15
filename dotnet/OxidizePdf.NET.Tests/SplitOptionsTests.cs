namespace OxidizePdf.NET.Tests;

public class SplitOptionsTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task MetadataAndOptimizationFollowCoreContract(bool keep, bool optimize)
    {
        var output = await PdfOperations.SplitNamedAsync(MergeMetadataTests.Pdf("source", unicode: true),
            new() { PreserveMetadata = keep, Optimize = optimize });
        Assert.Equal(new[] { "page_1.pdf", "page_2.pdf" }, output.Select(r => r.FileName));
        var extractor = new PdfExtractor();
        for (int i = 0; i < output.Count; i++)
        {
            Assert.False(output[i].OptimizationApplied);
            var metadata = await extractor.ExtractMetadataAsync(output[i].Bytes);
            Assert.Equal(1, metadata.PageCount);
            Assert.Equal(keep ? "Título 東京 🧪 title" : null, metadata.Title);
            Assert.Equal(keep ? "Título 東京 🧪 author" : null, metadata.Author);
            Assert.Equal(keep ? "Título 東京 🧪 subject" : null, metadata.Subject);
            Assert.Equal(keep ? "Título 東京 🧪 keywords" : null, metadata.Keywords);
            Assert.Contains(i == 0 ? "source-one" : "source-two", await extractor.ExtractTextAsync(output[i].Bytes));
            Assert.DoesNotContain("source-xmp-marker", System.Text.Encoding.ASCII.GetString(output[i].Bytes));
        }
    }

    [Fact]
    public async Task RangesKeepOrderAndUseOneBasedNames()
    {
        var output = await PdfOperations.SplitNamedAsync(MergeMetadataTests.Pdf("source"), new()
        {
            Mode = PdfSplitMode.Ranges,
            Ranges = [(1, 1), (0, 1), (1, 1)],
            OutputPattern = "part-{n}-{start}-{end}-{}.pdf"
        });
        Assert.Equal(new[] { "part-1-2-2-2-2.pdf", "part-2-1-2-1-2.pdf", "part-3-2-2-2-2.pdf" }, output.Select(r => r.FileName));
        var extractor = new PdfExtractor();
        Assert.Equal(2, await extractor.GetPageCountAsync(output[1].Bytes));
        Assert.Contains("source-two", await extractor.ExtractTextAsync(output[0].Bytes));
        output[0].Bytes[0] = 0;
        Assert.Equal((byte)'%', output[2].Bytes[0]);
        var legacy = await PdfOperations.SplitAsync(MergeMetadataTests.Pdf("source"), new PdfSplitOptions
        { Mode = PdfSplitMode.Ranges, Ranges = [(1, 1), (1, 1)] });
        Assert.Equal(2, legacy.Count);
    }

    [Theory]
    [InlineData(PdfSplitMode.ChunkSize, "page_1-2.pdf", 1)]
    [InlineData(PdfSplitMode.SplitAt, "page_1-1.pdf", 2)]
    [InlineData(PdfSplitMode.SinglePages, "page_1.pdf", 2)]
    public async Task ExistingModesRetainTheirNaming(PdfSplitMode mode, string firstName, int count)
    {
        var output = await PdfOperations.SplitNamedAsync(MergeMetadataTests.Pdf("source"),
            new() { Mode = mode, ChunkSize = 2, SplitAt = [1] });
        Assert.Equal(count, output.Count);
        Assert.Equal(firstName, output[0].FileName);
    }

    [Theory]
    [InlineData("../escape-{}.pdf")]
    [InlineData("/tmp/escape-{}.pdf")]
    [InlineData("C:\\escape-{}.pdf")]
    [InlineData("{unknown}.pdf")]
    [InlineData("{start}.pdf")]
    [InlineData("fixed.pdf")]
    [InlineData("CON.pdf")]
    [InlineData("COM¹.pdf")]
    [InlineData("CON .pdf")]
    [InlineData("")]
    [InlineData("page-{}.pdf\u0000")]
    public async Task InvalidOrDuplicateNamesAreRejected(string pattern)
    {
        await Assert.ThrowsAsync<PdfSplitException>(() => PdfOperations.SplitNamedAsync(
            MergeMetadataTests.Pdf("source"), new() { OutputPattern = pattern }));
    }

    [Fact]
    public async Task LegacyOptionsHonorMetadataAndMissingInfo()
    {
        var extractor = new PdfExtractor();
        var source = MergeMetadataTests.Pdf("source");
        var defaultOutput = await PdfOperations.SplitAsync(source, new PdfSplitOptions());
        var discarded = await PdfOperations.SplitAsync(source, new PdfSplitOptions { PreserveMetadata = false });
        Assert.Equal("source-title", (await extractor.ExtractMetadataAsync(defaultOutput[0])).Title);
        Assert.Null((await extractor.ExtractMetadataAsync(discarded[0])).Title);
        var absent = await PdfOperations.SplitNamedAsync(MergeMetadataTests.Pdf("source", info: false));
        Assert.Null((await extractor.ExtractMetadataAsync(absent[0].Bytes)).Title);
        var constant = await PdfOperations.SplitNamedAsync(source, new() { Mode = PdfSplitMode.ChunkSize, ChunkSize = 99, OutputPattern = "single.pdf" });
        Assert.Equal("single.pdf", Assert.Single(constant).FileName);
        Assert.Equal(2, await extractor.GetPageCountAsync(constant[0].Bytes));
    }

    [Fact]
    public async Task FailuresAndSnapshotsCrossRealFfi()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => PdfOperations.SplitAsync([1], new PdfSplitOptions { Mode = PdfSplitMode.ChunkSize, ChunkSize = 0 }));
        var malformed = await Assert.ThrowsAsync<PdfSplitException>(() => PdfOperations.SplitNamedAsync([1]));
        Assert.Equal(PdfSplitError.InvalidPdf, malformed.Error);
        var bounds = await Assert.ThrowsAsync<PdfSplitException>(() => PdfOperations.SplitNamedAsync(MergeMetadataTests.Pdf("s"), new()
        { Mode = PdfSplitMode.Ranges, Ranges = [(0, 0), (99, 99)] }));
        Assert.Equal(PdfSplitError.InvalidInput, bounds.Error);
        var bytes = MergeMetadataTests.Pdf("source");
        (int From, int To)[] ranges = [(1, 1)];
        var pending = PdfOperations.SplitNamedAsync(bytes, new() { Mode = PdfSplitMode.Ranges, Ranges = ranges });
        Array.Clear(bytes);
        ranges[0] = (99, 99);
        var result = Assert.Single(await pending);
        Assert.Contains("source-two", await new PdfExtractor().ExtractTextAsync(result.Bytes));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfOperations.SplitNamedAsync([1], ct: cts.Token));
    }
}
