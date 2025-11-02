using OxidizePdf.NET;
using OxidizePdf.NET.Models;

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

        using var extractor = new PdfExtractor();

        // Example 1: Extract plain text
        Console.WriteLine("Example 1: Plain Text Extraction");
        Console.WriteLine("---------------------------------");
        var startTime = DateTime.UtcNow;

        var text = await extractor.ExtractTextAsync(pdfBytes);

        var elapsed = DateTime.UtcNow - startTime;
        Console.WriteLine($"Extracted {text.Length:N0} characters in {elapsed.TotalMilliseconds:F2}ms");
        Console.WriteLine($"\nFirst 500 characters:\n{text[..Math.Min(500, text.Length)]}...\n");

        // Example 2: Extract chunks for RAG/LLM
        Console.WriteLine("\nExample 2: Chunked Extraction (RAG/LLM optimized)");
        Console.WriteLine("--------------------------------------------------");

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
    }

}
