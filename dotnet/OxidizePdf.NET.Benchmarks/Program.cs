using OxidizePdf.NET.Benchmarks;
using OxidizePdf.NET.Benchmarks.Adapters;
using OxidizePdf.NET.Benchmarks.Reporting;

// --- Parse args: --corpus <dir> --timeout <seconds> --out <dir> ---
string? corpus = null;
int timeoutSeconds = 30;
string outDir = ".";

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--corpus" when i + 1 < args.Length:
            corpus = args[++i];
            break;
        case "--timeout" when i + 1 < args.Length:
            if (!int.TryParse(args[++i], out timeoutSeconds) || timeoutSeconds <= 0)
            {
                Console.Error.WriteLine("--timeout must be a positive integer (seconds).");
                return 2;
            }
            break;
        case "--out" when i + 1 < args.Length:
            outDir = args[++i];
            break;
        default:
            Console.Error.WriteLine($"Unknown or incomplete argument: {args[i]}");
            Console.Error.WriteLine("Usage: --corpus <dir> [--timeout <seconds>] [--out <dir>]");
            return 2;
    }
}

// --- Fail fast on a bad corpus before any work ---
if (string.IsNullOrWhiteSpace(corpus))
{
    Console.Error.WriteLine("--corpus <dir> is required (e.g. --corpus ../fixtures).");
    return 2;
}
if (!Directory.Exists(corpus))
{
    Console.Error.WriteLine($"Corpus directory not found: {corpus}");
    return 2;
}

var files = Directory.EnumerateFiles(corpus, "*.pdf", SearchOption.AllDirectories)
    .OrderBy(f => f, StringComparer.Ordinal)
    .ToList();

if (files.Count == 0)
{
    Console.Error.WriteLine($"No *.pdf files found under: {corpus}");
    return 2;
}

Directory.CreateDirectory(outDir);

// --- Adapters. PdfPig is the reference page-count provider (neutral third party). ---
var adapters = new IPdfExtractorAdapter[]
{
    new OxidizeAdapter(),
    new PdfPigAdapter(),
    new IText7Adapter(),
    new DocnetAdapter(),
};
const string referenceAdapter = "PdfPig";

Console.WriteLine($"Corpus: {corpus} ({files.Count} PDFs)");
Console.WriteLine($"Adapters: {string.Join(", ", adapters.Select(a => a.Name))}");
Console.WriteLine($"Per-file timeout: {timeoutSeconds}s");
Console.WriteLine("Running...");

var runner = new BenchmarkRunner(adapters, TimeSpan.FromSeconds(timeoutSeconds));

// Progress: report every 50 files so a long run is not silent.
var results = new List<FileResult>(files.Count * adapters.Length);
for (int i = 0; i < files.Count; i++)
{
    byte[] bytes;
    try { bytes = File.ReadAllBytes(files[i]); }
    catch (Exception ex)
    {
        foreach (var a in adapters)
            results.Add(new FileResult(a.Name, files[i], 0, 0, ExtractStatus.Error, 0, ex.GetType().Name));
        continue;
    }
    foreach (var a in adapters)
        results.Add(runner.RunOne(a, files[i], bytes));

    if ((i + 1) % 50 == 0 || i + 1 == files.Count)
        Console.WriteLine($"  {i + 1}/{files.Count}");
}

var aggregates = ResultsAggregator.Aggregate(results, referenceAdapter);
var env = EnvironmentInfo.Capture(Path.GetFullPath(corpus), files.Count, adapters);

string jsonPath = Path.Combine(outDir, "results.json");
string mdPath = Path.Combine(outDir, "results.md");
JsonReportWriter.Write(jsonPath, env, results, aggregates);
MarkdownReportWriter.Write(mdPath, env, aggregates);

Console.WriteLine($"Wrote {jsonPath}");
Console.WriteLine($"Wrote {mdPath}");
Console.WriteLine($"Common-success subset: {aggregates.CommonSuccessFiles.Count} of {files.Count}");
return 0;
