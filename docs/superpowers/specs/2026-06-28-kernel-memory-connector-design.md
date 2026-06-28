# Kernel Memory Connector — Design

**Date:** 2026-06-28
**Status:** Approved (design)
**Adoption lever:** #3 (Kernel Memory connector — ecosystem discovery)

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

### Preserving chunks 1:1 (the differentiator)

Each oxidize chunk becomes one `Chunk` with `completeSentences=true`. To stop KM re-splitting,
the **sample** configures `TextPartitioningOptions { MaxTokensPerParagraph = 2048,
OverlappingTokens = 0 }` so sections pass through 1:1. If a single chunk exceeds the max, KM
re-divides it — an acceptable fallback already surfaced via `RagChunk.IsOversized`. This config
is documented prominently in the sample README: it is what makes oxidize's structure-aware
chunking survive into the vector store.

## Data flow

```
PDF bytes ──> OxidizePdfDecoder.DecodeAsync
           ──> PdfExtractor.RagChunksAsync (profile=Rag)
           ──> List<RagChunk>  (FullText + PageNumbers + HeadingContext + TokenEstimate)
           ──> map 1:1 ──> List<Chunk>  (Content=FullText, Number=ChunkIndex, Meta:page+complete)
           ──> FileContent("application/pdf")
KM pipeline ──> split_text_in_partitions (passthrough: MaxTokensPerParagraph=2048)
           ──> embeddings ──> vector store  (1 partition per oxidize chunk)
```

## Sample (replaces the fake demo)

`examples/KernelMemory` rewritten end-to-end, SharePoint fiction removed:

- Build KM with `.WithOxidizePdf()` + `WithSimpleVectorDb()` (in-memory) + the passthrough
  `TextPartitioningOptions`.
- Import a **real bundled PDF fixture** (reused from the test fixtures), then run a real
  semantic `SearchAsync` query.
- **Embeddings need a key:** real embedding generation is gated behind the `OPENAI_API_KEY`
  env var. Without the key, the sample still runs the decode→chunk mapping and prints the
  resulting chunks (the oxidize half runs keyless); with the key, it completes the full
  index→query loop.
- Sample README documents the passthrough partitioning config and the keyless/keyed paths.

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
5. **Differentiator e2e:** build KM with the default handlers, `WithOxidizePdf()`, the
   passthrough `TextPartitioningOptions` (MaxTokensPerParagraph=2048, OverlappingTokens=0),
   a **deterministic fake `ITextEmbeddingGenerator`** (keyless), and `SimpleVectorDb`; import
   the fixture; assert the number of stored partitions equals the number of oxidize chunks —
   the real behavioral proof of 1:1 preservation.

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
