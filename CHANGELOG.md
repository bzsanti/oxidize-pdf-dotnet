# Changelog

All notable changes to OxidizePdf.NET will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

[unreleased]: https://github.com/bzsanti/oxidize-pdf-dotnet/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/bzsanti/oxidize-pdf-dotnet/releases/tag/v0.1.0
