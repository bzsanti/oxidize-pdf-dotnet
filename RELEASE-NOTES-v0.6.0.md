# Release Notes — v0.6.0

**Release Date:** 2026-03-21
**Previous Version:** v0.5.0
**Type:** MINOR (new features, backward compatible)

## Summary

Major feature expansion: digital signatures, interactive forms, annotations, advanced page operations, AES encryption, custom fonts, image handling, graphics transforms, text measurement, and bookmarks. Feature parity increased from ~60% to 83% (112/135 features).

## New Features

### Digital Signatures (SIG-001/002/003)
- `HasDigitalSignaturesAsync` — detect if a PDF contains digital signatures
- `GetDigitalSignaturesAsync` — extract signature details (signer, timestamp, certificate info)
- `VerifySignaturesAsync` — verify signature validity and integrity

### Interactive Forms (FORM-001 to FORM-006)
- `HasFormFieldsAsync` — detect form fields in a PDF
- `GetFormFieldsAsync` — extract form field definitions (text, checkbox, radio, dropdown, etc.)

### Annotations (ANN-001 to ANN-005)
- `GetAnnotationsAsync` — extract all annotations from a PDF
- Page-level annotation creation: links, highlights, underlines, strikeouts, text notes, stamps, lines, rectangles, circles

### Advanced Page Operations
- `SplitAsync` with `PdfSplitOptions` — split by chunk size or at markers (OPS-010)
- `MergeAsync` with `PdfMergeInput` + `PdfPageRange` — merge with per-PDF page ranges (OPS-011)
- `RotatePagesAsync` with selective `PdfPageRange` rotation (OPS-012)
- `ExtractImagesAsync` — extract all images with metadata from PDF bytes (OPS-013)

### Security & Encryption
- `EncryptAes128` / `EncryptAes256` with `PdfPermissions` — AES encryption for PDF documents (DOC-010)

### Document Creation
- `AddFontFromFile` — register custom fonts from TTF/OTF files (DOC-013)
- `SetOutline` — set bookmarks/table of contents with tree structure (DOC-016)
- `PdfImage.FromFile` — auto-detect JPEG/PNG/TIFF from file path (IMG-005)

### Page Layout & Graphics
- `GetMargins` / `ContentWidth` / `ContentHeight` / `ContentArea` (PAGE-004/005)
- `Translate` / `Scale` / `RotateRadians` / `RotateDegrees` / `Transform` — affine transforms (GFX-013)
- `SetTextStrokeColor` with RGB/Gray/CMYK variants (TXT-013)

### Text Measurement
- `PdfTextMeasurement.Measure` — measure text dimensions with custom fonts (TXT-015)

### Testing
- Corpus regression test suite: 9,051 PDFs across 7 tiers (T0-T6)
- 453 total tests (446 unit + 7 corpus tier tests)

## Bug Fixes

- None (new feature release)

## Breaking Changes

- None (all new features are additive)

## Changelog (since v0.5.0)

```
5216346 test: add corpus regression tests for .NET FFI bridge (9K+ PDFs)
83558a1 feat: implement TIER A features (13 phases) — margins, fonts, images, AES, transforms, operations, bookmarks
df048f1 feat: implement OPS-013 image extraction from PDF bytes
d5cf62b feat: implement OPS-010/011/012 (split with options, merge with page ranges, selective page rotation)
e2eeef3 feat: implement annotation creation (ANN-001 to ANN-005) with quality fixes
7e42deb feat: implement digital signatures (SIG-001/002/003) and interactive forms (FORM-001 to FORM-006)
```
