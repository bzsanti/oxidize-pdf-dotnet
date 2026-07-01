using System.Text.Json;
using OxidizePdf.NET.Benchmarks;
using OxidizePdf.NET.Benchmarks.Reporting;

namespace OxidizePdf.NET.Benchmarks.Tests;

public class ReportWriterTests
{
    private static (EnvironmentInfo env, List<FileResult> results, Aggregates agg) Sample()
    {
        var env = new EnvironmentInfo(
            "host", "linux", "net8.0", "/corpus", 2,
            new[] { new AdapterInfo("OxidizePdf.NET", "MIT", "0.16.1") });
        var results = new List<FileResult>
        {
            new("OxidizePdf.NET", "f1.pdf", 1, 10, ExtractStatus.Ok, 100, null),
        };
        var agg = new Aggregates(
            new[] { "f1.pdf" },
            new[] { new SpeedMetric("OxidizePdf.NET", 10.0, 100.0, 1) },
            new[] { new RobustnessMetric("OxidizePdf.NET", 2, 1, 0, 1, 0) });
        return (env, results, agg);
    }

    [Fact]
    public void Json_RoundTripsEnvironmentResultsAndAggregates()
    {
        var (env, results, agg) = Sample();
        var path = Path.Combine(Path.GetTempPath(), $"bench-{Guid.NewGuid():N}.json");
        try
        {
            JsonReportWriter.Write(path, env, results, agg);
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;

            Assert.Equal("host", root.GetProperty("environment").GetProperty("machine").GetString());
            Assert.Equal(1, root.GetProperty("results").GetArrayLength());
            Assert.Equal("f1.pdf",
                root.GetProperty("aggregates").GetProperty("commonSuccessFiles")[0].GetString());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Markdown_ContainsSpeedRobustnessAndCapabilityTables()
    {
        var (env, _, agg) = Sample();
        string md = MarkdownReportWriter.Render(env, agg);

        Assert.Contains("## Speed", md);
        Assert.Contains("common-success subset", md);
        Assert.Contains("## Robustness", md);
        Assert.Contains("## Capability matrix", md);
        Assert.Contains("OxidizePdf.NET", md);
        // The speed table must state the subset size so the number is honest.
        Assert.Contains("1 of 2", md);
    }
}
