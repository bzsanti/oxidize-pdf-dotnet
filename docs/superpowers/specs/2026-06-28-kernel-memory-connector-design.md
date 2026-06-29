# Kernel Memory Connector — Design

**Date:** 2026-06-28 (architecture corrected 2026-06-29)
**Status:** Approved (design)
**Adoption lever:** #3 (Kernel Memory connector — ecosystem discovery)

## Correction (2026-06-29) — chunking lives in a pipeline handler, not the decoder

Discovered during implementation and verified against KM source (`main`) + the
pinned package `0.98.250508.3`:

- The `IContentDecoder` only feeds the `extract_text` step. The default
  `split_text_in_partitions` step (`TextPartitioningHandler`) **re-chunks** the
  extracted text with `PlainTextChunker` and **ignores `FileContent.Sections`**.
  It also only processes `text/plain` / `text/markdown` artifacts, so the
  decoder's output MimeType must be `text/plain` (input MIME `application/pdf`
  stays in `SupportsMimeType`).
- KM's `extract_text` writes **two** artifacts: `ExtractedText` (flattened
  plain text — what the default splitter reads) and **`ExtractedContent`** (the
  structured `FileContent` JSON, preserving every `Section` + page/heading
  metadata).
- Therefore the original "passthrough partitioning options" mechanism is invalid
  (`WithCustomTextPartitioningOptions` / `WithCustomEmbeddingGenerator` do not
  exist in `0.98.250508.3`). Preserving oxidize chunks 1:1 requires
  **replacing the `split_text_in_partitions` step** with a custom
  `IPipelineStepHandler` that reads the `ExtractedContent` JSON and emits one
  `TextPartition` per oxidize chunk. The existing `OxidizePdfDecoder` is reused
  unchanged (other than the `text/plain` output MIME).

The sections below are updated to this handler-based architecture.

## Problem

`OxidizePdf.NET` ships a structure-aware RAG chunking pipeline (`RagChunksAsync` →
`RagChunk`) whose differentiator is heading-context-aware, page-cited, token-estimated
chunks. Microsoft Kernel Memory (KM) is the natural ingestion host for .NET RAG
pipelines, but there is no real integration:

- `examples/KernelMemory` is a **fake demo** — `Console.WriteLine` walkthroughs with the
  real code commented out, and it uses the deprecated `ExtractChunksAsync`/`DocumentChunk`
  API, not the RAG surface.
- KM's extension point for "turn a PDF into retrievable text" is `IContentDecoder`. None
  is provided, so KM falls back to its built-in PDF decoder and oxidize-pdf's chunking is
  never used.

Goal: a real, publishable connector that drops oxidize-pdf's chunking into KM with one
call, plus a runnable end-to-end sample replacing the fake demo.

## Verified KM API (source: microsoft/kernel-memory `main`, 2026-06-28)

Namespace `Microsoft.KernelMemory.DataFormats`:

```csharp
public interface IContentDecoder
{
    bool SupportsMimeType(string mimeType);
    Task<FileContent> DecodeAsync(string filename, CancellationToken ct = default);
    Task<FileContent> DecodeAsync(BinaryData data, CancellationToken ct = default);
    Task<FileContent> DecodeAsync(Stream data, CancellationToken ct = default);
}

public class FileContent
{
    public List<Chunk> Sections { get; set; } = [];
    public string MimeType { get; set; }
    public FileContent(string mimeType);
}

public class Chunk
{
    public int Number { get; }
    public string Content { get; }
    public Dictionary<string, string> Metadata { get; }
    public bool IsSeparator { get; }
    public bool SentencesAreComplete { get; }   // read-only, from metadata "completeSentences"
    public int PageNumber { get; }               // read-only, from metadata "pageNumber", -1 if absent
    public Chunk(string? text, int number);
    public Chunk(string? text, int number, Dictionary<string, string> metadata);
    public static Dictionary<string, string> Meta(bool? sentencesAreComplete = null, int? pageNumber = null);
}
```

KM ingestion pipeline: `extract_text` (decoder → `FileContent`) → `split_text_in_partitions`
(`TextPartitioningHandler` re-splits each section by `MaxTokensPerParagraph`) → embeddings
→ save. The re-partition step is why a naive decoder discards oxidize's chunking.

The exact KM package version to target is locked during implementation against the
published `Microsoft.KernelMemory.Abstractions` on NuGet; the signatures above are the
contract the decoder implements.

## Architecture

New project `dotnet/OxidizePdf.NET.KernelMemory/`, `net8.0`, added to `OxidizePdf.sln`.

