using System.Text;
using System.Text.RegularExpressions;

namespace OxidizePdf.NET.Tests;

public class IncrementalWriterTests
{
    private static byte[] Base(bool info = true, bool ids = true)
    {
        // Independently authored source: generation-3 Info, catalog XMP, custom
        // Info fields, and binary (not UTF-8) file identifiers.
        var original = Encoding.Latin1.GetString(MergeMetadataTests.Pdf("base", info, unicode: true));
        original = original.Replace("8 0 obj", "8 3 obj").Replace("/Info 8 0 R", "/Info 8 3 R");
        int xref = original.IndexOf("xref\n", StringComparison.Ordinal);
        var lines = original[xref..].Split('\n');
        lines[10] = lines[10].Replace("00000 n", "00003 n");
        original = original[..xref] + string.Join('\n', lines);
        if (ids) original = original.Replace("/Root 1 0 R", "/ID [<0080FF41> <FF008042>] /Root 1 0 R");
        return Encoding.Latin1.GetBytes(original);
    }

    private static PdfDocument Changes(int pages = 1)
    {
        var doc = new PdfDocument();
        doc.SetTitle("replacement");
        for (int i = 0; i < pages; i++)
        {
            using var page = PdfPage.A4();
            page.SetFont(StandardFont.Helvetica, 12).TextAt(50, 700, $"new-{i}");
            doc.AddPage(page);
        }
        return doc;
    }

    private static string LastTrailer(byte[] bytes)
    {
        string text = Encoding.Latin1.GetString(bytes);
        return text[(text.LastIndexOf("trailer\n", StringComparison.Ordinal) + 8)..];
    }

