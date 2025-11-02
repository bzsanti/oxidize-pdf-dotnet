# OxidizePdf.NET Architecture

This document describes the design decisions and architecture of OxidizePdf.NET, the .NET bindings for oxidize-pdf.

## Overview

OxidizePdf.NET is a **thin FFI wrapper** around the Rust library `oxidize-pdf`, exposing a type-safe, idiomatic C# API for PDF text extraction optimized for RAG/LLM pipelines.

```
┌─────────────────────────────────────────┐
│         C# Application                  │
│  (SharePoint Crawler, KernelMemory)     │
└─────────────────┬───────────────────────┘
                  │
                  │ High-level API
                  ▼
┌─────────────────────────────────────────┐
│      OxidizePdf.NET (C# Wrapper)        │
│  ┌─────────────────────────────────┐   │
│  │ PdfExtractor                    │   │  Type-safe API
│  │ - ExtractTextAsync()            │   │  IDisposable pattern
│  │ - ExtractChunksAsync()          │   │  Async/await support
│  └─────────────────────────────────┘   │
│  ┌─────────────────────────────────┐   │
│  │ NativeMethods (P/Invoke)        │   │  Platform detection
│  │ - DllImportResolver             │   │  Memory marshalling
│  │ - Platform-specific loading     │   │
│  └─────────────────────────────────┘   │
└─────────────────┬───────────────────────┘
                  │ FFI (C ABI)
                  ▼
┌─────────────────────────────────────────┐
│   oxidize-pdf-ffi (Rust cdylib)         │
│  ┌─────────────────────────────────┐   │
│  │ FFI Functions                   │   │  #[no_mangle]
│  │ - oxidize_extract_text()        │   │  extern "C"
│  │ - oxidize_extract_chunks()      │   │  Memory-safe wrappers
│  │ - oxidize_free_string()         │   │
│  └─────────────────────────────────┘   │
└─────────────────┬───────────────────────┘
                  │ Rust API
                  ▼
┌─────────────────────────────────────────┐
│        oxidize-pdf (Core Library)       │
│  - Document parsing                     │
│  - TextExtractor with chunking          │
│  - AI/RAG optimizations                 │
└─────────────────────────────────────────┘
```

## Design Decisions

### 1. FFI Layer (Rust)

**Decision**: Implement a separate `oxidize-pdf-ffi` crate as a `cdylib` wrapper.

**Rationale**:
- **Separation of concerns**: FFI code is distinct from core library logic
- **Memory safety**: Explicit ownership transfer between Rust and C#
- **Versioning**: FFI layer can version independently from core library
- **Stability**: C ABI is stable across compiler versions

**Implementation**:
```rust
#[no_mangle]
pub extern "C" fn oxidize_extract_text(
    pdf_bytes: *const u8,
    pdf_len: usize,
    out_text: *mut *mut c_char,
) -> c_int
```

**Memory Management**:
- C# allocates PDF bytes, passes pointer to Rust
- Rust allocates result string, transfers ownership to C#
- C# must call `oxidize_free_string()` to release Rust-allocated memory
- Error handling via integer error codes (C-compatible)

### 2. C# Wrapper Design

**Decision**: High-level async API with `IDisposable` pattern.

**Rationale**:
- **Idiomatic .NET**: Async/await matches .NET conventions
- **Resource safety**: IDisposable ensures cleanup (though currently stateless)
- **Future-proof**: Allows stateful extractors (caching, pooling)
- **Testability**: Easy to mock for unit tests

**API Layers**:

1. **Low-level** (`NativeMethods`): Raw P/Invoke declarations
   - Platform-specific library loading
   - Unsafe pointer marshalling
   - Direct FFI function calls

2. **Mid-level** (`PdfExtractor` private methods): Memory management
   - Marshal byte[] to unmanaged memory
   - Call native functions
   - Marshal results back to managed memory
   - Free unmanaged resources

