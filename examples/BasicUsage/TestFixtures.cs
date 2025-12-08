using OxidizePdf.NET;
using OxidizePdf.NET.Models;
using System.Diagnostics;

namespace BasicUsage;

/// <summary>
/// Test runner for validating oxidize-pdf-dotnet with 801 real-world PDFs
/// </summary>
public class TestFixtures
{
    private const string FixturesPath = "/Users/santifdezmunoz/Documents/repos/BelowZero/oxidizePdf/fixtures";

    public static async Task RunAsync(int maxFiles = 20)
    {
        Console.WriteLine("OxidizePdf.NET - Fixtures Test Suite");
        Console.WriteLine("====================================\n");
        Console.WriteLine($"Library version: {PdfExtractor.Version}\n");

        // Get all PDF files
        var pdfFiles = Directory.GetFiles(FixturesPath, "*.pdf")
            .Take(maxFiles)
            .ToArray();

        Console.WriteLine($"Testing with {pdfFiles.Length} PDFs from fixtures directory");
        Console.WriteLine($"(Total available: {Directory.GetFiles(FixturesPath, "*.pdf").Length})\n");

        var stats = new TestStatistics();
        var stopwatch = Stopwatch.StartNew();

        using var extractor = new PdfExtractor();

        foreach (var pdfPath in pdfFiles)
        {
            var fileName = Path.GetFileName(pdfPath);
            await TestSinglePdf(extractor, pdfPath, fileName, stats);
        }

        stopwatch.Stop();

        // Display results
        DisplayResults(stats, stopwatch.Elapsed, pdfFiles.Length);
    }

    private static async Task TestSinglePdf(
        PdfExtractor extractor,
        string pdfPath,
        string fileName,
        TestStatistics stats)
    {
        try
        {
            var fileInfo = new FileInfo(pdfPath);
            stats.TotalSize += fileInfo.Length;

            var pdfBytes = await File.ReadAllBytesAsync(pdfPath);

            var sw = Stopwatch.StartNew();

            // Test 1: Plain text extraction
            var text = await extractor.ExtractTextAsync(pdfBytes);
            stats.TextExtractionTime += sw.Elapsed;

            // Test 2: Chunked extraction
            sw.Restart();
            var chunks = await extractor.ExtractChunksAsync(
                pdfBytes,
                new ChunkOptions
                {
                    MaxChunkSize = 512,
                    Overlap = 50,
                    PreserveSentenceBoundaries = true
                }
            );
            stats.ChunkExtractionTime += sw.Elapsed;

            stats.SuccessCount++;
            stats.TotalChars += text.Length;
            stats.TotalChunks += chunks.Count;

            // Sample output for first few files
            if (stats.SuccessCount <= 5)
            {
                Console.WriteLine($"✓ {fileName}");
                Console.WriteLine($"  Size: {fileInfo.Length:N0} bytes");
                Console.WriteLine($"  Text: {text.Length:N0} chars");
                Console.WriteLine($"  Chunks: {chunks.Count}");
                Console.WriteLine($"  Time: {sw.ElapsedMilliseconds}ms\n");
            }
            else if (stats.SuccessCount % 10 == 0)
            {
                Console.Write(".");
            }
        }
        catch (PdfExtractionException ex)
        {
            stats.ErrorCount++;
            stats.Errors.Add((fileName, ex.Message));

            if (stats.ErrorCount <= 5)
            {
                Console.WriteLine($"✗ {fileName}: {ex.Message}\n");
            }
        }
        catch (Exception ex)
        {
            stats.ErrorCount++;
            stats.Errors.Add((fileName, $"Unexpected error: {ex.Message}"));

            if (stats.ErrorCount <= 5)
            {
                Console.WriteLine($"✗ {fileName}: Unexpected error\n");
            }
        }
    }

    private static void DisplayResults(TestStatistics stats, TimeSpan elapsed, int totalFiles)
    {
        Console.WriteLine("\n\n=== Test Results ===\n");

        Console.WriteLine($"Total files processed: {totalFiles}");
        Console.WriteLine($"Successful extractions: {stats.SuccessCount} ({stats.SuccessRate:F1}%)");
        Console.WriteLine($"Failed extractions: {stats.ErrorCount}");
        Console.WriteLine();

        Console.WriteLine($"Total text extracted: {stats.TotalChars:N0} chars");
        Console.WriteLine($"Total chunks generated: {stats.TotalChunks:N0}");
        Console.WriteLine($"Total PDF size: {stats.TotalSize / 1024.0 / 1024.0:F2} MB");
        Console.WriteLine();

        Console.WriteLine($"Total time: {elapsed.TotalSeconds:F2}s");
        Console.WriteLine($"Text extraction: {stats.TextExtractionTime.TotalSeconds:F2}s");
        Console.WriteLine($"Chunk extraction: {stats.ChunkExtractionTime.TotalSeconds:F2}s");
        Console.WriteLine();

        Console.WriteLine($"Throughput:");
        Console.WriteLine($"  {totalFiles / elapsed.TotalSeconds:F2} PDFs/second");
        Console.WriteLine($"  {stats.TotalSize / 1024.0 / 1024.0 / elapsed.TotalSeconds:F2} MB/second");
        Console.WriteLine($"  {stats.AvgTimePerPdf:F0}ms per PDF (average)");
        Console.WriteLine();

        if (stats.ErrorCount > 0)
        {
            Console.WriteLine("\nFirst errors:");
            foreach (var (file, error) in stats.Errors.Take(5))
            {
                Console.WriteLine($"  - {file}: {error}");
            }

            if (stats.ErrorCount > 5)
            {
                Console.WriteLine($"  ... and {stats.ErrorCount - 5} more errors");
            }
        }

        Console.WriteLine("\n=== Summary ===");
        Console.WriteLine($"Success rate: {stats.SuccessRate:F1}%");
        Console.WriteLine($"Performance: {totalFiles / elapsed.TotalSeconds:F2} PDFs/sec");
        Console.WriteLine(stats.SuccessRate >= 95.0 ? "✅ PASS" : "⚠️  SOME FAILURES");
    }

    private class TestStatistics
    {
        public int SuccessCount { get; set; }
        public int ErrorCount { get; set; }
        public long TotalSize { get; set; }
        public long TotalChars { get; set; }
        public int TotalChunks { get; set; }
        public TimeSpan TextExtractionTime { get; set; }
        public TimeSpan ChunkExtractionTime { get; set; }
        public List<(string File, string Error)> Errors { get; } = new();

        public double SuccessRate =>
            (SuccessCount + ErrorCount) > 0
                ? (SuccessCount * 100.0) / (SuccessCount + ErrorCount)
                : 0;

        public double AvgTimePerPdf =>
            SuccessCount > 0
                ? (TextExtractionTime.TotalMilliseconds + ChunkExtractionTime.TotalMilliseconds) / SuccessCount
                : 0;
    }
}
