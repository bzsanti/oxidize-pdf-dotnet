using OxidizePdf.NET;
using OxidizePdf.NET.Ai;
using OxidizePdf.NET.Models;
using OxidizePdf.NET.Pipeline;

namespace BasicUsage;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("OxidizePdf.NET - Basic Usage Example");
        Console.WriteLine("====================================\n");

        // Display version
        Console.WriteLine($"Library version: {PdfExtractor.Version}\n");

        // Check for test mode
        if (args.Length > 0 && args[0] == "--test-fixtures")
        {
            var maxFiles = args.Length > 1 && int.TryParse(args[1], out var n) ? n : 20;
            await TestFixtures.RunAsync(maxFiles);
            return;
        }

        // Check if PDF file provided
        if (args.Length == 0)
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  BasicUsage <path-to-pdf>           # Extract single PDF");
            Console.WriteLine("  BasicUsage --test-fixtures [count] # Test with fixtures (default: 20)");
            Console.WriteLine("\nExamples:");
            Console.WriteLine("  BasicUsage sample.pdf");
            Console.WriteLine("  BasicUsage --test-fixtures 50\n");
            return;
        }

        var pdfPath = args[0];
        if (!File.Exists(pdfPath))
        {
            Console.WriteLine($"Error: File not found: {pdfPath}");
            return;
        }

        await ExtractFromFile(pdfPath);
    }

    static async Task ExtractFromFile(string pdfPath)
    {
        Console.WriteLine($"Reading PDF: {pdfPath}");

        // Read PDF bytes
        var pdfBytes = await File.ReadAllBytesAsync(pdfPath);
        Console.WriteLine($"PDF size: {pdfBytes.Length:N0} bytes\n");

        var extractor = new PdfExtractor();

        // Example 1: Extract plain text
        Console.WriteLine("Example 1: Plain Text Extraction");
        Console.WriteLine("---------------------------------");
        var startTime = DateTime.UtcNow;

        var text = await extractor.ExtractTextAsync(pdfBytes);

        var elapsed = DateTime.UtcNow - startTime;
        Console.WriteLine($"Extracted {text.Length:N0} characters in {elapsed.TotalMilliseconds:F2}ms");
        Console.WriteLine($"\nFirst 500 characters:\n{text[..Math.Min(500, text.Length)]}...\n");

        // Example 2 (LEGACY): character-based chunking via ChunkOptions.
        // Kept for one minor release. Prefer Example 3+ for new code.
#pragma warning disable CS0618 // ChunkOptions is obsolete; demo kept until removal.
        Console.WriteLine("\nExample 2: Chunked Extraction (LEGACY ChunkOptions)");
        Console.WriteLine("---------------------------------------------------");

        startTime = DateTime.UtcNow;

        var chunks = await extractor.ExtractChunksAsync(
            pdfBytes,
            new ChunkOptions
            {
                MaxChunkSize = 512,
                Overlap = 50,
                PreserveSentenceBoundaries = true,
                IncludeMetadata = true
            }
        );

        elapsed = DateTime.UtcNow - startTime;
        Console.WriteLine($"Extracted {chunks.Count} chunks in {elapsed.TotalMilliseconds:F2}ms");

        // Display first 3 chunks
        for (int i = 0; i < Math.Min(3, chunks.Count); i++)
        {
            var chunk = chunks[i];
            Console.WriteLine($"\n--- Chunk {chunk.Index + 1} ---");
            Console.WriteLine($"Page: {chunk.PageNumber}");
            Console.WriteLine($"Confidence: {chunk.Confidence:F2}");
            Console.WriteLine($"Position: ({chunk.BoundingBox.X:F1}, {chunk.BoundingBox.Y:F1})");
            Console.WriteLine($"Size: {chunk.BoundingBox.Width:F1} x {chunk.BoundingBox.Height:F1}");
            Console.WriteLine($"Text ({chunk.Text.Length} chars): {chunk.Text[..Math.Min(200, chunk.Text.Length)]}...");
        }

        Console.WriteLine($"\n... and {chunks.Count - 3} more chunks");
#pragma warning restore CS0618

        // Example 3 (NEW): RAG chunks with the Rag profile — token-aware,
        // structure-aware, ready for vector store ingestion.
        Console.WriteLine("\nExample 3: RAG profile demo");
        Console.WriteLine("---------------------------");
        var ragChunks = await extractor.RagChunksAsync(pdfBytes, ExtractionProfile.Rag);
        Console.WriteLine($"Extracted {ragChunks.Count} RAG chunks (profile=Rag)");
        foreach (var chunk in ragChunks.Take(3))
        {
            var preview = chunk.Text[..Math.Min(80, chunk.Text.Length)];
            Console.WriteLine($"  Chunk {chunk.ChunkIndex} pages=[{string.Join(",", chunk.PageNumbers)}] tokens≈{chunk.TokenEstimate}");
            Console.WriteLine($"    heading: {chunk.HeadingContext ?? "(none)"}");
            Console.WriteLine($"    text   : {preview}…");
        }

        // Example 4 (NEW): Custom partition config — XY-Cut reading order
        // for multi-column layouts plus a tighter table-confidence floor.
        Console.WriteLine("\nExample 4: Custom partition config (multi-column)");
        Console.WriteLine("-------------------------------------------------");
        var partitionCfg = new PartitionConfig()
            .WithReadingOrder(ReadingOrderStrategy.XyCut(20.0))
            .WithMinTableConfidence(0.7);
        var elements = await extractor.PartitionAsync(pdfBytes, partitionCfg);
        Console.WriteLine($"Got {elements.Count} semantic elements using XY-Cut reading order");

        // Example 5 (NEW): Markdown export with explicit options (RAG-012).
        Console.WriteLine("\nExample 5: Markdown with options");
        Console.WriteLine("--------------------------------");
        var md = await extractor.ToMarkdownAsync(
            pdfBytes,
            new MarkdownOptions { IncludeMetadata = false, IncludePageNumbers = true });
        Console.WriteLine(md[..Math.Min(200, md.Length)]);

        // Example 6 (NEW): Standalone DocumentChunker — works on raw text,
        // no PDF needed. Useful for chunking non-PDF sources before
        // embedding-store ingestion.
        Console.WriteLine("\nExample 6: Standalone DocumentChunker (no PDF)");
        Console.WriteLine("----------------------------------------------");
        var chunker = new DocumentChunker(chunkSize: 64, overlap: 8);
        var longText = "Paragraph one about oxidize-pdf. " +
                       string.Concat(Enumerable.Repeat("word ", 200));
        var textChunks = chunker.ChunkText(longText);
        Console.WriteLine($"Chunked {longText.Length} chars into {textChunks.Count} chunks");
        Console.WriteLine($"Token estimate for input: {DocumentChunker.EstimateTokens(longText)}");
    }

}