3. **High-level** (`PdfExtractor` public API): Idiomatic C#
   - Async/await (Task-based)
   - Strong typing (no IntPtr in public API)
   - Validation (ArgumentNullException, options validation)
   - Exception handling (PdfExtractionException)

### 3. Cross-Platform Loading

**Decision**: Custom `DllImportResolver` for platform-specific library loading.

**Rationale**:
- **NuGet packaging**: Single package with embedded native libraries
- **Runtime detection**: Automatically selects correct binary (win-x64, linux-x64, osx-x64)
- **Development support**: Loads from current directory if runtimes folder missing
- **Clear errors**: Helpful exception messages when library not found

**Library Naming**:
| Platform | File Name | Runtime ID |
|----------|-----------|------------|
| Windows | `oxidize_pdf_ffi.dll` | win-x64 |
| Linux | `liboxidize_pdf_ffi.so` | linux-x64 |
| macOS | `liboxidize_pdf_ffi.dylib` | osx-x64 |

**Search Paths**:
1. `{BaseDirectory}/runtimes/{rid}/native/{library}` (NuGet standard)
2. `{BaseDirectory}/{library}` (development fallback)

### 4. Data Serialization

**Decision**: JSON for complex data structures (chunks), UTF-8 strings for plain text.

**Rationale**:
- **Simplicity**: No complex struct marshalling across FFI boundary
- **Flexibility**: Easy to add fields without breaking ABI
- **Standard**: System.Text.Json is fast and built-in (.NET 6+)
- **Debugging**: JSON is human-readable for troubleshooting

**Alternative Considered**: Binary serialization (MessagePack, Protobuf)
- **Rejected**: Added complexity for minimal performance gain (serialization is <1% of total time)

### 5. Error Handling

**Decision**: Integer error codes in FFI, exceptions in C# API.

**FFI Layer**:
```rust
pub enum ErrorCode {
    Success = 0,
    NullPointer = 1,
    InvalidUtf8 = 2,
    PdfParseError = 3,
    AllocationError = 4,
    SerializationError = 5,
}
```

**C# Layer**:
```csharp
private static void ThrowIfError(int errorCode, string message)
{
    if (errorCode != 0)
        throw new PdfExtractionException($"{message}: {error}");
}
```

**Rationale**:
- **C ABI compatibility**: Exceptions don't cross FFI boundary
- **Idiomatic**: C# consumers expect exceptions, not error codes
- **Detailed errors**: Rust can include context via JSON in future

### 6. Async API

**Decision**: `Task`-based async API wrapping synchronous FFI calls.

**Rationale**:
- **Non-blocking**: PDF extraction can be CPU-intensive (seconds for large PDFs)
- **Cancellation support**: Allows timeout/cancellation (future work)
- **Parallel processing**: Easy to extract multiple PDFs concurrently

**Current Limitation**: Native layer is synchronous (no cancellation support yet)

**Future Enhancement**: Add Rust async with tokio, expose cancellation callback

### 7. NuGet Packaging

**Decision**: Single multi-platform NuGet package with embedded native binaries.

**Package Structure**:
```
OxidizePdf.NET.0.1.0.nupkg
├── lib/
│   ├── net6.0/
│   │   └── OxidizePdf.NET.dll
│   └── net8.0/
│       └── OxidizePdf.NET.dll
└── runtimes/
    ├── win-x64/native/oxidize_pdf_ffi.dll
    ├── linux-x64/native/liboxidize_pdf_ffi.so
    └── osx-x64/native/liboxidize_pdf_ffi.dylib
```

**Rationale**:
- **Zero dependencies**: No separate native package to install
- **Automatic deployment**: .NET runtime copies correct binary to output
- **Standard convention**: Follows .NET native interop patterns (SQLite, etc.)

**Build Process**:
1. Cross-compile Rust for each target (GitHub Actions)
2. Copy native binaries to `dotnet/OxidizePdf.NET/runtimes/{rid}/native/`
3. Build .NET project (includes native binaries in package)
4. Publish to NuGet.org

