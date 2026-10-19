using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OxidizePdf.NET;

/// <summary>File jobs execute core operations and may write files. Custom jobs execute
/// caller code on native worker threads; exceptions become per-item failures.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "Kind")]
[JsonDerivedType(typeof(Split), "Split")]
[JsonDerivedType(typeof(Merge), "Merge")]
[JsonDerivedType(typeof(Rotate), "Rotate")]
[JsonDerivedType(typeof(Extract), "Extract")]
[JsonDerivedType(typeof(Compress), "Compress")]
[JsonDerivedType(typeof(Custom), "Custom")]
public abstract record PdfBatchJob
{
    /// <summary>Split into files using the core output pattern.</summary>
    public sealed record Split(string Input, string OutputPattern, int PagesPerFile = 1) : PdfBatchJob;
    /// <summary>Merge input files to Output.</summary>
    public sealed record Merge(IReadOnlyList<string> Inputs, string Output) : PdfBatchJob;
    /// <summary>Unsupported in core 5.5.0; rejected before any batch work starts.</summary>
    public sealed record Rotate(string Input, string Output, int Rotation, IReadOnlyList<int>? Pages = null) : PdfBatchJob;
    /// <summary>Extract selected core page indices.</summary>
    public sealed record Extract(string Input, string Output, IReadOnlyList<int> Pages) : PdfBatchJob;
    /// <summary>Unsupported in core 5.5.0; rejected before any batch work starts.</summary>
    public sealed record Compress(string Input, string Output, byte Quality) : PdfBatchJob;
    /// <summary>Explicit caller operation; never sourced from PDF scripts.</summary>
    public sealed record Custom(string Name, [property: JsonIgnore] Action Operation) : PdfBatchJob;
}
/// <summary>Core worker options. No allocator/process memory cap is available.</summary>
public sealed record PdfBatchOptions
{
    /// <summary>Worker count, 1..64. One worker gives serial execution.</summary>
    public int Parallelism { get; init; } = Math.Min(Environment.ProcessorCount, 8);
    /// <summary>Only zero (no cap) is supported; nonzero requests fail before work starts.</summary>
    public ulong MemoryLimitPerWorker { get; init; }
    /// <summary>Progress interval 0..60000ms. Core normalizes zero to 1ms.</summary>
    public uint ProgressIntervalMs { get; init; } = 100;
    /// <summary>Stop queued work on first failure. Already running work may finish.</summary>
    public bool StopOnError { get; init; }
    /// <summary>Checked after completion, without interrupting work or rolling back files.
    /// Zero prevents a job from starting; null disables the budget.</summary>
    public ulong? JobTimeoutMs { get; init; } = 300_000;
}
/// <summary>Per-job outcome.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PdfBatchStatus>))]
public enum PdfBatchStatus
{
    /// <summary>Completed within the budget.</summary>
    Success,
    /// <summary>Core/callback failure or elapsed-time budget exceeded.</summary>
    Failed,
    /// <summary>Queued operation skipped after cancellation.</summary>
    Cancelled,
}
/// <summary>Owned outcome in original input order; names need not be unique.</summary>
public sealed record PdfBatchJobResult(int Index, string Name, PdfBatchStatus Status, double? DurationSeconds,
    string? Error, IReadOnlyList<string>? OutputFiles)
{
    /// <summary>Caller exception message, if a custom callback failed. Core Error is retained.</summary>
    public string? CallbackError { get; init; }
}
/// <summary>Core summary with all per-item outcomes.</summary>
public sealed record PdfBatchResult(int TotalJobs, int Successful, int Failed, bool Cancelled, double DurationSeconds,
    IReadOnlyList<PdfBatchJobResult> Results);
/// <summary>Core progress. FailedJobs includes cancelled outcomes; Percentage counts successes only.</summary>
public sealed record PdfBatchProgress(int TotalJobs, int CompletedJobs, int FailedJobs, int RunningJobs,
    double ElapsedSeconds, double? EstimatedRemainingSeconds, double Throughput, double Percentage, bool IsComplete);

