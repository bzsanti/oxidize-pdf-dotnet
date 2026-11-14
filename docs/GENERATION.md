# Optional page generation

`PdfGeneration.Create` runs page callbacks through core 5.6.0's
`ParallelPageGenerator`, then returns an owned `PdfGeneratedDocument` backed by
`HighPerformanceDocument`. Its `Save` method can be called repeatedly and `Dispose`
releases pages and retained buffers. `PdfGeneration.Save` is a one-shot convenience. It is separate from `PdfBatch` file jobs. Default
native builds include performance and Rayon. Availability comes from the loaded
library, not a managed compile-time constant.

```csharp
var result = PdfGeneration.Save("report.pdf",
    new[] { new PdfGenerationPage(42, 200, 300) },
    spec => new PdfPage(spec.Width, spec.Height)
        .DrawTextAt(StandardFont.Helvetica, 12, 20, 30, "Report"),
    new PdfGenerationOptions { Mode = PdfGenerationMode.Auto, MaxThreads = 2 });
```

Auto chooses Rayon when available and otherwise serial; Serial never creates a
Rayon pool. Explicit Rayon fails if absent. No performance feature means Save
fails before callbacks or file writes. `IsAvailable` and `SupportsRayon` report
those distinctions. Current package RIDs follow the existing native build matrix;
the optional configuration CI exercises Linux, while default builds use the
existing platform build/test matrix.

Each callback must return a fresh owned PdfPage. Ownership transfers to Save:
the binding snapshots it with a SafeHandle-pinned clone and disposes it; do not
reuse or mutate it concurrently. The core owns the clone. All callbacks finish
before Create returns or throws. No callbacks run during an owner Save; Save and
Dispose on the owner are serialized. Exceptions become PdfGenerationException with the
first captured managed exception as InnerException; other callbacks already
running may finish. Their side effects are not rolled back. Input order is
preserved even if callbacks finish out of order; Index is caller identity and
need not be unique. Dimensions specify the callback's requested page size; the
returned page defines the actual page and must have valid geometry.

Fonts are copied by name before execution. Custom-font pages must use matching
names; images and existing PdfPage drawing operations retain their resources.
Callback failures and core write/flush failures preserve an existing destination.
Output uses the core's sibling temporary-file publication; an existing symlink
follows the core's resolved-target policy. Concurrent calls must not share an
output path. Disk publication is not a transaction over callback side effects.

Options map to the core: positive MaxThreads, ChunkSize and StreamBufferSize;
StackSize and ThreadNamePrefix configure the Rayon pool. The binding rejects
stacks below 2 MiB before creating workers because they must enter the CLR. This
minimum does not protect arbitrary caller recursion from stack overflow. Only Normal priority,
LoadBalancing=true, MemoryLimitPerCallback=0 and ResourceDeduplication=true are
supported. Unsupported requests fail before callbacks. Compression controls PDF
stream compression. StreamingWriter avoids a full output buffer, but pages and
writer resources remain resident. MemoryPoolSize bounds retained buffer capacity,
not allocations, callback memory, total document size or process RSS. PoolRequests and PoolHits are cumulative over an owner's saves; buffered
repeated saves can reuse capacity, while zero retention or disabled pooling cannot.
RecordedPages and ThreadUsage describe initial generation and are populated when
CollectMetrics is true. The core's incomplete return/eviction counters are not
exposed. ErrorCode distinguishes I/O (6), unsupported engines (11), and invalid
requests/callback failures (9); callback exceptions remain in InnerException.

Native requests are limited to 64 MiB of JSON (including base64 font bytes).
Synchronous callbacks should not wait for their containing Save operation.
No cancellation, sandboxing of caller code, OS memory cap or universal speedup
is implied. Benchmark only a named workload with its hardware and configuration.
