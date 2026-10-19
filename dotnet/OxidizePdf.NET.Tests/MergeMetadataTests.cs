using System.Text;
using OxidizePdf.NET.Models;

namespace OxidizePdf.NET.Tests;

public class MergeMetadataTests
{
    // Independent PDF with two identifiable pages, four Info fields and an XMP
    // stream. Metadata assertions read the serialized result through the real FFI.
    internal static byte[] Pdf(string name, bool info = true, bool noPages = false, bool unicode = false)
    {
        string content1 = $"BT /F1 12 Tf 20 100 Td ({name}-one) Tj ET";
        string content2 = $"BT /F1 12 Tf 20 100 Td ({name}-two) Tj ET";
        string xmp = "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\">source-xmp-marker</x:xmpmeta>";
        string Field(string suffix) => unicode ? "<FEFF" + Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes("Título 東京 🧪 " + suffix)) + ">" : $"({name}-{suffix})";
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R /Metadata 9 0 R >>",
            noPages ? "<< /Type /Pages /Kids [] /Count 0 >>" : "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] /Resources << /Font << /F1 7 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] /Resources << /Font << /F1 7 0 R >> >> /Contents 6 0 R >>",
            $"<< /Length {content1.Length} >>\nstream\n{content1}\nendstream",
            $"<< /Length {content2.Length} >>\nstream\n{content2}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Title {Field("title")} /Author {Field("author")} /Subject {Field("subject")} /Keywords {Field("keywords")} /Creator (source-creator) /Producer (source-producer) /PrivateKey (source-private) >>",
            $"<< /Type /Metadata /Subtype /XML /Length {xmp.Length} >>\nstream\n{xmp}\nendstream"
        ];
        using var output = new MemoryStream();
        void Write(string text) => output.Write(Encoding.ASCII.GetBytes(text));
        Write("%PDF-1.4\n");
        var offsets = new List<long>();
        for (int i = 0; i < objects.Length; i++)
        {
            offsets.Add(output.Position);
            Write($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = output.Position;
        Write($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets) Write($"{offset:D10} 00000 n \n");
        Write($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R {(info ? "/Info 8 0 R" : "")} >>\nstartxref\n{xref}\n%%EOF\n");
        return output.ToArray();
    }

    private static void Fields(PdfMetadata metadata, string? title, string? author, string? subject, string? keywords)
    {
        Assert.Equal(title, metadata.Title);
        Assert.Equal(author, metadata.Author);
        Assert.Equal(subject, metadata.Subject);
        Assert.Equal(keywords, metadata.Keywords);
    }

    [Fact]
    public async Task DefaultAndLegacyMergesKeepFirstInputFields()
    {
        byte[][] bytes = [Pdf("first"), Pdf("second")];
        PdfMergeInput[] inputs = bytes.Select(b => new PdfMergeInput(b)).ToArray();
        // These calls also guard source compatibility of the old default literal.
        byte[][] outputs = [await PdfOperations.MergeAsync(bytes, default),
            await PdfOperations.MergeAsync(inputs, default),
            await PdfOperations.MergeWithMetadataAsync(inputs),
            await PdfOperations.MergeWithMetadataAsync(inputs, new())];
        foreach (var output in outputs)
        {
            var metadata = await new PdfExtractor().ExtractMetadataAsync(output);
            Fields(metadata, "first-title", "first-author", "first-subject", "first-keywords");
            Assert.Equal(4, metadata.PageCount);
        }
    }

    [Theory]
    [InlineData(0, "first")]
    [InlineData(1, "second")]
    [InlineData(2, "third")]
    public async Task SelectedInputUsesOriginalIndexAndPreservesPageOrder(int index, string source)
    {
        PdfMergeInput[] inputs = [new(Pdf("first"), new PdfPageRange.Single(1)),
            new(Pdf("second"), new PdfPageRange.Range(0, 1)),
            new(Pdf("third"), new PdfPageRange.List([1, 0, 1]))];
        var output = await PdfOperations.MergeWithMetadataAsync(inputs,
            new() { Mode = PdfMergeMetadataMode.FromDocument, SourceIndex = index });
        var extractor = new PdfExtractor();
        Fields(await extractor.ExtractMetadataAsync(output), source + "-title", source + "-author", source + "-subject", source + "-keywords");
        string[] pages = ["first-two", "second-one", "second-two", "third-two", "third-one", "third-two"];
        Assert.Equal(pages.Length, await extractor.GetPageCountAsync(output));
        for (int i = 0; i < pages.Length; i++)
            Assert.Contains(pages[i], await extractor.ExtractTextFromPageAsync(output, i + 1));
    }

    [Fact]
    public async Task CustomFieldsRetainUnicodeEscapesNullAndEmptyValues()
    {
        const string title = "Título (custom) \\ line\n東京";
        var output = await PdfOperations.MergeWithMetadataAsync([new(Pdf("first"))],
            new() { Mode = PdfMergeMetadataMode.Custom, Custom = new() { Title = title, Author = "", Subject = null, Keywords = "a,b;ç" } });
        Fields(await new PdfExtractor().ExtractMetadataAsync(output), title, "", null, "a,b;ç");
    }

    [Theory]
    [InlineData(PdfMergeMetadataMode.FromFirst)]
    [InlineData(PdfMergeMetadataMode.FromDocument)]
    [InlineData(PdfMergeMetadataMode.Custom)]
    public async Task UnicodeFieldsRoundTripForEverySourceMode(PdfMergeMetadataMode mode)
    {
        const string prefix = "Título 東京 🧪 ";
        PdfMergeInput[] inputs = mode == PdfMergeMetadataMode.FromFirst
            ? [new(Pdf("first", unicode: true)), new(Pdf("second"))]
            : [new(Pdf("first")), new(Pdf("second", unicode: true))];
        var options = new PdfMergeMetadataOptions
        {
            Mode = mode,
            SourceIndex = mode == PdfMergeMetadataMode.FromDocument ? 1 : null,
            Custom = mode == PdfMergeMetadataMode.Custom ? new() { Title = prefix + "title", Author = prefix + "author", Subject = prefix + "subject", Keywords = prefix + "keywords" } : null
        };
        var output = await PdfOperations.MergeWithMetadataAsync(inputs, options);
        var extractor = new PdfExtractor();
        Fields(await extractor.ExtractMetadataAsync(output), prefix + "title", prefix + "author", prefix + "subject", prefix + "keywords");
        Assert.Equal(4, await extractor.GetPageCountAsync(output));
        Assert.Contains("first-one", await extractor.ExtractTextFromPageAsync(output, 1));
        Assert.Contains("second-two", await extractor.ExtractTextFromPageAsync(output, 4));
        // Reparse/remerge the repaired revision to exercise the latest Info/xref.
        var again = await PdfOperations.MergeWithMetadataAsync([new(output)]);
        Fields(await extractor.ExtractMetadataAsync(again), prefix + "title", prefix + "author", prefix + "subject", prefix + "keywords");
    }

    [Theory]
    [InlineData(PdfMergeMetadataMode.None)]
    [InlineData(PdfMergeMetadataMode.Custom)]
    public async Task NoSourceFieldsAreCopiedForNoneOrEmptyCustom(PdfMergeMetadataMode mode)
    {
        var output = await PdfOperations.MergeWithMetadataAsync([new(Pdf("first"))],
            new() { Mode = mode, Custom = mode == PdfMergeMetadataMode.Custom ? new() : null });
        Fields(await new PdfExtractor().ExtractMetadataAsync(output), null, null, null, null);
    }

    [Fact]
    public async Task MissingSourceInfoDoesNotFallBackToAnotherInput()
    {
        var output = await PdfOperations.MergeWithMetadataAsync([new(Pdf("first")), new(Pdf("second", info: false))],
            new() { Mode = PdfMergeMetadataMode.FromDocument, SourceIndex = 1 });
        Fields(await new PdfExtractor().ExtractMetadataAsync(output), null, null, null, null);
    }

    [Fact]
    public async Task ZeroPageInputRetainsItsMetadataSourceIndex()
    {
        var output = await PdfOperations.MergeWithMetadataAsync([new(Pdf("empty", noPages: true)), new(Pdf("second"))],
            new() { Mode = PdfMergeMetadataMode.FromDocument, SourceIndex = 0 });
        var metadata = await new PdfExtractor().ExtractMetadataAsync(output);
        Fields(metadata, "empty-title", "empty-author", "empty-subject", "empty-keywords");
        Assert.Equal(2, metadata.PageCount);
    }

    [Fact]
    public async Task InfoCopyDoesNotClaimStructuralOrXmpPreservation()
    {
        var input = Pdf("first");
        var reconstructed = await PdfOperations.MergeWithMetadataAsync([new(input)]);
        var metadata = await new PdfExtractor().ExtractMetadataAsync(reconstructed);
        Assert.NotEqual("source-creator", metadata.Creator);
        Assert.NotEqual("source-producer", metadata.Producer);
        Assert.DoesNotContain("source-xmp-marker", Encoding.ASCII.GetString(reconstructed));
        Assert.DoesNotContain("/PrivateKey", Encoding.ASCII.GetString(reconstructed));
        var preserved = await PdfExistingDocumentOperations.MergeAsync([new(input)], PdfExistingDocumentPolicy.PreserveBase);
        Assert.True(Assert.Single(preserved.Outputs).AsSpan().StartsWith(input));
        Assert.Contains("source-xmp-marker", Encoding.ASCII.GetString(preserved.Outputs[0]));
    }

    [Fact]
    public async Task InvalidIndexesEmptySelectionsAndConflictingOptionsAreRejected()
    {
        PdfMergeInput[] inputs = [new(Pdf("first"))];
        foreach (int? index in new int?[] { null, -1, 1, int.MaxValue })
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => PdfOperations.MergeWithMetadataAsync(inputs,
                new() { Mode = PdfMergeMetadataMode.FromDocument, SourceIndex = index }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => PdfOperations.MergeWithMetadataAsync(inputs, new() { Mode = (PdfMergeMetadataMode)99 }));
        await Assert.ThrowsAsync<ArgumentException>(() => PdfOperations.MergeWithMetadataAsync(inputs, new() { SourceIndex = 0 }));
        await Assert.ThrowsAsync<ArgumentException>(() => PdfOperations.MergeWithMetadataAsync(inputs, new() { Custom = new() }));
        await Assert.ThrowsAsync<ArgumentException>(() => PdfOperations.MergeWithMetadataAsync(inputs, new() { Mode = PdfMergeMetadataMode.Custom }));
        await Assert.ThrowsAsync<ArgumentException>(() => PdfOperations.MergeWithMetadataAsync([]));
        await Assert.ThrowsAsync<ArgumentNullException>(() => PdfOperations.MergeWithMetadataAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => PdfOperations.MergeWithMetadataAsync([null!]));
        var pages = new List<int> { 0 };
        var mutable = new PdfMergeInput(Pdf("first"), new PdfPageRange.List(pages));
        pages.Clear();
        await Assert.ThrowsAsync<ArgumentException>(() => PdfOperations.MergeWithMetadataAsync([mutable]));
        pages.Add(-1);
        await Assert.ThrowsAsync<ArgumentException>(() => PdfOperations.MergeWithMetadataAsync([mutable]));
    }

    [Fact]
    public async Task NativeFailuresAreTypedAndLaterMergesStillSucceed()
    {
        var invalid = await Assert.ThrowsAsync<PdfMergeException>(() => PdfOperations.MergeWithMetadataAsync([new("not PDF"u8.ToArray())]));
        Assert.Equal(PdfMergeError.InvalidPdf, invalid.Error);
        var range = await Assert.ThrowsAsync<PdfMergeException>(() => PdfOperations.MergeWithMetadataAsync([new(Pdf("first"), new PdfPageRange.Single(2))]));
        Assert.Equal(PdfMergeError.InvalidInput, range.Error);
        Assert.Contains("out of bounds", range.Message);
        var output = await PdfOperations.MergeWithMetadataAsync([new(Pdf("after"))]);
        Assert.Equal("after-title", (await new PdfExtractor().ExtractMetadataAsync(output)).Title);
    }

    [Fact]
    public async Task CallerMutationsAfterReturnCannotChangeQueuedRequest()
    {
        var bytes = Pdf("first");
        var indices = new List<int> { 1 };
        PdfMergeInput[] inputs = [new(bytes, new PdfPageRange.List(indices))];
        var options = new PdfMergeMetadataOptions { Mode = PdfMergeMetadataMode.Custom, Custom = new() { Title = "snapshot" } };
        var pending = PdfOperations.MergeWithMetadataAsync(inputs, options);
        Array.Clear(bytes); indices.Clear(); inputs[0] = null!;
        options.Custom.Title = "mutated"; options.Mode = PdfMergeMetadataMode.None;
        var output = await pending;
        var extractor = new PdfExtractor();
        Assert.Equal("snapshot", (await extractor.ExtractMetadataAsync(output)).Title);
        Assert.Equal(1, await extractor.GetPageCountAsync(output));
        Assert.Contains("first-two", await extractor.ExtractTextFromPageAsync(output, 1));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfOperations.MergeWithMetadataAsync([], ct: cancelled.Token));
    }
}
