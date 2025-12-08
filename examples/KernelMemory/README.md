# KernelMemory Integration Example

Complete example of SharePoint PDF crawler with KernelMemory for RAG/LLM pipelines.

## Overview

This example demonstrates:
1. **PDF Text Extraction** - Using OxidizePdf.NET to extract chunked text
2. **Metadata Enrichment** - Adding SharePoint metadata (source, page, confidence)
3. **Vector Storage** - Storing chunks in KernelMemory for semantic search
4. **Semantic Search** - Querying indexed documents with natural language

## Use Case: SharePoint Document Crawler

```
SharePoint Library → OxidizePdf.NET → KernelMemory → Vector DB
     (PDFs)        (Extract chunks)  (Generate embeddings) (Search)
```

## Running the Example

```bash
# Install dependencies
dotnet restore

# Run demo
dotnet run

# For production use, configure SharePoint credentials
# See commented SharePointCrawler class in Program.cs
```

## Code Walkthrough

### 1. Initialize Components

```csharp
// KernelMemory for RAG pipeline
var memory = new KernelMemoryBuilder()
    .WithSimpleVectorDb()
    .Build();

// PDF extractor
using var extractor = new PdfExtractor();
```

### 2. Extract PDF Chunks

```csharp
var chunks = await extractor.ExtractChunksAsync(
    pdfBytes,
    new ChunkOptions
    {
        MaxChunkSize = 512,        // Match your embedding model
        Overlap = 50,              // Context overlap
        PreserveSentenceBoundaries = true
    }
);
```

### 3. Store in KernelMemory

```csharp
foreach (var chunk in chunks)
{
    await memory.ImportTextAsync(
        text: chunk.Text,
        documentId: $"{fileId}_p{chunk.PageNumber}_c{chunk.Index}",
        tags: new TagCollection
        {
            ["source"] = sharePointUrl,
            ["page"] = chunk.PageNumber.ToString(),
            ["confidence"] = chunk.Confidence.ToString()
        }
    );
}
```

### 4. Query Documents

```csharp
var results = await memory.SearchAsync(
    "What are the Q4 revenue projections?",
    limit: 5
);

foreach (var result in results.Results)
{
    Console.WriteLine($"Source: {result.SourceName}");
    Console.WriteLine($"Relevance: {result.Relevance:F2}");
    Console.WriteLine($"Text: {result.Partitions[0].Text}");
}
```

## Production Implementation

### SharePoint Authentication

```csharp
using Azure.Identity;
using Microsoft.Graph;

var credential = new ClientSecretCredential(
    tenantId: "your-tenant-id",
    clientId: "your-client-id",
    clientSecret: "your-client-secret"
);

var graphClient = new GraphServiceClient(credential);
```

### Crawl Document Library

```csharp
// Get all PDFs from library
var items = await graphClient.Sites["site-id"]
    .Drives["drive-id"]
    .Root
    .Children
    .GetAsync(r => r.QueryParameters.Filter = "endsWith(name,'.pdf')");

// Download and process each PDF
foreach (var item in items.Value)
{
    var pdfBytes = await DownloadPdfAsync(item.Id);
    var chunks = await extractor.ExtractChunksAsync(pdfBytes);
    await StoreChunksInMemory(memory, item, chunks);
}
```

### Error Handling

```csharp
try
{
    var chunks = await extractor.ExtractChunksAsync(pdfBytes);
}
catch (PdfExtractionException ex)
{
    Console.WriteLine($"Failed to extract {fileName}: {ex.Message}");
    // Log error and continue with next file
}
```

## Performance Considerations

### Batch Processing

```csharp
// Process multiple PDFs concurrently
var tasks = pdfFiles.Select(async file =>
{
    using var extractor = new PdfExtractor();
    var pdfBytes = await DownloadPdfAsync(file.Id);
    var chunks = await extractor.ExtractChunksAsync(pdfBytes);
    await StoreChunksInMemory(memory, file, chunks);
});

await Task.WhenAll(tasks);
```

### Chunking Strategy

| Embedding Model | MaxChunkSize | Overlap | Notes |
|-----------------|--------------|---------|-------|
| OpenAI text-embedding-ada-002 | 512 | 50 | 8191 token limit |
| Azure OpenAI | 512 | 50 | Same as OpenAI |
| Sentence-BERT | 256 | 25 | 512 token limit |
| Custom models | Adjust | 10-20% | Based on token limit |

## Cost Estimation

**For 1,000 PDFs (avg 10 pages each)**:

| Component | Cost | Notes |
|-----------|------|-------|
| PDF Extraction | Free | Local processing |
| Embeddings (OpenAI) | ~$0.40 | 10,000 pages × 512 tokens × $0.0001/1K |
| Vector Storage | ~$5/month | Qdrant/Pinecone |
| **Total** | **~$5.40 initial + $5/month** | |

## Architecture Diagram

```
┌─────────────────────────────────────────────────────┐
│                 Your Application                     │
│  (SharePoint Crawler / Document Processor)           │
└────────────────────┬────────────────────────────────┘
                     │
                     ├─► OxidizePdf.NET
                     │   └─► Extract chunks (512 chars)
                     │
                     ├─► KernelMemory
                     │   ├─► Generate embeddings
                     │   └─► Store in vector DB
                     │
                     └─► Query with natural language
                         └─► Return relevant chunks
```

## See Also

- [BasicUsage Example](../BasicUsage/) - Simple text extraction
- [OxidizePdf.NET API](../../README.md#api-reference)
- [KernelMemory Documentation](https://github.com/microsoft/kernel-memory)
- [SharePoint Graph API](https://learn.microsoft.com/en-us/graph/api/resources/sharepoint)

## License

This example code is provided under MIT license.
