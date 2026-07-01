using OxidizePdf.NET.Benchmarks;

namespace OxidizePdf.NET.Benchmarks.Tests;

public class ResultsAggregatorTests
{
    private static FileResult Ok(string adapter, string file, int pages, long ms) =>
        new(adapter, file, pages, ms, ExtractStatus.Ok, 100, null);

    private static FileResult Bad(string adapter, string file, ExtractStatus status) =>
        new(adapter, file, 0, 0, status, 0, status == ExtractStatus.Error ? "X" : null);

    [Fact]
    public void CommonSuccess_IsIntersectionOfOkFilesAcrossAllAdapters()
    {
        // A and B both Ok on f1. A Ok on f2 but B Errored -> f2 excluded.
        var results = new List<FileResult>
        {
            Ok("A", "f1.pdf", 10, 100), Ok("B", "f1.pdf", 10, 200),
            Ok("A", "f2.pdf", 10, 100), Bad("B", "f2.pdf", ExtractStatus.Error),
        };

        var agg = ResultsAggregator.Aggregate(results, referenceAdapter: "B");

        Assert.Equal(new[] { "f1.pdf" }, agg.CommonSuccessFiles);
    }

    [Fact]
    public void Speed_IsComputedOnlyOverCommonSubset_UsingReferencePageCount()
    {
        // f1 common (ref B says 10 pages). f2 not common (B failed).
        // A on f1 took 100ms -> 10 ms/page. A's f2 time must NOT count.
        var results = new List<FileResult>
        {
            Ok("A", "f1.pdf", 999, 100), Ok("B", "f1.pdf", 10, 200),
            Ok("A", "f2.pdf", 999, 5),   Bad("B", "f2.pdf", ExtractStatus.Timeout),
        };

        var agg = ResultsAggregator.Aggregate(results, referenceAdapter: "B");
        var speedA = agg.Speed.Single(s => s.Adapter == "A");

        Assert.Equal(1, speedA.SampleSize);           // only f1
        Assert.Equal(10.0, speedA.MedianMsPerPage);   // 100ms / 10 ref pages
    }

    [Fact]
    public void MedianMsPerPage_IsTrueMedianOverCommonFiles()
    {
        // Three common files; ref C = 1 page each so ms/page == ms.
        var results = new List<FileResult>
        {
            Ok("A", "f1.pdf", 1, 10), Ok("A", "f2.pdf", 1, 50), Ok("A", "f3.pdf", 1, 90),
            Ok("C", "f1.pdf", 1, 1),  Ok("C", "f2.pdf", 1, 1),  Ok("C", "f3.pdf", 1, 1),
        };

        var agg = ResultsAggregator.Aggregate(results, referenceAdapter: "C");
        var speedA = agg.Speed.Single(s => s.Adapter == "A");

        Assert.Equal(50.0, speedA.MedianMsPerPage);   // median of {10,50,90}
    }

    [Fact]
    public void Robustness_IsOverFullCorpus_PerAdapter()
    {
        var results = new List<FileResult>
        {
            Ok("A", "f1.pdf", 1, 10), Ok("A", "f2.pdf", 1, 10),
            Bad("A", "f3.pdf", ExtractStatus.Empty), Bad("A", "f4.pdf", ExtractStatus.Error),
            Ok("B", "f1.pdf", 1, 10), Bad("B", "f2.pdf", ExtractStatus.Timeout),
            Ok("B", "f3.pdf", 1, 10), Ok("B", "f4.pdf", 1, 10),
        };

        var agg = ResultsAggregator.Aggregate(results, referenceAdapter: "B");
        var robA = agg.Robustness.Single(r => r.Adapter == "A");
        var robB = agg.Robustness.Single(r => r.Adapter == "B");

        Assert.Equal(4, robA.Total);
        Assert.Equal(2, robA.Ok);
        Assert.Equal(1, robA.Empty);
        Assert.Equal(1, robA.Error);
        Assert.Equal(0, robA.Timeout);

        Assert.Equal(4, robB.Total);
        Assert.Equal(3, robB.Ok);
        Assert.Equal(1, robB.Timeout);
    }
}
