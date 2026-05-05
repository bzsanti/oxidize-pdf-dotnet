# OxidizePdf.NET

[![NuGet](https://img.shields.io/nuget/v/OxidizePdf.NET.svg)](https://www.nuget.org/packages/OxidizePdf.NET/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0%2B-purple)](https://dotnet.microsoft.com/)

.NET bindings for [oxidize-pdf](https://github.com/bzsanti/oxidizePdf) - Fast, memory-safe PDF text extraction optimized for RAG/LLM pipelines with intelligent chunking.

## Features

- 🚀 **High Performance** - Native Rust speed (3,000-4,000 pages/second)
- 🧠 **AI/RAG Optimized** - Intelligent text chunking with sentence boundaries
- 🛡️ **Memory Safe** - Zero-copy FFI with automatic resource management
- 🌍 **Cross-Platform** - Linux, Windows, macOS (x64)
- 📦 **Zero Dependencies** - Self-contained native binaries in NuGet package
- 🔍 **Metadata Rich** - Page numbers, confidence scores, bounding boxes

## Installation

```bash
dotnet add package OxidizePdf.NET
```

## Quick Start

### Basic Text Extraction

```csharp
using OxidizePdf.NET;

// Extract all text from PDF
using var extractor = new PdfExtractor();
byte[] pdfBytes = File.ReadAllBytes("document.pdf");

string text = await extractor.ExtractTextAsync(pdfBytes);
Console.WriteLine(text);
```

### RAG Extraction (recommended)

`OxidizePdf.NET` mirrors the RAG-first surface of the Python bridge
(`oxidize-python`). Token-aware, structure-aware chunks ready for vector
store ingestion in one call:

```csharp
using OxidizePdf.NET;
using OxidizePdf.NET.Pipeline;

var extractor = new PdfExtractor();
var chunks = await extractor.RagChunksAsync(pdfBytes, ExtractionProfile.Rag);
foreach (var c in chunks)
{
    // c.FullText  — text + heading context (use this for embeddings)
    // c.Text      — the chunk's own text
    // c.PageNumbers — 1-based source pages (cite results)
    // c.TokenEstimate — plan batch sizes / model windows
    // c.HeadingContext — section heading the chunk belongs to (or null)
}
```

Seven profiles: `Standard`, `Academic`, `Form`, `Government`, `Dense`,
`Presentation`, `Rag`. For fine-grained control pass an explicit
`PartitionConfig` (reading order, header/footer zones, table confidence)
and/or `HybridChunkConfig` (max tokens, overlap, merge policy):

```csharp
var partition = new PartitionConfig()
    .WithReadingOrder(ReadingOrderStrategy.XyCut(20.0))   // multi-column
    .WithMinTableConfidence(0.7);
var hybrid = new HybridChunkConfig().WithMaxTokens(256).WithOverlap(32);
var chunks = await extractor.RagChunksAsync(pdfBytes, partition, hybrid);
```

Element-aware semantic chunks (titles/tables kept whole):

```csharp
var semantic = await extractor.SemanticChunksAsync(
    pdfBytes,
    new SemanticChunkConfig(maxTokens: 512));
```

Markdown export with explicit options (RAG-012):

```csharp
using OxidizePdf.NET.Ai;
var md = await extractor.ToMarkdownAsync(
    pdfBytes,
    new MarkdownOptions { IncludeMetadata = true, IncludePageNumbers = true });
```

Standalone text chunker (no PDF — for non-PDF sources):

```csharp
using OxidizePdf.NET.Ai;
var chunker = new DocumentChunker(chunkSize: 512, overlap: 50);
var pieces = chunker.ChunkText(rawText);
var tokens = DocumentChunker.EstimateTokens(rawText);
```

End-to-end vector-store ingestion with KernelMemory:

```csharp
using OxidizePdf.NET;
using OxidizePdf.NET.Pipeline;
using Microsoft.KernelMemory;

var extractor = new PdfExtractor();
var memory = new KernelMemoryBuilder().Build();

var chunks = await extractor.RagChunksAsync(pdfBytes, ExtractionProfile.Rag);

foreach (var c in chunks)
{
    await memory.ImportTextAsync(
        text: c.FullText,
        documentId: $"doc_p{c.PageNumbers[0]}_c{c.ChunkIndex}",
        tags: new Dictionary<string, object>
        {
            ["source"] = "SharePoint/Documents/report.pdf",
            ["pages"] = string.Join(",", c.PageNumbers),
            ["heading"] = c.HeadingContext ?? string.Empty,
            ["tokens"] = c.TokenEstimate,
        });
}
```

> **Legacy character-based chunking** (`ChunkOptions` + `ExtractChunksAsync`) is
> marked `[Obsolete]` since 0.9.0-rag.1 and will be removed one minor release
> later. Prefer the token-aware overloads above.

### SharePoint Crawler Example

```csharp
using OxidizePdf.NET;
using Microsoft.Graph;

var extractor = new PdfExtractor();
var graphClient = new GraphServiceClient(...);

// Crawl SharePoint document library
var driveItems = await graphClient.Sites["root"]
    .Drives["Documents"]
    .Root
    .Children
    .Request()
    .Filter("endsWith(name,'.pdf')")
    .GetAsync();

foreach (var item in driveItems)
{
    var stream = await graphClient.Sites["root"]
        .Drives["Documents"]
        .Items[item.Id]
        .Content
        .Request()
        .GetAsync();

    using var ms = new MemoryStream();
    await stream.CopyToAsync(ms);

    var chunks = await extractor.ExtractChunksAsync(ms.ToArray());

    // Process chunks for embeddings...
}
```

## Performance

Based on oxidize-pdf v1.6.4 benchmarks:

- **Text Extraction**: 3,000-4,000 pages/second
- **Chunking**: 0.62ms for 100 pages
- **Memory Overhead**: <1MB per document
- **PDF Parsing**: 98.8% success rate on 759 real-world PDFs

## Supported Platforms

| Platform | Runtime Identifier | Status |
|----------|-------------------|--------|
| Linux x64 | `linux-x64` | ✅ Supported |
| Windows x64 | `win-x64` | ✅ Supported |
| macOS x64 | `osx-x64` | ✅ Supported |

Native binaries are automatically included in the NuGet package.

## Architecture

- **native/** - Rust FFI layer (cdylib)
- **dotnet/** - C# wrapper with P/Invoke
- **examples/** - Integration examples (KernelMemory, BasicUsage)

See [ARCHITECTURE.md](ARCHITECTURE.md) for detailed design decisions.

## API Reference

### PdfExtractor

```csharp
public class PdfExtractor : IDisposable
{
    // Extract plain text from PDF
    public Task<string> ExtractTextAsync(byte[] pdfBytes);

    // Extract text chunks optimized for RAG/LLM
    public Task<DocumentChunks> ExtractChunksAsync(
        byte[] pdfBytes,
        ChunkOptions options = null
    );

    // Extract metadata (page count, title, author)
    public Task<PdfMetadata> ExtractMetadataAsync(byte[] pdfBytes);
}
```

### ChunkOptions

```csharp
public class ChunkOptions
{
    public int MaxChunkSize { get; set; } = 512;          // Max tokens per chunk
    public int Overlap { get; set; } = 50;                // Overlap between chunks
    public bool PreserveSentenceBoundaries { get; set; } = true;
    public bool IncludeMetadata { get; set; } = true;
}
```

### DocumentChunk

```csharp
public class DocumentChunk
{
    public int Index { get; set; }             // Chunk index in document
    public int PageNumber { get; set; }        // Source page number
    public string Text { get; set; }           // Chunk text content
    public double Confidence { get; set; }     // Extraction confidence (0.0-1.0)
    public BoundingBox BoundingBox { get; set; } // Optional spatial info
}
```

## Requirements

- **.NET 8.0+** (tested on .NET 8, 9)
- **Native Runtime**: Automatically included in NuGet package

> **Note**: .NET 6 support was dropped in v0.2.0 as it reached end-of-support in November 2024. Use v0.1.0 if you still require .NET 6 compatibility.

## Building from Source

```bash
# Clone repository
git clone https://github.com/bzsanti/oxidize-pdf-dotnet.git
cd oxidize-pdf-dotnet

# Build native library
cd native
cargo build --release

# Build .NET wrapper
cd ../dotnet
dotnet build

# Run tests
dotnet test
```

## Examples

See [examples/](examples/) directory:
- **BasicUsage/** - Simple text extraction
- **KernelMemory/** - Full SharePoint crawler with RAG pipeline

## License

This project is licensed under the MIT License - see [LICENSE](LICENSE) file.

## Contributing

Contributions are welcome! Please read [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## Acknowledgments

Built on top of [oxidize-pdf](https://github.com/bzsanti/oxidizePdf) by Santiago Fernández Muñoz.
