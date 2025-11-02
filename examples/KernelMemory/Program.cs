using Microsoft.KernelMemory;
using OxidizePdf.NET;
using OxidizePdf.NET.Models;

namespace KernelMemoryExample;

/// <summary>
/// Complete example of SharePoint PDF crawler with KernelMemory integration
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("OxidizePdf.NET + KernelMemory Integration");
        Console.WriteLine("=========================================\n");

        // Initialize KernelMemory
        var memory = new KernelMemoryBuilder()
            .WithSimpleVectorDb()  // Use in-memory vector DB for demo
            .Build();

        // Initialize PDF extractor
        using var pdfExtractor = new PdfExtractor();

        Console.WriteLine($"PDF Extractor: {PdfExtractor.Version}");
        Console.WriteLine($"KernelMemory: Ready\n");

        // Example 1: Process single PDF
        await ProcessSinglePdf(pdfExtractor, memory);

        // Example 2: Simulate SharePoint crawler
        await SimulateSharePointCrawler(pdfExtractor, memory);

        // Example 3: Query the indexed documents
        await QueryDocuments(memory);
    }

    /// <summary>
    /// Example 1: Process a single PDF file
    /// </summary>
    static async Task ProcessSinglePdf(PdfExtractor extractor, IKernelMemory memory)
    {
        Console.WriteLine("Example 1: Processing Single PDF");
        Console.WriteLine("---------------------------------");

        // In real usage: byte[] pdfBytes = await File.ReadAllBytesAsync(pdfPath);
        // For demo: Use sample data or skip
        Console.WriteLine("Skipping (provide PDF path to test)");
        Console.WriteLine("Usage: var pdfBytes = await File.ReadAllBytesAsync(\"sample.pdf\");\n");

        await Task.CompletedTask;
    }

    /// <summary>
    /// Example 2: Simulate SharePoint document library crawler
    /// </summary>
    static async Task SimulateSharePointCrawler(PdfExtractor extractor, IKernelMemory memory)
    {
        Console.WriteLine("\nExample 2: SharePoint Crawler Simulation");
        Console.WriteLine("----------------------------------------");

        // Simulated SharePoint metadata
        var sharePointFiles = new[]
        {
            new SharePointFile
            {
                Id = "file-001",
                Name = "Q4_2024_Report.pdf",
                Url = "https://contoso.sharepoint.com/sites/Finance/Documents/Q4_2024_Report.pdf",
                Library = "Finance",
                Size = 245678
            },
            new SharePointFile
            {
                Id = "file-002",
                Name = "Product_Roadmap.pdf",
                Url = "https://contoso.sharepoint.com/sites/Product/Documents/Product_Roadmap.pdf",
                Library = "Product",
                Size = 1234567
            }
        };

        Console.WriteLine($"Found {sharePointFiles.Length} PDF files in SharePoint");
        Console.WriteLine("\nCrawling workflow:");
        Console.WriteLine("  1. Download PDF from SharePoint");
        Console.WriteLine("  2. Extract chunks with oxidize-pdf");
        Console.WriteLine("  3. Store chunks in KernelMemory");
        Console.WriteLine("  4. Generate embeddings");
        Console.WriteLine("  5. Index for semantic search\n");

        foreach (var file in sharePointFiles)
        {
            Console.WriteLine($"Processing: {file.Name}");
            Console.WriteLine($"  Source: {file.Library}");
            Console.WriteLine($"  Size: {file.Size:N0} bytes");

            // In real implementation:
            // 1. Download from SharePoint
            //    var pdfBytes = await DownloadFromSharePoint(file.Url);
            //
            // 2. Extract chunks
            //    var chunks = await extractor.ExtractChunksAsync(pdfBytes, new ChunkOptions
            //    {
            //        MaxChunkSize = 512,
            //        Overlap = 50,
            //        PreserveSentenceBoundaries = true
            //    });
            //
            // 3. Store in KernelMemory
            //    await StoreChunksInMemory(memory, file, chunks);

            Console.WriteLine($"  ✓ Indexed\n");
        }

        Console.WriteLine($"Crawler complete: {sharePointFiles.Length} files processed");
    }

    /// <summary>
    /// Store PDF chunks in KernelMemory with metadata
    /// </summary>
    static async Task StoreChunksInMemory(
        IKernelMemory memory,
        SharePointFile file,
        List<DocumentChunk> chunks)
    {
        foreach (var chunk in chunks)
        {
            var documentId = $"{file.Id}_p{chunk.PageNumber}_c{chunk.Index}";

            await memory.ImportTextAsync(
                text: chunk.Text,
                documentId: documentId,
                tags: new TagCollection
                {
                    ["source"] = file.Url,
                    ["fileName"] = file.Name,
                    ["library"] = file.Library,
                    ["page"] = chunk.PageNumber.ToString(),
                    ["chunkIndex"] = chunk.Index.ToString(),
                    ["confidence"] = chunk.Confidence.ToString("F2")
                }
            );
        }
    }

    /// <summary>
    /// Example 3: Query indexed documents
    /// </summary>
    static async Task QueryDocuments(IKernelMemory memory)
    {
        Console.WriteLine("\nExample 3: Semantic Search");
        Console.WriteLine("--------------------------");

        var queries = new[]
        {
            "What are the Q4 revenue projections?",
            "Product roadmap for next year",
            "Risk assessment findings"
        };

        Console.WriteLine("Sample queries:\n");
        foreach (var query in queries)
        {
            Console.WriteLine($"Q: {query}");
            Console.WriteLine("   (In real usage, this would search indexed documents)");

            // In real implementation:
            // var results = await memory.SearchAsync(query, limit: 5);
            // foreach (var result in results.Results)
            // {
            //     Console.WriteLine($"   - {result.SourceName} (score: {result.Relevance:F2})");
            //     Console.WriteLine($"     {result.Partitions[0].Text[..100]}...");
            // }

            Console.WriteLine();
        }
    }
}

