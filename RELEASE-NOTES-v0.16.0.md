# Release Notes — v0.16.0

**Release Date:** 2026-06-27
**Previous Version:** v0.15.0
**Type:** MINOR (new platform support + FFI robustness; one small behavior change)

## Summary

A reliability- and reach-focused release for the Rust↔.NET FFI bridge. It
ships native binaries for **containers and ARM** (Alpine/musl, Linux ARM64,
Windows ARM64), makes a malformed PDF **no longer able to crash the host
process**, and removes a class of native-handle memory-safety bugs. The public
API is unchanged except for one additive error code and one deliberate
behavior change (`AddPage` now rejects post-add edits instead of silently
dropping them).

## New Features

### Broader platform coverage (#57)
- Native binaries are now built and packaged for **`linux-musl-x64`**,
  **`linux-musl-arm64`** (Alpine / containers), **`linux-arm64`** (ARM servers,
  AWS Graviton) and **`win-arm64`**, in addition to the existing `linux-x64`,
  `win-x64`, `osx-x64`, and `osx-arm64`.
- The native-library resolver now selects the correct runtime identifier from
  the OS, **process architecture, and C library (glibc vs musl)**. Previously it
  was hardcoded to `linux-x64` / `win-x64`, so ARM and Alpine consumers hit a
  `DllNotFoundException` even when a matching binary was present.

## Reliability / Security

### Panics no longer crash the host process (#53)
- Every FFI entry point now catches Rust panics at the boundary
  (`panic = "unwind"` + `catch_unwind`). A panic — e.g. from a malformed PDF in
  the parser — is converted into the new **`ErrorCode.Panic` (10)** plus a
  message retrievable as usual, instead of aborting the entire .NET host
  process. **This is the headline fix for any long-running service.**

### Native handles owned via SafeHandle (#54)
- The seven disposable wrappers (`PdfDocument`, `PdfPage`, `PdfImage`,
  `PdfTable`, `PdfTextFlow`, `PdfFlowLayout`, `PdfDocumentBuilder`) now own their
  native handle through a `SafeHandle`. The native free runs **exactly once,
  atomically**, eliminating the double-free possible under concurrent
  `Dispose()`/finalization with the previous manual pattern.

### ABI conformance guard (#56)
- A test asserts every `[DllImport]` entry point resolves to a real exported
  native symbol on all platforms, catching ABI drift from upstream bumps at CI
  time instead of as a runtime failure on a consumer's machine.

### Thread-local error contract (#55)
- The synchronous same-thread contract for reading the last error is documented
  and covered by a concurrency test (verifies per-thread error isolation under
  load). No behavior change — the existing wrappers already honor it.

## Changed

### `AddPage` rejects post-add edits (#58) — behavior change
- `Document.AddPage(page)` snapshots (clones) the page natively. Editing the
  page **after** `AddPage` previously had no effect and was silently lost. The
  page is now marked consumed on add: further mutation (or adding the same page
  again) throws `InvalidOperationException`. Disposing a consumed page is still
  valid. **Migration:** create a fresh page for additional content instead of
  reusing one already added.

### Packaging hygiene (#59)
- The release pipeline already builds the native library fresh per RID, and no
  native binary is committed to source control; a CI guard now fails the build
  if a `*.so`/`*.dll`/`*.dylib` is ever committed, so a release can never ship a
  stale binary.

### Positioning
- Package metadata and README repositioned to the **RAG/LLM PDF-ingestion**
  niche (chunking, semantic splitting, citation-grade extraction, Semantic
  Kernel / Kernel Memory), with corrected, complete platform documentation.

## Internal

- FFI crate (`oxidize-pdf-ffi`) bumped 0.10.0 → 0.11.0 to reflect the native
  changes; this version is surfaced by `oxidize_version()` for diagnostics. It
  is decoupled from the .NET package version and not published separately.

## Compatibility

- Backward compatible except for the `AddPage` behavior change above. The native
  `oxidize-pdf` core version is unchanged from v0.15.0 (3.0.1).
