using System.Runtime.InteropServices;
using System.Text.Json;

namespace OxidizePdf.NET;

/// <summary>Requested page executor; batch jobs use a separate core worker pool.</summary>
public enum PdfGenerationMode
{
    /// <summary>Use the compiled Rayon engine when available, otherwise serial.</summary>
    Auto,
    /// <summary>Run page callbacks sequentially on the calling thread.</summary>
    Serial,
    /// <summary>Require the compiled Rayon engine; absence is an error.</summary>
    Rayon
}
/// <summary>Core thread priority request; only Normal is supported.</summary>
public enum PdfGenerationPriority
{
    /// <summary>Unsupported low OS priority.</summary>
    Low,
    /// <summary>Default OS thread priority.</summary>
    Normal,
    /// <summary>Unsupported high OS priority.</summary>
    High
}
/// <summary>Caller identity and positive dimensions in PDF points. Input order is preserved.</summary>
public sealed record PdfGenerationPage(uint Index, double Width, double Height);
/// <summary>Core page generation and output controls. No process-wide memory bound is provided.</summary>
public sealed record PdfGenerationOptions
{
    /// <summary>Executor selection.</summary>
    public PdfGenerationMode Mode { get; init; } = PdfGenerationMode.Auto;
    /// <summary>Positive maximum Rayon workers.</summary>
    public int MaxThreads { get; init; } = Math.Min(Environment.ProcessorCount, 8);
    /// <summary>Positive number of pages per scheduling chunk.</summary>
    public int ChunkSize { get; init; } = 4;
    /// <summary>Worker stack size in bytes, passed to the core thread pool.</summary>
    public ulong StackSize { get; init; } = 2 * 1024 * 1024;
    /// <summary>Native worker thread-name prefix.</summary>
    public string ThreadNamePrefix { get; init; } = "pdf-worker";
    /// <summary>Only Normal is supported; OS priority controls are not implemented by core.</summary>
    public PdfGenerationPriority ThreadPriority { get; init; } = PdfGenerationPriority.Normal;
    /// <summary>Only true is supported by the core callback executor.</summary>
    public bool LoadBalancing { get; init; } = true;
    /// <summary>Only zero is supported: arbitrary callback allocations cannot be bounded.</summary>
    public ulong MemoryLimitPerCallback { get; init; }
    /// <summary>Write through a buffered file instead of first building a full PDF buffer.</summary>
    public bool StreamingWriter { get; init; } = true;
    /// <summary>Positive output buffer size in bytes.</summary>
    public int StreamBufferSize { get; init; } = 1024 * 1024;
    /// <summary>Enable PDF stream compression.</summary>
    public bool Compression { get; init; } = true;
    /// <summary>Reuse buffered-output allocations.</summary>
    public bool MemoryPooling { get; init; } = true;
    /// <summary>Maximum retained output-buffer capacity, not peak allocation or process RSS.</summary>
    public ulong MemoryPoolSize { get; init; } = 16 * 1024 * 1024;
    /// <summary>Core resource sharing is required; false is rejected.</summary>
    public bool ResourceDeduplication { get; init; } = true;
    /// <summary>Collect core successful-call page statistics.</summary>
    public bool CollectMetrics { get; init; } = true;
}
/// <summary>Completed output; Mode reports the executor actually selected.</summary>
public sealed record PdfGenerationResult(int Pages, PdfGenerationMode Mode);
/// <summary>Core generation, persistence, availability or callback failure.</summary>
public sealed class PdfGenerationException : Exception
{
    /// <summary>Native error code, or zero for a managed callback failure.</summary>
    public int ErrorCode { get; }
    internal PdfGenerationException(int code, string? message, Exception? inner = null) : base(message, inner) => ErrorCode = code;
}
/// <summary>Synchronous optional core page generation with atomic output publication.</summary>
public static class PdfGeneration
{
    /// <summary>Whether the loaded native library includes the performance writer.</summary>
    public static bool IsAvailable => (NativeMethods.oxidize_generation_capabilities() & 1) != 0;
    /// <summary>Whether the loaded native library includes the Rayon executor.</summary>
    public static bool SupportsRayon => (NativeMethods.oxidize_generation_capabilities() & 2) != 0;
    /// <summary>Generate pages and publish a complete PDF. Each callback returns a fresh page whose
    /// ownership transfers to this call. Callbacks must be thread-safe; their side effects are not
    /// rolled back. All callbacks finish before return, including failure. Fonts register owned
    /// bytes by the names used by custom-font pages. Existing destinations survive generation errors.</summary>
    public static PdfGenerationResult Save(string path, IReadOnlyList<PdfGenerationPage> pages,
        Func<PdfGenerationPage, PdfPage> render, PdfGenerationOptions? options = null,
        IReadOnlyDictionary<string, byte[]>? fonts = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(render);
        var snapshot = pages.ToArray();
        foreach (var page in snapshot) ArgumentNullException.ThrowIfNull(page);
        var ownedFonts = fonts?.ToDictionary(p => p.Key, p => (byte[])p.Value.Clone()) ?? new();
        byte[] input = JsonSerializer.SerializeToUtf8Bytes(new { Path = path, Pages = snapshot, Options = options ?? new(), Fonts = ownedFonts });
        if (input.Length > 64 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(pages), "Generation request exceeds 64 MiB.");
        Exception? failure = null;
        NativeMethods.GenerationCallback callback = ordinal =>
        {
            try
            {
                using var page = render(snapshot[checked((int)ordinal)]);
                if (page is null) throw new InvalidOperationException("Generation callback returned null.");
                var clone = page.CloneForGeneration();
                if (clone == IntPtr.Zero) throw new PdfGenerationException(5, NativeMethods.GetLastError());
                return clone;
            }
            catch (Exception e) { Interlocked.CompareExchange(ref failure, e, null); return IntPtr.Zero; }
        };
        IntPtr output = IntPtr.Zero;
        try
        {
            int code = NativeMethods.oxidize_generation_save(input, (nuint)input.Length, callback, out output);
            if (code != 0 || failure is not null) throw new PdfGenerationException(code, NativeMethods.GetLastError(), failure);
            return JsonSerializer.Deserialize<PdfGenerationResult>(Marshal.PtrToStringUTF8(output)!)!;
        }
        finally
        {
            if (output != IntPtr.Zero) NativeMethods.oxidize_free_string(output);
            GC.KeepAlive(callback);
        }
    }
}
