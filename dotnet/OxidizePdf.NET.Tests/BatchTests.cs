using System.Collections.Concurrent;

namespace OxidizePdf.NET.Tests;

public class BatchTests
{
    [Fact]
    public void MixedOutcomesPreserveIndicesDuplicatesAndBoundedConcurrency()
    {
        int active = 0, maximum = 0;
        var progress = new ConcurrentQueue<PdfBatchProgress>();
        var jobs = Enumerable.Range(0, 12).Select<int, PdfBatchJob>(i => new PdfBatchJob.Custom("same-name", () =>
        {
            int now = Interlocked.Increment(ref active);
            int observed;
            do { observed = Volatile.Read(ref maximum); } while (now > observed && Interlocked.CompareExchange(ref maximum, now, observed) != observed);
            try { Thread.Sleep(5); if (i % 3 == 0) throw new InvalidOperationException($"failure-{i}"); }
            finally { Interlocked.Decrement(ref active); }
        })).ToArray();
        var result = PdfBatch.Run(jobs, new() { Parallelism = 2, ProgressIntervalMs = 1 }, progress.Enqueue);
        Assert.Equal(12, result.TotalJobs); Assert.Equal(8, result.Successful); Assert.Equal(4, result.Failed);
        Assert.InRange(maximum, 1, 2); Assert.Equal(0, active);
        Assert.Equal(Enumerable.Range(0, 12), result.Results.Select(r => r.Index));
        for (int i = 0; i < 12; i++)
        {
            Assert.Equal("same-name", result.Results[i].Name);
            Assert.Equal(i % 3 == 0 ? PdfBatchStatus.Failed : PdfBatchStatus.Success, result.Results[i].Status);
            if (i % 3 == 0) Assert.Equal($"failure-{i}", result.Results[i].CallbackError);
        }
        Assert.True(progress.Last().IsComplete); Assert.Equal(4, progress.Last().FailedJobs);
    }
    [Fact]
    public void SerialStopAndTimeoutContractsDoNotClaimInterruption()
    {
        int called = 0;
        var stopped = PdfBatch.Run([new PdfBatchJob.Custom("fail", () => throw new Exception("failure")),
            new PdfBatchJob.Custom("later", () => called++)], new() { Parallelism = 1, StopOnError = true });
        Assert.True(stopped.Cancelled); Assert.Equal(PdfBatchStatus.Cancelled, stopped.Results[1].Status); Assert.Equal(0, called);
        var zero = PdfBatch.Run([new PdfBatchJob.Custom("zero", () => called++)], new() { JobTimeoutMs = 0 });
        Assert.Equal(PdfBatchStatus.Failed, zero.Results[0].Status); Assert.Equal(0, called);
        var late = PdfBatch.Run([new PdfBatchJob.Custom("late", () => { Thread.Sleep(25); called++; })], new() { JobTimeoutMs = 1 });
        Assert.Equal(PdfBatchStatus.Failed, late.Results[0].Status); Assert.Equal(1, called);
        Assert.Contains("not interrupted", late.Results[0].Error);
    }
    [Fact]
    public void UnsupportedMemoryAndProgressFailuresLeaveNoRunningCallbacks()
    {
        int called = 0;
        Assert.Throws<PdfBatchException>(() => PdfBatch.Run([new PdfBatchJob.Custom("never", () => called++)], new() { MemoryLimitPerWorker = 1 }));
        Assert.Equal(0, called);
        foreach (var unsupported in new PdfBatchJob[] { new PdfBatchJob.Rotate("in.pdf", "out.pdf", 90), new PdfBatchJob.Compress("in.pdf", "out.pdf", 80) })
        {
            var unsupportedError = Assert.Throws<PdfBatchException>(() => PdfBatch.Run([new PdfBatchJob.Custom("never", () => called++), unsupported]));
            Assert.Contains("unsupported", unsupportedError.Message);
            Assert.Equal(0, called);
        }
        Assert.Throws<OperationCanceledException>(() => PdfBatch.Run([new PdfBatchJob.Custom("never", () => called++)], ct: new CancellationToken(true)));
        var error = Assert.Throws<PdfBatchException>(() => PdfBatch.Run([new PdfBatchJob.Custom("runs", () => called++)], progress: _ => throw new InvalidOperationException("progress")));
        Assert.IsType<InvalidOperationException>(error.InnerException); Assert.Equal(1, called);
        Assert.False(PdfBatch.SupportsRayon); Assert.False(PdfBatch.SupportsPerformanceEngine);
        Assert.Empty(PdfBatch.Run([]).Results);
        Assert.Throws<PdfBatchException>(() => PdfBatch.Run([], new() { Parallelism = 0 }));
    }
    [Fact]
    public async Task BuiltInMergeAndExtractWriteReadableFilesAndRetainFailures()
    {
        string directory = Path.Combine(Path.GetTempPath(), "oxidize-batch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var doc = new PdfDocument(); using var page = new PdfPage(321, 456); doc.AddPage(page);
            string input = Path.Combine(directory, "input.pdf"), merged = Path.Combine(directory, "merged.pdf"), extracted = Path.Combine(directory, "extracted.pdf");
            File.WriteAllBytes(input, doc.SaveToBytes());
            var result = PdfBatch.Run([new PdfBatchJob.Merge([input, input], merged), new PdfBatchJob.Extract(input, extracted, [0]),
                new PdfBatchJob.Merge([Path.Combine(directory, "missing.pdf")], Path.Combine(directory, "bad.pdf"))], new() { Parallelism = 1 });
            Assert.Equal(PdfBatchStatus.Success, result.Results[0].Status); Assert.Equal(PdfBatchStatus.Success, result.Results[1].Status);
            Assert.Equal(PdfBatchStatus.Failed, result.Results[2].Status);
            Assert.Contains(merged, result.Results[0].OutputFiles!);
            Assert.Equal(2, await new PdfExtractor().GetPageCountAsync(File.ReadAllBytes(merged)));
            Assert.Equal(1, await new PdfExtractor().GetPageCountAsync(File.ReadAllBytes(extracted)));
            var split = PdfBatch.Run([new PdfBatchJob.Split(merged, Path.Combine(directory, "part-%d.pdf"))]);
            Assert.Equal(PdfBatchStatus.Success, split.Results[0].Status);
            Assert.Equal(2, split.Results[0].OutputFiles!.Count);
            foreach (string output in split.Results[0].OutputFiles!)
                Assert.Equal(1, await new PdfExtractor().GetPageCountAsync(File.ReadAllBytes(output)));

        }
        finally { Directory.Delete(directory, true); }
    }
}
