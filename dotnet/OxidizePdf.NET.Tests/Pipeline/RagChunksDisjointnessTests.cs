// Ported from oxidize-python/tests/test_rag_chunks_disjoint.py.
//
// Contract verified here:
//   * Pairwise disjointness: no chunk's text is a substring of another's.
//   * Marker uniqueness: each unique paragraph marker in the source PDF
//     must appear in exactly one chunk.
//   * Bounded fan-out: chunk count <= source element count.
//
// These are SEMANTIC regression tests (input → expected output), NOT shape
// smoke tests. Required by PARITY_SPEC maintenance rule #4 (gating for
// marking any Tier 0 row ✅).
//
// If a test here fails: do NOT patch the test. The failure indicates a
// real disjointness regression in the chunker (oxidize-pdf 2.5.5+ fixed
// the quadratic-accumulation bug that motivated these tests). Investigate
// the Rust side first.

using OxidizePdf.NET.Models;
using OxidizePdf.NET.Pipeline;

namespace OxidizePdf.NET.Tests.Pipeline;

public class RagChunksDisjointnessTests
{
    // Markers chosen to be unique tokens unlikely to collide with framework
    // punctuation so substring checks are unambiguous.
    private const string TitleMarker = "HEAD-ALPHA";
    private static readonly string[] ParaMarkers =
    {
        "alpha-content-line",
        "bravo-content-line",
        "charlie-content-line",
    };

    // ── Synthetic PDF builders ─────────────────────────────────────────────

