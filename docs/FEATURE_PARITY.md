# Active increment — page geometry (#105), 2026-10-14

Sequence: existing-page geometry (#105), then resource provenance (#96) before
named images (#97) and bounded stream access (#95). Geometry is independent;
resource identity/lookup should be shared by the later reader capabilities.
Other issues remain open under #81; no total parity claim.

| Requirement | Evidence / state | Blocker / closure evidence |
| --- | --- | --- |
| Typed geometry, preserve tuple API | Implemented: `GetPageGeometryAsync` / `oxidize_get_page_geometry` | Local tests pass; platform CI pending |
| Indexing and ownership | Demonstrated: one-based indexes, configured size cap, owned snapshot | Invalid indexes/input, cancellation and snapshot tests pass |
| Real FFI behavior | Demonstrated: 8 geometry cases through real FFI | Four rotations, nearest inheritance/page override, absent crop/rotation, bounds and ownership |
| Contract, quality/security, platform CI | Contract gate and local review pass | Platform CI required before integration |

Scope follows Rust 5.4.2 `ParsedPage`: MediaBox and CropBox are the typed boxes
supported by the parser. BleedBox/TrimBox/ArtBox are not parsed geometry fields.
No normalization of raw rotation beyond the core's behavior is introduced.

### Quality review — geometry increment

Scope: `native/src/parser.rs`, managed `NativeMethods.cs`, `PdfExtractor.cs`,
`Models/PageGeometry.cs`, `PageGeometryTests.cs`, parity contract and CI trigger.
Reviewed consumers: existing pinned JSON helper, thread-local error retrieval,
`oxidize_free_string`, Rust `ParsedPage` and inheritance resolution. Existing
APIs remain unchanged. No architecture expansion or dependency update.

Contracts and evidence: independently authored two-leaf/three-level page trees
verify all four rotations, original nonzero box origins, nearest inherited boxes
and rotation, explicit zero overriding inherited rotation, absent CropBox and
zero default, and tuple equivalence. Failure cases cover null/empty/malformed
input, negative/zero/missing/very large page numbers, exact file-size boundary,
pre-cancellation and owned input snapshot. Native tests additionally exercise
null pointers, isize length overflow, page narrowing and output clearing.
15 focused managed tests (8 geometry, 6 parity gate, 1 ABI) pass; 225 native tests
pass; Clippy with denied warnings passes. Local .NET compilation initially
rejected synchronous xUnit assertions on Task-returning methods; corrected to
awaited ThrowsAsync and the focused suite passed.

Findings: no unresolved finding in the changed contract. The shared helper frees
JSON in finally even if deserialization fails; native output is reset before
validation, ownership transfers only on success, and ffi_guard contains panics.
Finite-coordinate validation prevents JSON null replacing non-finite numbers.
Input bytes are cloned before scheduling and checked against the extractor cap.
Native callers must still supply a genuinely readable allocation, as documented.
Cancellation does not interrupt native execution. Parser inheritance/cycle behavior
and lenient defaults remain those of pinned core 5.4.2; no stronger validation or
page-box support is claimed. These are read-only operations with no signing,
preservation or redaction decision, so no mutation test is required for this scope.

Kripteia test quality: Rust score 97, 225 detected tests/20 files; C# score 95,
951 detected tests/121 files. Geometry C# tests scored 100. The Rust geometry
boundary test scored 90 with a false "happy path only" report: it actually
asserts NullPointer, InvalidArgument and PdfParseError plus null outputs. Its
multiple assertions cover distinct ABI boundary failures; assertion failures show
the call and actual return code. Existing worst scores (65 Rust panic guard,
80 C# corpus cases) predate this increment and do not invalidate these assertions.

Kripteia security: both scans completed. Rust lists 288 unsafe usages across the
FFI, including the new entry point; its pointer/length/ownership path was manually
reviewed. C# reports no security issues but degraded syntax analysis in 27 files,
including PageGeometryTests; all new tests were manually inspected and compiled.
Telemetry SQLite was read-only, which did not prevent analysis (exit 0).
No secrets or taint-flow finding was emitted for the changed code. Reports cover
the implementation before the test-only ThrowsAsync correction; that correction
was manually reviewed and validated by the compiler/test rerun.

Metrics: 8 changed implementation/test/contract/CI files reviewed; 0 unresolved
findings; 0 product files changed during the read-only review. Platform validation
remains pending. Full logs are retained in `/tmp/geometry-kripteia-rust.log`,
`/tmp/geometry-kripteia-csharp.log`, `/tmp/geometry-native-tests.log`.


# Current parity target — Rust 5.4.2

The bridge target is **all functional capabilities of Rust**, including options,
results and errors. A matching core version does not establish full API parity.
Tracking: [#81](https://github.com/bzsanti/oxidize-pdf-dotnet/issues/81), coordinated
by [core #710](https://github.com/bzsanti/oxidizePdf/issues/710).

| Capability | Current status |
| --- | --- |
| Existing-document preservation, lossless page mutations, CMS signing, incremental editors, security redaction, revision comparison | Bindings exist; complete option/result audit pending |
| Recovery with full diagnostics, extraction/parser settings, NBSP normalization, effective PDF version, build identification | Integrated via #83; released in 0.19.0 |
| Signature slots and handwriting completion | Integrated via #82; 14 platform CI checks passed |
| Batch, recovery/streaming and experimental SPI | Exposure and equivalence audit pending |
| Generation, graphics, parsing, forms, layout and remaining public API | Partial exposure; full functional audit pending |
| Tesseract recognition and optional production features | Full exposure pending |

The audited subset is recorded in `docs/parity-contract.json`. CI reflects every
listed callable, option/result property and default, requires behavioral tests,
and rejects an unreviewed core-pin change. An isolated test host also checks that
native startup works before any PDF-generation API is called.

No current overall coverage percentage has been established. The historical
matrix below includes obsolete versions and must not be used to claim current
completeness. Package publication is separate from the parity implementation.

The core currently leaves space-decision arrays empty; the bridge exposes the
option and result schema without fabricating diagnostics. Legacy parser recovery
flags do not permit incomplete Flate data; recovery is a separate explicit API.

## Audit and issue reconciliation

# Feature Parity — oxidize-pdf-dotnet vs oxidize-pdf core

## Issue reconciliation — current remote develop, 2026-10-13

The audit below describes this older dirty checkout. Before issue creation,
remote `develop` was inspected at [`adb94995899bdc57f1d622431a9c6510dd055edd`](https://github.com/bzsanti/oxidize-pdf-dotnet/tree/adb94995899bdc57f1d622431a9c6510dd055edd).
It already implements five of the eight NEW-* entries: NBSP, signature slots,
build identification, effective PDF version and text recovery/parser settings.
Those entries are **not current remote implementation gaps**. Standalone raw
stream recovery remains covered below. This historical audit predates the checkout reconciliation on 2026-10-14; the checkout now includes those implementations.

Twenty specific remaining issues were created after checking all existing issues:

| Audit key | Issue |
|---|---|
| NEW-003 | [#86 — expose tagged-PDF preflight and recovery preparation](https://github.com/bzsanti/oxidize-pdf-dotnet/issues/86) |
| NEW-007 | [#87 — complete incremental writer configuration and Info preservation policy](https://github.com/bzsanti/oxidize-pdf-dotnet/issues/87) |
| NEW-008 | [#88 — expose complete CID Unicode mapping and vertical font metrics](https://github.com/bzsanti/oxidize-pdf-dotnet/issues/88) |
| OCR-001-005 | [#89 — expose OCR providers, recognition options and searchable-PDF conversion](https://github.com/bzsanti/oxidize-pdf-dotnet/issues/89) |
| PDFA-001 | [#90 — expose PDF/A validation levels and structured findings](https://github.com/bzsanti/oxidize-pdf-dotnet/issues/90) |
| PDFA-002 | [#91 — expose XMP metadata parsing, serialization and PDF integration](https://github.com/bzsanti/oxidize-pdf-dotnet/issues/91) |
| FORM-INCREMENTAL | [#92 — expose incremental filling of existing AcroForm documents](https://github.com/bzsanti/oxidize-pdf-dotnet/issues/92) |
| FORM-004 | [#93 — expose the Rust form calculation engine](https://github.com/bzsanti/oxidize-pdf-dotnet/issues/93) |
| FORM-005 | [#94 — expose form field validation rules and results](https://github.com/bzsanti/oxidize-pdf-dotnet/issues/94) |
| READ-017 | [#95 — expose raw content streams, filter chains and bounded decoding](https://github.com/bzsanti/oxidize-pdf-dotnet/issues/95) |
| READ-016 | [#96 — expose direct versus inherited page-resource provenance](https://github.com/bzsanti/oxidize-pdf-dotnet/issues/96) |
| READ-015 | [#97 — expose selective image XObject extraction by resource name](https://github.com/bzsanti/oxidize-pdf-dotnet/issues/97) |
| ANN-CREATION | [#98 — complete annotation creation beyond the currently exposed constructors](https://github.com/bzsanti/oxidize-pdf-dotnet/issues/98) |
| GFX-024 | [#99 — expose arbitrary clipping paths and winding rules](https://github.com/bzsanti/oxidize-pdf-dotnet/issues/99) |
| STREAMING | [#100 — expose bounded-memory lazy and streaming document APIs](https://github.com/bzsanti/oxidize-pdf-dotnet/issues/100) |
| BATCH-PERFORMANCE | [#101 — expose batch processing and optional parallel execution controls](https://github.com/bzsanti/oxidize-pdf-dotnet/issues/101) |
| OPS-012 | [#102 — complete split options and output naming semantics](https://github.com/bzsanti/oxidize-pdf-dotnet/issues/102) |
| OPS-013 | [#103 — expose all merge metadata source modes](https://github.com/bzsanti/oxidize-pdf-dotnet/issues/103) |
| TOKENS-TIKTOKEN | [#104 — expose optional exact token counting alongside heuristic estimates](https://github.com/bzsanti/oxidize-pdf-dotnet/issues/104) |
| READ-010 | [#105 — expose rotation when inspecting existing page geometry](https://github.com/bzsanti/oxidize-pdf-dotnet/issues/105) |

All are linked from umbrella [#81](https://github.com/bzsanti/oxidize-pdf-dotnet/issues/81).
Historical false gaps (document metadata controls, forms creation, most graphics,
embedded font programs, per-page extraction) were not duplicated. Remaining
arbitrary clipping was split from closed #23. Python-specific integrations and
old cross-bridge naming proposals are not Rust parity gaps.

The earlier CR failure is already addressed on remote develop in #79 by an
explicit ToUnicode test fixture, preserving all three policy assertions;
`docs/UPSTREAM-5.4.2.md` records the negative control and successful validation.
No duplicate upstream bug was filed. PDF/A validation is available in Rust,
but the 5.4.2 public exports do not include a general PDF/A converter; the older
broad wording below must not be read as evidence that such a converter exists.
No new implementation or test execution was performed for issue reconciliation.

## Current source audit — 2026-10-13 / Rust 5.4.2

The manifest pins official `oxidize-pdf = 5.4.2`, with no source patches.
Latest non-yanked release verified in the [Cargo registry index](https://index.crates.io/ox/id/oxidize-pdf)
(published 2026-10-11). Scope: current managed/FFI sources and the published
Rust 5.1.5 → 5.4.2 source delta, including new files and enabled Cargo features.
This is a capability audit, not a claim of exhaustive symbol or behavioral parity.
Validation and update acceptance live in [UPSTREAM_5_MIGRATION.md](UPSTREAM_5_MIGRATION.md).

The historical **83% figure is not a current parity metric**: its rows and totals
were already inconsistent, omit entire Rust modules, and mark implemented APIs
as missing. No defensible overall percentage can be inferred from that inventory.
`PARITY_SPEC.md` is a historical Python/.NET comparison, not current Rust evidence.

### Existing coverage confirmed in source

| Area | Managed / FFI evidence | Limits |
|---|---|---|
| Document settings | `PdfDocument.cs`, `document_metadata.rs`: open action, viewer preferences, destinations, labels, save options | Selected core options, not every WriterConfig field |
| Tagged creation and entities | `SetStructureTree`, `BeginMarkedContent`, entity methods; `tagged.rs`, `semantic.rs` | Does not establish full PDF/UA compliance |
| Pages, text and graphics | `PdfPage.cs`, `Graphics/`, `TextValidation.cs`; `page.rs`, `graphics.rs`, `columns.rs`, `text_validation.rs` | Includes parsed-page import, screen coordinates, columns, ICC, tiling, gradients, XObjects, groups and masks; arbitrary path clipping remains absent |
| Forms | `PdfDocument` Add*Field/FillField, `PdfPage.AddFormWidget`; `forms.rs` | Creation/filling on owned documents; not a general incremental existing-form filling API |
| Extraction / RAG | `PdfExtractor`, `Pipeline/`, `Ai/`; `parser.rs`, `pipeline_config.rs`, `chunking.rs` | Positioned fragments, URI opt-in, reading order, profiles and semantic chunks; parser recovery controls remain limited |
| Bookmarks | `GetBookmarksAsync`, `SetOutline`; `outline_read.rs`, `document.rs` | Regression validation covers mixed sibling/nested trees |
| Existing annotations | `PdfOperations` Highlight/FreeText/Ink/Geometric methods and matching native modules | Highlight Add/Remove; other three families Add/Update/Remove |
| Structural operations | `PdfExistingDocumentOperations`, `existing_pdf.rs` | Explicit preservation policy and reports; bounded wrapper contract |
| Signing / comparison | `PdfSigning`, `PdfSemanticComparison`; `signing.rs`, `semantic_compare.rs` | External CMS provider and bounded comparison; not every signing workflow |
| Tagged editing / OCR / redaction | `PdfTaggedOperations`, `PdfOcrLayer`, `PdfRedaction` and matching native modules | OCR consumes caller recognition; irreversible redaction is restricted ASCII; visual masking is distinct |

### New or expanded upstream surfaces not exposed

Paths in the Rust column are relative to the published 5.4.2 crate's `src/`.
All rows below are follow-up parity work, not blockers to adopting the dependency:
the current request is to update and analyze, not implement every Rust API.

| ID / priority | Rust evidence | .NET gap / consequence |
|---|---|---|
| NEW-001 / high | `text/extraction.rs`: `with_non_breaking_space_normalization` | `ExtractionOptions` has no control for U+00A0/U+202F normalization; core defaults remain in effect |
| NEW-002 / high | `signatures/preparation.rs`: create/read/list/remove/complete/draw signature slots | No `SignatureSlot` workflow or handwriting completion in `PdfSigning`; existing CMS preparation is a different operation |
| NEW-003 / high | `writer/incremental_tagged_pdf.rs`: `preflight`, `with_recovered_indexes`, `with_document_language`, `preparation_objects` | Existing tagged editor lacks explicit recovery, missing-language preparation and preparation-object inventory |
| NEW-004 / medium | `document.rs`: `BuildIdentification`, `set_build_identification` | Cannot disable generated build identification through `PdfDocument`; upstream default is Enabled |
| NEW-005 / medium | `parser/reader.rs`, `parser/document.rs`: `effective_version`; `parser/objects.rs`: `to_text_with_version` | `GetPdfVersionAsync` still calls `reader.version()` in `native/src/parser.rs`; no catalog-aware effective-version API |
| NEW-006 / medium | `parser/document.rs`: `decode_stream_with_recovery`, `extract_text_with_recovery` | No explicit recovery context/reporting contract in `PdfExtractor` |
| NEW-007 / medium | `writer/pdf_writer/mod.rs`: `IncrementalInfoPolicy` | No general Preserve/Replace Info policy for the low-level incremental writer; distinct from existing structural-operation metadata policies |
| NEW-008 / low | `text/metrics.rs`, `fonts/resolved.rs`, `text/font.rs`: CID collections, Unicode sequences and vertical metrics | Extraction benefits internally; managed font-resource models do not expose the full new CID mapping/vertical-metrics surface |

### Improvements inherited through existing calls

The source delta changes font/CMap and intrinsic-encoding resolution (including
CJK collections and vertical metrics), text sanitization, filter decoding,
outline serialization, structural metadata preservation, tagged-page operations,
and signature handling. Existing wrapper calls use these implementations after
rebuilding; this does not mean the wrapper exposes every new option or that each
upstream edge case has a dedicated .NET regression.

`operations/tagged_split.rs` is internal (`pub(super)`), so it is not counted as
a missing standalone API. Public-key recipient encryption is explicitly unsupported
in 5.4.2 (`encryption/public_key.rs`: add/decrypt return errors and permission
verification fails closed); it must not be advertised as working Rust functionality
missing only from .NET.

### Remaining broader parity gaps

| Priority | Rust area | Current managed boundary / next work |
|---|---|---|
| High for scanned ingestion | `text/ocr`, OCR provider/converter APIs; `ocr-tesseract` feature | No bundled recognition provider; `PdfOcrLayer` adds supplied text only. Requires a separate provider/dependency design |
| Medium | `pdfa`, XMP metadata APIs | No PDF/A validator/converter or dedicated XMP read/write API |
| Medium | `writer/incremental_form_fill.rs`, `forms` calculations/validation | Owned-document `FillField` does not expose incremental existing-document filling or the full form engines |
| Medium | `parser::ParseOptions`, recovery and lower-level resource access | Wrapper mostly selects lenient parsing internally; no complete parsing policy or raw resource/stream API |
| Medium | `annotations` | No creation API for every Rust annotation subtype (for example FileAttachment); existing annotation enumeration is not creation parity |
| Low / specialist | `graphics` path clipping; broader writer configuration | `ClipEllipse` exists, arbitrary path clipping does not; selected save presets are not all writer controls |
| Separate API design | `streaming`, `memory`, `batch`, optional `performance` | Byte-array Task APIs do not expose Rust lazy/streaming/batch engines |

Features enabled in `native/Cargo.toml`: compression, semantic, signatures,
language-detection. Optional OCR, external-images, performance/rayon and tiktoken
are not enabled. Absence of a feature flag is not proof that all related basic
capabilities are absent (for example existing JPEG/PNG APIs).

## Historical parity inventory

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
| GFX-014 | Calibrated colors (CalRGB, CalGray) | yes | Hardcoded + named variants; `SetFillColorCalGray`/`CalRgb`, `AddColorSpace` + `*CalibratedNamed`. PR #37 |
| GFX-015 | Lab colors | yes | Hardcoded + named; `SetFillColorLab`, `SetFillColorLabNamed`. PR #37 |
| GFX-016 | Patterns (tiling) | no | |
| GFX-017 | Shadings (axial, radial gradients) | no | |
| GFX-018 | FormXObject / templates | no | |
| GFX-019 | ICC color profiles | yes | Inline ICCBased + embedded profile (.NET superset); `AddColorSpace`/`AddIccColorSpace` + `SetFillColorIcc`. PR #37 |
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
| PARSE-014 | ExtractionOptions (granular control) | yes | ExtractionOptions class with layout, columns, hyphenation, plus oxidize-pdf 2.10.0 additions: `TjSpaceThreshold` (TJ-kern space synthesis, upstream #272), `ReconstructParagraphs` (line/paragraph grouping, upstream #261), `IncludeArtifacts` (page-furniture filtering, upstream #269) |
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
