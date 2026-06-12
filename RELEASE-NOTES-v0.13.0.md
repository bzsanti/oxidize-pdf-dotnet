# Release Notes — v0.13.0

**Release Date:** 2026-06-12
**Previous Version:** v0.12.0
**Type:** MINOR (new features, backward compatible)

## Summary

Milestones M1, M2, M5 and M6 plus axial/radial gradients (GFX-017), built on
upstream `oxidize-pdf` 2.14.0. This release adds document metadata and navigation
(open actions, viewer preferences, named destinations, page labels, writer
options), the forms write-path, AI-oriented chunking with language detection,
gradients, page editing on existing PDFs, screen-space coordinates, and a full
accessibility/semantic layer (Tagged PDF structure tree, marked content, column
layout, semantic entity export, text validation). All additions are backward
compatible.

## New Features

### M1 — Document metadata and navigation (#20)
- **DOC-014 Open action.** `PdfDocument.SetOpenAction(PdfOpenAction)` —
  `PdfOpenAction.GoTo(pageIndex, PdfDestination?)` or `.Uri(string)`;
  `PdfDestination.Fit/Xyz/FitH/FitV` with `PdfDestinationFit`.
- **DOC-015 Viewer preferences.** `PdfDocument.SetViewerPreferences(PdfViewerPreferences)`
  (toolbar/menu visibility, window behaviour, `PdfPageLayout`, `PdfPageMode`,
  `PdfPrintScaling`, `PdfDuplex`, copies, tray).
- **DOC-017 Named destinations.** `PdfDocument.AddNamedDestination(name, PdfDestination)`
  (name tree; re-adding a name overwrites it).
- **DOC-018 Page labels.** `PdfDocument.SetPageLabels(PdfPageLabels)` via fluent
  `PdfPageLabels.Create().AddRange(startPage, PdfPageLabelStyle, prefix?, startAt?)`
  (decimal / roman / letters).
- **DOC-020 Save with writer options.** `PdfDocument.SaveToBytes(PdfSaveOptions)`
  controlling PDF version, xref/object streams, and stream compression
  (`Default()` / `Modern()` / `Legacy()`).

### M2 — Forms write-path (#21)
- **FORM-007 / PAGE-008.** Create form fields and place their widgets on a page
  (write-path counterpart to the existing form read APIs).

### AI chunking (oxidize-pdf 2.13.0)
- **Language detection.** `DocumentChunker.WithLanguageDetection(bool)` +
  `ChunkPdf(byte[])` returning full-fidelity `OxidizePdf.NET.Ai.DocumentChunk`
  records; `DocumentChunker.DocumentLanguage(...)` returns the length-weighted
  dominant language.
- **Token-efficient export.** `OxidizePdf.NET.Ai.TokenEfficientExporter`
  (`Export` / `Parse`) — TOON-style tabular serialization (~64% fewer tokens
  than JSON), round-trippable except per-chunk `Language`.
- **Ruling-based table detection toggle.** `PartitionConfig.PreferRulingTables`
  (+ `WithoutRulingTables()`).

### GFX-017 — Gradients (oxidize-pdf 2.14.0)
- **Axial (linear) gradients.** `PdfPage.AddAxialShading(...)` (Type 2 shading)
  with `GradientStop(position, r, g, b)`.
- **Radial gradients.** `PdfPage.AddRadialShading(...)` (Type 3 shading).
- **Paint + clip primitives.** `PdfPage.PaintShading(name)` (`sh`) and
  `PdfPage.EndPath()` (`n`); bound via
  `SaveGraphicsState().ClipRect(..).PaintShading(name).RestoreGraphicsState()`.

### M5 — Page editing and coordinate systems (#24)
- **PAGE-010 Edit an existing page.** `PdfPage.FromParsedBytes(byte[] pdf, int pageIndex)`
  returns a writable page preserving original content streams and resources;
  new content overlays the original.
- **PAGE-011 Screen-space coordinates.** `PdfPage.BeginScreenSpace(double scale = 1.0)`
  switches to a top-left origin (Y grows downward) via a Y-flip matrix. Intended
  for shape/line/path ops; text after the switch is mirrored (documented caveat).

### M6 — Accessibility, semantic, text advanced (#25)
- **DOC-019 Tagged PDF structure tree.** `PdfDocument.SetStructureTree(PdfStructureTree)`
  emits `/StructTreeRoot`, `/MarkInfo <</Marked true>>` and `/StructElem`
  dictionaries (standard structure types, lang/alt-text/actual-text/title, role
  mapping, marked-content links).
- **PAGE-009 Marked content.** `PdfPage.BeginMarkedContent(tag)` / `EndMarkedContent()`
  emit `BDC`/`EMC` with auto-assigned MCIDs for PDF/UA accessibility.
- **TXT-014 Column layout.** `PdfPage.RenderColumns(ColumnTextOptions)` flows text
  across N columns (width, alignment, line height, separators, balance).
- **DOC-021 Semantic entities (AI-ready markup).** `MarkEntity` / `SetEntityContent` /
  `AddEntityMetadata` / `SetEntityConfidence` / `RelateEntities`, exported via
  `ExportSemanticEntitiesJson` or `ExportSemanticEntitiesJsonLd` (Schema.org).
  **Caveat:** export-only — entities are NOT embedded in the saved PDF (use
  DOC-019 for in-PDF tagged structure).
- **TXT-016 Text validation.** `TextValidation.ValidateContract` / `Search` /
  `ExtractKeyInfo` classify dates, monetary amounts, contract numbers and party
  names in already-extracted text. **Caveat:** text-content validator, not a
  PDF-structure integrity checker — feed it extracted text, not raw PDF bytes.

## Bug Fixes

- None (new feature release).

## Breaking Changes

- None (all additions are additive and backward compatible).

### Note
- `OxidizePdf.NET.Ai.DocumentChunk` (RAG chunk) is distinct from
  `OxidizePdf.NET.Models.DocumentChunk` (per-page chunk). Consumers importing both
  namespaces must qualify the unqualified name.

## Dependencies

- Upstream `oxidize-pdf` 2.12.0 → 2.14.0 (enables `language-detection` /
  `whatlang`, and the `sh` shading-paint operator for gradients).

## Validation

- Build: 0 warnings, 0 errors (net8.0 / net9.0 / net10.0, `TreatWarningsAsErrors`).
- Tests: 857 passed, 0 failed, 0 skipped.

## Changelog (since v0.12.0)

```
639b387 feat(m6): accessibility, semantic, text advanced (#25)
c9d2016 feat(m5): page editing + custom coordinate systems (#24)
07655ac feat(gfx-017): axial/radial gradients + upstream 2.14.0 (#40)
af858bb feat(m2): forms write-path — FORM-007 create fields + PAGE-008 widgets (#21)
3303966 feat(m1): document metadata — open action, viewer prefs, named dests, page labels, WriterConfig save (#20)
7c5c231 feat(upstream): bump oxidize-pdf 2.13.0 and expose its new AI features
```
