# OxidizePdf.NET 0.18.0

This release integrates the official oxidize-pdf **5.1.5** core without local
upstream patches. Native FFI version: **0.14.0**. Supports .NET 8, 9 and 10.

## Installation

```bash
dotnet add package OxidizePdf.NET --version 0.18.0
```

## New capabilities

- Optional link/figure text extraction, positioned fragments with rendering modes,
  and bounded bookmark reading.
- Incremental Highlight, FreeText, Ink and geometric annotation editing.
- Explicit preservation policies, planning and reports for structural operations.
- External-CMS signing with visible text/image appearances.
- Bounded semantic comparison and revision/object attribution.
- Tagged-PDF inspection, validation, planning and incremental editing.
- Invisible OCR layers from caller-provided recognition results.
- Restricted irreversible text removal and separately named visual masking,
  with explicit residual-risk reports.

## Known limitations shipped in this release

- [Upstream #633](https://github.com/bzsanti/oxidizePdf/issues/633): writing bookmarks
  with mixed nested/sibling structures may create invalid outline links.
- [Upstream #634](https://github.com/bzsanti/oxidizePdf/issues/634): reconstructive
  extraction and split may retain source metadata despite reporting it discarded.
  Do not rely on those operations to remove sensitive metadata.

These limitations are explicitly accepted for 0.18.0. The four regression tests
remain enabled; another release will incorporate official upstream fixes.
Irreversible redaction supports a restricted subset of PDFs and fails on
unsupported input; visual masking does not remove recoverable source content.

## Validation

- Native suite: 220 passed.
- .NET suite: 989 passed, 4 failed, all attributable to #633/#634.
- Builds for net8.0, net9.0 and net10.0: zero warnings/errors.
- Native Clippy and formatting checks passed.

## Platforms

Linux x64/ARM64 (glibc and musl), Windows x64/ARM64, macOS x64/ARM64.
Native release binaries are built by the release workflow for all eight RIDs.

See [CHANGELOG](https://github.com/bzsanti/oxidize-pdf-dotnet/blob/v0.18.0/CHANGELOG.md)
and [API guides](https://github.com/bzsanti/oxidize-pdf-dotnet/blob/v0.18.0/README.md).
