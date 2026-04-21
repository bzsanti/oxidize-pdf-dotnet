# Changelog

All notable changes to OxidizePdf.NET will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
