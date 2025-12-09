# Changelog

All notable changes to OxidizePdf.NET will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

[unreleased]: https://github.com/bzsanti/oxidize-pdf-dotnet/compare/v0.2.2...HEAD
[0.2.2]: https://github.com/bzsanti/oxidize-pdf-dotnet/compare/v0.2.1...v0.2.2
[0.2.1]: https://github.com/bzsanti/oxidize-pdf-dotnet/compare/v0.1.0...v0.2.1
[0.1.0]: https://github.com/bzsanti/oxidize-pdf-dotnet/releases/tag/v0.1.0
