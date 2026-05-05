using OxidizePdf.NET.Ai;
using OxidizePdf.NET.Models;

namespace OxidizePdf.NET.Tests.Ai;

/// <summary>
/// Tests for the standalone <see cref="DocumentChunker"/> + the static
/// <see cref="DocumentChunker.EstimateTokens"/> introduced in Task 16c
/// (RAG-008, RAG-009). Operates on raw text — no PDF parsing involved.
/// </summary>
public class DocumentChunkerTests
{
    [Fact]
    public void Default_ctor_matches_rust_defaults()
    {
        var c = new DocumentChunker();
        Assert.Equal(512, c.ChunkSize);
        Assert.Equal(50, c.Overlap);
    }

    [Fact]
    public void Ctor_accepts_custom_chunk_size_and_overlap()
    {
        var c = new DocumentChunker(256, 32);
        Assert.Equal(256, c.ChunkSize);
        Assert.Equal(32, c.Overlap);
    }

    [Fact]
    public void Ctor_rejects_overlap_equal_to_chunk_size()
    {
        Assert.Throws<ArgumentException>(() => new DocumentChunker(10, 10));
    }

    [Fact]
    public void Ctor_rejects_overlap_greater_than_chunk_size()
    {
        Assert.Throws<ArgumentException>(() => new DocumentChunker(10, 20));
    }

    [Fact]
    public void Ctor_rejects_zero_chunk_size()
    {
        Assert.Throws<ArgumentException>(() => new DocumentChunker(0, 0));
    }

    [Fact]
    public void Ctor_rejects_negative_chunk_size()
    {
        Assert.Throws<ArgumentException>(() => new DocumentChunker(-5, 0));
    }

    [Fact]
    public void Ctor_rejects_negative_overlap()
    {
        Assert.Throws<ArgumentException>(() => new DocumentChunker(10, -1));
    }

    [Fact]
    public void EstimateTokens_uses_word_count_x_1_33_formula()
    {
        // Upstream `DocumentChunker::estimate_tokens` returns
        // `(words * 1.33) as usize`. Lock the contract — a future formula
        // change must break this test, not silently shift consumer
        // assumptions about chunk count.
        Assert.Equal(0, DocumentChunker.EstimateTokens(""));
        Assert.Equal(1, DocumentChunker.EstimateTokens("hello"));            // floor(1.33) = 1
        Assert.Equal(2, DocumentChunker.EstimateTokens("hello world"));      // floor(2.66) = 2
        Assert.Equal(3, DocumentChunker.EstimateTokens("a b c"));            // floor(3.99) = 3
        Assert.Equal(5, DocumentChunker.EstimateTokens("hello world from oxidize")); // floor(5.32) = 5
        Assert.Equal(10, DocumentChunker.EstimateTokens("a b c d e f g h")); // floor(10.64) = 10
    }

    [Fact]
    public void EstimateTokens_handles_multiple_whitespace_correctly()
    {
        // Upstream tokenises with `split_whitespace()` which collapses
        // runs of any whitespace.
        Assert.Equal(2, DocumentChunker.EstimateTokens("  hello   world  "));   // 2 words
        Assert.Equal(2, DocumentChunker.EstimateTokens("hello\nworld"));        // newline-separated
        Assert.Equal(2, DocumentChunker.EstimateTokens("hello\tworld"));        // tab-separated
    }

    [Fact]
    public void EstimateTokens_null_throws_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => DocumentChunker.EstimateTokens(null!));
    }

    [Fact]
    public void ChunkText_splits_long_text_into_multiple_chunks()
    {
        var chunker = new DocumentChunker(20, 2);
        var longText = string.Join(" ", Enumerable.Repeat("word", 200));

        var chunks = chunker.ChunkText(longText);

        Assert.True(chunks.Count > 1, $"expected multiple chunks for 200-word input at size=20, got {chunks.Count}");
        Assert.Equal(0, chunks[0].ChunkIndex);
        Assert.StartsWith("chunk_", chunks[0].Id);
        Assert.False(string.IsNullOrEmpty(chunks[0].Content));
    }

    [Fact]
    public void ChunkText_emits_sequential_chunk_index_starting_at_zero()
    {
        var chunker = new DocumentChunker(15, 0);
        var text = string.Join(" ", Enumerable.Range(0, 100).Select(i => $"w{i}"));

        var chunks = chunker.ChunkText(text);

        for (int i = 0; i < chunks.Count; i++)
        {
            Assert.Equal(i, chunks[i].ChunkIndex);
            Assert.Equal($"chunk_{i}", chunks[i].Id);
        }
    }

    [Fact]
    public void ChunkText_overlap_produces_shared_words_between_consecutive_chunks()
    {
        // Semantic verification: overlap parameter must affect content,
        // not just chunk boundaries. Mirrors the Rust-side test.
        var chunker = new DocumentChunker(10, 3);
        var text = string.Join(" ", Enumerable.Range(0, 30).Select(i => $"w{i}"));

        var chunks = chunker.ChunkText(text);

        Assert.True(chunks.Count >= 2);
        var c0Words = chunks[0].Content.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var c1Words = chunks[1].Content.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var shared = c0Words.Intersect(c1Words).ToList();
        Assert.NotEmpty(shared);
    }

    [Fact]
    public void ChunkText_short_input_produces_single_chunk()
    {
        var chunker = new DocumentChunker(100, 10);
        var chunks = chunker.ChunkText("one two three four five");

        Assert.Single(chunks);
        Assert.Equal(0, chunks[0].ChunkIndex);
    }

    [Fact]
    public void ChunkText_empty_returns_empty_list()
    {
        var chunker = new DocumentChunker();
        var chunks = chunker.ChunkText("");

        Assert.Empty(chunks);
    }

    [Fact]
    public void ChunkText_null_throws_ArgumentNullException()
    {
        var chunker = new DocumentChunker();
        Assert.Throws<ArgumentNullException>(() => chunker.ChunkText(null!));
    }

    [Fact]
    public void ChunkText_emits_TextChunk_with_populated_schema()
    {
        var chunker = new DocumentChunker(10, 0);
        var text = string.Join(" ", Enumerable.Range(0, 30).Select(i => $"w{i}"));

        var chunks = chunker.ChunkText(text);

        Assert.All(chunks, c =>
        {
            Assert.False(string.IsNullOrEmpty(c.Id));
            Assert.False(string.IsNullOrEmpty(c.Content));
            Assert.True(c.Tokens >= 0);
            Assert.NotNull(c.PageNumbers); // may be empty for text-only chunking
        });
    }
}
