# Feature Parity — oxidize-pdf-dotnet vs oxidize-pdf core

Bridge version: 0.4.0
Core dependency: oxidize-pdf 2.3.2
Last updated: 2026-03-17

Status values:
- `yes` — fully implemented and tested in FFI + .NET
- `partial` — implemented but with known gaps (see Notes)
- `no` — exists in core, NOT exposed through FFI
- `n/a` — not applicable to this runtime

---

## DOC — Document creation

| Feature ID | Feature | .NET | Notes |
|---|---|---|---|
| DOC-001 | Create empty document | yes | |
| DOC-002 | Set metadata (title, author, subject, keywords, creator) | yes | |
| DOC-003 | Set producer | yes | |
| DOC-004 | Set creation/modification date | yes | |
| DOC-005 | Add pages | yes | |
| DOC-006 | Page count | yes | |
| DOC-007 | Save to bytes | yes | |
| DOC-008 | Save to file | yes | |
| DOC-009 | Encrypt RC4-128 | yes | |
| DOC-010 | Encrypt AES-128/256 | no | Core has AES support, FFI only exposes RC4-128 |
| DOC-011 | Permissions | yes | |
| DOC-012 | Add font from bytes | yes | |
| DOC-013 | Add font from file path | no | Core supports it |
| DOC-014 | Set open action | no | GoTo page, URI, etc. |
| DOC-015 | Viewer preferences | no | Toolbar, menubar, page layout, print scaling |
| DOC-016 | Bookmarks / Outlines | no | Table of contents navigation |
| DOC-017 | Named destinations | no | Internal document links |
| DOC-018 | Page labels | no | Custom page numbering (roman, alpha) |
| DOC-019 | Structure tree (Tagged PDF) | no | Accessibility |
| DOC-020 | Save with config (WriterConfig) | no | Compression, xref streams, PDF version |
| DOC-021 | Semantic entities | no | AI-ready markup |

## PAGE — Page creation

| Feature ID | Feature | .NET | Notes |
|---|---|---|---|
| PAGE-001 | Arbitrary dimensions | yes | |
| PAGE-002 | Standard presets (A4, Letter, Legal + landscape) | yes | |
| PAGE-003 | Set margins | yes | |
| PAGE-004 | Get margins | no | Getter not exposed |
| PAGE-005 | Content width/height/area | no | Margin-aware dimensions |
| PAGE-006 | Set/get rotation | yes | |
| PAGE-007 | Add annotations | no | Links, highlights, notes, stamps, geometric |
| PAGE-008 | Add form widgets | no | Text fields, checkboxes, radio, dropdowns |
| PAGE-009 | Marked content (Tagged PDF) | no | Accessibility tagging |
| PAGE-010 | Convert parsed page to editable | no | Page::from_parsed() — edit existing PDFs |
| PAGE-011 | Coordinate system | no | Custom coordinate transforms |

## TXT — Text operations

| Feature ID | Feature | .NET | Notes |
|---|---|---|---|
| TXT-001 | Standard fonts (14) | yes | |
| TXT-002 | Custom/embedded fonts | yes | |
| TXT-003 | Fill color | yes | |
| TXT-004 | Character spacing | yes | |
| TXT-005 | Word spacing | yes | |
| TXT-006 | Line leading | yes | |
| TXT-007 | Text at position | yes | |
| TXT-008 | Horizontal scaling | yes | |
| TXT-009 | Text rise | yes | |
| TXT-010 | Text rendering mode (8 modes) | yes | |
| TXT-011 | Text alignment (L/R/C/J) | yes | |
| TXT-012 | Text flow (wrapped text) | yes | |
| TXT-013 | Stroke color for text | no | |
| TXT-014 | Column layout | no | Multi-column text |
| TXT-015 | Text measurement (measure_text) | no | |
| TXT-016 | Text validation | no | |

## GFX — Graphics operations

| Feature ID | Feature | .NET | Notes |
|---|---|---|---|
| GFX-001 | Fill/stroke color (RGB, Gray, CMYK) | yes | |
| GFX-002 | Line width | yes | |
| GFX-003 | Fill/stroke opacity | yes | |
| GFX-004 | Rectangle | yes | |
| GFX-005 | Circle | yes | |
| GFX-006 | Arbitrary paths (move/line/curve/close) | yes | |
| GFX-007 | Fill / Stroke / Fill-stroke | yes | |
| GFX-008 | Line cap / join / miter limit | yes | |
| GFX-009 | Dash patterns | yes | |
| GFX-010 | Save/restore state | yes | |
| GFX-011 | Clipping (rect, circle) | yes | |
| GFX-012 | Blend modes (16) | yes | |
| GFX-013 | Coordinate transforms (translate, scale, rotate) | no | Core has full affine transforms |
| GFX-014 | Calibrated colors (CalRGB, CalGray) | no | |
| GFX-015 | Lab colors | no | |
| GFX-016 | Patterns (tiling) | no | |
| GFX-017 | Shadings (axial, radial gradients) | no | |
| GFX-018 | FormXObject / templates | no | |
| GFX-019 | ICC color profiles | no | |
| GFX-020 | Transparency groups | no | |
| GFX-021 | Soft masks | no | |
| GFX-022 | Draw text from graphics context | no | |
| GFX-023 | Draw image from graphics context | no | |
| GFX-024 | Clip ellipse / arbitrary path clip | no | FFI only has rect/circle clip |

## IMG — Image operations

| Feature ID | Feature | .NET | Notes |
|---|---|---|---|
| IMG-001 | Embed JPEG | yes | |
| IMG-002 | Embed PNG | yes | |
| IMG-003 | Add image to page | yes | |
| IMG-004 | Draw image at position | yes | |
| IMG-005 | Image from file path | no | Core supports Image::from_file() |

