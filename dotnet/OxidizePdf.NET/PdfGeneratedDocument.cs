using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace OxidizePdf.NET;

/// <summary>Owned generated pages and reusable output buffers. Save and Dispose are serialized;
/// no callbacks run during Save. Dispose releases pages, fonts and retained buffers.</summary>
public sealed class PdfGeneratedDocument : IDisposable
{
    private readonly GenerationSafeHandle handle;
    private readonly object gate = new();
    internal PdfGeneratedDocument(GenerationSafeHandle handle) => this.handle = handle;

    /// <summary>Publish a complete PDF without consuming pages. Repeated buffered saves may
    /// reuse buffers within the configured retained-capacity limit. PoolRequests and PoolHits
    /// are cumulative; RecordedPages and ThreadUsage describe the original generation.</summary>
    public PdfGenerationResult Save(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        byte[] bytes = Encoding.UTF8.GetBytes(path);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(handle.IsClosed, this);
            IntPtr output = IntPtr.Zero;
            try
            {
                int code = NativeMethods.oxidize_generation_save(handle, bytes, (nuint)bytes.Length, out output);
                if (code != 0) throw new PdfGenerationException(code, NativeMethods.GetLastError());
                return JsonSerializer.Deserialize<PdfGenerationResult>(Marshal.PtrToStringUTF8(output)!)!;
            }
            finally
            {
                if (output != IntPtr.Zero) NativeMethods.oxidize_free_string(output);
            }
        }
    }

    /// <summary>Release native ownership once; waits for an active Save to finish.</summary>
    public void Dispose()
    {
        lock (gate) handle.Dispose();
    }
}

internal sealed class GenerationSafeHandle : OxidizeSafeHandle
{
    internal GenerationSafeHandle(IntPtr value) : base(value) { }
    protected override bool ReleaseHandle()
    {
        NativeMethods.oxidize_generation_free(handle);
        return true;
    }
}
