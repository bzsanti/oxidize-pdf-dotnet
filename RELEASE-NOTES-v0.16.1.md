# Release Notes — v0.16.1

**Release Date:** 2026-06-29
**Previous Version:** v0.16.0
**Type:** PATCH (native core bug fixes; no public .NET API change) + new companion package

## Summary

A maintenance release that refreshes the embedded Rust core to **oxidize-pdf
3.0.4** — most notably fixing AES-128/AES-256 documents that previously
decrypted to empty content — with no change to the `OxidizePdf.NET` public API.
It also introduces a new, independently-versioned companion package,
**`OxidizePdf.NET.KernelMemory` (0.1.0-preview)**, that drops oxidize-pdf's
structure-aware chunking into Microsoft Kernel Memory.

## Native core: oxidize-pdf 3.0.1 → 3.0.4

The native binaries shipped in `OxidizePdf.NET` now embed core 3.0.4 (FFI crate
`oxidize-pdf-ffi` 0.11.0 → 0.11.1; `oxidize_version()` reports `0.11.1`).

### Bug Fixes (core 3.0.2)
- **AES-128 / AES-256 read-back.** Documents encrypted with AES by the core
  previously decrypted to empty content (text extraction returned nothing) even
  with the correct password; RC4-128 was unaffected. The reader now selects the
  cipher from the `/CFM` crypt filter, and the AES-256 writer uses the file key
  sealed in `/UE`. **Decryption failures are now surfaced as errors instead of
  being swallowed into empty content.**
- **AES-256 owner password.** Owner-password unlock of AES-256 documents now
  works; unlocking with a wrong password no longer panics.

### Security (core 3.0.2)
- Removed debug logging that leaked the derived encryption key and user password
  to stderr in debug builds.

### Internal (core 3.0.4)
- `PdfDocument<R>` is now `Send` (internal `Rc` → owned resource manager). No
  behavioral change for the .NET bindings.

> The upstream `rag_chunks_from_elements` API (core 3.0.3) is gated behind the
> `unstable-spi` feature and is **deliberately not surfaced** through the FFI.

## New: `OxidizePdf.NET.KernelMemory` (0.1.0-preview)

A separate NuGet package — a Microsoft Kernel Memory `IContentDecoder` backed by
oxidize-pdf — that ingests PDFs using oxidize-pdf's structure-aware chunking
instead of Kernel Memory's default re-chunker.

- `OxidizePdfDecoder` maps each oxidize-pdf RAG chunk to a KM `Chunk`.
- `OxidizeChunkPartitioningHandler` replaces KM's `partition` step and emits
  **one partition per oxidize-pdf chunk** (1:1), preserving heading context and
  source page — verified end-to-end against a real KM pipeline.
- Registration: `.WithOxidizePdf()` + `.WithoutDefaultHandlers()`, then register
  the four ingestion steps post-`Build` with the handler on `partition`.
- PDF-only; Abstractions-only dependency; Kernel Memory exact-pinned
  (`[0.98.250508.3]`).

Independently versioned at `0.1.0-preview` and published alongside this release.

## Compatibility

- `OxidizePdf.NET` public API: **unchanged** from 0.16.0.
- Supported platforms: Linux x64 (glibc/musl), Linux ARM64 (glibc/musl),
  Windows x64/ARM64, macOS x64/ARM64.

## Install

```bash
dotnet add package OxidizePdf.NET --version 0.16.1
dotnet add package OxidizePdf.NET.KernelMemory --version 0.1.0-preview
```