    private static byte[] BuildTitleThenParagraphsPdf()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.SetFont(StandardFont.HelveticaBold, 16.0).TextAt(50, 750, TitleMarker);
        page.SetFont(StandardFont.Helvetica, 11.0);
        page.TextAt(50, 700, $"Para1 body paragraph {ParaMarkers[0]}.");
        page.TextAt(50, 680, $"Para2 body paragraph {ParaMarkers[1]}.");
        page.TextAt(50, 660, $"Para3 body paragraph {ParaMarkers[2]}.");
        doc.AddPage(page);
        return doc.SaveToBytes();
    }

    private static byte[] BuildMultiSectionPdf()
    {
        using var doc = new PdfDocument();
        var sections = new[] { "SECTION-ONE", "SECTION-TWO" };
        for (int s = 0; s < sections.Length; s++)
        {
            using var page = PdfPage.A4();
            page.SetFont(StandardFont.HelveticaBold, 16.0).TextAt(50, 750, sections[s]);
            page.SetFont(StandardFont.Helvetica, 11.0);
            for (int p = 0; p < 3; p++)
            {
                var marker = $"sec{s}-para{p}-unique-token";
                var y = 700.0 - p * 20.0;
                page.TextAt(50, y, $"Body line {marker} ends here.");
            }
            doc.AddPage(page);
        }
        return doc.SaveToBytes();
    }

    // ── Generic semantic assertions ────────────────────────────────────────

    private static void AssertChunksPairwiseDisjoint(IReadOnlyList<RagChunk> chunks)
    {
        for (int i = 0; i < chunks.Count; i++)
        {
            for (int j = i + 1; j < chunks.Count; j++)
            {
                var ti = chunks[i].Text;
                var tj = chunks[j].Text;
                Assert.False(string.IsNullOrEmpty(ti), $"chunk[{i}].Text is empty");
                Assert.False(string.IsNullOrEmpty(tj), $"chunk[{j}].Text is empty");
                Assert.False(
                    tj.Contains(ti, StringComparison.Ordinal),
                    $"chunk[{i}].Text is a substring of chunk[{j}].Text (quadratic accumulation bug)\n  i=\"{ti}\"\n  j=\"{tj}\"");
                Assert.False(
                    ti.Contains(tj, StringComparison.Ordinal),
                    $"chunk[{j}].Text is a substring of chunk[{i}].Text (quadratic accumulation bug)\n  i=\"{ti}\"\n  j=\"{tj}\"");
            }
        }
    }

    private static void AssertMarkerAppearsExactlyOnce(IReadOnlyList<RagChunk> chunks, string marker)
    {
        var occurrences = chunks.Count(c => c.Text.Contains(marker, StringComparison.Ordinal));
        Assert.True(
            occurrences == 1,
            $"marker \"{marker}\" must appear in exactly one chunk, found in {occurrences}\n" +
            $"  chunks: [{string.Join(", ", chunks.Select(c => $"\"{c.Text}\""))}]");
    }

    // ── Tests: single-page fixture ─────────────────────────────────────────

    [Fact]
    public async Task TitlePlusParagraphsChunksAreDisjoint()
    {
        var pdf = BuildTitleThenParagraphsPdf();
        var extractor = new PdfExtractor();
        var chunks = await extractor.RagChunksAsync(pdf);

        Assert.NotEmpty(chunks);
        AssertChunksPairwiseDisjoint(chunks);
    }

    [Fact]
    public async Task EachParagraphMarkerAppearsInExactlyOneChunk()
    {
        var pdf = BuildTitleThenParagraphsPdf();
        var extractor = new PdfExtractor();
        var chunks = await extractor.RagChunksAsync(pdf);

        foreach (var marker in ParaMarkers)
            AssertMarkerAppearsExactlyOnce(chunks, marker);
    }

    [Fact]
    public async Task ChunkCountBoundedBySourceElements()
    {
        // Title + 3 paragraphs = 4 source elements. The chunker may merge
        // (resulting in fewer chunks) but MUST NOT split a paragraph or
        // duplicate elements (which would push chunk count > 4).
        var pdf = BuildTitleThenParagraphsPdf();
        var extractor = new PdfExtractor();
        var chunks = await extractor.RagChunksAsync(pdf);

        Assert.True(
            chunks.Count <= 4,
            $"chunk count ({chunks.Count}) exceeds source element count (4); duplication suspected");
    }

    // ── Tests: multi-section fixture ───────────────────────────────────────

    [Fact]
    public async Task MultiSectionPdfChunksAreDisjoint()
    {
        var pdf = BuildMultiSectionPdf();
        var extractor = new PdfExtractor();
        var chunks = await extractor.RagChunksAsync(pdf);

        Assert.NotEmpty(chunks);
        AssertChunksPairwiseDisjoint(chunks);
    }

    [Fact]
    public async Task MultiSectionEachMarkerAppearsOnce()
    {
        var pdf = BuildMultiSectionPdf();
        var extractor = new PdfExtractor();
        var chunks = await extractor.RagChunksAsync(pdf);

        for (int s = 0; s < 2; s++)
            for (int p = 0; p < 3; p++)
                AssertMarkerAppearsExactlyOnce(chunks, $"sec{s}-para{p}-unique-token");
    }

    [Fact]
    public async Task MultiSectionChunkCountBounded()
    {
        // 2 sections × (1 title + 3 paragraphs) = 8 source elements.
        var pdf = BuildMultiSectionPdf();
        var extractor = new PdfExtractor();
        var chunks = await extractor.RagChunksAsync(pdf);

        Assert.True(
            chunks.Count <= 8,
            $"chunk count ({chunks.Count}) exceeds source element count (8); duplication suspected");
    }

    // ── Tests: disjointness across profiles ────────────────────────────────

    public static IEnumerable<object[]> ProfileCases() => new[]
    {
        new object[] { ExtractionProfile.Standard },
        new object[] { ExtractionProfile.Rag },
        new object[] { ExtractionProfile.Academic },
    };

    [Theory]
    [MemberData(nameof(ProfileCases))]
    public async Task ProfileEmitsDisjointChunks(ExtractionProfile profile)
    {
        var pdf = BuildMultiSectionPdf();
        var extractor = new PdfExtractor();
        var chunks = await extractor.RagChunksAsync(pdf, profile);

        Assert.NotEmpty(chunks);
        AssertChunksPairwiseDisjoint(chunks);
    }

    [Theory]
    [MemberData(nameof(ProfileCases))]
    public async Task ProfileMarkerUniqueness(ExtractionProfile profile)
    {
        var pdf = BuildMultiSectionPdf();
        var extractor = new PdfExtractor();
        var chunks = await extractor.RagChunksAsync(pdf, profile);

        for (int s = 0; s < 2; s++)
            for (int p = 0; p < 3; p++)
                AssertMarkerAppearsExactlyOnce(chunks, $"sec{s}-para{p}-unique-token");
    }
}
