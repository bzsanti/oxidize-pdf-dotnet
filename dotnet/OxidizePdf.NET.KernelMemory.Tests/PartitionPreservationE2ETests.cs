using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.Configuration;
using OxidizePdf.NET;
using OxidizePdf.NET.Pipeline;

namespace OxidizePdf.NET.KernelMemory.Tests;

public class PartitionPreservationE2ETests
{
    private static string FixturePath() =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "sample.pdf");

    [Fact]
    public async Task Import_stores_one_partition_per_oxidize_chunk()
    {
        var bytes = await File.ReadAllBytesAsync(FixturePath());
        var oxidizeChunks = await new PdfExtractor().RagChunksAsync(bytes, ExtractionProfile.Rag);

        var embeddings = new RecordingEmbeddingGenerator();

        // WithCustomEmbeddingGenerator and WithCustomTextPartitioningOptions do not exist
        // in Microsoft.KernelMemory.Core 0.98.250508.3. Use the actual API:
        //   - AddSingleton<ITextEmbeddingGenerator> satisfies GetBuildType() flag4 and is
        //     picked up by ReuseRetrievalEmbeddingGeneratorIfNecessary for ingestion.
        //   - AddSingleton(TextPartitioningOptions) is resolved via DI into
        //     TextPartitioningHandler(... TextPartitioningOptions? options ...).
        //   - WithoutTextGenerator() satisfies the required ITextGenerator flag6 check.
        //   - SimpleFileStorage + SimpleVectorDb are already configured as Volatile by
        //     the KernelMemoryBuilder constructor; no explicit WithSimpleVectorDb() needed.
        var memory = new KernelMemoryBuilder()
            .WithOxidizePdf()
            .WithoutTextGenerator()
            .AddSingleton<ITextEmbeddingGenerator>(embeddings)
            .AddSingleton(new TextPartitioningOptions
            {
                MaxTokensPerParagraph = 2048,
                OverlappingTokens = 0,
            })
            .Build<MemoryServerless>();

        using var stream = new MemoryStream(bytes);
        await memory.ImportDocumentAsync(
            new Document("doc-1").AddStream("sample.pdf", stream));

        // Passthrough partitioning => exactly one embedded partition per oxidize chunk.
        Assert.Equal(oxidizeChunks.Count, embeddings.EmbeddedTexts.Count);
        // And the embedded text is the oxidize chunk's full text (heading context preserved).
        Assert.All(oxidizeChunks, c => Assert.Contains(c.FullText, embeddings.EmbeddedTexts));
    }
}
