using OxidizePdf.NET;
using OxidizePdf.NET.Ai;

namespace OxidizePdf.NET.Tests.Ai;

/// <summary>
/// Tests for the 2.13.0 chunking additions on <see cref="DocumentChunker"/>:
/// PDF chunking into full-fidelity <see cref="DocumentChunk"/> records,
/// per-chunk language detection, and the dominant-language aggregate.
/// </summary>
public class DocumentChunkerLanguageTests
{
    /// <summary>A PDF carrying a substantial English paragraph.</summary>
    private static byte[] EnglishPdf()
    {
        using var builder = PdfDocumentBuilder.A4();
        builder.AddText(
            "The quick brown fox jumps over the lazy dog near the river bank. " +
            "This paragraph is written in clear English prose so that the language " +
            "detector has enough textual signal to classify the dominant language " +
            "of the document with high confidence.",
            StandardFont.Helvetica, 12);
        using var doc = builder.Build();
        return doc.SaveToBytes();
    }

    [Fact]
    public void WithLanguageDetection_returns_chunker_with_flag_and_same_dimensions()
    {
        var baseChunker = new DocumentChunker(256, 32);
        var enabled = baseChunker.WithLanguageDetection(true);

        Assert.False(baseChunker.LanguageDetectionEnabled);
        Assert.True(enabled.LanguageDetectionEnabled);
        Assert.Equal(256, enabled.ChunkSize);
        Assert.Equal(32, enabled.Overlap);
    }

    [Fact]
    public void ChunkPdf_returns_chunks_preserving_content_without_language_by_default()
    {
        var chunks = new DocumentChunker().ChunkPdf(EnglishPdf());

        Assert.NotEmpty(chunks);
        Assert.Contains(chunks, c => c.Content.Contains("quick brown fox"));
        Assert.All(chunks, c => Assert.Null(c.Metadata.Language));
        Assert.All(chunks, c => Assert.All(c.PageNumbers, p => Assert.True(p >= 1)));
    }

    [Fact]
    public void ChunkPdf_with_detection_populates_chunk_language_as_english()
    {
        var chunks = new DocumentChunker().WithLanguageDetection(true).ChunkPdf(EnglishPdf());

        var lang = chunks.Select(c => c.Metadata.Language).FirstOrDefault(l => l is not null);
        Assert.NotNull(lang);
        Assert.Equal("eng", lang!.Code);
    }

    [Fact]
    public void ChunkPdf_null_bytes_throws_argument_null()
    {
        Assert.Throws<ArgumentNullException>(() => new DocumentChunker().ChunkPdf(null!));
    }

    [Fact]
    public void DocumentLanguage_aggregates_dominant_language_from_detected_chunks()
    {
        var chunks = new DocumentChunker().WithLanguageDetection(true).ChunkPdf(EnglishPdf());

        var dominant = DocumentChunker.DocumentLanguage(chunks);

        Assert.NotNull(dominant);
        Assert.Equal("eng", dominant!.Code);
    }

    [Fact]
    public void DocumentLanguage_returns_null_when_no_chunk_has_language()
    {
        var chunks = new List<DocumentChunk>
        {
            new() { Id = "chunk_0", Content = "hello", Tokens = 1, ChunkIndex = 0 },
        };

        Assert.Null(DocumentChunker.DocumentLanguage(chunks));
    }

    [Fact]
    public void DocumentLanguage_null_chunks_throws_argument_null()
    {
        Assert.Throws<ArgumentNullException>(() => DocumentChunker.DocumentLanguage(null!));
    }
}
