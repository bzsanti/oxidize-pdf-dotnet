using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OxidizePdf.NET;

/// <summary>Core lazy options. Mapping and streaming requests are reported unsupported,
/// not silently promised. CacheSize counts adapter objects, not total memory.</summary>
public sealed record PdfLazyOptions
{
    /// <summary>Parse objects on demand rather than eagerly.</summary>
    public bool LazyLoading { get; init; } = true;
    /// <summary>Unsupported request, reported in configuration.</summary>
    public bool MemoryMapping { get; init; } = true;
    /// <summary>Adapter cache capacity, 0..65536 objects.</summary>
    public uint CacheSize { get; init; } = 1000;
    /// <summary>Unsupported request for this lazy adapter.</summary>
    public bool Streaming { get; init; } = true;
    /// <summary>Reader buffer, 0..1 MiB; zero normalizes to one.</summary>
    public uint BufferSize { get; init; } = 65536;
    /// <summary>Reserved while mapping is unsupported; effective value is native usize::MAX.</summary>
    public ulong MmapThreshold { get; init; } = 10485760;
}
/// <summary>Core streaming options. MemoryLimit covers retained adapter page payloads only.</summary>
public sealed record PdfStreamingOptions
{
    /// <summary>Read buffer, 0..1 MiB; zero normalizes to one.</summary>
    public uint BufferSize { get; init; } = 262144;
    /// <summary>Cached page count, 0..1024.</summary>
    public uint PageCacheSize { get; init; } = 3;
    /// <summary>Combined decoded content per page, 0..16 MiB.</summary>
    public uint MaxContentStreamSize { get; init; } = 10485760;
    /// <summary>Reserved request, reported unsupported by the core.</summary>
    public bool ProgressiveHints { get; init; } = true;
    /// <summary>Retained page-cache payload budget, 0..256 MiB. Excludes parser and caller memory.</summary>
    public uint MemoryLimit { get; init; } = 104857600;
}
/// <summary>Core adapter counters; allocated bytes do not measure parser/process allocations.</summary>
public sealed record PdfLazyStats(ulong AllocatedBytes, ulong CachedObjects, ulong CacheHits, ulong CacheMisses, ulong LazyLoads, ulong MappedRegions);
/// <summary>Requested/effective lazy options and unsupported modes.</summary>
public sealed record PdfLazyConfiguration(uint PageCount, PdfLazyOptions Requested, PdfLazyOptions Effective, IReadOnlyList<string> Unsupported, PdfLazyStats Stats);
/// <summary>Requested/effective streaming options and retained-cache counters.</summary>
public sealed record PdfStreamingConfiguration(PdfStreamingOptions Requested, PdfStreamingOptions Effective, bool ProgressiveHintsUnsupported, uint CachedPages, ulong MemoryUsage);
/// <summary>Original lazy page geometry and raw page/resources/annotations object trees.</summary>
public sealed record PdfLazyPage(uint Number, ushort Generation, IReadOnlyList<double> MediaBox, IReadOnlyList<double>? CropBox,
    int Rotation, PdfLazyObject Dictionary, PdfLazyObject? InheritedResources, PdfLazyObject? Annotations);
/// <summary>Owned streamed page snapshot; original MediaBox origin and decoded content are retained.</summary>
public sealed record PdfStreamedPage(uint PageIndex, double Width, double Height, IReadOnlyList<double> MediaBox, string Text, byte[] Content);
/// <summary>PDF object kinds; references remain unresolved.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PdfLazyObjectKind>))]
public enum PdfLazyObjectKind
{
    /// <summary>PDF null.</summary>
    Null,
    /// <summary>Boolean.</summary>
    Boolean,
    /// <summary>Signed integer.</summary>
    Integer,
    /// <summary>Real number.</summary>
    Real,
    /// <summary>Name.</summary>
    Name,
    /// <summary>Raw string bytes.</summary>
    String,
    /// <summary>Array.</summary>
    Array,
    /// <summary>Dictionary.</summary>
    Dictionary,
    /// <summary>Raw stream bytes and dictionary.</summary>
    Stream,
    /// <summary>Indirect reference.</summary>
    Reference,
}
/// <summary>Owned typed PDF object snapshot. The member corresponding to Kind is populated.
/// Raw strings/streams are bytes; no lossy text conversion or automatic reference traversal.</summary>
public sealed record PdfLazyObject
{
    /// <summary>Object variant.</summary>
    public PdfLazyObjectKind Kind { get; init; }
    /// <summary>Boolean payload.</summary>
    public bool? Boolean { get; init; }
    /// <summary>Integer payload.</summary>
    public long? Integer { get; init; }
    /// <summary>Real payload.</summary>
    public double? Real { get; init; }
    /// <summary>Name payload.</summary>
    public string? Name { get; init; }
    /// <summary>String or raw stream payload.</summary>
    public byte[]? Data { get; init; }
    /// <summary>Array items.</summary>
    public IReadOnlyList<PdfLazyObject>? Items { get; init; }
    /// <summary>Dictionary/stream entries.</summary>
    public IReadOnlyDictionary<string, PdfLazyObject>? Entries { get; init; }
    /// <summary>Reference object number.</summary>
    public uint? Number { get; init; }
    /// <summary>Reference generation.</summary>
    public ushort? Generation { get; init; }
}