    // Inspect serialized offsets independently of the library parser. Each /Prev
    // must lead to a real xref and each in-use entry to the declared object/gen.
    private static void Revisions(byte[] original, byte[] output)
    {
        Assert.True(output.AsSpan(0, original.Length).SequenceEqual(original));
        string text = Encoding.Latin1.GetString(output);
        int offset = int.Parse(Regex.Matches(text, @"startxref\s+(\d+)")[^1].Groups[1].Value);
        int revisions = 0;
        while (offset >= original.Length)
        {
            Assert.StartsWith("xref\n", text[offset..]);
            int end = text.IndexOf("trailer\n", offset, StringComparison.Ordinal);
            string[] lines = text[offset..end].Split('\n', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i < lines.Length;)
            {
                string[] range = lines[i++].Split(' ');
                int first = int.Parse(range[0]), count = int.Parse(range[1]);
                for (int n = 0; n < count; n++)
                {
                    var entry = lines[i++].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (entry[2] == "n")
                        Assert.StartsWith($"{first + n} {int.Parse(entry[1])} obj", text[int.Parse(entry[0])..]);
                }
            }
            int previous = int.Parse(Regex.Match(text[end..], @"/Prev\s+(\d+)").Groups[1].Value);
            Assert.True(previous < offset);
            offset = previous;
            revisions++;
        }
        Assert.True(revisions > 0);
        Assert.StartsWith("xref\n", text[offset..]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AppendPreservesInfoIdentityOrAbsenceAndOriginalPrefix(bool hasInfo)
    {
        byte[] source = Base(hasInfo), snapshot = source.ToArray();
        using var doc = Changes();
        byte[] output = doc.SaveIncrementalToBytes(source);
        Revisions(source, output);
        Assert.Equal(snapshot, source);
        string trailer = LastTrailer(output);
        if (hasInfo) Assert.Contains("/Info 8 3 R", trailer);
        else Assert.DoesNotContain("/Info", trailer);
        Assert.Contains("/ID [<0080FF41> <FF008042>", trailer);
        var extractor = new PdfExtractor();
        var metadata = await extractor.ExtractMetadataAsync(output);
        Assert.Equal(hasInfo ? "Título 東京 🧪 title" : null, metadata.Title);
        Assert.Equal(3, metadata.PageCount);
        for (int i = 0; i < 3; i++)
            Assert.Contains(new[] { "base-one", "base-two", "new-0" }[i], await extractor.ExtractTextFromPageAsync(output, i + 1));
        // Reusing the same document and result as a base preserves the full chain.
        byte[] again = doc.SaveIncrementalToBytes(output);
        Revisions(output, again);
        Assert.Equal(4, await extractor.GetPageCountAsync(again));
        Assert.Equal(1, doc.PageCount);
        Assert.Equal(1, await extractor.GetPageCountAsync(doc.SaveToBytes()));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task ReplacementUsesLeadingPagesAndRetainsUntouchedTail(int pages)
    {
        byte[] source = Base();
        using var doc = Changes(pages);
        var output = doc.SaveIncrementalToBytes(source, new() { PageMode = PdfIncrementalPageMode.ReplaceLeadingPages });
        Revisions(source, output);
        var extractor = new PdfExtractor();
        Assert.Equal(Math.Max(2, pages), await extractor.GetPageCountAsync(output));
        for (int i = 0; i < pages; i++)
            Assert.Contains($"new-{i}", await extractor.ExtractTextFromPageAsync(output, i + 1));
        Assert.DoesNotContain("base-one", await extractor.ExtractTextFromPageAsync(output, 1));
        if (pages == 1) Assert.Contains("base-two", await extractor.ExtractTextFromPageAsync(output, 2));
    }

    [Theory]
    [InlineData(BuildIdentification.Enabled)]
    [InlineData(BuildIdentification.Disabled)]
    public async Task ReplaceInfoEncodesAllSixUnicodeFieldsAndHonorsIdentification(BuildIdentification identification)
    {
        byte[] source = Base();
        using var doc = Changes();
        const string value = "Título 東京 🧪 (\\)";
        doc.SetTitle(value).SetAuthor(value).SetSubject(value).SetKeywords(value).SetCreator(value).SetProducer(value);
        doc.SetBuildIdentification(identification);
        var output = doc.SaveIncrementalToBytes(source, new() { InfoPolicy = PdfIncrementalInfoPolicy.Replace });
        Revisions(source, output);
        var metadata = await new PdfExtractor().ExtractMetadataAsync(output);
        Assert.Equal(value, metadata.Title);
        Assert.Equal(value, metadata.Author);
        Assert.Equal(value, metadata.Subject);
        Assert.Equal(value, metadata.Keywords);
        Assert.Equal(value, metadata.Creator);
        Assert.Equal(value, metadata.Producer);
        string trailer = LastTrailer(output);
        Assert.DoesNotContain("/Info 8 3 R", trailer);
        Assert.Contains("/ID [<0080FF41> <FF008042>", trailer);
        // Last Info object uses hex-escaped names; inspect its actual serialized data.
        var infoRef = Regex.Match(trailer, @"/Info (\d+) (\d+) R");
        string text = Encoding.Latin1.GetString(output);
        string info = text[text.LastIndexOf($"{infoRef.Groups[1].Value} 0 obj", StringComparison.Ordinal)..];
        Assert.DoesNotContain("PrivateKey", info);
        Assert.DoesNotContain("#50#72#69#76#61#74#65#4B#65#79", info);
        string marker = Convert.ToHexString(Encoding.ASCII.GetBytes("oxidize-pdf-build"));
        string escaped = string.Concat(Enumerable.Range(0, marker.Length / 2).Select(i => "#" + marker.Substring(i * 2, 2)));
        Assert.Equal(identification == BuildIdentification.Enabled, info.Contains(escaped, StringComparison.Ordinal));
        Assert.Contains("/Metadata 9 0 R", text[source.Length..]);
        var again = doc.SaveIncrementalToBytes(output);
        Revisions(output, again);
        Assert.Equal(value, (await new PdfExtractor().ExtractMetadataAsync(again)).Title);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompressionOnlyAffectsNewStreamsAndBaseVersionRemains(bool compress)
    {
        byte[] source = Base(ids: false);
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.SetFont(StandardFont.Helvetica, 12).TextAt(50, 700, new string('a', 2000));
        doc.AddPage(page);
        var output = doc.SaveIncrementalToBytes(source, new() { Writer = new() { CompressStreams = compress, PdfVersion = "1.7" } });
        Revisions(source, output);
        Assert.StartsWith("%PDF-1.4", Encoding.Latin1.GetString(output));
        Assert.Equal(compress, Encoding.Latin1.GetString(output[source.Length..]).Contains("/FlateDecode", StringComparison.Ordinal));
        Assert.Contains(new string('a', 100), await new PdfExtractor().ExtractTextFromPageAsync(output, 3));
    }

    [Fact]
    public async Task StreamXrefBaseCanReceiveClassicIncrementalRevision()
    {
        byte[] source = TaggedPreparationTests.Pdf(streamXref: true);
        using var doc = Changes();
        var output = doc.SaveIncrementalToBytes(source);
        Assert.True(output.AsSpan(0, source.Length).SequenceEqual(source));
        Assert.Contains("/Prev ", LastTrailer(output));
        Assert.Equal(2, await new PdfExtractor().GetPageCountAsync(output));
    }

    [Fact]
    public async Task EmptyChangesCanReplaceInfoWithoutDroppingPages()
    {
        using var doc = new PdfDocument();
        doc.SetTitle("metadata only");
        byte[] source = Base(info: false);
        var output = doc.SaveIncrementalToBytes(source, new() { InfoPolicy = PdfIncrementalInfoPolicy.Replace });
        Revisions(source, output);
        var metadata = await new PdfExtractor().ExtractMetadataAsync(output);
        Assert.Equal(2, metadata.PageCount);
        Assert.Equal("metadata only", metadata.Title);
    }

    [Fact]
    public void MalformedTrailerIdsAndUnsupportedPageStructureFailWithoutConsumingChanges()
    {
        using var doc = Changes();
        byte[] malformed = Encoding.Latin1.GetBytes(Encoding.Latin1.GetString(Base())
            .Replace("/ID [<0080FF41> <FF008042>]", "/ID [1 2]"));
        Assert.Equal(PdfIncrementalSaveError.InvalidPdf,
            Assert.Throws<PdfIncrementalSaveException>(() => doc.SaveIncrementalToBytes(malformed)).Error);
        byte[] unsupported = Encoding.Latin1.GetBytes(Encoding.Latin1.GetString(Base())
            .Replace("/Contents 5 0 R", "/Annots   5 0 R"));
        Assert.Equal(PdfIncrementalSaveError.Serialization,
            Assert.Throws<PdfIncrementalSaveException>(() => doc.SaveIncrementalToBytes(unsupported)).Error);
        Assert.Equal(1, doc.PageCount);
        Assert.NotEmpty(doc.SaveIncrementalToBytes(Base()));
    }

    [Fact]
    public void InvalidArgumentsMalformedAndEncryptedBasesHaveDistinctFailures()
    {
        using var doc = Changes();
        Assert.Throws<ArgumentNullException>(() => doc.SaveIncrementalToBytes(null!));
        Assert.Throws<ArgumentException>(() => doc.SaveIncrementalToBytes([]));
        byte[] source = Base();
        foreach (var options in new PdfIncrementalSaveOptions[] {
            new() { PageMode = (PdfIncrementalPageMode)99 }, new() { InfoPolicy = (PdfIncrementalInfoPolicy)99 },
            new() { Writer = PdfSaveOptions.Modern() }, new() { Writer = new() { UseObjectStreams = true } },
            new() { Writer = new() { PdfVersion = "1.7\nInjected" } } })
            Assert.Throws<ArgumentException>(() => doc.SaveIncrementalToBytes(source, options));
        Assert.Throws<ArgumentNullException>(() => doc.SaveIncrementalToBytes(source, new() { Writer = null! }));
        Assert.Equal(PdfIncrementalSaveError.InvalidPdf,
            Assert.Throws<PdfIncrementalSaveException>(() => doc.SaveIncrementalToBytes("bad"u8.ToArray())).Error);
        using var encrypted = Changes();
        encrypted.Encrypt("user", "owner");
        Assert.Equal(PdfIncrementalSaveError.EncryptedBase,
            Assert.Throws<PdfIncrementalSaveException>(() => doc.SaveIncrementalToBytes(encrypted.SaveToBytes())).Error);
        doc.Dispose();
        Assert.Throws<ObjectDisposedException>(() => doc.SaveIncrementalToBytes(source));
    }
}
