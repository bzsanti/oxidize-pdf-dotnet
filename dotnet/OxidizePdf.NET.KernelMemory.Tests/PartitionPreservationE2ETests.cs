using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.Handlers;
using OxidizePdf.NET;
using OxidizePdf.NET.KernelMemory;
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
        //   - AddSingleton<ITextEmbeddingGenerator> satisfies the embedding generator flag
        //     and is picked up for ingestion.
        //   - WithoutTextGenerator() satisfies the required ITextGenerator flag check.
        //   - WithoutDefaultHandlers() skips the default AddDefaultHandlers() call so we can
        //     register OxidizeChunkPartitioningHandler for the "partition" step after Build.
        //     (AddHandler throws on duplicate step names — we cannot override after the fact.)
        //   - SimpleFileStorage + SimpleVectorDb are already configured as Volatile by
        //     the KernelMemoryBuilder constructor; no explicit WithSimpleVectorDb() needed.
        var memory = new KernelMemoryBuilder()
            .WithOxidizePdf()
            .WithoutTextGenerator()
            .WithoutDefaultHandlers()
            .AddSingleton<ITextEmbeddingGenerator>(embeddings)
            .Build<MemoryServerless>();

        // Register the full default ingestion pipeline (Constants.DefaultPipeline =
        // ["extract", "partition", "gen_embeddings", "save_records"]), replacing
        // "partition" with OxidizeChunkPartitioningHandler to preserve 1:1 chunks.
        memory.Orchestrator.AddHandler<TextExtractionHandler>("extract");
        memory.Orchestrator.AddHandler<OxidizeChunkPartitioningHandler>("partition");
        memory.Orchestrator.AddHandler<GenerateEmbeddingsHandler>("gen_embeddings");
        memory.Orchestrator.AddHandler<SaveRecordsHandler>("save_records");

        using var stream = new MemoryStream(bytes);
        await memory.ImportDocumentAsync(
            new Document("doc-1").AddStream("sample.pdf", stream));

        // Passthrough partitioning => exactly one embedded partition per oxidize chunk.
        Assert.Equal(oxidizeChunks.Count, embeddings.EmbeddedTexts.Count);
        // And the embedded text is the oxidize chunk's full text (heading context preserved).
        Assert.All(oxidizeChunks, c => Assert.Contains(c.FullText, embeddings.EmbeddedTexts));
    }
}
