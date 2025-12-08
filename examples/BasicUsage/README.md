# BasicUsage Example

Simple example demonstrating PDF text extraction with OxidizePdf.NET.

## Running the Example

```bash
# Build the project
dotnet build

# Run with a PDF file
dotnet run path/to/your-document.pdf

# Or run without arguments to see usage
dotnet run
```

## What It Demonstrates

1. **Plain Text Extraction** - Extract all text from a PDF
2. **Chunked Extraction** - Extract text in optimized chunks for RAG/LLM pipelines
3. **Performance Metrics** - Measure extraction speed
4. **Metadata Access** - Display chunk metadata (page numbers, positions, confidence)

## Sample Output

```
OxidizePdf.NET - Basic Usage Example
====================================

Library version: oxidize-pdf-ffi v0.1.0 (oxidize-pdf v1.6.4)

Reading PDF: sample.pdf
PDF size: 45,672 bytes

Example 1: Plain Text Extraction
---------------------------------
Extracted 2,543 characters in 15.23ms

First 500 characters:
Lorem ipsum dolor sit amet, consectetur adipiscing elit...

Example 2: Chunked Extraction (RAG/LLM optimized)
--------------------------------------------------
Extracted 8 chunks in 16.87ms

--- Chunk 1 ---
Page: 1
Confidence: 1.00
Position: (72.0, 720.0)
Size: 450.0 x 12.0
Text (487 chars): Lorem ipsum dolor sit amet...

--- Chunk 2 ---
Page: 1
Confidence: 1.00
Position: (72.0, 650.0)
Size: 450.0 x 12.0
Text (512 chars): Sed do eiusmod tempor incididunt...

... and 6 more chunks
```

## See Also

- [KernelMemory Example](../KernelMemory/) - Full SharePoint crawler with RAG pipeline
- [API Documentation](../../README.md#api-reference)