/// <summary>
/// SharePoint file metadata
/// </summary>
record SharePointFile
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Url { get; init; }
    public required string Library { get; init; }
    public required long Size { get; init; }
}

/// <summary>
/// Full SharePoint crawler implementation (optional - requires authentication)
/// </summary>
class SharePointCrawler
{
    // Uncomment to implement full SharePoint integration:
    /*
    private readonly GraphServiceClient _graphClient;

    public SharePointCrawler(string tenantId, string clientId, string clientSecret)
    {
        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        _graphClient = new GraphServiceClient(credential);
    }

    public async Task<List<SharePointFile>> GetPdfFilesAsync(string siteId, string libraryName)
    {
        var files = new List<SharePointFile>();

        var drive = await _graphClient.Sites[siteId]
            .Drives
            .GetAsync(r => r.QueryParameters.Filter = $"name eq '{libraryName}'");

        if (drive?.Value == null || !drive.Value.Any())
            return files;

        var items = await _graphClient.Sites[siteId]
            .Drives[drive.Value[0].Id]
            .Root
            .Children
            .GetAsync(r =>
            {
                r.QueryParameters.Filter = "endsWith(name,'.pdf')";
                r.QueryParameters.Select = new[] { "id", "name", "size", "webUrl" };
            });

        if (items?.Value != null)
        {
            files.AddRange(items.Value.Select(item => new SharePointFile
            {
                Id = item.Id!,
                Name = item.Name!,
                Url = item.WebUrl!,
                Library = libraryName,
                Size = item.Size ?? 0
            }));
        }

        return files;
    }

    public async Task<byte[]> DownloadPdfAsync(string siteId, string driveId, string itemId)
    {
        using var stream = await _graphClient.Sites[siteId]
            .Drives[driveId]
            .Items[itemId]
            .Content
            .GetAsync();

        if (stream == null)
            throw new InvalidOperationException($"Failed to download file {itemId}");

        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }
    */
}