/// <summary>Explicit synchronous batch execution through core workers. Supports the same
/// RIDs as the native library. Rayon/performance engines are not compiled in this build.
/// Caller paths may be overwritten by core operations; jobs must use nonconflicting outputs.</summary>
public static class PdfBatch
{
    /// <summary>Optional Rayon engine is unavailable in this build.</summary>
    public static bool SupportsRayon => false;
    /// <summary>Optional performance engine is unavailable in this build.</summary>
    public static bool SupportsPerformanceEngine => false;
    /// <summary>Execute up to 256 jobs / 1 MiB JSON. Cancellation is checked before starting;
    /// the consuming core BatchProcessor exposes no running cancellation handle. Custom
    /// callbacks may implement cooperative cancellation themselves. All workers/callbacks
    /// have finished before return; callbacks must not synchronously wait on this call.</summary>
    /// <remarks>Progress is invoked on native threads, without a synchronization context.
    /// A progress exception is rethrown after workers join; side effects are not rolled back.</remarks>
    public static PdfBatchResult Run(IReadOnlyList<PdfBatchJob> jobs, PdfBatchOptions? options = null,
        Action<PdfBatchProgress>? progress = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested(); ArgumentNullException.ThrowIfNull(jobs);
        if (jobs.Count > 256) throw new ArgumentOutOfRangeException(nameof(jobs));
        var snapshot = jobs.ToArray();
        foreach (var job in snapshot)
        {
            ArgumentNullException.ThrowIfNull(job);
            if (job is PdfBatchJob.Custom custom) ArgumentNullException.ThrowIfNull(custom.Operation);
        }
        var input = JsonSerializer.SerializeToUtf8Bytes(new { Jobs = snapshot, Options = options ?? new() });
        if (input.Length > 1_048_576) throw new ArgumentOutOfRangeException(nameof(jobs), "Batch JSON exceeds 1 MiB.");
        var failures = new ConcurrentDictionary<int, string>();
        Exception? progressError = null;
        NativeMethods.BatchJobCallback callback = index =>
        {
            try { ((PdfBatchJob.Custom)snapshot[checked((int)index)]).Operation(); return 0; }
            catch (Exception e) { failures[(int)index] = e.Message; return 1; }
        };
        NativeMethods.BatchProgressCallback? report = progress is null ? null : (data, length) =>
        {
            try { progress(JsonSerializer.Deserialize<PdfBatchProgress>(Marshal.PtrToStringUTF8(data, checked((int)length))!)!); }
            catch (Exception e) { Interlocked.CompareExchange(ref progressError, e, null); }
        };
        IntPtr output = IntPtr.Zero;
        try
        {
            ct.ThrowIfCancellationRequested();
            int code = NativeMethods.oxidize_batch(input, (nuint)input.Length, callback, report, out output);
            if (code != 0) throw new PdfBatchException(code, NativeMethods.GetLastError());
            var result = JsonSerializer.Deserialize<PdfBatchResult>(Marshal.PtrToStringUTF8(output)!)!;
            if (progressError is not null) throw new PdfBatchException(0, "Progress callback failed after batch execution.", progressError);
            return result with { Results = result.Results.Select(r => failures.TryGetValue(r.Index, out var message) ? r with { CallbackError = message } : r).ToArray() };
        }
        finally
        {
            if (output != IntPtr.Zero) NativeMethods.oxidize_free_string(output);
            GC.KeepAlive(callback); GC.KeepAlive(report);
        }
    }
}
/// <summary>Batch request/native/progress failure, distinct from per-job outcomes.</summary>
public sealed class PdfBatchException : Exception
{
    /// <summary>Native error code; zero denotes a managed progress callback failure.</summary>
    public int ErrorCode { get; }
    internal PdfBatchException(int code, string? message, Exception? inner = null) : base(message, inner) => ErrorCode = code;
}
