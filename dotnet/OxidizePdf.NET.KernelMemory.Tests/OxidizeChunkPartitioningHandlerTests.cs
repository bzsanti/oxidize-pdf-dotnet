using System.Text.Json;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.Context;
using Microsoft.KernelMemory.DataFormats;
using Microsoft.KernelMemory.MemoryStorage;
using Microsoft.KernelMemory.Pipeline;
using OxidizePdf.NET.KernelMemory;

namespace OxidizePdf.NET.KernelMemory.Tests;

public class OxidizeChunkPartitioningHandlerTests
{
    [Fact]
    public async Task InvokeAsync_emits_one_partition_per_section()
    {
        // Arrange: a FileContent with 3 sections, serialized as the ExtractedContent artifact.
        var content = new FileContent("text/plain");
        content.Sections.Add(new Chunk("Heading A\n\nfirst chunk", 0, Chunk.Meta(true, 1)));
        content.Sections.Add(new Chunk("Heading A\n\nsecond chunk", 1, Chunk.Meta(true, 1)));
        content.Sections.Add(new Chunk("Heading B\n\nthird chunk", 2, Chunk.Meta(true, 2)));
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(content);

        var orchestrator = new FakeOrchestrator(json);
        // Pipeline with a PDF-origin uploaded file (MimeType = "application/pdf").
        var pipeline = FakeOrchestrator.PipelineWithExtractedContent("doc.pdf", "extracted.json");

        var handler = new OxidizeChunkPartitioningHandler("partition", orchestrator);

        // Act
        var (result, _) = await handler.InvokeAsync(pipeline);

        // Assert: one TextPartition per section, content == each chunk's full text, ascending numbers.
        Assert.Equal(ReturnType.Success, result);
        Assert.Equal(3, orchestrator.Written.Count);
        Assert.Equal(
            new[] { "Heading A\n\nfirst chunk", "Heading A\n\nsecond chunk", "Heading B\n\nthird chunk" },
            orchestrator.Written.Select(w => w.content).ToArray());

        var file = pipeline.Files[0];
        var partitions = file.GeneratedFiles.Values
            .Where(f => f.ArtifactType == DataPipeline.ArtifactTypes.TextPartition)
            .OrderBy(f => f.PartitionNumber)
            .ToList();
        Assert.Equal(3, partitions.Count);
        Assert.Equal(new[] { 0, 1, 2 }, partitions.Select(p => p.PartitionNumber).ToArray());
        Assert.Equal(new[] { 1, 1, 2 }, partitions.Select(p => p.SectionNumber).ToArray()); // page numbers
    }

    /// <summary>
    /// Fix 3: when the artifact has bytes but deserializes to zero sections, throw instead of
    /// silently emitting zero partitions (data-loss scenario / format mismatch guard).
    /// </summary>
    [Fact]
    public async Task InvokeAsync_throws_InvalidOperationException_when_ExtractedContent_has_bytes_but_no_sections()
    {
        // Arrange: valid JSON but an empty sections array — simulates a KM format drift
        // (e.g. the ExtractedContent shape changed so "sections" no longer matches our DTO).
        byte[] json = System.Text.Encoding.UTF8.GetBytes("{\"sections\":[]}");
        var orchestrator = new FakeOrchestrator(json);
        var pipeline = FakeOrchestrator.PipelineWithExtractedContent("doc.pdf", "extracted.json");
        var handler = new OxidizeChunkPartitioningHandler("partition", orchestrator);

        // Act & Assert: must throw, NOT silently succeed with zero partitions.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.InvokeAsync(pipeline));
    }

    /// <summary>
    /// Fix 4: when the uploaded file is not PDF-origin, throw NotSupportedException instead of
    /// silently mangling non-PDF content.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_throws_NotSupportedException_for_non_PDF_uploaded_file()
    {
        // Arrange: a non-PDF uploaded file (MIME = text/plain, name = doc.txt).
        byte[] json = System.Text.Encoding.UTF8.GetBytes(
            "{\"sections\":[{\"content\":\"hello\",\"metadata\":{}}]}");
        var orchestrator = new FakeOrchestrator(json);
        var pipeline = FakeOrchestrator.PipelineWithExtractedContent("doc.txt", "extracted.json", "text/plain");
        var handler = new OxidizeChunkPartitioningHandler("partition", orchestrator);

        // Act & Assert: must throw, NOT silently mangle the non-PDF content.
        await Assert.ThrowsAsync<NotSupportedException>(
            () => handler.InvokeAsync(pipeline));
    }
}

