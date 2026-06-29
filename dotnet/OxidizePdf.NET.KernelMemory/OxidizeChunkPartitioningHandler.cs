using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.KernelMemory.Pipeline;

namespace OxidizePdf.NET.KernelMemory;

/// <summary>
/// Replaces Kernel Memory's default <c>partition</c> step. Instead
/// of re-chunking the flattened extracted text, it reads the structured
/// <c>ExtractedContent</c> artifact written by KM's <c>extract</c> handler
/// (a <c>FileContent</c> JSON with one section per oxidize chunk) and emits ONE
/// partition per section, preserving heading context and source page.
/// </summary>
/// <remarks>
/// This handler is pinned to Microsoft.KernelMemory 0.98.250508.3. It supports
/// PDF-origin files only; registering it as the global <c>partition</c> step in a
/// mixed-format KM instance will throw <see cref="NotSupportedException"/> for
/// non-PDF uploaded files (see README).
/// </remarks>
public sealed class OxidizeChunkPartitioningHandler : IPipelineStepHandler
{
    private readonly IPipelineOrchestrator _orchestrator;

    /// <summary>
    /// Initializes a new handler instance.
    /// </summary>
    /// <param name="stepName">Pipeline step name, typically <c>partition</c>.</param>
    /// <param name="orchestrator">KM orchestrator used to read and write pipeline artifacts.</param>
    public OxidizeChunkPartitioningHandler(string stepName, IPipelineOrchestrator orchestrator)
    {
        StepName = stepName;
        _orchestrator = orchestrator;
    }

    /// <inheritdoc />
    public string StepName { get; }

    /// <inheritdoc />
    public async Task<(ReturnType returnType, DataPipeline updatedPipeline)> InvokeAsync(
        DataPipeline pipeline, CancellationToken cancellationToken = default)
    {
        foreach (DataPipeline.FileDetails uploadedFile in pipeline.Files)
        {
            // Gate: this handler supports PDF-origin files only.
            // MimeType is set by KM's PrepareNewDocumentUpload via MIME detection on the filename.
            // Fallback to .pdf extension for cases where MimeType is not populated (e.g. unit-test fakes).
            bool isPdf = uploadedFile.MimeType == "application/pdf"
                || uploadedFile.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
            if (!isPdf)
            {
                throw new NotSupportedException(
                    $"OxidizeChunkPartitioningHandler supports PDF ingestion only. " +
                    $"File '{uploadedFile.Name}' (MIME: '{uploadedFile.MimeType}') is not a PDF. " +
                    "Mixed-format KM instances are not supported by OxidizeChunkPartitioningHandler (see README).");
            }

            var newFiles = new Dictionary<string, DataPipeline.GeneratedFileDetails>();

            foreach (DataPipeline.GeneratedFileDetails generated in uploadedFile.GeneratedFiles.Values)
            {
                // Read the STRUCTURED artifact (sections intact), not the flattened ExtractedText.
                if (generated.ArtifactType != DataPipeline.ArtifactTypes.ExtractedContent)
                    continue;

                BinaryData raw = await _orchestrator
                    .ReadFileAsync(pipeline, generated.Name, cancellationToken)
                    .ConfigureAwait(false);

                byte[] rawBytes = raw.ToArray();

                // Legitimately empty artifact (no content extracted from this file section): skip.
                if (rawBytes.Length == 0 || Encoding.UTF8.GetString(rawBytes).Trim().Length == 0)
                    continue;

                // KM's Chunk class has multiple constructors and no [JsonConstructor], so STJ
                // cannot deserialize FileContent<Chunk> with default options. We use private DTOs
                // that mirror only the fields the handler needs (content text + page number).
                FileContentDto? content = JsonSerializer.Deserialize<FileContentDto>(rawBytes);

                // Guard: non-empty bytes that deserialize to zero sections indicate a format mismatch
                // (e.g. the KM ExtractedContent JSON shape changed). Throw instead of silently emitting
                // zero partitions — a document indexed with nothing is a data-loss scenario.
                // This connector is pinned to Microsoft.KernelMemory 0.98.250508.3.
                if (content is null || content.Sections.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"ExtractedContent artifact '{generated.Name}' has {rawBytes.Length} bytes " +
                        "but deserializes to zero sections. " +
                        "The KM ExtractedContent JSON format may have changed. " +
                        "This connector is pinned to Microsoft.KernelMemory 0.98.250508.3.");
                }

                int partitionNumber = 0;
                foreach (ChunkDto chunk in content.Sections)
                {
                    string destFile = uploadedFile.GetPartitionFileName(partitionNumber);
                    var data = new BinaryData(chunk.Content);

                    await _orchestrator
                        .WriteFileAsync(pipeline, destFile, data, cancellationToken)
                        .ConfigureAwait(false);

                    newFiles.Add(destFile, new DataPipeline.GeneratedFileDetails
                    {
                        Name = destFile,
                        MimeType = MimeTypes.PlainText,
                        ArtifactType = DataPipeline.ArtifactTypes.TextPartition,
                        PartitionNumber = partitionNumber,
                        SectionNumber = chunk.PageNumber,
                        Tags = pipeline.Tags,
                        ContentSHA256 = ComputeSHA256(data),
                    });
                    partitionNumber++;
                }
            }

            foreach (var nf in newFiles) { uploadedFile.GeneratedFiles.Add(nf.Key, nf.Value); }
        }

        return (ReturnType.Success, pipeline);
    }

    /// <summary>
    /// Computes a lowercase hex SHA-256 digest of <paramref name="data"/>.
    /// KM's <c>BinaryDataExtensions.CalculateSHA256</c> lives in the Core assembly as an
    /// internal class and is not accessible from the Abstractions-only library.
    /// </summary>
    private static string ComputeSHA256(BinaryData data)
    {
        byte[] hash = SHA256.HashData(data.ToArray());
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    // ---- Private DTOs for FileContent deserialization ----
    // KM's Chunk class has multiple public constructors and no [JsonConstructor], so default
    // System.Text.Json cannot deserialize it. These DTOs mirror the fields that the handler needs.

    private sealed class FileContentDto
    {
        [JsonPropertyName("sections")]
        public List<ChunkDto> Sections { get; set; } = new();
    }

    private sealed class ChunkDto
    {
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("metadata")]
        public Dictionary<string, string> Metadata { get; set; } = new();

        /// <summary>
        /// Page number from metadata, matching the logic in <c>Chunk.PageNumber</c>.
        /// The value is stored as a JSON-serialized integer (e.g. <c>"1"</c>).
        /// </summary>
        public int PageNumber
        {
            get
            {
                if (Metadata.TryGetValue("pageNumber", out string? v))
                    return JsonSerializer.Deserialize<int>(v);
                return -1;
            }
        }
    }
}
