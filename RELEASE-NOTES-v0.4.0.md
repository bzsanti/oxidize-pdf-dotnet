# Release Notes — v0.4.0

## Summary

Major feature release achieving near-complete API parity with oxidize-pdf 2.3.2. This release adds full document creation, text operations (including text flow with alignment and custom fonts), graphics, security, document operations, and expanded PDF parsing capabilities.

## New Features

### Document Creation (DOC)
- Create empty documents, set metadata (title, author, subject, keywords, creator)
- Add pages, serialize to bytes
- Encrypt with RC4-128 (user/owner passwords, granular permissions)

### Page Creation (PAGE)
- Arbitrary dimensions and standard presets (A4, Letter, Legal + landscape variants)
- Configurable margins

### Text Operations (TXT)
- 14 standard PDF fonts with size control
- **Custom/embedded fonts** (TTF/OTF) via `PdfDocument.AddFont()` + `PdfPage.SetCustomFont()`
- **Text alignment** via `PdfTextFlow` — Left, Right, Center, Justified with auto-wrapping
- Text color (RGB, Gray, CMYK), character spacing, word spacing, line leading
- Text positioning with `TextAt(x, y, text)`

### Graphics Operations (GFX)
- Fill/stroke colors (RGB, Gray, CMYK), line width, opacity
- Rectangle, circle, arbitrary paths (move/line/curve/close)
- Fill, stroke, fill-and-stroke painting

### PDF Parsing (PARSE)
- **`IsEncryptedAsync()`** — detect encrypted PDFs
- **`UnlockWithPasswordAsync()`** — unlock with user or owner password
- **`GetPdfVersionAsync()`** — extract PDF version string (e.g., "1.4")
- **`GetPageDimensionsAsync()`** — get parsed page width/height
- Extract text (single page or all pages), text chunking for RAG/LLM

### Document Operations (OPS)
- Split PDF into individual pages
- Merge multiple PDFs
- Rotate all pages
- Extract specific pages

## Breaking Changes

None. This is a backwards-compatible feature release.

## Dependencies

- Core library: oxidize-pdf 2.3.2

## Known Gaps (depend on core library)

- GFX-013: Image embedding
- OPS-005: Overlay/watermark
- OPS-006: Page reordering

## Changelog

- `6575a33` feat: full API parity — document creation, text, graphics, operations, and security
- `d1c14bb` feat: implement 6 remaining FFI gaps — custom fonts, text alignment, encryption, version, and page dimensions
- `49fa44c` chore: bump oxidize-pdf dependency 2.3.1 → 2.3.2
- `927bf6e` style: apply cargo fmt formatting
