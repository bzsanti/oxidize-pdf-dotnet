namespace OxidizePdf.NET.Benchmarks;

/// <summary>Speed for one adapter, measured over the common-success subset only.</summary>
public sealed record SpeedMetric(string Adapter, double MedianMsPerPage, double PdfsPerSec, int SampleSize);

/// <summary>Robustness for one adapter, over the full corpus.</summary>
public sealed record RobustnessMetric(string Adapter, int Total, int Ok, int Empty, int Error, int Timeout);

/// <summary>The reported aggregates.</summary>
public sealed record Aggregates(
    IReadOnlyList<string> CommonSuccessFiles,
    IReadOnlyList<SpeedMetric> Speed,
    IReadOnlyList<RobustnessMetric> Robustness);

/// <summary>
/// Turns raw <see cref="FileResult"/>s into reported metrics. Speed is computed
/// ONLY over the set of files every adapter extracted with status Ok, using the
/// reference adapter's page count as the denominator. Robustness is over all files.
/// </summary>
public static class ResultsAggregator
{
    public static Aggregates Aggregate(IReadOnlyList<FileResult> results, string referenceAdapter)
    {
        var adapters = results.Select(r => r.Adapter).Distinct().ToList();
        var allFiles = results.Select(r => r.File).Distinct().ToList();

        // Common-success = files where EVERY adapter has status Ok.
        var okByFile = results
            .Where(r => r.Status == ExtractStatus.Ok)
            .GroupBy(r => r.File)
            .ToDictionary(g => g.Key, g => g.Select(r => r.Adapter).ToHashSet());

        var commonFiles = allFiles
            .Where(f => okByFile.TryGetValue(f, out var oks) && oks.Count == adapters.Count)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        // Reference page count per common file (neutral denominator).
        var refPages = results
            .Where(r => r.Adapter == referenceAdapter && r.Status == ExtractStatus.Ok)
            .GroupBy(r => r.File)
            .ToDictionary(g => g.Key, g => g.First().PageCount);

        var lookup = results.ToDictionary(r => (r.Adapter, r.File));

        var speed = new List<SpeedMetric>();
        foreach (var adapter in adapters)
        {
            var msPerPage = new List<double>();
            long totalMs = 0;
            foreach (var file in commonFiles)
            {
                var fr = lookup[(adapter, file)];
                int pages = refPages.TryGetValue(file, out var p) && p > 0 ? p : 1;
                msPerPage.Add((double)fr.ElapsedMs / pages);
                totalMs += fr.ElapsedMs;
            }

            double median = Median(msPerPage);
            double pdfsPerSec = totalMs > 0 ? commonFiles.Count / (totalMs / 1000.0) : 0.0;
            speed.Add(new SpeedMetric(adapter, median, pdfsPerSec, commonFiles.Count));
        }

        var robustness = new List<RobustnessMetric>();
        foreach (var adapter in adapters)
        {
            var rows = results.Where(r => r.Adapter == adapter).ToList();
            robustness.Add(new RobustnessMetric(
                adapter,
                Total: rows.Count,
                Ok: rows.Count(r => r.Status == ExtractStatus.Ok),
                Empty: rows.Count(r => r.Status == ExtractStatus.Empty),
                Error: rows.Count(r => r.Status == ExtractStatus.Error),
                Timeout: rows.Count(r => r.Status == ExtractStatus.Timeout)));
        }

        return new Aggregates(commonFiles, speed, robustness);
    }

    private static double Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0) return 0.0;
        var sorted = values.OrderBy(v => v).ToList();
        int mid = sorted.Count / 2;
        return sorted.Count % 2 == 1
            ? sorted[mid]
            : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }
}
