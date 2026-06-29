# OxidizePdf.NET + Kernel Memory

Real end-to-end sample: oxidize-pdf's structure-aware RAG chunks dropped into
Microsoft Kernel Memory via the `OxidizePdf.NET.KernelMemory` connector.

## Run

```bash
# Keyless: prints oxidize-pdf's structure-aware chunks for the bundled PDF.
dotnet run --project examples/KernelMemory

# Full loop: index into Kernel Memory + ask a semantic question.
export OPENAI_API_KEY=sk-...
dotnet run --project examples/KernelMemory path/to/your.pdf
```

## Why the custom partition handler?

`.WithOxidizePdf()` registers a content decoder. Kernel Memory would then re-chunk
the extracted text with its default `partition` step, discarding oxidize-pdf's
structure-aware boundaries. So this sample calls `.WithoutDefaultHandlers()` and
registers the four ingestion steps itself, swapping the `partition` step for
`OxidizeChunkPartitioningHandler`. That handler reads the structured
`ExtractedContent` artifact and emits **one partition per oxidize-pdf chunk** —
each carrying heading context and source page — so the chunks you see keyless are
exactly what gets embedded and stored. (`AddHandler` throws on duplicate step
names, which is why the defaults are skipped rather than overridden.)
