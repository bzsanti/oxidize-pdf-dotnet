using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Handlers;
using OxidizePdf.NET.KernelMemory;

string pdfPath = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "fixtures", "sample.pdf");
byte[] pdfBytes = await File.ReadAllBytesAsync(pdfPath);

Console.WriteLine("OxidizePdf.NET + Kernel Memory");
Console.WriteLine("==============================\n");

// 1. Always show oxidize-pdf's structure-aware chunks (no API key needed).
var decoder = new OxidizePdfDecoder();
var content = await decoder.DecodeAsync(new BinaryData(pdfBytes));
Console.WriteLine($"oxidize-pdf produced {content.Sections.Count} structure-aware chunks:");
foreach (var c in content.Sections.Take(5))
    Console.WriteLine($"  [page {c.PageNumber}] {Truncate(c.Content, 90)}");
Console.WriteLine();

// 2. With OPENAI_API_KEY, run the full index + semantic query loop.
string? apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.WriteLine("Set OPENAI_API_KEY to run the full Kernel Memory index + semantic query loop.");
    return;
}

// AddHandler throws on duplicate step names, so skip the default handler set and
// register the four ingestion steps ourselves, swapping the "partition" step for ours.
// Real KM step names (verified): extract / partition / gen_embeddings / save_records.
var memory = new KernelMemoryBuilder()
    .WithOpenAIDefaults(apiKey)
    .WithOxidizePdf()
    .WithoutDefaultHandlers()
    .Build<MemoryServerless>();

memory.Orchestrator.AddHandler<TextExtractionHandler>("extract");
memory.Orchestrator.AddHandler<OxidizeChunkPartitioningHandler>("partition"); // 1:1 chunks
memory.Orchestrator.AddHandler<GenerateEmbeddingsHandler>("gen_embeddings");
memory.Orchestrator.AddHandler<SaveRecordsHandler>("save_records");

using var stream = new MemoryStream(pdfBytes);
await memory.ImportDocumentAsync(new Document("sample").AddStream(Path.GetFileName(pdfPath), stream));
Console.WriteLine("Indexed. Asking a question...\n");

var answer = await memory.AskAsync("What is this document about?");
Console.WriteLine($"Q: What is this document about?\nA: {answer.Result}");

static string Truncate(string s, int n) => s.Length <= n ? s : string.Concat(s.AsSpan(0, n), "...");