- **Dependencies:** `Microsoft.KernelMemory.Abstractions` only (not `.Core` — `IContentDecoder`
  and `Chunk` live in Abstractions; keeps the connector footprint minimal) + `ProjectReference`
  to `OxidizePdf.NET`.
- **Package id:** `OxidizePdf.NET.KernelMemory`, **own version `0.1.0` (preview)**, decoupled
  from the main package because the connector surface is new and may evolve.

### Components

1. **`OxidizePdfDecoder : IContentDecoder`** — the connector.
   - `SupportsMimeType(mime)` → true for `application/pdf` (case-insensitive).
   - The three `DecodeAsync` overloads funnel to a private `DecodeBytesAsync(byte[])`:
     - `filename` → `File.ReadAllBytesAsync`
     - `BinaryData` → `.ToArray()`
     - `Stream` → copy to `byte[]`
   - Calls `PdfExtractor.RagChunksAsync(bytes, options.Profile, options.Partition, options.Hybrid)`.
   - Maps each `RagChunk` → `new Chunk(ragChunk.FullText, ragChunk.ChunkIndex,
     Chunk.Meta(sentencesAreComplete: true, pageNumber: ragChunk.PageNumbers.FirstOrDefault()))`.
     `FullText` is used (not `Text`) so the embedded text carries heading context.
   - Returns `FileContent("application/pdf")` with the mapped chunks.
   - Owns a `PdfExtractor` (created internally; disposed with the decoder).

2. **`OxidizePdfDecoderOptions`**
   ```csharp
   public sealed class OxidizePdfDecoderOptions
   {
       public ExtractionProfile Profile { get; set; } = ExtractionProfile.Rag;
       public PartitionConfig? Partition { get; set; }
       public HybridChunkConfig? Hybrid { get; set; }
   }
   ```

3. **Registration extension**
   ```csharp
   public static IKernelMemoryBuilder WithOxidizePdf(
       this IKernelMemoryBuilder builder, OxidizePdfDecoderOptions? options = null);
   ```
   Registers `OxidizePdfDecoder` as an `IContentDecoder` (mirrors KM's `WithContentDecoder`).

4. **`OxidizeChunkPartitioningHandler : IPipelineStepHandler`** — replaces the
   `split_text_in_partitions` step; emits one partition per oxidize chunk (detailed below).
   Registered post-`Build` via `memory.Orchestrator.AddHandler<…>("split_text_in_partitions")`.

### Preserving chunks 1:1 (the differentiator) — via a custom partition handler

The decoder maps each oxidize chunk → one `Chunk` (`Content = FullText`, page +
`completeSentences` metadata) inside the `FileContent` it returns. KM persists that
`FileContent` as the `ExtractedContent` JSON artifact.

`OxidizeChunkPartitioningHandler : IPipelineStepHandler` replaces the default
`split_text_in_partitions` step. It:
1. Iterates `pipeline.Files` → `GeneratedFiles`, selecting `ArtifactType == ExtractedContent`.
2. Reads that artifact and deserializes it to `FileContent`.
3. For each `Chunk` in `Sections`, writes one partition file
   (`uploadedFile.GetPartitionFileName(n)` + `orchestrator.WriteFileAsync`), tagged
   `ArtifactType.TextPartition`, `PartitionNumber = n`, `SectionNumber = chunk.PageNumber`,
   `Tags = pipeline.Tags`, `ContentSHA256` set. Content is the chunk's `FullText`
   (heading context preserved).

No re-chunking occurs, so partitions == oxidize chunks 1:1.

**Registration** keeps the default handlers and overrides only the partition step,
post-`Build`:
```csharp
var memory = new KernelMemoryBuilder()
    .WithOpenAIDefaults(key)
    .WithOxidizePdf()          // decoder → extract_text
    .WithSimpleVectorDb()
    .Build<MemoryServerless>();
memory.Orchestrator.AddHandler<OxidizeChunkPartitioningHandler>("split_text_in_partitions");
```

**To verify against `0.98.250508.3` at implementation time:** `ReturnType.Success`
and the exact `GeneratedFileDetails` field set; `IPipelineOrchestrator.ReadFileAsync` /
`WriteFileAsync` signatures; that `AddHandler<T>(stepName)` overrides the default for
that step (else use `WithoutDefaultHandlers()` + register all four). The handler applies
to every `ExtractedContent` artifact — for a mixed-format KM instance, gate it (PDF-origin
only); for PDF-only ingestion it is unconditional.

## Data flow

```
PDF bytes ──> OxidizePdfDecoder.DecodeAsync
           ──> PdfExtractor.RagChunksAsync (profile=Rag)
           ──> List<RagChunk>  (FullText + PageNumbers + HeadingContext + TokenEstimate)
           ──> map 1:1 ──> List<Chunk>  (Content=FullText, Number=ChunkIndex, Meta:page+complete)
           ──> FileContent("application/pdf")
KM extract_text ──> ExtractedContent artifact (FileContent JSON, Sections intact)
OxidizeChunkPartitioningHandler (replaces split_text_in_partitions)
           ──> 1 TextPartition per Section (no re-chunking)
           ──> embeddings ──> vector store  (1 partition per oxidize chunk)
```

## Sample (replaces the fake demo)

`examples/KernelMemory` rewritten end-to-end, SharePoint fiction removed:

- Build KM with `.WithOxidizePdf()` + `WithSimpleVectorDb()` (in-memory), then
  `memory.Orchestrator.AddHandler<OxidizeChunkPartitioningHandler>("split_text_in_partitions")`
  to install the 1:1 partitioning.
- Import a **real bundled PDF fixture** (reused from the test fixtures), then run a real
  semantic `SearchAsync` query.
- **Embeddings need a key:** real embedding generation is gated behind the `OPENAI_API_KEY`
  env var. Without the key, the sample still runs the decode→chunk mapping and prints the
  resulting chunks (the oxidize half runs keyless); with the key, it completes the full
  index→query loop.
- Sample README documents the `AddHandler` partition-step override and the keyless/keyed paths.

## Error handling

- `DecodeAsync(string filename)` with a missing file → propagate `FileNotFoundException`
  (standard .NET; do not swallow).
- A malformed PDF surfaces as the extractor's existing exception (already panic-safe at the
  FFI boundary as of v0.16.0); the decoder does not catch it — KM's pipeline records the
  failure per its own contract.