/// <summary>Lazy core document over an exclusively borrowed readable, seekable Stream.
/// Calls are serialized. Caller must not access the stream until disposal. There is no
/// constant/process-memory guarantee; parser caches remain outside adapter limits.</summary>
public sealed class PdfLazyDocument : IDisposable
{
    private readonly ReaderSession _session;
    private PdfLazyDocument(ReaderSession session) => _session = session;
    /// <summary>Open using core defaults. Reads the page tree immediately. LeaveOpen controls
    /// ownership after success or failure. Cancellation is checked at managed I/O boundaries.</summary>
    public static PdfLazyDocument Open(Stream stream, PdfLazyOptions? options = null, bool leaveOpen = true, CancellationToken ct = default)
        => new(new ReaderSession(stream, false, options ?? new(), leaveOpen, ct));
    /// <summary>Read effective options and adapter statistics.</summary>
    public PdfLazyConfiguration GetConfiguration() => _session.Query<PdfLazyConfiguration>(0)!;
    /// <summary>Read a zero-based page. Object responses are capped at depth 64, 65536 nodes and 8 MiB raw payload.</summary>
    public PdfLazyPage GetPage(uint pageIndex, CancellationToken ct = default) => _session.Query<PdfLazyPage>(1, pageIndex, ct: ct)!;
    /// <summary>Read one exact object/generation. Stream data remains encoded.</summary>
    public PdfLazyObject GetObject(uint number, ushort generation = 0, CancellationToken ct = default) => _session.Query<PdfLazyObject>(2, number, generation, ct)!;
    /// <summary>Preload the page's immediate resource references through the core.</summary>
    public void PreloadPage(uint pageIndex, CancellationToken ct = default) => _session.Query<object>(3, pageIndex, ct: ct);
    /// <summary>Clear adapter cache; underlying parser and caller-owned objects remain.</summary>
    public void ClearCache() => _session.Query<object>(4);
    /// <summary>Wait for any active operation, release native state and optionally dispose input.</summary>
    public void Dispose() => _session.Dispose();
}
/// <summary>Core page-at-a-time processing over an exclusively borrowed readable, seekable
/// Stream. Each NextPage materializes that page's bounded content and text; parser caches
/// and snapshots retained by the caller are outside the page-cache budget.</summary>
public sealed class PdfStreamingDocument : IDisposable
{
    private readonly ReaderSession _session;
    private PdfStreamingDocument(ReaderSession session) => _session = session;
    /// <summary>Open a stream from its beginning. LeaveOpen controls ownership, including failures.</summary>
    public static PdfStreamingDocument Open(Stream stream, PdfStreamingOptions? options = null, bool leaveOpen = true, CancellationToken ct = default)
        => new(new ReaderSession(stream, true, options ?? new(), leaveOpen, ct));
    /// <summary>Read effective configuration and cache counters.</summary>
    public PdfStreamingConfiguration GetConfiguration() => _session.Query<PdfStreamingConfiguration>(0)!;
    /// <summary>Read the next page, or null at EOF. Cancellation is cooperative at I/O
    /// boundaries; an I/O failure faults this instance and requires disposal/reopening.</summary>
    public PdfStreamedPage? NextPage(CancellationToken ct = default) => _session.Query<PdfStreamedPage>(5, ct: ct);
    /// <summary>Clear only the retained page cache.</summary>
    public void ClearCache() => _session.Query<object>(4);
    /// <summary>Wait for an active operation and release resources.</summary>
    public void Dispose() => _session.Dispose();
}
/// <summary>Native reader operation failure with any managed I/O error as InnerException.</summary>
public sealed class PdfStreamReaderException : Exception
{
    /// <summary>Native status code.</summary>
    public int ErrorCode { get; }
    internal PdfStreamReaderException(int code, string? message, Exception? inner) : base(message, inner) => ErrorCode = code;
}
internal sealed class StreamBridge(Stream stream, bool leaveOpen)
{
    internal Stream Stream { get; } = stream;
    internal bool LeaveOpen { get; } = leaveOpen;
    internal Exception? Error;
    internal CancellationToken Cancellation;
    internal static readonly NativeMethods.ReaderRead Read = ReadImpl;
    internal static readonly NativeMethods.ReaderSeek Seek = SeekImpl;
    private static StreamBridge Get(nuint context) => (StreamBridge)GCHandle.FromIntPtr((IntPtr)context).Target!;
    private static unsafe nint ReadImpl(nuint context, IntPtr destination, nuint length)
    {
        var bridge = Get(context);
        try { bridge.Cancellation.ThrowIfCancellationRequested(); return bridge.Stream.Read(new Span<byte>((void*)destination, checked((int)length))); }
        catch (Exception error) { bridge.Error = error; return -1; }
    }
    private static long SeekImpl(nuint context, long offset, byte origin)
    {
        var bridge = Get(context);
        try { bridge.Cancellation.ThrowIfCancellationRequested(); return bridge.Stream.Seek(offset, (SeekOrigin)origin); }
        catch (Exception error) { bridge.Error = error; return -1; }
    }
}
internal sealed class ReaderSafeHandle(IntPtr pointer, GCHandle root, StreamBridge bridge) : OxidizeSafeHandle(pointer)
{
    protected override bool ReleaseHandle()
    {
        try { NativeMethods.oxidize_reader_free(handle); }
        finally
        {
            root.Free();
            if (!bridge.LeaveOpen) { try { bridge.Stream.Dispose(); } catch { /* Finalizer must not throw. */ } }
        }
        return true;
    }
}
internal sealed class ReaderSession : IDisposable
{
    private readonly object _gate = new();
    private readonly ReaderSafeHandle _handle;
    private readonly StreamBridge _bridge;
    private bool _faulted;
    private bool _active;
    private static readonly JsonSerializerOptions Json = new() { MaxDepth = 192 };
    internal ReaderSession(Stream stream, bool streaming, object options, bool leaveOpen, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(stream);
        GCHandle root = default;
        try
        {
            ct.ThrowIfCancellationRequested();
            if (!stream.CanRead || !stream.CanSeek) throw new ArgumentException("A readable, seekable stream is required.", nameof(stream));
            _bridge = new(stream, leaveOpen) { Cancellation = ct };
            root = GCHandle.Alloc(_bridge);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(options);
            int code = NativeMethods.oxidize_reader_open((nuint)GCHandle.ToIntPtr(root), StreamBridge.Read, StreamBridge.Seek, streaming, bytes, (nuint)bytes.Length, out var pointer);
            if (code != 0 || _bridge.Error is not null)
            {
                if (pointer != IntPtr.Zero) NativeMethods.oxidize_reader_free(pointer);
                Throw(code == 0 ? 3 : code);
            }
            _handle = new ReaderSafeHandle(pointer, root, _bridge);
        }
        catch
        {
            if (root.IsAllocated) root.Free();
            if (!leaveOpen) stream.Dispose();
            throw;
        }
    }
    private void Throw(int code)
    {
        if (_bridge.Error is OperationCanceledException cancelled) ExceptionDispatchInfo.Capture(cancelled).Throw();
        throw new PdfStreamReaderException(code, NativeMethods.GetLastError(), _bridge.Error);
    }
    internal T? Query<T>(byte operation, uint index = 0, ushort generation = 0, CancellationToken ct = default)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_handle.IsClosed, this);
            if (_faulted) throw new InvalidOperationException("Reader had an I/O failure; dispose and reopen it.");
            if (_active) throw new InvalidOperationException("Reader callbacks cannot reenter the reader.");
            ct.ThrowIfCancellationRequested(); _bridge.Error = null; _bridge.Cancellation = ct;
            _active = true;
            IntPtr output = IntPtr.Zero;
            try
            {
                int code = NativeMethods.oxidize_reader_query(_handle, operation, index, generation, out output);
                if (_bridge.Error is not null) { _faulted = true; Throw(code); }
                if (code != 0) { if (operation == 5) _faulted = true; Throw(code); }
                return JsonSerializer.Deserialize<T>(Marshal.PtrToStringUTF8(output)!, Json);
            }
            finally { _active = false; if (output != IntPtr.Zero) NativeMethods.oxidize_free_string(output); }
        }
    }
    public void Dispose() { lock (_gate) { if (_active) throw new InvalidOperationException("Cannot dispose from a reader callback."); _handle.Dispose(); } }
}
