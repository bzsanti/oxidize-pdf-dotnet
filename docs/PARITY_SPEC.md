# Bridge Parity Spec — oxidize-pdf (Python + .NET)

**Last updated:** 2026-05-05
**Bridge versions checkpoint:** Python `oxidize-pdf` 0.5.0 (=core 2.6.0) · .NET `OxidizePdf.NET` 0.9.0-rag.1 (=core 2.6.0)

This document is the **canonical contract** that both bridges must satisfy. The same matrix exists in [`oxidize-pdf-dotnet/docs/PARITY_SPEC.md`](https://github.com/bzsanti/oxidize-pdf-dotnet/blob/main/docs/PARITY_SPEC.md). Any divergence between the two copies is a bug — the IDs, capability descriptions, and "Action for parity" cells must stay synchronized.

**Driver priority:** RAG/AI is the #1 surface. Tier 0 takes precedence over every other tier when scheduling work.

## Status legend

| Symbol | Meaning |
|---|---|
| ✅ | Implemented and tested |
| ⚠️ | Partial — present but missing options, missing config knobs, or missing semantic tests |
| ❌ | Not exposed in this bridge |
| 🚫 | N/A — does not apply to this runtime (justified asymmetry) |

When marking a row ⚠️ or ❌, the **Action for parity** column must specify exactly which side owes work and what the work is. Cells that read `—` mean "parity reached, no work pending".

---

## Tier 0 — RAG/AI pipeline (HIGHEST PRIORITY)

The reason we maintain bridges in lockstep. A consumer building a RAG pipeline must be able to swap Python ↔ .NET and get the same semantic guarantees.

| ID | Capability | Python | .NET | Action for parity |
|---|---|---|---|---|
| RAG-001 | Partition document → semantic elements | ✅ `PdfReader.partition()` | ✅ `PdfExtractor.PartitionAsync()` | — |
| RAG-002 | RAG chunks (default config) | ✅ `PdfReader.rag_chunks()` | ✅ `PdfExtractor.RagChunksAsync()` | — |
| RAG-003 | RAG chunks with `ExtractionProfile` | ✅ `PdfReader.rag_chunks_with_profile(p)` | ✅ `RagChunksAsync(byte[], ExtractionProfile)` | — |
| RAG-004 | `ExtractionProfile` enum (7 values: `STANDARD`, `RAG`, `ACADEMIC`, `FORM`, `GOVERNMENT`, `DENSE`, `PRESENTATION`) | ✅ | ✅ | — |
| RAG-005 | RAG chunks with granular `HybridChunkConfig` (`max_tokens`, `overlap_tokens`, `merge_adjacent`, `propagate_headings`, `merge_policy`) | ✅ `rag_chunks_with(config)` | ✅ `RagChunksAsync(byte[], PartitionConfig?, HybridChunkConfig?)` (5 fields, all present) | — |
| RAG-006 | `MergePolicy` enum (`AnyInlineContent`, `SameTypeOnly`) | ✅ (2 variants) | ✅ (2 variants) | **Python: drop spurious `None` variant from this row (docs-only drift; Rust core + Python bridge both expose 2)** |
| RAG-007 | `ReadingOrderStrategy` (`Simple`, `None`, `XyCut(min_gap)`) | ✅ | ✅ | — |
| RAG-008 | Low-level reusable chunker (chunk arbitrary text, not just PDFs) | ✅ `DocumentChunker(size, overlap).chunk_text(str)` | ✅ `DocumentChunker(chunkSize, overlap).ChunkText(text)` | — |
| RAG-009 | Token estimation utility | ✅ `DocumentChunker.estimate_tokens(str)` static | ✅ `DocumentChunker.EstimateTokens(string)` static | — |
| RAG-010 | Per-page chunking | ✅ `PdfReader.chunk_page(idx, options)` | ✅ `ExtractChunksFromPageAsync()` | — |
| RAG-011 | Markdown export | ✅ `PdfReader.to_markdown()` | ✅ `ToMarkdownAsync()` | — |
| RAG-012 | Markdown export with configurable `MarkdownOptions` | ✅ `MarkdownExporter(opts).export(text)` | ✅ `ToMarkdownAsync(byte[], MarkdownOptions)` | — |
| RAG-013 | Contextual export (LLM context windows) | ✅ `to_contextual()` | ✅ `ToContextualAsync()` | — |
| RAG-014 | Structured JSON export | ⚠️ via `EntityMap.to_json()` (entity-centric) | ⚠️ `ToJsonAsync()` direct (extraction-centric) | **Decide single philosophy; recommend direct method on both:** `to_json()` / `ToJsonAsync()` |
| RAG-015 | `RagChunk` schema (`chunk_index`, `text`, `full_text`, `page_numbers`, `element_types`, `heading_context`, `token_estimate`, `is_oversized`) | ✅ | ✅ | — (verify identical serialization in cross-bridge integration test) |
| RAG-016 | `Element` schema (`element_type`, `text`, `page_number`, `x`, `y`, `width`, `height`, `confidence`) | ✅ `Element` | ✅ `PdfElement` | **Both: align name (recommend `Element` without prefix; namespace is sufficient)** |
| RAG-017 | `DocumentChunk` schema | ✅ | ✅ | — |
| RAG-018 | Page content analysis (text/scanned/mixed) | ✅ `analyze_page_content(idx)` | ✅ `AnalyzePageContentAsync()` | — |
| RAG-019 | OCR for scanned PDFs (Tesseract) | ❌ (known gap) | ❌ | **BOTH: expose core's `OcrProvider`** |
| RAG-020 | **Semantic disjointness regression tests** (known input → expected output) | ✅ 12 bridge + 7 reader (`test_rag_chunks_disjoint.py`, `test_reader_disjoint.py`) | ✅ 12 ported (`RagChunksDisjointnessTests.cs`) | — |
| RAG-021 | First-class adapter for ecosystem RAG framework | ✅ `llama-index-readers-oxidize-pdf 0.1.1` (PyPI) | ⚠️ `examples/KernelMemory/` (sample, not a NuGet package) | **.NET: package `OxidizePdf.KernelMemory.DocumentReader` as a NuGet** |

### Tier 0 priority within itself

1. **Immediate (zero risk, high impact):** RAG-003, RAG-004, RAG-005, RAG-006, RAG-007, RAG-008, RAG-009, RAG-012, RAG-020. **All closed for .NET in 0.9.0-rag.1.**
2. **After (philosophy decisions required):** RAG-014, RAG-019, RAG-021.

---

## Tier 1 — Reading & Extraction

Consumed downstream by every RAG/AI use case, so kept high-priority even though not strictly Tier 0.

| ID | Capability | Python | .NET | Action for parity |
|---|---|---|---|---|
| READ-001 | Open PDF from file path | ✅ `PdfReader.open(path)` | ⚠️ requires `File.ReadAllBytes` + `ExtractTextAsync(bytes)` | **.NET: add overload `Open(string path)` or extension method** |
| READ-002 | Open PDF from bytes | ✅ `PdfReader.from_bytes(bytes)` | ✅ all methods accept `byte[]` | — |
| READ-003 | Open PDF from stream (cloud/network) | ✅ `PdfReader.from_stream(stream)` | ✅ via `byte[]` | — |
| READ-004 | Page count | ✅ `len(reader)` / `reader.page_count` | ✅ `GetPageCountAsync()` | — |
| READ-005 | PDF version | ✅ `reader.version` | ✅ `GetPdfVersionAsync()` | — |
| READ-006 | Extract text (single page) | ✅ `extract_text_from_page(idx)` | ✅ `ExtractTextFromPageAsync(bytes, idx)` | **Align page indexing — Python 0-based, .NET 1-based; pick one** |
| READ-007 | Extract text (all pages) | ✅ `extract_text() -> list[str]` | ⚠️ `ExtractTextAsync() -> string` (concatenated) | **.NET: add overload `ExtractTextPerPageAsync() -> List<string>`** |
| READ-008 | Extract text with `ExtractionOptions` | ✅ `extract_text_with_options(opts)` | ✅ `ExtractTextAsync(bytes, opts)` | — |
| READ-009 | Document metadata | ✅ `reader.metadata` property | ✅ `ExtractMetadataAsync()` | — |
| READ-010 | Page dimensions | ✅ `ParsedPage(width, height, rotation)` | ⚠️ `(Width, Height)` tuple, no rotation | **.NET: add `Rotation` to return** |
| READ-011 | Page resources (fonts, images) | ✅ `get_page_resources(idx) → PageResources` rich hierarchy (fonts, images, forms, ext_g_states, proc_sets, resource_keys) | ✅ `GetPageResourcesAsync()` | — |
| READ-012 | Page content streams (raw decoded bytes) | ✅ `get_page_content_streams(idx) → list[bytes]` (decoded) | ✅ `GetPageContentStreamAsync()` | — |
| READ-013 | Lenient/tolerant parsing | ✅ `ParseOptions.strict()/.tolerant()/.lenient()/.skip_errors()` + kwargs, accepted by `PdfReader.open/from_bytes/from_stream` | ✅ `ParseOptions::lenient()` | — |
| READ-014 | Extract embedded font program bytes | ❌ | ❌ | **BOTH: expose `/FontDescriptor/FontFile*` bytes via `get_embedded_font_bytes(page_idx, font_name)`** |
| READ-015 | Extract single image XObject by name | ❌ | ❌ | **BOTH: selective image extraction beyond batch `extract_images()`** |
| READ-016 | Distinguish direct vs inherited resources | ❌ | ❌ | **BOTH: surface `ParsedPage.inherited_resources` (core has it in `page_tree.rs`)** |
| READ-017 | Raw undecoded content streams + filter chain metadata | ❌ | ❌ | **BOTH: forensic/debug API for filter-chain analysis (FlateDecode, DCTDecode, …)** |

---

## Tier 2 — Document creation (writing)

| ID | Capability | Python | .NET | Action for parity |
|---|---|---|---|---|
| WRITE-001 | Create empty document | ✅ `Document()` | ✅ `PdfDocument()` | — |
| WRITE-002 | Set metadata (title, author, subject, keywords, creator, producer, dates) | ✅ | ✅ | — |
| WRITE-003 | Add pages | ✅ `add_page(page)` | ✅ `AddPage(page)` | — |
| WRITE-004 | Save to bytes | ✅ `save_to_bytes()` | ✅ `SaveToBytes()` | — |
| WRITE-005 | Save to file | ✅ `save(path)` | ✅ `SaveToFile(path)` | — |
| WRITE-006 | Save with `WriterConfig` presets (compression, xref streams, PDF version) | ✅ `WriterConfig.modern()/legacy()/incremental()` + kwargs (`pdf_version`, `compress_streams`, `use_xref_streams`, `use_object_streams`, `incremental_update`) + `Document.save_with_config()` / `save_to_bytes_with_config()` | ✅ `SaveToBytes(PdfSaveOptions)` with Default/Modern/Legacy | — |
| WRITE-007 | Encrypt RC4-128 | ✅ `encrypt(user_pwd, owner_pwd)` | ✅ `Encrypt()` | — |
| WRITE-008 | Encrypt AES-128 | ✅ `doc.encrypt(user, owner, strength=EncryptionStrength.AES_128)` (Python idiom: kwarg+enum) | ✅ `EncryptAes128()` explicit | — |
| WRITE-009 | Encrypt AES-256 | ✅ `doc.encrypt(user, owner, strength=EncryptionStrength.AES_256)` (Python idiom: kwarg+enum) | ✅ `EncryptAes256()` explicit | — |
| WRITE-010 | Permissions | ✅ | ✅ | — |
| WRITE-011 | Embed font from bytes | ✅ | ✅ `AddFont(name, bytes)` | — |
| WRITE-012 | Embed font from file path | ✅ `doc.add_font(name, path)` accepts a path directly (plus `add_font_from_bytes(name, data)` for the bytes case) | ✅ `AddFontFromFile(name, path)` | — |
| WRITE-013 | Open action (GoTo page, URI) | ✅ | ❌ | **.NET: implement (already in backlog as DOC-014)** |
| WRITE-014 | Viewer preferences | ✅ | ✅ DOC-015 (recent: commit `6e7e9c4`) | — |
| WRITE-015 | Outlines/bookmarks tree | ✅ `OutlineTree` + `OutlineItem` (read+write) | ⚠️ `SetOutline()` write-only, no read | **.NET: add `GetOutlineAsync()`** |
| WRITE-016 | Named destinations | ✅ | ✅ DOC-017 (recent: commit `dbb918f`) | — |
| WRITE-017 | Page labels (custom numbering) | ✅ | ❌ | **.NET: implement (DOC-018 backlog)** |
| WRITE-018 | Tagged PDF (Structure tree) | ❌ | ❌ | **BOTH: accessibility backlog** |
| WRITE-019 | Semantic entities (AI-ready markup) | ✅ `Entity`/`EntityMap` | ❌ | **.NET: implement (DOC-021 backlog)** |
| WRITE-020 | `WriterConfig.pdf_version` / `PdfSaveOptions.PdfVersion` explicit version kwarg | ✅ kwarg + getter on `WriterConfig` | ✅ `PdfSaveOptions.PdfVersion` property | — |
| WRITE-021 | In-memory save with config (no file roundtrip) | ✅ `Document.save_to_bytes_with_config(config)` | ✅ `PdfDocument.SaveToBytes(PdfSaveOptions)` | — |

---

## Tier 3 — Annotations / Forms (architectural asymmetry)

This tier exposes the largest current asymmetry: Python is "build + read"; .NET is "read only" for forms and most annotations.

| ID | Capability | Python | .NET | Action for parity |
|---|---|---|---|---|
| ANN-001 | Read annotations from existing PDF | ✅ | ✅ `GetAnnotationsAsync()` | — |
| ANN-002 | Write link annotations (URI + GoTo) | ✅ | ✅ `AddLinkUri()` / `AddLinkGoToPage()` | — |
| ANN-003 | Write text markup (highlight, underline, strikeout) | ✅ rich types | ✅ `AddHighlight/Underline/StrikeOut` | — |
| ANN-004 | Write text note + stamp + geometric annotations | ✅ | ✅ | — |
| ANN-005 | FileAttachment annotation | ✅ | ❌ | **.NET: add `AddFileAttachment()`** |
| ANN-006 | FreeText annotation | ✅ | ❌ | **.NET: add `AddFreeText()`** |
| FORM-001 | Read form fields (text, checkbox, radio, dropdown, listbox, button) | ✅ | ✅ `GetFormFieldsAsync()` | — |
| FORM-002 | Create form fields | ✅ `CheckBox`/`TextField`/`ListBox`/etc. | ❌ | **.NET: implement form-creation API** |
| FORM-003 | Fill form fields programmatically | ✅ | ❌ | **.NET: implement `FillField(name, value)`** |
| FORM-004 | Form calculation engine | ✅ `FormCalculationSystem` | ❌ | **.NET: scope decision pending** |
| FORM-005 | Field validation | ✅ `FieldValidator` | ❌ | same as FORM-004 |

---

## Tier 4 — Document operations

| ID | Capability | Python | .NET | Action for parity |
|---|---|---|---|---|
| OPS-001 | Split to individual pages | ✅ `split()` | ✅ `SplitAsync()` | — |
| OPS-002 | Split by size/range | ✅ `split_pdf_with_options(SplitOptions)` | ✅ `SplitAsync(PdfSplitOptions)` rich | — |
| OPS-003 | Merge documents | ✅ | ✅ `MergeAsync()` | — |
| OPS-004 | Merge with selective page ranges | ✅ `merge_pdfs_with_inputs(MergeInput, PageRange)` | ✅ `PdfMergeInput + PdfPageRange` | — |
| OPS-005 | Rotate all pages | ✅ | ✅ | — |
| OPS-006 | Rotate selective pages | ✅ | ✅ `RotatePagesAsync(PdfPageRange)` | — |
| OPS-007 | Extract pages | ✅ | ✅ | — |
| OPS-008 | Overlay/watermark | ✅ | ✅ | — |
| OPS-009 | Reorder/swap/move/reverse pages | ✅ | ✅ | — |
| OPS-010 | Extract images | ✅ | ✅ `ExtractImagesAsync()` | — |
| OPS-011 | `SplitMode.Ranges` constructor (core has the variant) | ✅ `SplitMode.ranges([PageRange])` | ❌ | **.NET: expose `Ranges` constructor** |
| OPS-012 | `SplitOptions` full shape (`output_pattern` + `preserve_metadata` + `optimize`) | ✅ `SplitOptions(mode, output_pattern, preserve_metadata, optimize)` | ❌ | **.NET: expose all three fields on `PdfSplitOptions`** |
| OPS-013 | Expose `MetadataMode` in merge | ❌ | ❌ | **BOTH: needed for multi-PDF publishing flows that fix a unified title** |

---

## Tier 5 — Signatures + PDF/A

| ID | Capability | Python | .NET | Action for parity |
|---|---|---|---|---|
| SIG-001 | Detect signatures | ✅ | ✅ `HasDigitalSignaturesAsync()` | — |
| SIG-002 | Parse signatures (PKCS#7/CMS) | ✅ + low-level utils | ✅ `GetDigitalSignaturesAsync()` | — |
| SIG-003 | Verify signatures | ✅ `verify_pdf_signature()` | ✅ `VerifySignaturesAsync()` | — |
| PDFA-001 | PDF/A validation | ✅ `PdfAValidationResult` | ❌ | **.NET: add `ValidatePdfAAsync()`** |
| PDFA-002 | XMP metadata read/write | ✅ | ❌ | **.NET: add XMP support** |

---

## Tier 6 — Integrations / Tooling (platform-specific surface)

| ID | Capability | Python | .NET | Action for parity |
|---|---|---|---|---|
| INT-001 | LlamaIndex reader (PyPI) | ✅ `llama-index-readers-oxidize-pdf 0.1.1` | 🚫 N/A (LlamaIndex is Python-only) | — |
| INT-002 | Microsoft Kernel Memory adapter (NuGet) | 🚫 N/A | ⚠️ `examples/KernelMemory/` only sample | **.NET: package as NuGet `OxidizePdf.KernelMemory.DocumentReader`** |
| INT-003 | LangChain document loader | ❌ | 🚫 N/A | **Python: add LangChain adapter** |
| INT-004 | Haystack converter | ❌ | 🚫 N/A | **Python: add Haystack adapter** |
| INT-005 | Semantic Kernel plugin | 🚫 N/A | ❌ | **.NET: add Semantic Kernel plugin** |
| MCP-001 | MCP server (Anthropic protocol) | ✅ FastMCP, 12 tools, 6 resources, session + workspace | ❌ | **.NET: implement MCP server (official `ModelContextProtocol` C# SDK exists)** |
| MCP-002 | Official MCP registry submission | ⚠️ `server.json` local at 0.4.1, registry at 0.3.1 (stale) | 🚫 blocked by MCP-001 | **Python: refresh `server.json` and run `mcp-publisher publish`** |
| INT-006 | Claude Code plugin | 🚫 lives in `oxidize-pdf-integrations/claude-code` 1.0.1 (cross-language) | 🚫 same | — |

---

## Tier 7 — Quality / Tooling alignment

| ID | Capability | Python | .NET | Action for parity |
|---|---|---|---|---|
| QA-001 | `FEATURE_PARITY.md` up to date with current bridge version | ✅ 2026-04-22 | ❌ stale 2026-03-20 (says core 2.3.2; actual is 2.5.5) | **.NET: refresh parity doc when closing the alignment analysis** |
| QA-002 | Semantic disjointness tests for RAG | ✅ 19 tests (12 bridge + 7 reader) | ❌ | (covered by RAG-020) |
| QA-003 | CI cross-platform matrix (3.10–3.14 / .NET 6/8/9) | ✅ 5 OS × 5 Python | ✅ similar matrix | — |
| QA-004 | Empirical audit script for RAG correctness | ✅ `.private/audit_rag_chunks.py` | ❌ | **.NET: add equivalent C# script that runs against `fixtures/`** |
| QA-005 | Cross-session error log | ✅ `.private/error-log.md` (60+ entries) | ✅ similar | — |
| QA-006 | TDD plan visible | ✅ `.private/tdd-plan-full.md` | ✅ `PROJECT_PROGRESS.md` | **Align doc name/location** |

---

## Tier 8 — OCR (ecosystem gap)

Tesseract-based OCR exists in the core but is absent from both bridges. This tier blocks every RAG-019 consumer that processes scanned documents.

| ID | Capability | Python | .NET | Action for parity |
|---|---|---|---|---|
| OCR-001 | `TesseractOcrProvider` real provider | ❌ | ❌ | **BOTH: expose the core provider; ship as extras (`pip install oxidize-pdf[ocr]` / NuGet `OxidizePdf.Ocr`) with Tesseract as a documented system dependency** |
| OCR-002 | `OcrOptions.preprocessing` (deskew, denoise, enhance_contrast) | ❌ | ❌ | **BOTH: domain-tuning knobs without dropping to Rust** |
| OCR-003 | `OcrProcessingResult.fragments` with per-word confidence | ❌ | ❌ | **BOTH: enables hybrid OCR + human-review pipelines** |
| OCR-004 | `PdfOcrConverter` / `to_searchable_pdf()` | ❌ | ❌ | **BOTH: flagship OCR workflow — generate PDF with invisible text layer** |
| OCR-005 | `OcrProvider` protocol (Python ABC / C# interface) | ❌ | ❌ | **BOTH: lets cloud providers (AWS Textract, GCP Vision) plug in without Rust changes** |

---

## Summary: work owed by each side

### .NET — bridge-specific actions (priority RAG first)

**RAG/AI (Tier 0):** ~~RAG-003~~, ~~RAG-004~~, ~~RAG-005~~, ~~RAG-006~~, ~~RAG-007~~, ~~RAG-008~~, ~~RAG-009~~, ~~RAG-012~~, ~~RAG-020 (tests)~~ — **all closed in 0.9.0-rag.1**. Decisions still pending: RAG-014 (JSON export philosophy), RAG-019 (OCR), RAG-021 (NuGet KernelMemory package).

**Reading:** READ-001 (path overload), READ-007 (per-page list), READ-010 (rotation).
**Writing:** WRITE-013 (open action), WRITE-015 (outline read), WRITE-017 (page labels), WRITE-019 (semantic entities).
**Annotations/Forms:** ANN-005 (file attachment), ANN-006 (free text), FORM-002 (create), FORM-003 (fill), FORM-004/005 (calculation + validation).
**Other:** PDFA-001, PDFA-002, MCP-001, INT-002 (NuGet pkg), INT-005 (Semantic Kernel), QA-001, QA-004.

### Python — 6 bridge-specific actions

**RAG/AI:** RAG-019 (OCR — known gap, covered by Tier 8).
**Ops:** OPS-002, OPS-004 (page ranges).
**Integrations:** INT-003 (LangChain), INT-004 (Haystack), MCP-002 (refresh registry).

### Ecosystem gaps shared by both bridges

Neither side exposes these today. Listed as first-class rows above to gain visibility in scheduling.

**Reading:** READ-014 (embedded font bytes), READ-015 (single-image extraction by name), READ-016 (direct vs inherited resources), READ-017 (raw content streams + filter chain).
**Writing:** WRITE-018 (tagged PDF / structure tree).
**Ops:** OPS-011 (`SplitMode.Ranges`), OPS-012 (`SplitOptions` full shape), OPS-013 (`MetadataMode` in merge).
**OCR (Tier 8):** OCR-001 through OCR-005 — blocks every RAG-019 consumer.

---

## Philosophical decisions to settle BEFORE implementing parity work

These are not implementation tasks — they are scope decisions that will change how rows above are written.

1. **Page indexing.** Python is 0-based, .NET is 1-based. Pick one and migrate the other. Recommendation: 0-based (matches the PDF spec and the underlying Rust core).
2. **`Element` vs `PdfElement` naming.** Both wrap the same Rust struct. Pick one. Recommendation: `Element` — the namespace already prevents collisions.
3. **Sync vs async.** Python is sync-only for extraction; .NET is async-only. Recommendation: leave as-is — each is idiomatic for its language; forcing parity adds noise.
4. **Stateful (file-based) vs stateless (bytes-based) reader.** Python stateful, .NET stateless. Recommendation: add overloads to both (READ-001 to .NET, READ-003 to Python) so users can choose without bridge-shopping.
5. **MCP server in .NET (MCP-001).** The official `ModelContextProtocol` C# SDK exists. Decision pending: build it now (parallel investment) or defer until the .NET surface justifies it.
6. **`.NET` writing-API scope.** Today .NET is mostly "read & analyze". The parity gaps in Tier 3 (forms, annotations) are large. Decision pending: full parity (large C# investment) vs deliberate read-focus (document the asymmetry as intentional).

---

## Maintenance rules

1. This document and its sibling in `oxidize-pdf-dotnet` are **mirrors**. Any edit to one must be propagated to the other in the same PR cycle.
2. When a row changes status (e.g. ❌ → ✅), update both copies AND the row's "Action for parity" cell to `—`.
3. When a new capability lands in either bridge, add a new row with a unique ID following the existing tier numbering.
4. **Tier 0 rows MUST have semantic regression tests** before they can be marked ✅. Shape-only / smoke tests do not count (lesson from the 2026-04-21 RAG audit).
5. Do not delete IDs — if a capability is intentionally dropped, mark it 🚫 with the reason in the action cell.
