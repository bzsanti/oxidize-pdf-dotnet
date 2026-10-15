using System.Text;

namespace OxidizePdf.NET.Tests;

public class FontInspectionTests
{
    internal static byte[] Pdf(bool vertical = true, bool unicode = true, string? metrics = null,
        string ordering = "Japan1", string registry = "Adobe", bool identity = false, bool defaults = false)
    {
        string cmap = $"begincmap /WMode {(vertical ? 1 : 0)} def 1 begincodespacerange <00> <ff> endcodespacerange 3 begincidchar <41> 100 <42> 101 <43> 103 endcidchar endcmap";
        const string toUnicode = "begincmap 1 begincodespacerange <00> <ff> endcodespacerange 2 beginbfchar <41> <00660069> <42> <D83EDDEA> endbfchar endcmap";
        byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);
        byte[] Stream(byte[] value) => Ascii($"<< /Length {value.Length} >>\nstream\n").Concat(value).Concat(Ascii("\nendstream")).ToArray();
        byte[] gids = new byte[204]; gids[201] = 7; gids[203] = 9;
        var objects = new List<byte[]>
        {
            Ascii("<< /Type /Catalog /Pages 2 0 R >>"),
            Ascii("<< /Type /Pages /Kids [3 0 R] /Count 1 /Resources << /Font << /F1 4 0 R >> >> >>"),
            Ascii("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] >>"),
            Ascii($"<< /Type /Font /Subtype /Type0 /BaseFont /HeiseiMin-W3 /Encoding {(identity ? vertical ? "/Identity-V" : "/Identity-H" : "6 0 R")} /DescendantFonts [5 0 R] {(unicode ? "/ToUnicode 7 0 R" : "")} >>"),
            Ascii($"<< /Type /Font /Subtype /CIDFontType2 /BaseFont /HeiseiMin-W3 /CIDSystemInfo << /Registry ({registry}) /Ordering ({ordering}) /Supplement 0 >> /CIDToGIDMap 8 0 R /DW 600 /W [100 [500 550]] {(defaults ? "" : "/DW2 9 0 R /W2 10 0 R")} >>"),
            Stream(Ascii(cmap)), Stream(Ascii(toUnicode)), Stream(gids),
            Ascii("[880 -800]"),
            Ascii(metrics ?? "[100 [-900 500 880] 100 102 -1100 500 880 101 102 -1200 500 880]")
        };
        using var output = new MemoryStream();
        void Write(string text) => output.Write(Ascii(text));
        Write("%PDF-1.7\n");
        var offsets = new List<long>();
        for (int i = 0; i < objects.Count; i++)
        {
            offsets.Add(output.Position); Write($"{i + 1} 0 obj\n"); output.Write(objects[i]); Write("\nendobj\n");
        }
        long xref = output.Position;
        Write($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (long offset in offsets) Write($"{offset:D10} 00000 n \n");
        Write($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return output.ToArray();
    }

    [Theory]
    [InlineData("CNS1", "\uFF0C")]
    [InlineData("GB1", "\u02C9")]
    [InlineData("Japan1", "~")]
    [InlineData("Korea1", "\u22EF")]
    [InlineData("KR", "\u00A9")]
    public void CollectionLookupDistinguishesCollectionsMissingCidsAndScalarResults(string ordering, string expected)
    {
        var result = PdfFonts.LookupCollection(ordering, [100, 0, ushort.MaxValue, 100]);
        Assert.True(result.Supported);
        Assert.Equal(ordering, result.Ordering);
        Assert.Equal(new ushort[] { 100, 0, ushort.MaxValue, 100 }, result.Mappings.Select(m => m.Cid));
        Assert.Equal(expected, result.Mappings[0].Unicode);
        Assert.Equal((uint)expected[0], result.Mappings[0].UnicodeScalar);
        Assert.Equal(expected, result.Mappings[3].Unicode);
        Assert.Null(result.Mappings[1].Unicode);
        Assert.Null(result.Mappings[1].UnicodeScalar);
        Assert.Null(result.Mappings[2].Unicode);
    }

    [Theory]
    [InlineData("CNS1", 14270, "\u82B3\uFE00", null)]
    [InlineData("CNS1", 14000, "\U000200CC", 0x200CC)]
    [InlineData("Japan1", 1133, "\u9022\U000E0100", null)]
    [InlineData("KR", 12239, "\u1100\u119E\u11A8", null)]
    [InlineData("Korea1", 8207, "((", null)]
    public void CollectionSequencesRetainAllScalars(string ordering, ushort cid, string expected, int? scalar)
    {
        var mapping = Assert.Single(PdfFonts.LookupCollection(ordering, [cid]).Mappings);
        Assert.Equal(expected, mapping.Unicode);
        Assert.Equal(scalar is null ? null : (uint?)scalar.Value, mapping.UnicodeScalar);
    }

    [Fact]
    public void UnknownCollectionAndLookupBoundsAreExplicit()
    {
        var unknown = PdfFonts.LookupCollection("japan1", [100]);
        Assert.False(unknown.Supported);
        Assert.Null(Assert.Single(unknown.Mappings).Unicode);
        Assert.Empty(PdfFonts.LookupCollection("Japan1", []).Mappings);
        Assert.Equal(4096, PdfFonts.LookupCollection("Japan1", new ushort[4096]).Mappings.Count);
        Assert.Equal(PdfFontError.LimitExceeded, Assert.Throws<PdfFontException>(() => PdfFonts.LookupCollection("Japan1", new ushort[4097])).Error);
        Assert.Equal(PdfFontError.LimitExceeded, Assert.Throws<PdfFontException>(() => PdfFonts.LookupCollection(new string('x', 65), [])).Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CodesCidsGidsUnicodeAndWritingDirectionRemainIndependent(bool vertical)
    {
        var pdf = Pdf(vertical);
        var result = await PdfFonts.InspectAsync(pdf, 0, "F1", [0x41, 0x42, 0x43, 0x44], [100, 101, 102, 103, uint.MaxValue]);
        Assert.Equal("cid_font_type2", result.Subtype);
        Assert.Equal(vertical ? PdfFontWritingMode.Vertical : PdfFontWritingMode.Horizontal, result.WritingMode);
        Assert.Equal(new byte[] { 0x41 }, result.Glyphs[0].SourceCode);
        Assert.Equal(new uint?[] { 100, 101, 103, null }, result.Glyphs.Select(g => g.Cid));
        Assert.Equal(new ushort?[] { 7, 9, null, null }, result.Glyphs.Select(g => g.GlyphId));
        Assert.Equal(new string?[] { "fi", "\U0001F9EA", null, null }, result.Glyphs.Select(g => g.Unicode));
        Assert.Equal(vertical ? new double[] { -900, -1200, -800, -800 } : [500, 550, 600, 600], result.Glyphs.Select(g => g.Advance));
        Assert.Equal(new double?[] { -900, -1200, -1200, -800, -800 }, result.VerticalAdvances.Select(a => a.Advance));
        Assert.Equal(new uint[] { 100, 101, 102, 103, uint.MaxValue }, result.VerticalAdvances.Select(a => a.Cid));
        var metrics = Assert.IsType<PdfCidVerticalMetrics>(result.VerticalMetrics);
        Assert.Equal(-800, metrics.DefaultAdvance);
        Assert.Equal(-900, Assert.Single(metrics.Overrides).Value);
        Assert.Collection(metrics.Ranges,
            r => { Assert.Equal(100u, r.First); Assert.Equal(102u, r.Last); Assert.Equal(-1100, r.Advance); },
            r => { Assert.Equal(101u, r.First); Assert.Equal(102u, r.Last); Assert.Equal(-1200, r.Advance); });
        // Existing resolved-font API stays callable with its old 1-based page argument.
        Assert.Equal("cid_font_type2", (await new PdfExtractor().GetResolvedFontResourceAsync(pdf, 1, "F1")).Subtype);
    }

    [Theory]
    [InlineData("Adobe", "Japan1", "~")]
    [InlineData("Adobe", "KR", "\u00A9")]
    [InlineData("Other", "Japan1", null)]
    [InlineData("Adobe", "Unknown", null)]
    public async Task CollectionFallbackUsesCidAndRegistryWithoutInventingUnicode(string registry, string ordering, string? expected)
    {
        var result = await PdfFonts.InspectAsync(Pdf(unicode: false, registry: registry, ordering: ordering, identity: true), 0, "F1", [0, 100, 0, 0, 255, 255]);
        Assert.Equal(registry, result.CidSystemInfo!.Registry);
        Assert.Equal(ordering, result.CidSystemInfo.Ordering);
        Assert.Equal(0, result.CidSystemInfo.Supplement);
        Assert.Equal(expected, result.Glyphs[0].Unicode);
        Assert.Equal(100u, result.Glyphs[0].Cid);
        Assert.Equal((ushort)0, result.Glyphs[1].GlyphId);
        Assert.Null(result.Glyphs[1].Unicode);
        Assert.Null(result.Glyphs[2].Unicode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CjkGlyphFallbackRetainsSupplementaryVariationSelector(bool vertical)
    {
        var result = await PdfFonts.InspectAsync(Pdf(vertical: vertical, unicode: false, identity: true),
            0, "F1", [0x04, 0x6D]);
        var glyph = Assert.Single(result.Glyphs);
        Assert.Equal(1133u, glyph.Cid);
        Assert.Null(glyph.GlyphId);
        Assert.Equal("\u9022\U000E0100", glyph.Unicode);
        Assert.Equal(vertical ? -800 : 600, glyph.Advance);
    }

    [Fact]
    public async Task DefaultMetricsAndFullCidRangeRemainCompact()
    {
        var defaults = await PdfFonts.InspectAsync(Pdf(defaults: true), 0, "F1", [0x41], [100]);
        Assert.Equal(-1000, defaults.VerticalMetrics!.DefaultAdvance);
        Assert.Empty(defaults.VerticalMetrics.Ranges);
        Assert.Empty(defaults.VerticalMetrics.Overrides);
        Assert.Equal(-1000, Assert.Single(defaults.Glyphs).Advance);
        var range = await PdfFonts.InspectAsync(Pdf(metrics: "[0 65535 -777 0 0]"), 0, "F1", [0x41], [0, 65535, 65536], new() { MaxMetricEntries = 1 });
        Assert.Single(range.VerticalMetrics!.Ranges);
        Assert.Empty(range.VerticalMetrics.Overrides);
        Assert.Equal(new double?[] { -777, -777, -800 }, range.VerticalAdvances.Select(a => a.Advance));
        Assert.Equal(-777, Assert.Single(range.Glyphs).Advance);
    }

    [Fact]
    public async Task SimpleFontHasNoCidAndNoVerticalTable()
    {
        var result = await PdfFonts.InspectAsync(MergeMetadataTests.Pdf("simple"), 0, "F1", [65], [65]);
        var glyph = Assert.Single(result.Glyphs);
        Assert.Null(glyph.Cid); Assert.Null(glyph.GlyphId);
        Assert.Equal("A", glyph.Unicode);
        Assert.Equal(PdfFontWritingMode.Horizontal, result.WritingMode);
        Assert.Null(result.VerticalMetrics);
        Assert.Null(Assert.Single(result.VerticalAdvances).Advance);
    }

    [Fact]
    public async Task QueuedInputsAndOptionsAreSnapshotted()
    {
        var pdf = Pdf(); byte[] codes = [0x41]; uint[] cids = [101];
        var options = new PdfFontInspectionOptions();
        var task = PdfFonts.InspectAsync(pdf, 0, "F1", codes, cids, options);
        Array.Fill(pdf, (byte)0); codes[0] = 0x44; cids[0] = 65535; options.MaxMetricEntries = 0;
        var result = await task;
        Assert.Equal("fi", Assert.Single(result.Glyphs).Unicode);
        Assert.Equal(101u, Assert.Single(result.VerticalAdvances).Cid);
        Assert.Equal(-1200, result.VerticalAdvances[0].Advance);
    }

    [Theory]
    [InlineData("[100 [1 2]]")]
    [InlineData("[100 99 -800 0 0]")]
    [InlineData("[65535 [-800 0 0 -900 0 0]]")]
    [InlineData("[100 101 -800]")]
    public async Task MalformedVerticalMetricsFailExplicitly(string metrics)
    {
        Assert.Equal(PdfFontError.InvalidPdf, (await Assert.ThrowsAsync<PdfFontException>(() => PdfFonts.InspectAsync(Pdf(metrics: metrics), 0, "F1"))).Error);
    }

    [Theory]
    [InlineData("Codes")]
    [InlineData("Cids")]
    [InlineData("Entries")]
    [InlineData("Work")]
    [InlineData("Unicode")]
    public async Task EachQueryAndResponseBudgetIsEnforced(string budget)
    {
        var options = budget switch
        {
            "Codes" => new PdfFontInspectionOptions { MaxCodeBytes = 0 },
            "Cids" => new PdfFontInspectionOptions { MaxCids = 0 },
            "Entries" => new PdfFontInspectionOptions { MaxMetricEntries = 2 },
            "Work" => new PdfFontInspectionOptions { MaxLookupWork = 2 },
            _ => new PdfFontInspectionOptions { MaxUnicodeBytes = 1 }
        };
        Assert.Equal(PdfFontError.LimitExceeded, (await Assert.ThrowsAsync<PdfFontException>(() =>
            PdfFonts.InspectAsync(Pdf(), 0, "F1", [65], [100], options))).Error);
    }

    [Fact]
    public async Task WorkTableResponseAndArgumentBoundsAreEnforced()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => PdfFonts.InspectAsync(Pdf(), 0, "F1", options: new() { MaxCodeBytes = -1 }));
        Assert.Equal(PdfFontError.InvalidPdf, (await Assert.ThrowsAsync<PdfFontException>(() => PdfFonts.InspectAsync(Pdf(identity: true), 0, "F1", [0]))).Error);
        Assert.Equal(PdfFontError.MissingFont, (await Assert.ThrowsAsync<PdfFontException>(() => PdfFonts.InspectAsync(Pdf(), 0, "missing"))).Error);
        Assert.Equal(PdfFontError.InvalidPdf, (await Assert.ThrowsAsync<PdfFontException>(() => PdfFonts.InspectAsync("bad"u8.ToArray(), 0, "F1"))).Error);
        Assert.Equal(PdfFontError.InvalidPdf, (await Assert.ThrowsAsync<PdfFontException>(() => PdfFonts.InspectAsync(Pdf(), uint.MaxValue, "F1"))).Error);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfFonts.InspectAsync(Pdf(), 0, "F1", ct: new CancellationToken(true)));
    }
}
