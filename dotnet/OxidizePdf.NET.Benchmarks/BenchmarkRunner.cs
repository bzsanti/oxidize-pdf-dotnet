using System.Diagnostics;

namespace OxidizePdf.NET.Benchmarks;

/// <summary>
/// Runs every adapter over every file, isolating per-file timeouts and
/// exceptions so a single bad PDF never aborts the run.
/// </summary>
public sealed class BenchmarkRunner
{
    private readonly TimeSpan _timeout;

    public BenchmarkRunner(IReadOnlyList<IPdfExtractorAdapter> adapters, TimeSpan timeout)
    {
        Adapters = adapters;
        _timeout = timeout;
    }

    public IReadOnlyList<IPdfExtractorAdapter> Adapters { get; }

    /// <summary>Run one (adapter, file). Never throws; failures become statuses.</summary>
    public FileResult RunOne(IPdfExtractorAdapter adapter, string file, byte[] pdfBytes)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            // A synchronous native call cannot be hard-aborted; run it on a
            // background task and stop waiting after the budget. The task may
            // linger, but the run continues — that is the spec's contract.
            var task = Task.Run(() => adapter.Extract(pdfBytes));
            if (!task.Wait(_timeout))
            {
                sw.Stop();
                return new FileResult(adapter.Name, file, 0, sw.ElapsedMilliseconds,
                    ExtractStatus.Timeout, 0, null);
            }

            sw.Stop();
            var result = task.Result;
            var status = result.Text.Length == 0 ? ExtractStatus.Empty : ExtractStatus.Ok;
            return new FileResult(adapter.Name, file, result.PageCount, sw.ElapsedMilliseconds,
                status, result.Text.Length, null);
        }
        catch (Exception ex)
        {
            sw.Stop();
            var inner = ex is AggregateException ae && ae.InnerException is not null
                ? ae.InnerException
                : ex;
            return new FileResult(adapter.Name, file, 0, sw.ElapsedMilliseconds,
                ExtractStatus.Error, 0, inner.GetType().Name);
        }
    }

    /// <summary>Run all adapters over all files, in order.</summary>
    public List<FileResult> Run(IReadOnlyList<string> files)
    {
        var results = new List<FileResult>(files.Count * Adapters.Count);
        foreach (var file in files)
        {
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(file);
            }
            catch (Exception ex)
            {
                // Unreadable file: record an Error for every adapter, keep going.
                foreach (var adapter in Adapters)
                {
                    results.Add(new FileResult(adapter.Name, file, 0, 0,
                        ExtractStatus.Error, 0, ex.GetType().Name));
                }
                continue;
            }

            foreach (var adapter in Adapters)
            {
                results.Add(RunOne(adapter, file, bytes));
            }
        }
        return results;
    }
}