## Performance Considerations

### Memory Allocation

**FFI Calls**:
- **C# → Rust**: PDF bytes copied to unmanaged memory (unavoidable)
- **Rust → C#**: Result string allocated by Rust, freed by C#
- **Zero-copy alternative**: Pinned GC memory (future optimization)

**Benchmark** (1 MB PDF):
- Marshal PDF bytes: ~0.5ms
- Extract text (Rust): ~15ms
- Marshal result string: ~0.1ms
- **Total overhead**: ~0.6ms (4% of extraction time)

### Threading

**Current**: Each `PdfExtractor` instance is thread-safe (stateless)

**Concurrency**:
```csharp
// Process multiple PDFs in parallel
var tasks = pdfFiles.Select(async file =>
{
    using var extractor = new PdfExtractor();
    return await extractor.ExtractChunksAsync(file);
});

var results = await Task.WhenAll(tasks);
```

**Rust Safety**: No shared mutable state, each call is independent

## Security

### Memory Safety

**Rust Guarantees**:
- No buffer overflows (compile-time enforcement)
- No null pointer dereferences (Option<T> types)
- No use-after-free (ownership system)

**FFI Boundary**:
- Validate all pointers (null checks)
- Validate all lengths (bounds checks)
- Never trust C# to provide valid data

### Input Validation

**C# Layer**:
- ArgumentNullException for null PDF bytes
- ArgumentException for invalid chunk options
- PdfExtractionException for malformed PDFs

**Rust Layer**:
- Robust parser with error recovery
- No panics on malformed input
- Lenient mode for damaged PDFs (98.8% success rate)

## Testing Strategy

### Unit Tests

**Rust** (`native/src/lib.rs`):
- Test FFI functions with sample PDFs
- Validate memory safety (null pointers, zero-length buffers)
- Test error code paths

**C#** (`dotnet/OxidizePdf.NET.Tests/`):
- Test public API (ExtractTextAsync, ExtractChunksAsync)
- Test error handling (invalid PDFs, invalid options)
- Test resource cleanup (IDisposable)

### Integration Tests

**End-to-end**:
- Extract text from real PDFs (test-pdfs/)
- Validate chunking (overlap, sentence boundaries)
- Performance benchmarks (pages/second)

### Platform Tests

**GitHub Actions CI**:
- Build and test on linux-x64, win-x64, osx-x64
- Validate native library loading on each platform
- Cross-compilation verification

## Future Enhancements

### v0.2.0 Roadmap

1. **OCR Support**: Expose `ocr-tesseract` feature
2. **Metadata Extraction**: PDF properties (title, author, page count)
3. **Streaming API**: Process large PDFs without loading entire file
4. **Progress Callbacks**: Report extraction progress to C#

### v0.3.0 Roadmap

1. **Invoice Extraction**: Expose structured invoice data API
2. **Table Detection**: Extract tables with cell boundaries
3. **Image Extraction**: Export embedded images

### Performance Optimizations

1. **Pinned Memory**: Use `GCHandle.Alloc(Pinned)` to avoid copying PDF bytes
2. **Object Pooling**: Reuse `PdfExtractor` instances (connection pool pattern)
3. **Async Rust**: Add tokio runtime with cancellation support
4. **Batch Processing**: Extract multiple PDFs in single FFI call

## References

- [oxidize-pdf Core Library](https://github.com/bzsanti/oxidizePdf)
- [Rust FFI Guide](https://doc.rust-lang.org/nomicon/ffi.html)
- [.NET P/Invoke Best Practices](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/best-practices)
- [NuGet Native Package Guidelines](https://learn.microsoft.com/en-us/nuget/create-packages/supporting-multiple-target-frameworks)

## License

This project (OxidizePdf.NET) is licensed under MIT.

The underlying oxidize-pdf library is licensed under AGPL-3.0. Users of OxidizePdf.NET must comply with AGPL-3.0 terms when distributing applications.