- Empty `RagChunksAsync` result → `FileContent` with an empty `Sections` list (valid; KM
  stores nothing for that document).

## Testing (TDD, no smoke tests)

New project `dotnet/OxidizePdf.NET.KernelMemory.Tests/` (separate — pulls the KM dependency).
Behavioral tests, written before implementation:

1. `SupportsMimeType("application/pdf")` is true; `"text/plain"`, `""`, arbitrary → false.
2. `DecodeAsync(byte[] fixture)` returns `FileContent.Sections.Count ==
   RagChunksAsync(fixture).Count`; for each section `Content == ragChunk.FullText`,
   `PageNumber == ragChunk.PageNumbers.First()`, `SentencesAreComplete == true`,
   `Number == ragChunk.ChunkIndex`.
3. The `Stream` and `BinaryData` overloads produce output identical to the `filename`
   overload for the same fixture (same count, same contents).
4. Empty/zero-chunk input → empty `Sections`, no exception.
5. **Handler unit test:** run `OxidizeChunkPartitioningHandler.InvokeAsync` against a
   `DataPipeline` carrying a known `ExtractedContent` artifact (a `FileContent` with N
   sections), backed by a fake `IPipelineOrchestrator` that serves the JSON and captures
   `WriteFileAsync` calls. Assert N partition files written, each tagged `TextPartition` with
   ascending `PartitionNumber`, and each partition's content == the corresponding chunk's
   `FullText`. Focused, deterministic, no full pipeline.
6. **Differentiator e2e:** build KM with default handlers + `WithOxidizePdf()` + a
   deterministic fake `ITextEmbeddingGenerator` (keyless, registered via `AddSingleton`) +
   `SimpleVectorDb`; `memory.Orchestrator.AddHandler<OxidizeChunkPartitioningHandler>(
   "split_text_in_partitions")`; import the fixture; assert embedded-partition count equals
   the oxidize chunk count and each chunk's `FullText` was embedded — the real end-to-end
   proof of 1:1 preservation.

All assertions verify real content/behavior, not status codes or object presence.

## Release

`release.yml` currently packs and publishes only the main package. Publishing
`OxidizePdf.NET.KernelMemory` to NuGet is a **separate pipeline change** and is explicitly a
follow-up — this spec delivers the project, tests, and sample, integrated into the solution
build, but does not assume CI publishes the new package. Wiring the second package into
`release.yml` is tracked as the next step after this lands.

## Out of scope (YAGNI)

- A per-page / KM-chunks mode (the "configurable" option was rejected in favor of preserving
  oxidize chunks only).
- Async-mode KM handlers / distributed pipeline wiring.
- SharePoint / Graph crawler code (the old demo's fiction).
- Embedding-generator implementations (the connector is decode-only; embeddings stay KM's job).

## Open follow-ups (post-merge)

1. Wire `OxidizePdf.NET.KernelMemory` into `release.yml` pack + publish.
2. Adoption lever #4 (benchmarks + referral) — separate spec.