/// <summary>
/// Minimal fake for unit-testing <see cref="OxidizeChunkPartitioningHandler"/>.
/// Serves a fixed ExtractedContent JSON from ReadFileAsync and captures WriteFileAsync calls.
/// All other <see cref="IPipelineOrchestrator"/> members throw <see cref="NotImplementedException"/>.
/// </summary>
internal sealed class FakeOrchestrator : IPipelineOrchestrator
{
    private readonly byte[] _json;

    public List<(string name, string content)> Written { get; } = new();

    public FakeOrchestrator(byte[] json) => _json = json;

    public Task<BinaryData> ReadFileAsync(DataPipeline pipeline, string fileName, CancellationToken cancellationToken = default)
        => Task.FromResult(new BinaryData(_json));

    public Task WriteFileAsync(DataPipeline pipeline, string fileName, BinaryData fileContent, CancellationToken cancellationToken = default)
    {
        Written.Add((fileName, fileContent.ToString()));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates a minimal pipeline with one uploaded file and one ExtractedContent artifact.
    /// </summary>
    /// <param name="fileName">The uploaded file name (e.g. "doc.pdf").</param>
    /// <param name="artifactName">The generated artifact name (e.g. "extracted.json").</param>
    /// <param name="mimeType">
    /// MIME type of the uploaded file. Defaults to <c>"application/pdf"</c> so the handler's
    /// PDF-origin gate passes out of the box for PDF tests.
    /// </param>
    public static DataPipeline PipelineWithExtractedContent(
        string fileName, string artifactName, string mimeType = "application/pdf")
    {
        var pipeline = new DataPipeline();
        var fileDetails = new DataPipeline.FileDetails { Name = fileName, MimeType = mimeType };
        fileDetails.GeneratedFiles.Add(artifactName, new DataPipeline.GeneratedFileDetails
        {
            Name = artifactName,
            ArtifactType = DataPipeline.ArtifactTypes.ExtractedContent,
            MimeType = MimeTypes.PlainText,
        });
        pipeline.Files.Add(fileDetails);
        return pipeline;
    }

    // ---- All other IPipelineOrchestrator members — not used by the handler ----

    public List<string> HandlerNames => throw new NotImplementedException();

    public bool EmbeddingGenerationEnabled => throw new NotImplementedException();

    public Task AddHandlerAsync(IPipelineStepHandler handler, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task TryAddHandlerAsync(IPipelineStepHandler handler, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<string> ImportDocumentAsync(string index, DocumentUploadRequest uploadRequest, IContext? context = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public DataPipeline PrepareNewDocumentUpload(string index, string documentId, TagCollection tags, IEnumerable<DocumentUploadRequest.UploadedFile>? filesToUpload = null, IDictionary<string, object?>? contextArgs = null)
        => throw new NotImplementedException();

    public Task RunPipelineAsync(DataPipeline pipeline, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<DataPipeline?> ReadPipelineStatusAsync(string index, string documentId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<DataPipelineStatus?> ReadPipelineSummaryAsync(string index, string documentId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<bool> IsDocumentReadyAsync(string index, string documentId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task StopAllPipelinesAsync()
        => throw new NotImplementedException();

    public Task<StreamableFileContent> ReadFileAsStreamAsync(DataPipeline pipeline, string fileName, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<string> ReadTextFileAsync(DataPipeline pipeline, string fileName, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task WriteTextFileAsync(DataPipeline pipeline, string fileName, string fileContent, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public List<ITextEmbeddingGenerator> GetEmbeddingGenerators()
        => throw new NotImplementedException();

    public List<IMemoryDb> GetMemoryDbs()
        => throw new NotImplementedException();

    public ITextGenerator GetTextGenerator()
        => throw new NotImplementedException();

    public Task StartIndexDeletionAsync(string? index = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task StartDocumentDeletionAsync(string documentId, string? index = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
