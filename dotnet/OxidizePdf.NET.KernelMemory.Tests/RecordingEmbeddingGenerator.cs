using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;

namespace OxidizePdf.NET.KernelMemory.Tests;

/// <summary>
/// Keyless, deterministic embedding generator that records every text it is
/// asked to embed. During ingestion KM calls this once per stored partition,
/// so the recorded count equals the number of partitions.
/// </summary>
internal sealed class RecordingEmbeddingGenerator : ITextEmbeddingGenerator
{
    public List<string> EmbeddedTexts { get; } = new();

    public int MaxTokens => 100_000;

    public int CountTokens(string text) =>
        text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

    public IReadOnlyList<string> GetTokens(string text) =>
        text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    public Task<Embedding> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        EmbeddedTexts.Add(text);
        // Deterministic 3-dim vector; values are irrelevant to the assertion.
        return Task.FromResult(new Embedding(new[] { (float)text.Length, 1f, 0f }));
    }
}
