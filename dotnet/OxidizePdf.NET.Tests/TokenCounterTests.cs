using System.Text;
using System.Text.Json;
using OxidizePdf.NET.Pipeline;

namespace OxidizePdf.NET.Tests;

public class TokenCounterTests
{
    private static bool ExactExpected => Environment.GetEnvironmentVariable("OXIDIZE_EXPECT_NO_TIKTOKEN") != "1";

    [Fact]
    public void AvailabilityMatchesNativeBuild()
    {
        Assert.True(PdfTokenCounter.IsAvailable(TokenCounterKind.WordProxy));
        Assert.Equal(ExactExpected, PdfTokenCounter.IsAvailable(TokenCounterKind.Cl100kBase));
    }

    [Fact]
    public void CountsMatchPinnedIndependentOracle()
    {
        using var oracle = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "token-counter-oracle.json")));
        Assert.Equal("Python tiktoken 0.12.0", oracle.RootElement.GetProperty("oracle").GetString());
        foreach (var item in oracle.RootElement.GetProperty("cases").EnumerateArray())
        {
            var text = item.GetProperty("text").GetString()!;
            if (!ExactExpected)
            {
                Assert.Throws<NotSupportedException>(() => PdfTokenCounter.Count(text, TokenCounterKind.Cl100kBase));
                continue;
            }
            var result = PdfTokenCounter.Count(text, TokenCounterKind.Cl100kBase);
            Assert.Equal(item.GetProperty("count").GetUInt64(), result.Count);
            Assert.True(result.IsExact);
            Assert.Equal("cl100k_base", result.CounterName);
        }
        var estimate = PdfTokenCounter.Count("https://example.com/verify");
        Assert.Equal(1UL, estimate.Count);
        Assert.False(estimate.IsExact);
        Assert.Equal("word-proxy", estimate.CounterName);
    }

    [Fact]
    public void RejectsInvalidInputWithoutSubstitution()
    {
        Assert.Throws<ArgumentNullException>(() => PdfTokenCounter.Count(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfTokenCounter.Count("x", (TokenCounterKind)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfTokenCounter.IsAvailable((TokenCounterKind)99));
        Assert.Throws<EncoderFallbackException>(() => PdfTokenCounter.Count("\ud800"));
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfTokenCounter.Count(new string('x', 16 * 1024 * 1024 + 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SemanticChunkConfig { TokenCounter = (TokenCounterKind)99 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new HybridChunkConfig { TokenCounter = (TokenCounterKind)99 }.Validate());
        Assert.Equal(1UL, PdfTokenCounter.Count("a\0b").Count);
        Assert.Equal(2, NativeMethods.oxidize_count_tokens([0xff], 1, 0, out var invalidUtf8));
        Assert.Equal((nuint)0, invalidUtf8);
        Assert.Equal(9, NativeMethods.oxidize_count_tokens([65], 1, 99, out var invalidKind));
        Assert.Equal((nuint)0, invalidKind);
    }

    private static byte[] TwoParagraphs()
    {
        using var document = new PdfDocument();
        using var page = PdfPage.A4();
        page.SetFont(StandardFont.Helvetica, 12).TextAt(50, 700, "Hello, world!");
        page.TextAt(50, 600, "One more sentence.");
        document.AddPage(page);
        return document.SaveToBytes();
    }

    [Fact]
    public async Task BothChunkersUseSelectedBudgetsAndRejectUnavailableCounter()
    {
        var pdf = TwoParagraphs();
        var extractor = new PdfExtractor();
        var semantic = new SemanticChunkConfig(6) { OverlapTokens = 0, TokenCounter = TokenCounterKind.Cl100kBase };
        var hybrid = new HybridChunkConfig { MaxTokens = 6, OverlapTokens = 0, PropagateHeadings = false, TokenCounter = TokenCounterKind.Cl100kBase };
        if (!ExactExpected)
        {
            await Assert.ThrowsAsync<NotSupportedException>(() => extractor.SemanticChunksAsync(pdf, semantic));
            await Assert.ThrowsAsync<NotSupportedException>(() => extractor.RagChunksAsync(pdf, null, hybrid));
            return;
        }
        var sem = await extractor.SemanticChunksAsync(pdf, semantic);
        var hyb = await extractor.RagChunksAsync(pdf, null, hybrid);
        Assert.Equal(new[] { "Hello, world!", "One more sentence." }, sem.Select(c => c.Text));
        Assert.Equal(new[] { "Hello, world!", "One more sentence." }, hyb.Select(c => c.Text));
        // Independent cl100k oracle gives four tokens for each of these sentences.
        Assert.All(sem, c => { Assert.Equal(4, c.TokenEstimate); Assert.False(c.IsOversized); });
        Assert.All(hyb, c => { Assert.Equal(4, c.TokenEstimate); Assert.False(c.IsOversized); });
        semantic.TokenCounter = TokenCounterKind.WordProxy;
        hybrid.TokenCounter = TokenCounterKind.WordProxy;
        var words = await extractor.SemanticChunksAsync(pdf, semantic);
        var oldDefault = await extractor.SemanticChunksAsync(pdf, new SemanticChunkConfig(6).WithOverlap(0));
        Assert.Equal(oldDefault.Select(c => c.Text), words.Select(c => c.Text));
        Assert.Equal(oldDefault.Select(c => c.TokenEstimate), words.Select(c => c.TokenEstimate));
        var hybridWords = await extractor.RagChunksAsync(pdf, null, hybrid);
        Assert.Equal(5, hybridWords.Sum(c => c.TokenEstimate));
    }

    [Fact]
    public void ConcurrentCallsKeepIndependentOwnedResults()
    {
        var kind = ExactExpected ? TokenCounterKind.Cl100kBase : TokenCounterKind.WordProxy;
        Parallel.For(0, 32, _ => Assert.Equal(2UL, PdfTokenCounter.Count("hello world", kind).Count));
    }
}
