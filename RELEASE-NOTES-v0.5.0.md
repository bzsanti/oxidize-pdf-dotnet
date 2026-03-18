# Release Notes — v0.5.0

## Overview

Major feature release adding full PDF parsing capabilities (metadata, annotations, content analysis, RAG pipeline, structured export) plus a comprehensive quality refactor.

## New Features

### PDF Parsing — Phases 2-8

- **Metadata extraction** (`ExtractMetadataAsync`) — title, author, subject, keywords, creator, producer, dates, version, page count
- **ExtractionOptions** (`ExtractTextAsync(bytes, options)`) — granular control: layout preservation, column detection, hyphenation merging, space/newline thresholds
- **Structured export** — `ToMarkdownAsync`, `ToContextualAsync`, `ToJsonAsync`
- **RAG pipeline** — `PartitionAsync` (semantic elements) and `RagChunksAsync` (structure-aware chunks with headings, token estimates)
- **Annotations** (`GetAnnotationsAsync`) — extract all annotations with subtype, contents, title, page number, rect
- **Page resources** (`GetPageResourcesAsync`) — font names, XObject presence, resource keys
- **Content streams** (`GetPageContentStreamAsync`) — raw decoded content streams as `PageContentStreams`
- **Content analysis** (`AnalyzePageContentAsync`) — classify pages as Text/Scanned/Mixed/Unknown

### New Models

- `PdfMetadata`, `ExtractionOptions`, `PdfElement`, `RagChunk`
- `PdfAnnotation`, `PageResources`, `PageContentStreams`
- `ContentAnalysis` with `ContentPageType` enum
- `PageContentStreams` (typed wrapper for content stream bytes)

## Quality Improvements

- **C-1**: Fixed double parse in `AnalyzePageContent` — now uses `extract_text_from_page()` instead of `extract_text()` (O(1) vs O(n) per page)
- **C-2**: Renamed `HasImages` → `HasXObjects` (correct semantics — XObjects include Forms, not just images)
- **C-3**: `ContentAnalysis.PageType` is now a `ContentPageType` enum instead of magic strings; added `IsMixed` property
- **C-4**: `ExtractTextAsync(bytes, options)` now accepts null options (uses defaults), consistent with `ExtractChunksAsync`
- **R-1**: Extracted `WithPinnedPdf`, `CallNativeJson`, `CallNativeString` helpers — eliminated ~200 lines of duplicated FFI boilerplate
- **R-2**: Aligned empty page classification between Rust ("Unknown") and C# (`ContentPageType.Unknown`)
- **R-3**: `GetPageContentStreamAsync` returns typed `PageContentStreams` instead of `List<byte[]>`
- **R-4**: Added `ExtractionOptions.Validate()` with range checks (negative thresholds rejected)
- **R-5**: Removed duplicate annotation test
- **R-7**: Added 18 cancellation tests covering all async methods

## Breaking Changes

- `PageResources.HasImages` renamed to `PageResources.HasXObjects`
- `ContentAnalysis.PageType` changed from `string` to `ContentPageType` enum
- `GetPageContentStreamAsync` returns `PageContentStreams` instead of `List<byte[]>`
- `ExtractTextAsync(bytes, null)` no longer throws `ArgumentNullException` (uses defaults)
- `ChunkOptions.Validate()` and `ExtractionOptions.Validate()` are now `public` (were `internal`)

## Validation

- **322 unit tests**, all passing
- **98.4% success rate** on 124 non-encrypted production PDFs (135 total, 10 encrypted, 1 empty)
- 0 warnings in both Rust (`cargo clippy`) and .NET (`dotnet build`)

## Dependencies

- oxidize-pdf core: 2.3.2 (unchanged)
- base64: 0.22, serde: 1.0, serde_json: 1.0, chrono: 0.4
