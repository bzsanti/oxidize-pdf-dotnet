using System.Globalization;
using System.Text;

namespace OxidizePdf.NET.Benchmarks.Reporting;

/// <summary>Renders the human-readable summary: speed + robustness + capability matrix.</summary>
public static class MarkdownReportWriter
{
    public static void Write(string path, EnvironmentInfo env, Aggregates aggregates) =>
        File.WriteAllText(path, Render(env, aggregates));

    public static string Render(EnvironmentInfo env, Aggregates aggregates)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# PDF Extraction Benchmark Results");
        sb.AppendLine();
        sb.AppendLine("## Environment");
        sb.AppendLine();
        sb.AppendLine($"- Machine: {env.Machine}");
        sb.AppendLine($"- OS: {env.Os}");
        sb.AppendLine($"- Runtime: {env.DotnetVersion}");
        sb.AppendLine($"- Corpus: `{env.CorpusPath}` ({env.FileCount} PDFs)");
        sb.AppendLine();
        foreach (var a in env.Adapters)
        {
            sb.AppendLine($"  - {a.Name} {a.Version} ({a.License})");
        }
        sb.AppendLine();

        int subset = aggregates.CommonSuccessFiles.Count;
        sb.AppendLine("## Speed");
        sb.AppendLine();
        sb.AppendLine($"Measured on the **common-success subset**: {subset} of {env.FileCount} "
            + "PDFs every library parsed with status Ok. ms/page uses the reference page count.");
        sb.AppendLine();
        sb.AppendLine("| Library | Median ms/page | PDFs/sec | Sample |");
        sb.AppendLine("|---|---:|---:|---:|");
        foreach (var s in aggregates.Speed)
        {
            sb.AppendLine($"| {s.Adapter} | {Fmt(s.MedianMsPerPage)} | {Fmt(s.PdfsPerSec)} | {s.SampleSize} |");
        }
        sb.AppendLine();

        sb.AppendLine("## Robustness");
        sb.AppendLine();
        sb.AppendLine($"Over the full corpus ({env.FileCount} PDFs), per library.");
        sb.AppendLine();
        sb.AppendLine("| Library | Total | % Ok | % Empty | % Error | % Timeout |");
        sb.AppendLine("|---|---:|---:|---:|---:|---:|");
        foreach (var r in aggregates.Robustness)
        {
            sb.AppendLine($"| {r.Adapter} | {r.Total} | {Pct(r.Ok, r.Total)} | {Pct(r.Empty, r.Total)} "
                + $"| {Pct(r.Error, r.Total)} | {Pct(r.Timeout, r.Total)} |");
        }
        sb.AppendLine();

        sb.AppendLine(CapabilityMatrix);
        return sb.ToString();
    }

    private static string Fmt(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Pct(int n, int total) =>
        total == 0 ? "-" : ((double)n / total * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";

    // Hand-authored qualitative comparison — NOT a computed score. Edit by hand
    // when capabilities change. Makes no quantitative quality claim.
    private const string CapabilityMatrix = """
        ## Capability matrix

        Qualitative feature comparison (hand-authored, not measured).

        | Capability | OxidizePdf.NET | PdfPig | iText7 | Docnet.Core |
        |---|:---:|:---:|:---:|:---:|
        | Plain text extraction | ✓ | ✓ | ✓ | ✓ |
        | Heading detection | ✓ | ✗ | ✗ | ✗ |
        | Table extraction | ✓ | ✗ | ✗ | ✗ |
        | Reading order / multi-column | ✓ | ✗ | ✗ | ✗ |
        | RAG chunking with page citations | ✓ | ✗ | ✗ | ✗ |
        """;
}