## TBL — Tables

| Feature ID | Feature | .NET | Notes |
|---|---|---|---|
| TBL-001 | AdvancedTableBuilder | yes | |
| TBL-002 | Add table to page | yes | |

## HDR — Headers/Footers

| Feature ID | Feature | .NET | Notes |
|---|---|---|---|
| HDR-001 | Set header | yes | |
| HDR-002 | Set footer | yes | |

## LST — Lists

| Feature ID | Feature | .NET | Notes |
|---|---|---|---|
| LST-001 | Ordered lists (5 styles) | yes | |
| LST-002 | Unordered lists (4 styles) | yes | |

## PARSE — PDF parsing / reading

| Feature ID | Feature | .NET | Notes |
|---|---|---|---|
| PARSE-001 | Open from bytes | yes | |
| PARSE-002 | **Lenient/tolerant parsing** | **no** | **CRITICAL: ParseOptions::lenient() not used. 93% of real-world PDFs fail.** |
| PARSE-003 | Is encrypted | yes | |
| PARSE-004 | Unlock with password | yes | |
| PARSE-005 | Page count | yes | Uses extract_text().len() as proxy — inefficient |
| PARSE-006 | PDF version | yes | |
| PARSE-007 | Page dimensions | yes | |
| PARSE-008 | Extract text (single page) | yes | |
| PARSE-009 | Extract text (all pages) | yes | |
| PARSE-010 | Text chunking | partial | FFI has custom char-count chunker; core has semantic RAG chunking |
| PARSE-011 | **Read document metadata** | **no** | Title, author, subject, dates from existing PDFs |
| PARSE-012 | **RAG pipeline** (rag_chunks, partition) | **no** | Core has full Unstructured-style partitioning |
| PARSE-013 | **Structured export** (to_markdown, to_json, to_contextual) | **no** | AI/LLM-optimized output formats |
| PARSE-014 | ExtractionOptions (granular control) | no | |
| PARSE-015 | Page content analysis (scanned vs text) | no | PageContentAnalyzer |
| PARSE-016 | Read annotations | no | |
| PARSE-017 | Read page resources / content streams | no | |

## OPS — Document operations

| Feature ID | Feature | .NET | Notes |
|---|---|---|---|
| OPS-001 | Split to individual pages | yes | |
| OPS-002 | Merge documents | yes | |
| OPS-003 | Rotate all pages | yes | |
| OPS-004 | Extract pages | yes | |
| OPS-005 | Overlay / watermark | yes | |
| OPS-006 | Reorder pages | yes | |
| OPS-007 | Swap pages | yes | |
| OPS-008 | Move page | yes | |
| OPS-009 | Reverse pages | yes | |
| OPS-010 | Split with options (by size, by range) | no | Core has SplitOptions/SplitMode |
| OPS-011 | Merge with page ranges | no | Core has MergeInput::with_page_range() |
| OPS-012 | Selective page rotation | no | Core has RotateOptions |
| OPS-013 | **Extract images** | **no** | Core has full image extraction |

## SIG — Digital signatures

| Feature ID | Feature | .NET | Notes |
|---|---|---|---|
| SIG-001 | Detect signatures | no | Core can detect |
| SIG-002 | Parse signatures (PKCS#7/CMS) | no | Core can parse |
| SIG-003 | Verify signatures | no | Core can verify with trust store |

## FORM — Interactive forms

| Feature ID | Feature | .NET | Notes |
|---|---|---|---|
| FORM-001 | Text fields | no | Core has full AcroForm |
| FORM-002 | Checkboxes | no | |
| FORM-003 | Radio buttons | no | |
| FORM-004 | Dropdowns | no | |
| FORM-005 | Push buttons | no | |
| FORM-006 | Form validation | no | |

## ANN — Annotations

| Feature ID | Feature | .NET | Notes |
|---|---|---|---|
| ANN-001 | Link annotations | no | |
| ANN-002 | Text/highlight annotations | no | |
| ANN-003 | Note annotations | no | |
| ANN-004 | Stamp annotations | no | |
| ANN-005 | Geometric annotations | no | |

## ERR — Error model

| Feature ID | Feature | .NET | Notes |
|---|---|---|---|
| ERR-001 | Base PdfError | yes | |
| ERR-002 | PdfParseError | yes | |
| ERR-003 | PdfIoError | yes | |
| ERR-004 | PdfEncryptionError | yes | |
| ERR-005 | PdfPermissionError | yes | |

---

## Summary

| Category | Implemented | Not exposed | Total |
|---|---|---|---|
| Document creation | 12 | 9 | 21 |
| Pages | 6 | 5 | 11 |
| Text | 12 | 4 | 16 |
| Graphics | 12 | 12 | 24 |
| Images | 4 | 1 | 5 |
| Tables | 2 | 0 | 2 |
| Headers/Footers | 2 | 0 | 2 |
| Lists | 2 | 0 | 2 |
| Parsing | 9 | 8 | 17 |
| Operations | 9 | 4 | 13 |
| Signatures | 0 | 3 | 3 |
| Forms | 0 | 6 | 6 |
| Annotations | 0 | 5 | 5 |
| Errors | 5 | 0 | 5 |
| **Total** | **75** | **57** | **132** |

**Feature parity: 57% implemented, 43% not exposed.**

### Critical issues

1. **PARSE-002**: `ParseOptions::lenient()` not used in FFI. The parser fails on 93% of real-world PDFs (126/135 tested). This is the #1 priority fix.
2. **PARSE-012/013**: The core's RAG pipeline and structured export (markdown, JSON) are the library's headline features and are completely missing from the FFI.
3. **SIG/FORM/ANN**: Three entire feature categories (signatures, forms, annotations) have zero FFI coverage.
