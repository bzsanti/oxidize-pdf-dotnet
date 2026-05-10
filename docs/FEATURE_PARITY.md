# Feature Parity — oxidize-pdf-dotnet vs oxidize-pdf core

Bridge version: 0.9.0
Core dependency: oxidize-pdf 2.8.0
Last updated: 2026-05-10

For the cross-bridge (Python ↔ .NET) RAG-pipeline matrix used to schedule
work, see [`PARITY_SPEC.md`](PARITY_SPEC.md). This document is the
.NET-only feature inventory against the Rust core.

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
| DOC-010 | Encrypt AES-128/256 | yes | EncryptAes128/EncryptAes256 with optional permissions |
| DOC-011 | Permissions | yes | |
| DOC-012 | Add font from bytes | yes | |
| DOC-013 | Add font from file path | yes | AddFontFromFile(name, path) |
| DOC-014 | Set open action | no | GoTo page, URI, etc. |
| DOC-015 | Viewer preferences | no | Toolbar, menubar, page layout, print scaling |
| DOC-016 | Bookmarks / Outlines | yes | SetOutline with nested PdfOutlineItem tree |
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
| PAGE-004 | Get margins | yes | GetMargins() returns (Top, Right, Bottom, Left) tuple |
| PAGE-005 | Content width/height/area | yes | ContentWidth, ContentHeight, ContentArea properties |
| PAGE-006 | Set/get rotation | yes | |
| PAGE-007 | Add annotations | yes | Links, highlights, notes, stamps, geometric — via PdfPage fluent API |
| PAGE-008 | Add form widgets | no | Creation of form fields not exposed; only reading existing forms |
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
| TXT-013 | Stroke color for text | yes | SetTextStrokeColor RGB/Gray/CMYK |
| TXT-014 | Column layout | no | Multi-column text |
| TXT-015 | Text measurement (measure_text) | yes | PdfTextMeasurement.Measure (custom fonts only) |
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
| GFX-013 | Coordinate transforms (translate, scale, rotate) | yes | Translate, Scale, RotateRadians/Degrees, Transform (6-element CTM) |
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
| IMG-005 | Image from file path | yes | PdfImage.FromFile(path) — auto-detects JPEG/PNG/TIFF |

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
| PARSE-002 | Lenient/tolerant parsing | yes | ParseOptions::lenient() used in all parse operations |
| PARSE-003 | Is encrypted | yes | |
| PARSE-004 | Unlock with password | yes | |
| PARSE-005 | Page count | yes | Uses dedicated oxidize_get_page_count FFI |
| PARSE-006 | PDF version | yes | |
| PARSE-007 | Page dimensions | yes | |
| PARSE-008 | Extract text (single page) | yes | |
| PARSE-009 | Extract text (all pages) | yes | |
| PARSE-010 | Text chunking (token-aware) | yes | `RagChunksAsync(byte[], PartitionConfig?, HybridChunkConfig?)` (5-field config), `SemanticChunksAsync(SemanticChunkConfig)`. Standalone non-PDF chunker: `Ai.DocumentChunker(chunkSize, overlap).ChunkText(text)`. Legacy `ExtractChunksAsync(ChunkOptions)` kept callable but `[Obsolete]`. |
| PARSE-011 | Read document metadata | yes | ExtractMetadataAsync — title, author, subject, dates |
| PARSE-012 | RAG pipeline (rag_chunks, partition, profiles) | yes | `PartitionAsync(byte[])`, `PartitionAsync(byte[], ExtractionProfile)`, `PartitionAsync(byte[], PartitionConfig)`, `RagChunksAsync(byte[])`, `RagChunksAsync(byte[], ExtractionProfile)`, `RagChunksAsync(byte[], PartitionConfig?, HybridChunkConfig?)`, `SemanticChunksAsync`. Token estimator: `Ai.DocumentChunker.EstimateTokens(string)`. 12 ported semantic disjointness regression tests gate the surface. |
| PARSE-013 | Structured export (to_markdown, to_json, to_contextual) | yes | ToMarkdownAsync (no-arg + `(byte[], MarkdownOptions)` overload — RAG-012), ToJsonAsync, ToContextualAsync |
| PARSE-014 | ExtractionOptions (granular control) | yes | ExtractionOptions class with layout, columns, hyphenation |
| PARSE-015 | Page content analysis (scanned vs text) | yes | AnalyzePageContentAsync |
| PARSE-016 | Read annotations | yes | GetAnnotationsAsync |
| PARSE-017 | Read page resources / content streams | yes | GetPageResourcesAsync + GetPageContentStreamAsync |

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
| OPS-010 | Split with options (by size, by range) | yes | SplitAsync with PdfSplitOptions (ChunkSize, Ranges, SplitAt) |
| OPS-011 | Merge with page ranges | yes | MergeAsync with PdfMergeInput + PdfPageRange |
| OPS-012 | Selective page rotation | yes | RotatePagesAsync with PdfPageRange |
| OPS-013 | Extract images | yes | ExtractImagesAsync returns List&lt;ExtractedImageInfo&gt; |

