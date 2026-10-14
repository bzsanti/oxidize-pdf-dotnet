using System.IO.Compression;
using System.Text;

namespace OxidizePdf.NET.Tests;

public class RawStreamTests
{
    private static readonly PdfIndirectObjectId StreamId = new(4, 0);
    private static byte[] Bytes(string value) => Encoding.ASCII.GetBytes(value);
    private static byte[] Compress(byte[] value)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, true)) zlib.Write(value);
        return output.ToArray();
    }
    private static byte[] Pdf(byte[] data, string metadata = "", string contents = "4 0 R", params string[] extra)
    {
        var objects = new List<byte[]>
        {
            Bytes("<< /Type /Catalog /Pages 2 0 R >>"),
            Bytes("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
            Bytes("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] /Resources << >> " + (contents == "" ? "" : "/Contents " + contents) + " >>"),
            Bytes($"<< /Length {data.Length} {metadata} >>\nstream\n").Concat(data).Concat(Bytes("\nendstream")).ToArray()
        };
        objects.AddRange(extra.Select(Bytes));
        using var pdf = new MemoryStream();
        void Write(string text) => pdf.Write(Bytes(text));
        Write("%PDF-1.4\n");
        var offsets = new List<long>();
        for (int i = 0; i < objects.Count; i++)
        {
            offsets.Add(pdf.Position);
            Write($"{i + 1} 0 obj\n"); pdf.Write(objects[i]); Write("\nendobj\n");
        }
        var xref = pdf.Position;
        Write($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets) Write($"{offset:D10} 00000 n \n");
        Write($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return pdf.ToArray();
    }

    [Fact]
    public async Task RawChainPreservesBytesOrderParametersAndIdentity()
    {
        var expected = Bytes("BT (raw is not decoded) Tj ET");
        var encoded = Bytes(Convert.ToHexString(Compress(expected)) + ">");
        var pdf = Pdf(encoded, "/Filter [/ASCIIHexDecode /FlateDecode] /DecodeParms [null 5 0 R]", "[4 0 R 4 0 R]", "<< /Predictor 1 >>");
        var streams = await PdfStreams.ReadPageAsync(pdf, 0);
        Assert.Equal(2, streams.Length);
        for (int i = 0; i < streams.Length; i++)
        {
            Assert.Equal(encoded, streams[i].Data);
            Assert.Equal(StreamId, streams[i].ObjectId);
            Assert.Equal((ulong)i, streams[i].ContentsIndex);
            Assert.Equal("Array", streams[i].Filter!.Type);
            var filters = streams[i].Filter!.Value!.Value;
            Assert.Equal("ASCIIHexDecode", filters[0].GetProperty("value").GetString());
            Assert.Equal("FlateDecode", filters[1].GetProperty("value").GetString());
            var parms = streams[i].DecodeParameters!.Value!.Value;
            Assert.Equal("Null", parms[0].GetProperty("type").GetString());
            Assert.Equal("Reference", parms[1].GetProperty("type").GetString());
            Assert.Equal(5, parms[1].GetProperty("value")[0].GetInt32());
        }
        var decoded = await PdfStreams.DecodePageContentAsync(pdf, 0, 1);
        Assert.Equal(expected, decoded.Data);
        Assert.Equal(StreamId, decoded.ObjectId);
        Assert.Equal(1ul, decoded.ContentsIndex);
        Assert.True(decoded.DecodedWithoutRecovery);
        Assert.Empty(decoded.Diagnostics);
        var byObject = await PdfStreams.ReadAsync(pdf, StreamId);
        Assert.Null(byObject.ContentsIndex);
        Assert.Equal(encoded, byObject.Data);
        Assert.Equal(expected, (await PdfStreams.DecodeAsync(pdf, StreamId)).Data);
    }

    [Fact]
    public async Task UnfilteredEmptyAndAbsentContentsAreDistinct()
    {
        var payload = new byte[] { 0, 255, 13, 10, 7 };
        var pdf = Pdf(payload);
        var raw = await PdfStreams.ReadAsync(pdf, StreamId);
        Assert.Null(raw.Filter);
        Assert.Null(raw.DecodeParameters);
        Assert.Equal(payload, raw.Data);
        Assert.Equal(payload, (await PdfStreams.DecodeAsync(pdf, StreamId)).Data);
        Assert.Empty(await PdfStreams.ReadPageAsync(Pdf([], contents: ""), 0));
        Assert.Empty(await PdfStreams.ReadPageAsync(Pdf([], contents: "null"), 0));
        var empty = await PdfStreams.DecodeAsync(Pdf([]), StreamId, new() { MaxEncodedBytes = 0, MaxDecodedBytes = 0 });
        Assert.Empty(empty.Data);
        Assert.True(empty.DecodedWithoutRecovery);
        var explicitNull = await PdfStreams.ReadAsync(Pdf([], "/DecodeParms null"), StreamId);
        Assert.Equal("Null", explicitNull.DecodeParameters!.Type);
        Assert.Null(explicitNull.DecodeParameters.Value);
        var emptyFilters = Pdf(payload, "/Filter []");
        Assert.Equal(payload, (await PdfStreams.ReadAsync(emptyFilters, StreamId)).Data);
        var unsupported = await Assert.ThrowsAsync<PdfStreamException>(() => PdfStreams.DecodeAsync(emptyFilters, StreamId));
        Assert.Equal(PdfStreamErrorKind.DecodeError, unsupported.Kind);
        Assert.Equal(payload, (await PdfStreams.DecodeAsync(emptyFilters, StreamId, new() { AllowRecovery = true })).Data);
    }

    [Fact]
    public async Task RecoveryIsOptInAndKeepsFilterPosition()
    {
        var payload = Bytes(new string('A', 2000));
        var broken = Compress(payload)[..^4];
        var pdf = Pdf(Bytes(Convert.ToHexString(broken) + ">"), "/Filter [/ASCIIHexDecode /FlateDecode]");
        var strict = await Assert.ThrowsAsync<PdfStreamException>(() => PdfStreams.DecodeAsync(pdf, StreamId, parserOptions: PdfParserOptions.Tolerant()));
        Assert.Equal(PdfStreamErrorKind.DecodeError, strict.Kind);
        var result = await PdfStreams.DecodeAsync(pdf, StreamId, new() { AllowRecovery = true });
        Assert.Equal(payload, result.Data);
        Assert.False(result.DecodedWithoutRecovery);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(1ul, diagnostic.FilterIndex);
        Assert.Equal(PdfFlateRecoveryKind.Unverified, diagnostic.Kind);
        Assert.NotEmpty(diagnostic.Error);
        // A deliberately unterminated stored DEFLATE block with a known decoded prefix.
        var incomplete = new byte[] { 0x78, 0x01, 0x01, 0x0a, 0x00, 0xf5, 0xff, 65, 66, 67 };
        var partial = await PdfStreams.DecodeAsync(Pdf(incomplete, "/Filter /FlateDecode"), StreamId, new() { AllowRecovery = true });
        Assert.Equal(Bytes("ABC"), partial.Data);
        Assert.False(partial.DecodedWithoutRecovery);
        Assert.Equal(PdfFlateRecoveryKind.Incomplete, Assert.Single(partial.Diagnostics).Kind);
    }

    [Fact]
    public async Task CorruptionPredictorsAndUnsupportedFiltersRemainErrors()
    {
        var corrupt = Pdf([0, 1, 2, 3], "/Filter /FlateDecode");
        Assert.Equal(new byte[] { 0, 1, 2, 3 }, (await PdfStreams.ReadAsync(corrupt, StreamId)).Data);
        var invalid = await Assert.ThrowsAsync<PdfStreamException>(() => PdfStreams.DecodeAsync(corrupt, StreamId, new() { AllowRecovery = true }));
        Assert.Equal(PdfStreamErrorKind.InvalidFlate, invalid.Kind);
        var predictor = Pdf(Compress(Bytes("abc")), "/Filter /FlateDecode /DecodeParms << /Predictor 999 >>");
        foreach (var recover in new[] { false, true })
        {
            var error = await Assert.ThrowsAsync<PdfStreamException>(() => PdfStreams.DecodeAsync(predictor, StreamId, new() { AllowRecovery = recover }));
            Assert.Equal(recover ? PdfStreamErrorKind.Other : PdfStreamErrorKind.DecodeError, error.Kind);
            var unsupported = await Assert.ThrowsAsync<PdfStreamException>(() => PdfStreams.DecodeAsync(Pdf([1], "/Filter /DCTDecode"), StreamId, new() { AllowRecovery = recover }));
            Assert.Equal(recover ? PdfStreamErrorKind.Other : PdfStreamErrorKind.DecodeError, unsupported.Kind);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DecodedBudgetsRejectExpansionWithoutTruncating(bool recover)
    {
        var payload = Bytes(new string('x', 4096));
        var pdf = Pdf(Compress(payload), "/Filter /FlateDecode");
        var exact = new PdfStreamOptions { AllowRecovery = recover, MaxDecodedBytes = 4096 };
        Assert.Equal(payload, (await PdfStreams.DecodeAsync(pdf, StreamId, exact)).Data);
        exact.MaxDecodedBytes = 4095;
        var error = await Assert.ThrowsAsync<PdfStreamException>(() => PdfStreams.DecodeAsync(pdf, StreamId, exact));
        Assert.Equal(recover ? PdfStreamErrorKind.ResourceLimit : PdfStreamErrorKind.DecodeError, error.Kind);
        exact.MaxDecodedBytes = 0;
        await Assert.ThrowsAsync<PdfStreamException>(() => PdfStreams.DecodeAsync(pdf, StreamId, exact));
        await Assert.ThrowsAsync<PdfStreamException>(() => PdfStreams.DecodeAsync(Pdf([1]), StreamId, exact));
        // Intermediate ASCIIHex output exceeds the final output: limits apply at every stage.
        var compressed = Compress([1]);
        var chain = Pdf(Bytes(Convert.ToHexString(compressed) + ">"), "/Filter [/ASCIIHexDecode /FlateDecode]");
        exact.MaxDecodedBytes = 1;
        await Assert.ThrowsAsync<PdfStreamException>(() => PdfStreams.DecodeAsync(chain, StreamId, exact));
    }

    [Fact]
    public async Task InspectionBudgetsHaveExactAggregateBoundaries()
    {
        var pdf = Pdf([1, 2, 3], "/Filter /FlateDecode /DecodeParms << /Predictor 1 >>", "[4 0 R 4 0 R]");
        var options = new PdfStreamOptions { MaxEncodedBytes = 6, MaxStreams = 2, MaxMetadataNodes = 6, MaxMetadataBytes = 40, MaxMetadataDepth = 1, MaxReferenceDepth = 1 };
        Assert.Equal(2, (await PdfStreams.ReadPageAsync(pdf, 0, options)).Length);
        async Task Fails(Action<PdfStreamOptions> change)
        {
            var candidate = new PdfStreamOptions { MaxEncodedBytes = 6, MaxStreams = 2, MaxMetadataNodes = 6, MaxMetadataBytes = 40, MaxMetadataDepth = 1, MaxReferenceDepth = 1 };
            change(candidate);
            var error = await Assert.ThrowsAsync<PdfStreamException>(() => PdfStreams.ReadPageAsync(pdf, 0, candidate));
            Assert.Equal(PdfStreamErrorKind.ResourceLimit, error.Kind);
        }
        await Fails(o => o.MaxEncodedBytes = 5);
        await Fails(o => o.MaxStreams = 1);
        await Fails(o => o.MaxMetadataNodes = 5);
        await Fails(o => o.MaxMetadataBytes = 39);
        await Fails(o => o.MaxMetadataDepth = 0);
        await Fails(o => o.MaxReferenceDepth = 0);
        // Stop at the exhausted budget before resolving later entries (which may be enormous).
        var stopEarly = await Assert.ThrowsAsync<PdfStreamException>(() => PdfStreams.ReadPageAsync(
            Pdf([1, 2, 3], contents: "[4 0 R 4 0 R 999 0 R]"), 0, new() { MaxEncodedBytes = 3 }));
        Assert.Equal(PdfStreamErrorKind.ResourceLimit, stopEarly.Kind);
        Assert.Single(await PdfStreams.ReadPageAsync(Pdf([], contents: "4 0 R"), 0, new() { MaxEncodedBytes = 0, MaxMetadataNodes = 0, MaxMetadataBytes = 0 }));
    }

    [Fact]
    public async Task InvalidSelectorsAndIndirectMetadataFailExplicitly()
    {
        var pdf = Pdf([1]);
        await Assert.ThrowsAsync<PdfStreamException>(() => PdfStreams.ReadPageAsync(pdf, 1));
        await Assert.ThrowsAsync<PdfStreamException>(() => PdfStreams.DecodePageContentAsync(pdf, 0, 1));
        await Assert.ThrowsAsync<PdfStreamException>(() => PdfStreams.ReadAsync(pdf, new(1, 0)));
        await Assert.ThrowsAsync<PdfStreamException>(() => PdfStreams.ReadAsync(pdf, new(999, 0)));
        await Assert.ThrowsAsync<PdfStreamException>(() => PdfStreams.ReadPageAsync(Pdf([], contents: "[4 0 R 7]"), 0));
        await Assert.ThrowsAsync<PdfStreamException>(() => PdfStreams.ReadPageAsync(Pdf([], contents: "5 0 R", extra: ["5 0 R"]), 0));
        var indirectArray = Pdf([1], contents: "5 0 R", extra: ["[4 0 R]"]);
        Assert.Equal(StreamId, Assert.Single(await PdfStreams.ReadPageAsync(indirectArray, 0)).ObjectId);
        foreach (var parms in new[] { "5 0 R", "999 0 R", "42" })
        {
            var source = Pdf(Compress([1]), "/Filter /FlateDecode /DecodeParms " + parms, extra: ["5 0 R"]);
            Assert.NotNull((await PdfStreams.ReadAsync(source, StreamId)).DecodeParameters);
            await Assert.ThrowsAsync<PdfStreamException>(() => PdfStreams.DecodeAsync(source, StreamId));
        }
    }

    [Fact]
    public async Task IndirectPredictorParametersAreAppliedByTheDocument()
    {
        var pdf = Pdf(Compress([0, 10, 20, 30]), "/Filter /FlateDecode /DecodeParms 5 0 R",
            extra: ["<< /Predictor 12 /Columns 3 /Colors 1 /BitsPerComponent 8 >>"]);
        var raw = await PdfStreams.ReadAsync(pdf, StreamId);
        Assert.Equal("Reference", raw.DecodeParameters!.Type);
        foreach (var recover in new[] { false, true })
        {
            var decoded = await PdfStreams.DecodeAsync(pdf, StreamId, new() { AllowRecovery = recover });
            Assert.Equal(new byte[] { 10, 20, 30 }, decoded.Data);
            Assert.True(decoded.DecodedWithoutRecovery);
        }
    }

    [Fact]
    public async Task InputsAreOwnedAndLegacyDecodedApiIsPreserved()
    {
        var payload = Bytes("BT (old API) Tj ET");
        var original = Pdf(Compress(payload), "/Filter /FlateDecode");
        var pdf = (byte[])original.Clone();
        var options = new PdfStreamOptions();
        var parser = new PdfParserOptions();
        var pending = PdfStreams.DecodeAsync(pdf, StreamId, options, parser);
        Array.Fill(pdf, (byte)0);
        options.MaxDecodedBytes = 0;
        parser.PreferredEncoding = (PdfPreferredEncoding)255;
        Assert.Equal(payload, (await pending).Data);
        var extractor = new PdfExtractor();
        Assert.Equal(payload, Assert.Single((await extractor.GetPageContentStreamAsync(original, 1)).Streams));
        await Assert.ThrowsAsync<ArgumentNullException>(() => PdfStreams.ReadAsync(null!, StreamId));
        await Assert.ThrowsAsync<ArgumentException>(() => PdfStreams.ReadAsync([], StreamId));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfStreams.DecodeAsync(original, StreamId, ct: new CancellationToken(true)));
        await Assert.ThrowsAsync<PdfStreamException>(() => PdfStreams.ReadAsync([1, 2, 3], StreamId));
        await Assert.ThrowsAsync<PdfStreamException>(() => PdfStreams.ReadAsync(original, StreamId, new() { MaxMetadataDepth = 17 }));
        await Assert.ThrowsAsync<PdfStreamException>(() => PdfStreams.ReadAsync(original, StreamId, new() { MaxReferenceDepth = 129 }));
    }
}
