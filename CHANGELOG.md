# Changelog

All notable changes to OxidizePdf.NET will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.15.0] - 2026-06-26

### Added — CID-keyed positioned glyph runs (#358)
- **`PdfDocument.AddCidKeyedFont(string name, byte[] fontData, CidFontMapping mapping)`**
  registers a CID-keyed (CID = glyph id) TrueType font, and
  **`PdfPage.ShowCidArray(string fontName, double size, IReadOnlyList<CidGlyph> glyphs, double x, double y)`**
  draws a pre-shaped, positioned glyph run (a `TJ` array) over it. The caller
  supplies an already-shaped run (e.g. from a shaper such as `rustybuzz`),
  expressing ligatures and per-glyph kerning/offset the Unicode-keyed path
  cannot; the run stays extractable via the emitted `ToUnicode` CMap. New
  models `CidFontMapping` (CID→GID and CID→Unicode maps) and `CidGlyph`
  (`Cid`, `Adjust`, `XOffset`). Exposes upstream `oxidize-pdf` 3.0.0 issue #358.
  Only TrueType (CIDFontType2) fonts are supported.

### Changed
- Upgraded the native `oxidize-pdf` core from 2.15.0 to **3.0.1** (a major
  upstream release). Beyond the CID-keyed write API, this brings bounded-memory
  lenient parsing for damaged/large PDFs (#339), an xref-stream double-decode
  fix that repairs the strict reader for every xref-stream PDF (#341), a PDF/A
  validation fix for Flate-compressed XMP metadata (#346), and a
  manual-stream-reconstruction fix that preserves non-Flate filters on damaged
  files (#351).

### Breaking Changes
None at the .NET API surface. All additions are backward compatible.

## [0.14.0] - 2026-06-12

### Added — FORM-008: fill forms on existing PDFs
- **`PdfOperations.FillFormFieldsAsync(byte[] pdfBytes, IReadOnlyDictionary<string, string> fields)`** —
  fills AcroForm fields on an already-serialized PDF and returns the updated
  bytes, appending an ISO 32000-1 §7.5.6 incremental update (core
  `IncrementalFormFiller`, upstream oxidizePdf #318; previously blocked, now
  unblocked in core 2.15.0). Unlike
  `PdfDocument.FillField` (in-process builder fields only), this works on any
  parsed PDF produced elsewhere (Acrobat, pdftk, ReportLab, …). The base bytes
  are preserved verbatim as the output prefix; `/AcroForm/NeedAppearances` is
  set so compliant viewers regenerate the field appearance on open. Throws
  `PdfExtractionException` when a named field does not exist.

### Changed
- Upgraded the native `oxidize-pdf` core from 2.14.0 to **2.15.0**.

### Fixed — text extraction (via core 2.15.0, #319)
- Text past a malformed content operator is now recovered instead of dropped.
- Text inside Form XObjects is now extracted (the extractor recurses into
  them), recovering content previously missing from some invoices and forms.

## [0.13.0] - 2026-06-12

### Added — M1 Document metadata (#20)
- **DOC-014: Open action.** `PdfDocument.SetOpenAction(PdfOpenAction)` —
  `PdfOpenAction.GoTo(pageIndex, PdfDestination?)` or `.Uri(string)`;
  `PdfDestination.Fit/Xyz/FitH/FitV` with `PdfDestinationFit`.
- **DOC-015: Viewer preferences.** `PdfDocument.SetViewerPreferences(PdfViewerPreferences)`
  (toolbar/menu visibility, window behaviour, `PdfPageLayout`, `PdfPageMode`,
  `PdfPrintScaling`, `PdfDuplex`, copies, tray).
- **DOC-017: Named destinations.** `PdfDocument.AddNamedDestination(name, PdfDestination)`
  (name tree; re-adding a name overwrites it).
- **DOC-018: Page labels.** `PdfDocument.SetPageLabels(PdfPageLabels)` —
  fluent `PdfPageLabels.Create().AddRange(startPage, PdfPageLabelStyle, prefix?, startAt?)`
  (decimal / roman / letters, optional prefix and starting value).
- **DOC-020: Save with writer options.** `PdfDocument.SaveToBytes(PdfSaveOptions)`
  controlling PDF version, xref/object streams, and stream compression
  (`PdfSaveOptions.Default()` / `.Modern()` / `.Legacy()`).

### Changed
- **Upstream `oxidize-pdf` 2.12.0 → 2.13.0.** Enables the `language-detection`
  core feature (`whatlang`).
- **Upstream `oxidize-pdf` 2.13.0 → 2.14.0.** Adds the `sh` shading-paint
  operator, unblocking gradient rendering (GFX-017, core issue #297).

### Added — AI chunking (oxidize-pdf 2.13.0)
- **Language detection.** `DocumentChunker.WithLanguageDetection(bool)` enables
  per-chunk detection; `DocumentChunker.ChunkPdf(byte[])` returns full-fidelity
  `OxidizePdf.NET.Ai.DocumentChunk` records (with `ChunkMetadata` /
  `ChunkPosition` / `DetectedLanguage`); static
  `DocumentChunker.DocumentLanguage(IEnumerable<DocumentChunk>)` returns the
  length-weighted dominant language.
- **Token-efficient chunk export.** `OxidizePdf.NET.Ai.TokenEfficientExporter`
  with `Export(IEnumerable<DocumentChunk>)` / `Parse(string)` — a TOON-style
  tabular serialization (~64% fewer tokens than JSON on a representative corpus),
  round-trippable except for per-chunk `Language`.
- **Ruling-based table detection toggle.** `PartitionConfig.PreferRulingTables`
  (default `true`) + `WithoutRulingTables()`, mirroring the new
  `PartitionConfig::prefer_ruling_tables` core field.

### Note
- `OxidizePdf.NET.Ai.DocumentChunk` (RAG chunk) is distinct from the existing
  `OxidizePdf.NET.Models.DocumentChunk` (per-page chunk). Consumers importing
  both namespaces must qualify the unqualified name.

### Added — GFX-017 Gradients (oxidize-pdf 2.14.0)
- **Axial (linear) gradients.** `PdfPage.AddAxialShading(name, x1, y1, x2, y2,
  IEnumerable<GradientStop>, extendStart?, extendEnd?)` registers a Type 2
  shading; `GradientStop(position, r, g, b)` carries RGB color stops.
- **Radial gradients.** `PdfPage.AddRadialShading(name, startCenterX, startCenterY,
  startRadius, endCenterX, endCenterY, endRadius, IEnumerable<GradientStop>,
  extendStart?, extendEnd?)` registers a Type 3 shading.
- **Paint + clip primitives.** `PdfPage.PaintShading(name)` emits the `sh`
  operator; `PdfPage.EndPath()` emits the `n` path-terminator. Bound a gradient
  with `SaveGraphicsState().ClipRect(..).PaintShading(name).RestoreGraphicsState()`.

### Added — M6 Accessibility, semantic, text advanced (#25)
- **DOC-019: Tagged PDF structure tree.** `PdfDocument.SetStructureTree(PdfStructureTree)`
  attaches a logical structure tree built with `PdfStructureTree.AddRoot/AddChild`
  (standard structure types, `lang`/`alt_text`/`actual_text`/`title`, role
  mapping, marked-content links). The writer emits `/StructTreeRoot`,
  `/MarkInfo <</Marked true>>` and `/StructElem` dictionaries — a Tagged PDF.
- **PAGE-009: Marked content.** `PdfPage.BeginMarkedContent(tag)` returns an
  auto-assigned MCID and emits `/{tag} <</MCID n>> BDC`; `PdfPage.EndMarkedContent()`
  emits `EMC`. Link the MCID to a structure element for PDF/UA accessibility.
- **TXT-014: Column layout.** `PdfPage.RenderColumns(ColumnTextOptions)` flows
  text across N equal or custom-width columns (font, size, alignment, line
  height, separators, balance), emitting positioned text per column.
- **DOC-021: Semantic entities (AI-ready markup).** `PdfDocument.MarkEntity` /
  `SetEntityContent` / `AddEntityMetadata` / `SetEntityConfidence` /
  `RelateEntities`, exported with `ExportSemanticEntitiesJson` (full fidelity)
  or `ExportSemanticEntitiesJsonLd` (Schema.org). **Caveat:** entities are an
  in-memory annotation + export feature; they are NOT embedded in the saved PDF
  (use DOC-019 for in-PDF tagged structure).
- **TXT-016: Text validation.** `TextValidation.ValidateContract` / `Search` /
  `ExtractKeyInfo` classify dates, monetary amounts, contract numbers and party
  names in already-extracted text. **Caveat:** this is a text-content validator
  (upstream `text/validation.rs`), not a PDF-structure integrity checker; feed
  it extracted text, not raw PDF bytes.

### Added — M5 Page editing and coordinate systems (#24)
- **PAGE-010: Edit an existing page.** `PdfPage.FromParsedBytes(byte[] pdf, int pageIndex)`
  opens an existing PDF and returns a writable page that preserves the original
  content streams and resources (fonts resolved/embedded). New content drawn on
  the page is overlaid alongside the original; after saving and re-parsing, both
  the original and the overlay are present.
- **PAGE-011: Screen-space coordinates.** `PdfPage.BeginScreenSpace(double scale = 1.0)`
  switches the page to a top-left origin (Y grows downward) with a uniform scale,
  emitting a single Y-flip transformation matrix so subsequent draw operations
  use screen-space coordinates. Intended for shape/line/path ops; text drawn
  after the switch is mirrored vertically (documented caveat).

## [0.12.0] - 2026-06-06

### Added — M4a Advanced Graphics
- **GFX-016: Tiling patterns.** `PdfPage.AddTilingPattern` + `SetFillPattern` /
  `SetStrokePattern`; C# types `PdfTilingPattern`, `PaintType`, `TilingType`.
- **GFX-018: Form XObjects.** Reusable content streams via `PdfPage.AddFormXObject` /
  `InvokeXObject` (`/name Do`); C# type `PdfFormXObject` (bbox, content, optional matrix).
- **GFX-020: Transparency groups.** `PdfFormXObject` accepts an optional
  `PdfTransparencyGroup` (`colorSpace`, `isolated`, `knockout`), emitting a real
  `/Group << /S /Transparency … >>` dictionary (ISO 32000-1 §11.4.5).
- **GFX-021: Soft masks.** `PdfPage.ApplySoftMask` with `PdfSoftMask.None()` /
  `Alpha(group)` / `Luminosity(group)`; emits an ExtGState `/SMask` whose `/G`
  resolves to a registered Form XObject (§11.6.4.3).
- **GFX-022: Draw text from the graphics context.** `PdfPage.DrawTextAt(font, size,
  x, y, text)` emits `BT … Tf … Td (text) Tj ET` integrated with the graphics-state
  stack (fill colour, clipping, soft masks, transforms) — distinct from `TextAt`.
- **GFX-023: Draw image with transparency.** `PdfPage.DrawImageWithTransparency(image,
  x, y, w, h, mask?)` — placement matrix + `Do`, optionally soft-masked by a Form XObject.
- **GFX-024: Elliptical clipping.** `PdfPage.ClipEllipse(cx, cy, rx, ry)` emits the
  ellipse path + `W n`; non-positive radii are rejected.

### Note
- **GFX-017 (axial/radial gradients) is not included.** oxidize-pdf 2.12.0 emits a
  placeholder `/Function` for shadings and has no `sh` painting operator, so gradients
  do not render. Tracked upstream as bzsanti/oxidizePdf#297; deferred to a later milestone.

## [0.11.0] - 2026-06-05

### Added — M3 Color Spaces
- **GFX-014: CalGray and CalRGB calibrated color spaces** (hardcoded and named
  variants). `PdfPage.SetFillColorCalGray` / `SetStrokeColorCalGray`,
  `SetFillColorCalRgb` / `SetStrokeColorCalRgb`, `SetFillColorCalibratedNamed` /
  `SetStrokeColorCalibratedNamed`, and `AddColorSpace(name, PageColorSpace.CalGray|CalRgb(cs))`.
  C# types `CalGrayColorSpace`, `CalRgbColorSpace`, `CalibratedColor`.
- **GFX-015: CIE L\*a\*b\* color space** (hardcoded and named variants).
  `PdfPage.SetFillColorLab` / `SetStrokeColorLab`, `SetFillColorLabNamed` /
  `SetStrokeColorLabNamed`, `AddColorSpace(name, PageColorSpace.Lab(cs))`.
  C# types `LabColorSpace`, `LabColor`.
- **GFX-019: ICC color profiles** (unblocked by oxidize-pdf 2.12.0).
  Inline ICCBased path: `AddColorSpace(name, PageColorSpace.IccBased(n, alternate))`.
  Embedded-profile path (.NET superset over the Python binding):
  `AddIccColorSpace(name, IccProfile)`. Draw with `SetFillColorIcc` /
  `SetStrokeColorIcc`. C# types `IccProfile`, `IccColorSpace`, `PageColorSpace`.
  Empty ICC components / empty profile data are rejected in all builds.
- Multiple named color spaces per page are now supported (the previous
  one-calibrated-space-per-page limitation is removed via the upstream
  `*_named` variants). API surface mirrors the `oxidize-python` bridge.

### Note
- The hardcoded calibrated/Lab setters (`SetFillColorCalGray`, `SetFillColorLab`, …)
  emit a reference to the default `CalGray1` / `CalRGB1` / `Lab1` resource slot
  without registering it (matching upstream / the Python binding). For a
  self-contained, spec-valid resource dictionary use the named variants
  (`AddColorSpace` + `SetFillColor*Named`).

## [0.10.0] - 2026-05-28

### Added
- **`ExtractionOptions.TjSpaceThreshold`** (default `0.2`) — synthesises an
  implicit `U+0020` when a `TJ` numeric kerning offset exceeds
  `TjSpaceThreshold × font_size`. Inherited from upstream
  oxidize-pdf 2.10.0 (issue #272). Fixes run-on words like
  `MINISTERIO` → `M I N I S T E R I O` and similar artefacts on
  government / academic / LaTeX PDFs that encode inter-word gaps as
  wide negative kerns rather than literal spaces.
- **`ExtractionOptions.ReconstructParagraphs`** (default `false`) — groups
  raw text fragments by baseline into line-level fragments, then groups
  consecutive lines with normal leading into paragraph-level fragments.
  Inherited from upstream oxidize-pdf 2.10.0 (issue #261). Direct
  `ExtractTextWithOptionsAsync` callers must opt in; the partition
  pipeline forces it on internally.
- **`ExtractionOptions.IncludeArtifacts`** (default `false`) — opt-in
  inclusion of `/Artifact` marked-content scopes (page headers, footers,
  watermarks, decorative content). Inherited from upstream
  oxidize-pdf 2.10.0 (issue #269). Default matches PDF/UA accessibility
  guidance and typical RAG callers; flip to `true` for forensic auditing
  or redaction tooling.

### Changed
- **Updated oxidize-pdf dependency 2.8.0 → 2.10.0**. The bump spans two
  upstream releases (2.9.0 and 2.10.0). Inherited capabilities and
  observable behaviour changes are listed below.

  **Inherited from oxidize-pdf 2.9.0:**
  - **`ocr-tesseract` removed from default features** upstream. Not
    user-visible here: the FFI crate already pins
    `default-features = false` and only enables `compression`, `semantic`,
    and `signatures`.
  - **`external-images` added to default features** upstream. Not
    user-visible here for the same reason.
  - **README / crate metadata** rewritten by upstream to lead with RAG
    positioning. No FFI surface change.

  **Inherited from oxidize-pdf 2.10.0 (RAG-grade text extraction):**
  - **Non-Identity CID encoding decode for Type0 fonts** (upstream
    issue #272). Character codes are now resolved to CIDs via embedded
    `/Encoding` stream CMaps and predefined Adobe CMaps (GBK-EUC-H,
    GBKp-EUC-H, 90ms-RKSJ-H, 90pv-RKSJ-H, KSCms-UHC-H) before mapping
    to Unicode, instead of assuming Identity. Fixes garbled glyph-code-
    as-Latin1 output on CJK and government PDFs.
  - **CMap parser hardened** against minified PostScript and adversarial
    input — token-based parser with guaranteed progress invariant
    replaces the prior whitespace-sensitive scanner (no more parser
    hangs on stray close delimiters).
  - **Marked-content extraction** (upstream issue #269 Phase 1).
    `TextFragment` upstream now carries `mcid: Option<u32>` and
    `struct_tag: Option<String>` from the innermost BDC ancestor; this
    bridge consumes them via field access only, so the addition is
    transparent at the FFI boundary.
  - **CTM composed into fragment positions** (upstream issue #262).
    Text positions now respect the current transformation matrix; a
    `q`/`Q` graphics-state stack was added.
  - **Line interleaving / column splitting fix** on tightly-spaced
    multi-column layouts (upstream issue #265). `row_id`-aware
    `merge_into_lines`, font-size-relative Y tolerance, deferred sort,
    baseline tolerance tightened to `0.2 × height`, plus a
    `font_size = 0` fallback.
  - **Parser position leak** fixed (upstream issue #260). `peek_token`
    restores position on error; `find_keyword_ahead` no longer reads
    past the peek buffer.
  - **`rag_realworld` example** in upstream — not bridged.

### Migration notes
- **Wire-format change**: `ExtractionOptionsNative` (FFI struct backing
  `ExtractionOptions`) gained three trailing fields. The native binary
  shipped in this NuGet package and the managed wrapper are versioned
  together, so end users do not need to act — but anyone running a
  custom-built native lib against a managed wrapper of a different
  version will see a mismatch. Rebuild both.
- **Defaults preserve 0.9.0 behaviour**: all three new
  `ExtractionOptions` properties default to upstream defaults that
  match pre-bump behaviour (no paragraph reconstruction, no artifact
  inclusion). `TjSpaceThreshold` does introduce one new behaviour
  (implicit space synthesis from wide `TJ` kerns) but this is a
  correctness fix; extractor output on affected PDFs (CJK, LaTeX,
  government corpora) now contains the spaces those PDFs always meant
  to render. Set `TjSpaceThreshold = double.PositiveInfinity` to opt
  out (not recommended).

## [0.9.0] - 2026-05-10

### Added
- **`PdfDocument.NewPageA4()`, `NewPageLetter()`, `NewPage(width, height)`** —
  document-bound page factories that pre-bind the document's
  `FontMetricsStore` to the page at construction time. These replace the
  legacy two-step pattern (`PdfPage.A4()` / `Letter()` / `new PdfPage(w, h)`
  followed by `PdfDocument.AddPage(page)`) for any flow that draws custom
  fonts via `PdfTextFlow.WriteWrapped`, table layout, or header / footer
  width-based positioning. The legacy pattern keeps working for built-in
  fonts; for custom fonts it falls back to upstream hardcoded default widths
  in oxidize-pdf 2.8.0+, so the new factories are mandatory there. Backed by
  three new FFI entry points: `oxidize_document_new_page_a4`,
  `oxidize_document_new_page_letter`, `oxidize_document_new_page`.

### Fixed
- **Custom-font measurement during text wrapping** now resolves against the
  document's per-instance `FontMetricsStore` instead of falling back to the
  upstream hardcoded Helvetica-shaped default profile. Before this fix, the
  FFI flow constructed the drawing page standalone (`PdfPage.A4()`) and only
  bound the store at `add_page` time — too late to affect any draw
  operations that ran first. Symptoms in the rendered PDF: incorrect line
  breaks for paragraphs drawn through `PdfTextFlow.WriteWrapped`, incorrect
  column widths in auto-sized tables, incorrect x-positioning for justified
  / centered / right-aligned text. The fix is the new factory methods above;
  no behaviour change for callers that only use built-in fonts. Verified
  by a Rust regression suite
  (`native/src/document.rs::fontmetricsstore_binding_tests`) that locks the
  full chain from `add_font_from_bytes` through `measure_text_with`,
  including a cross-check against the oracle path
  (`Font::from_bytes::measure_text`).

### Changed
- **Updated oxidize-pdf dependency 2.6.0 → 2.8.0**. The FFI public surface is
  unchanged; the bump is transparent for `OxidizePdf.NET` callers. The bump
  spans two upstream releases (2.7.0 and 2.8.0); inherited capabilities and
  observable behaviour changes are listed below. None of the new APIs are
  yet exposed through the FFI.

  **Inherited from oxidize-pdf 2.7.0:**
  - **Painter-model call order preserved across `Page::graphics()` /
    `Page::text()` / `Page::add_text_flow()` / `Page::append_raw_content()`**
    (upstream issue #227). Pre-2.7.0 the writer flushed the entire graphics
    buffer before the entire text buffer regardless of caller order.
    Observable change for FFI callers that interleave
    `oxidize_page_*_graphics_*` and text-emitting calls on the same page —
    z-ordering of generated PDFs now follows call order. No FFI signature
    change required.
  - **Non-finite float sanitisation extended to all numeric content-stream
    operators** in `GraphicsContext`, `TextContext`, and `TextFlowContext`
    (upstream issues #220 / #221). Path coords (`m`, `l`, `c`, `re`), line
    widths, miter limits, transforms (`cm`), text positioning, and text-state
    operators now route through `finite_or_zero` at the emission boundary.
    Out-of-scope paths (`forms/appearance.rs`, `signature_widget.rs`,
    `annotations`, `Op::Raw`) remain caller responsibility upstream.
    Extends the 2.6.0 colour-only fix already inherited by 0.8.0.
  - **Typed content-stream IR (`graphics::ops::Op` + `serialize_ops`)** —
    internal refactor; no observable FFI impact.
  - **`cm` matrix wire format normalised to `{:.2}` precision** — cosmetic
    change in emitted PDF bytes; ISO 32000-1-conformant in both old and new
    forms. Tests asserting byte-exact `cm` output downstream may need
    updating.
  - **`TextFlowContext` text-state setters** (`set_character_spacing`,
    `set_word_spacing`, `set_horizontal_scaling`, `set_leading`,
    `set_text_rise`, `set_rendering_mode`, `set_stroke_color`, upstream
    issue #222) — available upstream but not currently bridged through the
    FFI.
  - **API surface change with patch-compat semantics**:
    `GraphicsContext::operations()` / `get_operations()`,
    `TextContext::operations()`, `TextFlowContext::operations()`, and
    `Page::graphics_operations()` now return owned `String` (was `&str`).
    The FFI does not call these methods, so the change is invisible at the
    FFI boundary.

  **Inherited from oxidize-pdf 2.8.0:**
  - **`FontMetricsStore`** — per-`Document` custom font metrics store
    (replaces the process-wide global registry).
    `Document::add_font_from_bytes` automatically routes to the new store,
    so any custom font registered via
    `oxidize_document_add_font_from_bytes` benefits from scoped metrics
    with no API change. Resolves the cross-`Document` leak /
    last-writer-wins race documented upstream as issue #230. **Caveat for
    custom-font drawing flow**: `oxidize_document_add_page` clones the page
    into the document and the cloned copy receives the metrics-store
    inject; the caller's `PageHandle` does not. Drawing operations on the
    `PageHandle` that require metrics for `Font::Custom(_)` (text wrapping,
    table layout, header/footer width measurement) measure against default
    widths — the per-Document store is not bound on the handle. Tracked for
    a future revision that adopts the upstream document-first factories
    (`Document::new_page_a4()` / `new_page_letter()` / `new_page(w,h)`).
  - **Form-filling fix for `ComboBox` and `ListBox` widgets with
    `Font::Custom(_)`** — `Document::fill_field` now correctly emits
    Type0/CID appearance streams for choice fields with custom fonts
    (upstream issue #212). The fix is available upstream;
    `Document::fill_field` is **not yet exposed through the FFI** — when
    bridged, it will inherit the fix automatically.
  - **Form-filling fix for `PushButton` widgets with `Font::Custom(_)`** —
    the `/AP` resource dictionary now emits a `/Subtype /Type0` placeholder
    instead of an invalid `/Type1` entry that previously rejected
    non-WinAnsi labels with `PdfError::EncodingError` (upstream issue #212).
  - **`Document::new_page_a4 / new_page_letter / new_page(w,h)` factory
    methods** — available upstream; FFI continues to use `Page::a4()` /
    `Page::letter()` / `Page::new(w,h)` followed by `add_page`. See the
    `FontMetricsStore` caveat above for the implications during custom-font
    drawing.
- **Deprecated upstream APIs the FFI does not call**:
  `text::metrics::register_custom_font_metrics` and
  `text::metrics::get_custom_font_metrics`. No FFI changes required.

## [0.8.0] - 2026-05-05

### Added — RAG pipeline parity with the Python bridge

Closes every "immediate" Tier 0 row of `docs/PARITY_SPEC.md` (RAG-003,
RAG-004, RAG-005, RAG-006, RAG-007, RAG-008, RAG-009, RAG-012, RAG-020):

- **`ExtractionProfile` enum** (`OxidizePdf.NET.Pipeline`) — 7 values
  (`Standard`, `Academic`, `Form`, `Government`, `Dense`, `Presentation`,
  `Rag`); discriminant order locked against the Rust core via an
  exhaustive FFI test (the upstream enum has no `#[repr(u8)]`).
- **`MergePolicy` enum** — 2 variants (`SameTypeOnly`, `AnyInlineContent`)
  matching the Rust core. (PARITY_SPEC RAG-006 previously listed three
  variants — docs-only drift; patch attached for the Python repo.)
- **`ReadingOrderStrategy`** — three variants `Simple`, `None`,
  `XyCut(double minGap)`; serialises as serde-tagged JSON
  (`"Simple"` / `"None"` / `{"XYCut":{"min_gap":N}}`).
- **`PartitionConfig`** with fluent builder (`WithReadingOrder`,
  `WithMinTableConfidence`, …) plus client-side `Validate()`.
- **`HybridChunkConfig`** — full 5-field shape (`MaxTokens`,
  `OverlapTokens`, `MergeAdjacent`, `PropagateHeadings`, `MergePolicy`).
  Closes the RAG-005 ⚠️→✅ flip (legacy `ChunkOptions` only had 4).
- **`SemanticChunkConfig`** — element-boundary-aware chunker config.
- **`Ai.MarkdownOptions`** + **`Ai.DocumentChunker`** classes.
- **Six new `PdfExtractor` overloads:**
  - `PartitionAsync(byte[], ExtractionProfile, CancellationToken)`
  - `PartitionAsync(byte[], PartitionConfig, CancellationToken)`
  - `RagChunksAsync(byte[], ExtractionProfile, CancellationToken)` (RAG-003)
  - `RagChunksAsync(byte[], PartitionConfig?, HybridChunkConfig?, CancellationToken)`
  - `SemanticChunksAsync(byte[], SemanticChunkConfig?, PartitionConfig?, CancellationToken)`
  - `ToMarkdownAsync(byte[], MarkdownOptions, CancellationToken)` (RAG-012)
- **Standalone `Ai.DocumentChunker`** (RAG-008) — `ChunkText(string) → List<TextChunk>`
  for chunking arbitrary text without a PDF.
- **Static `Ai.DocumentChunker.EstimateTokens(string)`** (RAG-009) —
  upstream heuristic `floor(words * 1.33)`, contract locked in tests.
- **New result types:** `Models.SemanticChunk`, `Models.TextChunk`.

### Added — Tests

- **12 ported semantic disjointness regression tests** ported verbatim
  from the Python `test_rag_chunks_disjoint.py`
  (`Tests/Pipeline/RagChunksDisjointnessTests.cs`). Required by
  PARITY_SPEC maintenance rule #4 — these are the gating semantic
  tests for marking any Tier 0 row ✅.
- ~140 net new tests across the .NET and Rust suites.

### Added — FFI surface (eight new `extern "C"` entry points)

`oxidize_partition_with_profile`, `oxidize_partition_with_config`,
`oxidize_rag_chunks_with_profile`, `oxidize_rag_chunks_with_config`,
`oxidize_semantic_chunks`, `oxidize_to_markdown_with_options`,
`oxidize_chunk_text`, `oxidize_estimate_tokens`. Configuration crosses
the FFI boundary as UTF-8 JSON; profile crosses as a `u8` discriminant.

### Deprecated

- **`Models.ChunkOptions`** (character-based chunking) is now
  `[Obsolete]` with a migration message pointing at `HybridChunkConfig`
  (token-aware) or `SemanticChunkConfig` (element-aware). Remains
  callable for one minor release before removal.

### Security

- **Inherited from oxidize-pdf 2.6.0**: CWE-20 hardening — non-finite
  floats (`NaN`, `±inf`) in colour content-stream emission are now
  sanitised to `0.0` at the writer boundary. Direct construction of
  `Color::Rgb(NaN, ...)` (or `Gray`/`Cmyk`) previously bypassed the
  clamping in the public constructors and produced ISO 32000-1
  non-conformant content streams that conformant viewers reject
  (availability DoS via crafted input). The five `Color::Rgb(r, g, b)`
  sites in our annotation FFI inherit this protection automatically —
  no FFI code change needed. Upstream advisory: oxidize-pdf issue #220.

### Changed

- **Updated oxidize-pdf dependency 2.5.4 → 2.6.0**. Bundle release
  closing six upstream issues (security #220, #221 colour-emission
  single-source-of-truth, #216 `text_flow()` page-state propagation,
  #217 `TableStyle` header typography, #218 table-pagination APIs,
  #212 `fill_field` non-WinAnsi encoding). Also picks up 2.5.5 →
  2.5.7 fixes (per-font character tracking for subsetting #204,
  PDF-string escape SEC-F1, resource-name validation SEC-F5,
  deterministic resource-dict ordering, Roman-numeral page label
  case fix).
- **`oxidize_text_flow_create` now inherits page-level font /
  font_size / fill_color** from the caller's `Page::text()` state
  (oxidize-pdf #216). Existing FFI callers that explicitly call
  `oxidize_text_flow_set_font` after `oxidize_text_flow_create` are
  unaffected.
- **Slimmed native crate features** — switched to
  `default-features = false` with explicit
  `["compression", "semantic", "signatures"]`. Removed the
  implicitly-pulled `ocr-tesseract` default (no FFI entry point
  exposed OCR). **Native binary 11 MB → 5.7 MB (−48%)** on linux-x64;
  equivalent reduction expected on `osx-x64` / `osx-arm64` / `win-x64`.
  Compression (`flate2`) remains explicitly enabled — same wire
  format as before.
- **Documentation**: `docs/FEATURE_PARITY.md` refreshed (bridge
  0.5.0+feature/tier-a → 0.8.0; core 2.3.2 → 2.6.0; PARSE-010,
  PARSE-012, PARSE-013 entries cover the new overloads).
  **`docs/PARITY_SPEC.md`** added as a mirror of the Python repo's
  canonical cross-bridge spec (PARITY_SPEC maintenance rule #1).

### Dependencies

- oxidize-pdf 2.5.4 → 2.6.0
- Disabled implicit upstream defaults (`ocr-tesseract`); compression
  kept explicit

### Paused

- The DOC-014/015/017/020 milestone (`feature/m1-document-metadata`
  branch) is paused in favour of this RAG release. M1–M6 roadmap
  resumes after 0.8.0 ships. See
  `docs/superpowers/plans/2026-04-21-feature-parity-roadmap.md`.

## [0.7.2] - 2026-04-21

### Changed
- Updated oxidize-pdf dependency from 2.5.3 to 2.5.4 (upstream bug fixes)

### Dependencies
- oxidize-pdf 2.5.3 → 2.5.4

## [0.7.1] - 2026-04-20

### Changed
- Updated oxidize-pdf dependency from 2.5.1 to 2.5.3

### Dependencies
- oxidize-pdf 2.5.1 → 2.5.3

### Upstream fixes inherited (oxidize-pdf 2.5.2 + 2.5.3)
- **Font subsetting size reductions** (~3× smaller PDFs with embedded fonts)
  - TTF glyph hinting instructions stripped from subset output
  - FontFile2 / FontFile3 streams now FlateDecode-compressed (TTF glyf ~60-70% smaller)
  - ToUnicode CMap filtered to used characters only + FlateDecode-compressed
  - CFF String INDEX emitted empty in subset output (saves ~22 KB on Latin fonts)
- **CJK punctuation ToUnicode CMap offset math fix** — correct code-point mapping during CID CFF text extraction
- Full CFF Type 2 charstring desubroutinizer; SID-keyed CFF → CID-keyed conversion
- TTF `cmap`, `OS/2`, `name` tables stripped from subset output (not required for PDF embedding)

## [0.3.0] - 2026-03-04

### Changed
- **License changed from AGPL-3.0 to MIT** — aligned with oxidize-pdf core library
- Updated oxidize-pdf dependency from 1.6.6 to 2.0.0
- Updated version string in FFI layer to reflect oxidize-pdf v2.0.0

### Dependencies
- oxidize-pdf 1.6.6 → 2.0.0

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

[unreleased]: https://github.com/bzsanti/oxidize-pdf-dotnet/compare/v0.14.0...HEAD
[0.14.0]: https://github.com/bzsanti/oxidize-pdf-dotnet/compare/v0.13.0...v0.14.0
[0.13.0]: https://github.com/bzsanti/oxidize-pdf-dotnet/compare/v0.12.0...v0.13.0
[0.3.0]: https://github.com/bzsanti/oxidize-pdf-dotnet/compare/v0.2.2...v0.3.0
[0.2.2]: https://github.com/bzsanti/oxidize-pdf-dotnet/compare/v0.2.1...v0.2.2
[0.2.1]: https://github.com/bzsanti/oxidize-pdf-dotnet/compare/v0.1.0...v0.2.1
[0.1.0]: https://github.com/bzsanti/oxidize-pdf-dotnet/releases/tag/v0.1.0
