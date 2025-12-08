# OxidizePdf.NET

[![NuGet](https://img.shields.io/nuget/v/OxidizePdf.NET.svg)](https://www.nuget.org/packages/OxidizePdf.NET/)
[![License: AGPL-3.0](https://img.shields.io/badge/License-AGPL%203.0-blue.svg)](LICENSE)
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

### AI/RAG Integration with KernelMemory

```csharp
using OxidizePdf.NET;
using Microsoft.KernelMemory;

var extractor = new PdfExtractor();
var memory = new KernelMemoryBuilder().Build();

// Extract chunks optimized for embeddings
var chunks = await extractor.ExtractChunksAsync(
    pdfBytes,
    new ChunkOptions
    {
        MaxChunkSize = 512,                // Token limit for embedding model
        Overlap = 50,                      // Context overlap between chunks
        PreserveSentenceBoundaries = true, // No mid-sentence cuts
        IncludeMetadata = true             // Page numbers, confidence scores
    }
);

// Store in vector database
foreach (var chunk in chunks)
{
    await memory.ImportTextAsync(
        text: chunk.Text,
        documentId: $"doc_{chunk.PageNumber}_{chunk.Index}",
        tags: new Dictionary<string, object>
        {
            ["source"] = "SharePoint/Documents/report.pdf",
            ["page"] = chunk.PageNumber,
            ["confidence"] = chunk.Confidence
        }
    );
}
```

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

This project is licensed under the GNU Affero General Public License v3.0 (AGPL-3.0) - see [LICENSE](LICENSE) file.

This is consistent with the underlying [oxidize-pdf](https://github.com/bzsanti/oxidizePdf) library which is also licensed under AGPL-3.0.

**Key Points**:
- ✅ Free for open-source projects
- ✅ Commercial use allowed (must share modifications)
- ⚠️ Network use = distribution (must share source)
- ⚠️ If you use this in a web service, you must make your code public

For commercial licensing or questions, contact: licensing@belowzero.tech

## Contributing

Contributions are welcome! Please read [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## Acknowledgments

Built on top of [oxidize-pdf](https://github.com/bzsanti/oxidizePdf) by Santiago Fernández Muñoz.
