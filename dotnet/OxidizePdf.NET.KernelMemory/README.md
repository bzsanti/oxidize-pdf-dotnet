# OxidizePdf.NET.KernelMemory

A Microsoft Kernel Memory `IContentDecoder` backed by [oxidize-pdf](https://github.com/bzsanti/oxidizePdf).
Emits one KM partition per oxidize-pdf structure-aware chunk — heading context and
source page preserved.

```csharp
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Handlers;
using OxidizePdf.NET.KernelMemory;

var memory = new KernelMemoryBuilder()
    .WithOpenAIDefaults(apiKey)
    .WithOxidizePdf()
    .WithoutDefaultHandlers()
    .Build<MemoryServerless>();

// Register the ingestion steps, swapping the "partition" step for ours.
memory.Orchestrator.AddHandler<TextExtractionHandler>("extract");
memory.Orchestrator.AddHandler<OxidizeChunkPartitioningHandler>("partition");
memory.Orchestrator.AddHandler<GenerateEmbeddingsHandler>("gen_embeddings");
memory.Orchestrator.AddHandler<SaveRecordsHandler>("save_records");

await memory.ImportDocumentAsync(new Document("doc").AddFile("report.pdf"));
```

The custom `partition` handler makes oxidize-pdf's chunks land in the vector store
1:1; without it Kernel Memory re-chunks the text. `AddHandler` throws on duplicate
step names, so the defaults are skipped (`WithoutDefaultHandlers`) and re-registered.

> **Required:** `WithOxidizePdf()` alone is **not** enough — it only registers the
> decoder. You **must** also register `OxidizeChunkPartitioningHandler` on the
> `partition` step as shown above. Without it, Kernel Memory silently falls back to
> its default re-chunker and oxidize-pdf's structure-aware chunks are discarded.
>
> **PDF-only:** this connector's partition handler supports PDF ingestion only. In a
> mixed-format KM instance it throws `NotSupportedException` for non-PDF files. The KM
> packages are exact-pinned (`[0.98.250508.3]`) because the handler reads KM's internal
> `ExtractedContent` artifact format.
