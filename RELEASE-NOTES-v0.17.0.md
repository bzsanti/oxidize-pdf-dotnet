# Release Notes — v0.17.0

**Release Date:** 2026-08-22  
**Previous Version:** v0.16.1  
**Type:** MINOR (new public APIs, core 4.6.0, bounded image-extraction behavior)

## Summary

OxidizePdf.NET 0.17.0 updates its native core from `oxidize-pdf` 3.0.4 to
**4.6.0** and exposes the stable extraction, accessibility, graphics,
annotation and font-resource APIs introduced across the upstream 4.x series.
The native FFI version advances from 0.11.1 to 0.13.0.

## New Features

- Flat-path column reordering and XY-cut reading order.
- Per-page text byte budgets with explicit truncation reporting.
- Configurable handling of standalone carriage returns.
- Accessible `/ActualText` marked-content authoring.
- Exact conic gradients and Type 4 free-form Gouraud mesh shadings.
- Bounded image extraction performed entirely in memory.
- Incremental listing, addition, update and removal of text-note annotations.
- Resolved font resources with embedded font programs and bounded Type 3 glyph
  metadata.

## Behavioral Change and Migration

`PdfOperations.ExtractImagesAsync(pdfBytes)` now applies safe defaults:

- 1,000 images per document.
- 64 MiB encoded bytes per image.
- 256 MiB encoded bytes in total.
- 100 million decoded pixels per image.

Most applications require no change. Workloads that intentionally exceed a
default can call the overload accepting `ImageExtractionOptions` and select an
appropriate explicit budget. Limit violations are reported as
`PdfExtractionException` rather than risking unbounded memory growth.

## Safety and Validation

- New shading and text-note arguments are validated in both .NET and Rust.
- All new native exports document pointer validity and output ownership.
- Font-resource inspection honors the configured maximum PDF size.
- The core dependency is pinned exactly to 4.6.0 for reproducible builds.

## Compatibility

- Target frameworks: .NET 8, .NET 9 and .NET 10.
- Native platforms: Linux x64/ARM64 (glibc and musl), Windows x64/ARM64, and
  macOS x64/ARM64.
- Minimum Rust toolchain for native builds: 1.88.

## Verification

- Rust: rustfmt and Clippy clean; 219 tests pass.
- .NET: 890 tests pass.
- GitHub CI: all eight native builds, four .NET integration jobs, lint and
  security audit pass.

## Install

```bash
dotnet add package OxidizePdf.NET --version 0.17.0
```