## SIG — Digital signatures

| Feature ID | Feature | .NET | Notes |
|---|---|---|---|
| SIG-001 | Detect signatures | yes | HasDigitalSignaturesAsync |
| SIG-002 | Parse signatures (PKCS#7/CMS) | yes | GetDigitalSignaturesAsync — field name, signer, filter, dates |
| SIG-003 | Verify signatures | yes | VerifySignaturesAsync — hash, trust, certificate validation |

## FORM — Interactive forms

| Feature ID | Feature | .NET | Notes |
|---|---|---|---|
| FORM-001 | Detect and read text fields | yes | HasFormFieldsAsync + GetFormFieldsAsync |
| FORM-002 | Detect and read checkboxes | yes | Included in GetFormFieldsAsync |
| FORM-003 | Detect and read radio buttons | yes | Included in GetFormFieldsAsync |
| FORM-004 | Detect and read dropdowns | yes | Included in GetFormFieldsAsync |
| FORM-005 | Detect and read list boxes | yes | Included in GetFormFieldsAsync |
| FORM-006 | Detect and read push buttons | yes | Included in GetFormFieldsAsync |
| FORM-007 | Create form fields (write) | no | Only reading existing forms is implemented |
| FORM-008 | Fill form fields | no | Core may support programmatic form filling |

## ANN — Annotations

| Feature ID | Feature | .NET | Notes |
|---|---|---|---|
| ANN-001 | Link annotations (URI + GoTo) | yes | AddLinkUri, AddLinkGoToPage on PdfPage |
| ANN-002 | Text markup (highlight, underline, strikeout) | yes | AddHighlight, AddUnderline, AddStrikeOut |
| ANN-003 | Text note annotations | yes | AddTextNote with icon and open state |
| ANN-004 | Stamp annotations | yes | AddStamp (14 standard types) + AddCustomStamp |
| ANN-005 | Geometric annotations (line, rect, circle) | yes | AddAnnotationLine, AddAnnotationRect, AddAnnotationCircle |
| ANN-006 | Read annotations from existing PDFs | yes | GetAnnotationsAsync on PdfExtractor |

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
| Document creation | 15 | 6 | 21 |
| Pages | 9 | 2 | 11 |
| Text | 14 | 2 | 16 |
| Graphics | 13 | 11 | 24 |
| Images | 5 | 0 | 5 |
| Tables | 2 | 0 | 2 |
| Headers/Footers | 2 | 0 | 2 |
| Lists | 2 | 0 | 2 |
| Parsing | 17 | 0 | 17 |
| Operations | 13 | 0 | 13 |
| Signatures | 3 | 0 | 3 |
| Forms | 6 | 2 | 8 |
| Annotations | 6 | 0 | 6 |
| Errors | 5 | 0 | 5 |
| **Total** | **112** | **23** | **135** |

**Feature parity: 83% implemented, 17% not exposed.**

### Remaining gaps by priority

**Specialized / High complexity:**
- DOC-014/015/017/018: Open actions, viewer prefs, named destinations, page labels
- DOC-019/020/021: Tagged PDF, WriterConfig, semantic entities
- PAGE-008/009/010/011: Form widget creation, marked content, page editing, coordinate system
- GFX-014–024: Advanced color spaces, patterns, gradients, transparency, FormXObject
- TXT-014/016: Column layout, text validation
- FORM-007/008: Create and fill form fields
