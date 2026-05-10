# Changelog

All notable changes to OxidizePdf.NET will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed
- **Updated oxidize-pdf dependency 2.6.0 → 2.8.0**. The FFI public surface is
  unchanged; the bump is transparent for `OxidizePdf.NET` callers. The bump
  spans two upstream releases (2.7.0 and 2.8.0); inherited capabilities and
  observable behaviour changes are listed below. None of the new APIs are
  yet exposed through the FFI.

  **Inherited from oxidize-pdf 2.7.0:**
  - **Painter-model call order preserved across `Page::graphics()` /
    `Page::text()` / `Page::add_text_flow()` / `Page::append_raw_content()`**
    (upstream issue #227). Pre-2.7.0 the writer flushed the entire graphics
    buffer before the entire text buffer regardless of caller order.
    Observable change for FFI callers that interleave
    `oxidize_page_*_graphics_*` and text-emitting calls on the same page —
    z-ordering of generated PDFs now follows call order. No FFI signature
    change required.
  - **Non-finite float sanitisation extended to all numeric content-stream
    operators** in `GraphicsContext`, `TextContext`, and `TextFlowContext`
    (upstream issues #220 / #221). Path coords (`m`, `l`, `c`, `re`), line
    widths, miter limits, transforms (`cm`), text positioning, and text-state
    operators now route through `finite_or_zero` at the emission boundary.
    Out-of-scope paths (`forms/appearance.rs`, `signature_widget.rs`,
    `annotations`, `Op::Raw`) remain caller responsibility upstream.
    Extends the 2.6.0 colour-only fix already inherited by 0.8.0.
  - **Typed content-stream IR (`graphics::ops::Op` + `serialize_ops`)** —
    internal refactor; no observable FFI impact.
  - **`cm` matrix wire format normalised to `{:.2}` precision** — cosmetic
    change in emitted PDF bytes; ISO 32000-1-conformant in both old and new
    forms. Tests asserting byte-exact `cm` output downstream may need
    updating.
  - **`TextFlowContext` text-state setters** (`set_character_spacing`,
    `set_word_spacing`, `set_horizontal_scaling`, `set_leading`,
    `set_text_rise`, `set_rendering_mode`, `set_stroke_color`, upstream
    issue #222) — available upstream but not currently bridged through the
    FFI.
  - **API surface change with patch-compat semantics**:
    `GraphicsContext::operations()` / `get_operations()`,
    `TextContext::operations()`, `TextFlowContext::operations()`, and
    `Page::graphics_operations()` now return owned `String` (was `&str`).
    The FFI does not call these methods, so the change is invisible at the
    FFI boundary.

  **Inherited from oxidize-pdf 2.8.0:**
  - **`FontMetricsStore`** — per-`Document` custom font metrics store
    (replaces the process-wide global registry).
    `Document::add_font_from_bytes` automatically routes to the new store,
    so any custom font registered via
    `oxidize_document_add_font_from_bytes` benefits from scoped metrics
    with no API change. Resolves the cross-`Document` leak /
    last-writer-wins race documented upstream as issue #230. **Caveat for
    custom-font drawing flow**: `oxidize_document_add_page` clones the page
    into the document and the cloned copy receives the metrics-store
    inject; the caller's `PageHandle` does not. Drawing operations on the
    `PageHandle` that require metrics for `Font::Custom(_)` (text wrapping,
    table layout, header/footer width measurement) measure against default
    widths — the per-Document store is not bound on the handle. Tracked for
    a future revision that adopts the upstream document-first factories
    (`Document::new_page_a4()` / `new_page_letter()` / `new_page(w,h)`).
  - **Form-filling fix for `ComboBox` and `ListBox` widgets with
    `Font::Custom(_)`** — `Document::fill_field` now correctly emits
    Type0/CID appearance streams for choice fields with custom fonts
    (upstream issue #212). The fix is available upstream;
    `Document::fill_field` is **not yet exposed through the FFI** — when
    bridged, it will inherit the fix automatically.
  - **Form-filling fix for `PushButton` widgets with `Font::Custom(_)`** —
    the `/AP` resource dictionary now emits a `/Subtype /Type0` placeholder
    instead of an invalid `/Type1` entry that previously rejected
    non-WinAnsi labels with `PdfError::EncodingError` (upstream issue #212).
  - **`Document::new_page_a4 / new_page_letter / new_page(w,h)` factory
    methods** — available upstream; FFI continues to use `Page::a4()` /
    `Page::letter()` / `Page::new(w,h)` followed by `add_page`. See the
    `FontMetricsStore` caveat above for the implications during custom-font
    drawing.
- **Deprecated upstream APIs the FFI does not call**:
  `text::metrics::register_custom_font_metrics` and
  `text::metrics::get_custom_font_metrics`. No FFI changes required.

## [0.8.0] - 2026-05-05

### Added — RAG pipeline parity with the Python bridge

Closes every "immediate" Tier 0 row of `docs/PARITY_SPEC.md` (RAG-003,
RAG-004, RAG-005, RAG-006, RAG-007, RAG-008, RAG-009, RAG-012, RAG-020):

- **`ExtractionProfile` enum** (`OxidizePdf.NET.Pipeline`) — 7 values
  (`Standard`, `Academic`, `Form`, `Government`, `Dense`, `Presentation`,
  `Rag`); discriminant order locked against the Rust core via an
  exhaustive FFI test (the upstream enum has no `#[repr(u8)]`).
- **`MergePolicy` enum** — 2 variants (`SameTypeOnly`, `AnyInlineContent`)
  matching the Rust core. (PARITY_SPEC RAG-006 previously listed three
  variants — docs-only drift; patch attached for the Python repo.)
- **`ReadingOrderStrategy`** — three variants `Simple`, `None`,
  `XyCut(double minGap)`; serialises as serde-tagged JSON
  (`"Simple"` / `"None"` / `{"XYCut":{"min_gap":N}}`).
- **`PartitionConfig`** with fluent builder (`WithReadingOrder`,
  `WithMinTableConfidence`, …) plus client-side `Validate()`.
- **`HybridChunkConfig`** — full 5-field shape (`MaxTokens`,
  `OverlapTokens`, `MergeAdjacent`, `PropagateHeadings`, `MergePolicy`).
  Closes the RAG-005 ⚠️→✅ flip (legacy `ChunkOptions` only had 4).
- **`SemanticChunkConfig`** — element-boundary-aware chunker config.
- **`Ai.MarkdownOptions`** + **`Ai.DocumentChunker`** classes.
- **Six new `PdfExtractor` overloads:**
  - `PartitionAsync(byte[], ExtractionProfile, CancellationToken)`
  - `PartitionAsync(byte[], PartitionConfig, CancellationToken)`
  - `RagChunksAsync(byte[], ExtractionProfile, CancellationToken)` (RAG-003)
  - `RagChunksAsync(byte[], PartitionConfig?, HybridChunkConfig?, CancellationToken)`
  - `SemanticChunksAsync(byte[], SemanticChunkConfig?, PartitionConfig?, CancellationToken)`
  - `ToMarkdownAsync(byte[], MarkdownOptions, CancellationToken)` (RAG-012)
- **Standalone `Ai.DocumentChunker`** (RAG-008) — `ChunkText(string) → List<TextChunk>`
  for chunking arbitrary text without a PDF.
- **Static `Ai.DocumentChunker.EstimateTokens(string)`** (RAG-009) —
  upstream heuristic `floor(words * 1.33)`, contract locked in tests.
- **New result types:** `Models.SemanticChunk`, `Models.TextChunk`.

### Added — Tests

- **12 ported semantic disjointness regression tests** ported verbatim
  from the Python `test_rag_chunks_disjoint.py`
  (`Tests/Pipeline/RagChunksDisjointnessTests.cs`). Required by
  PARITY_SPEC maintenance rule #4 — these are the gating semantic
  tests for marking any Tier 0 row ✅.
- ~140 net new tests across the .NET and Rust suites.

### Added — FFI surface (eight new `extern "C"` entry points)

`oxidize_partition_with_profile`, `oxidize_partition_with_config`,
`oxidize_rag_chunks_with_profile`, `oxidize_rag_chunks_with_config`,
`oxidize_semantic_chunks`, `oxidize_to_markdown_with_options`,
`oxidize_chunk_text`, `oxidize_estimate_tokens`. Configuration crosses
the FFI boundary as UTF-8 JSON; profile crosses as a `u8` discriminant.

### Deprecated

- **`Models.ChunkOptions`** (character-based chunking) is now
  `[Obsolete]` with a migration message pointing at `HybridChunkConfig`
  (token-aware) or `SemanticChunkConfig` (element-aware). Remains
  callable for one minor release before removal.

### Security

- **Inherited from oxidize-pdf 2.6.0**: CWE-20 hardening — non-finite
  floats (`NaN`, `±inf`) in colour content-stream emission are now
  sanitised to `0.0` at the writer boundary. Direct construction of
  `Color::Rgb(NaN, ...)` (or `Gray`/`Cmyk`) previously bypassed the
  clamping in the public constructors and produced ISO 32000-1
  non-conformant content streams that conformant viewers reject
  (availability DoS via crafted input). The five `Color::Rgb(r, g, b)`
  sites in our annotation FFI inherit this protection automatically —
  no FFI code change needed. Upstream advisory: oxidize-pdf issue #220.

### Changed

- **Updated oxidize-pdf dependency 2.5.4 → 2.6.0**. Bundle release
  closing six upstream issues (security #220, #221 colour-emission
  single-source-of-truth, #216 `text_flow()` page-state propagation,
  #217 `TableStyle` header typography, #218 table-pagination APIs,
  #212 `fill_field` non-WinAnsi encoding). Also picks up 2.5.5 →
  2.5.7 fixes (per-font character tracking for subsetting #204,
  PDF-string escape SEC-F1, resource-name validation SEC-F5,
  deterministic resource-dict ordering, Roman-numeral page label
  case fix).
- **`oxidize_text_flow_create` now inherits page-level font /
  font_size / fill_color** from the caller's `Page::text()` state
  (oxidize-pdf #216). Existing FFI callers that explicitly call
  `oxidize_text_flow_set_font` after `oxidize_text_flow_create` are
  unaffected.
- **Slimmed native crate features** — switched to
  `default-features = false` with explicit
  `["compression", "semantic", "signatures"]`. Removed the
  implicitly-pulled `ocr-tesseract` default (no FFI entry point
  exposed OCR). **Native binary 11 MB → 5.7 MB (−48%)** on linux-x64;
  equivalent reduction expected on `osx-x64` / `osx-arm64` / `win-x64`.
  Compression (`flate2`) remains explicitly enabled — same wire
  format as before.
- **Documentation**: `docs/FEATURE_PARITY.md` refreshed (bridge
  0.5.0+feature/tier-a → 0.8.0; core 2.3.2 → 2.6.0; PARSE-010,
  PARSE-012, PARSE-013 entries cover the new overloads).
  **`docs/PARITY_SPEC.md`** added as a mirror of the Python repo's
  canonical cross-bridge spec (PARITY_SPEC maintenance rule #1).

### Dependencies

- oxidize-pdf 2.5.4 → 2.6.0
- Disabled implicit upstream defaults (`ocr-tesseract`); compression
  kept explicit

### Paused

- The DOC-014/015/017/020 milestone (`feature/m1-document-metadata`
  branch) is paused in favour of this RAG release. M1–M6 roadmap
  resumes after 0.8.0 ships. See
  `docs/superpowers/plans/2026-04-21-feature-parity-roadmap.md`.

## [0.7.2] - 2026-04-21

### Changed
- Updated oxidize-pdf dependency from 2.5.3 to 2.5.4 (upstream bug fixes)

### Dependencies
- oxidize-pdf 2.5.3 → 2.5.4

## [0.7.1] - 2026-04-20

### Changed
- Updated oxidize-pdf dependency from 2.5.1 to 2.5.3

### Dependencies
- oxidize-pdf 2.5.1 → 2.5.3

### Upstream fixes inherited (oxidize-pdf 2.5.2 + 2.5.3)
- **Font subsetting size reductions** (~3× smaller PDFs with embedded fonts)
  - TTF glyph hinting instructions stripped from subset output
  - FontFile2 / FontFile3 streams now FlateDecode-compressed (TTF glyf ~60-70% smaller)
  - ToUnicode CMap filtered to used characters only + FlateDecode-compressed
  - CFF String INDEX emitted empty in subset output (saves ~22 KB on Latin fonts)
- **CJK punctuation ToUnicode CMap offset math fix** — correct code-point mapping during CID CFF text extraction
- Full CFF Type 2 charstring desubroutinizer; SID-keyed CFF → CID-keyed conversion
- TTF `cmap`, `OS/2`, `name` tables stripped from subset output (not required for PDF embedding)

## [0.3.0] - 2026-03-04

### Changed
- **License changed from AGPL-3.0 to MIT** — aligned with oxidize-pdf core library
- Updated oxidize-pdf dependency from 1.6.6 to 2.0.0
- Updated version string in FFI layer to reflect oxidize-pdf v2.0.0

### Dependencies
- oxidize-pdf 1.6.6 → 2.0.0

## [0.2.2] - 2024-12-09

### Added
- Page-by-page extraction API
  - `GetPageCountAsync()` - Get total number of pages in a PDF
  - `ExtractTextFromPageAsync()` - Extract text from a specific page
  - `ExtractChunksFromPageAsync()` - Extract chunks from a specific page
- Comprehensive test suite (69 tests, 88.58% code coverage)
- PDF size validation to prevent OOM attacks (configurable max size)
- CancellationToken support for all async operations
- Detailed error messages from Rust layer propagated to .NET exceptions

### Changed
- **BREAKING**: Removed `IDisposable` from `PdfExtractor` - class is now stateless and can be reused without disposal
- **BREAKING**: Removed duplicate `X`, `Y`, `Width`, `Height` properties from `DocumentChunk` - use `BoundingBox` property instead
- Improved UTF-8 handling in Rust chunking to prevent panics on non-ASCII text
- `TreatWarningsAsErrors` enabled in project build settings

### Fixed
- Fixed UTF-8 boundary panic in Rust when chunking text with multi-byte characters
- Fixed error messages not propagating from Rust to .NET

### Dependencies
- Updated oxidize-pdf to 1.6.6

## [0.2.1] - Previous Release

### Added
- Initial project structure
- Rust FFI layer with memory-safe functions
- C# wrapper with P/Invoke and type-safe API
- Cross-platform support (linux-x64, win-x64, osx-x64)
- Text extraction API
- Chunked extraction for RAG/LLM pipelines
- BasicUsage example
- KernelMemory integration example
- GitHub Actions CI/CD pipeline
- Comprehensive documentation

## [0.1.0] - TBD

### Added
- Initial release
- PDF text extraction with `ExtractTextAsync()`
- Chunked extraction with `ExtractChunksAsync()`
- Metadata support (page numbers, confidence, bounding boxes)
- Multi-platform NuGet package with embedded native binaries
- Examples for basic usage and KernelMemory integration

### Supported Platforms
- Linux x64 (.NET 6.0+)
- Windows x64 (.NET 6.0+)
- macOS x64 (.NET 6.0+)

[unreleased]: https://github.com/bzsanti/oxidize-pdf-dotnet/compare/v0.3.0...HEAD
[0.3.0]: https://github.com/bzsanti/oxidize-pdf-dotnet/compare/v0.2.2...v0.3.0
[0.2.2]: https://github.com/bzsanti/oxidize-pdf-dotnet/compare/v0.2.1...v0.2.2
[0.2.1]: https://github.com/bzsanti/oxidize-pdf-dotnet/compare/v0.1.0...v0.2.1
[0.1.0]: https://github.com/bzsanti/oxidize-pdf-dotnet/releases/tag/v0.1.0
