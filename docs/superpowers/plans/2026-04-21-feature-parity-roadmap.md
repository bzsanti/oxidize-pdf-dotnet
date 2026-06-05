# Feature Parity Roadmap — closing the remaining 26 gaps

> ⏸ **Paused (2026-05-05).** RAG pipeline parity with the Python bridge took
> precedence and shipped as `0.8.0`. See
> [`2026-04-22-rag-pipeline-parity.md`](2026-04-22-rag-pipeline-parity.md).
> Resume M1 after 0.8.0 lands on `main`.

> **Scope:** Master roadmap grouping the 26 remaining features from `docs/FEATURE_PARITY.md` into six release milestones. Each milestone will get its own detailed TDD plan (this file is the index, not a TDD plan).

**Goal:** Reach 100% feature parity with `oxidize-pdf 2.5.4` core across the FFI + .NET wrapper, delivered incrementally in semver-minor releases.

**Architecture:** Each milestone follows the existing three-layer pattern:
1. **FFI layer (`native/src/*.rs`)** — new `#[no_mangle] extern "C"` functions wrapping the core API, using opaque handles and JSON-in/JSON-out where complex.
2. **.NET P/Invoke (`dotnet/OxidizePdf.NET/Native/*.cs`)** — `[DllImport]` entries mirroring the FFI signatures, plus `SafeHandle`-backed disposables.
3. **.NET public API (`dotnet/OxidizePdf.NET/*.cs`)** — idiomatic C# surface with XML docs, `async` variants where I/O is involved, `TreatWarningsAsErrors` clean.

**Tech stack:** Rust 1.77+ edition 2021 (FFI), .NET 8/9/10 (managed), xUnit + FluentAssertions (tests), cargo test for FFI smoke tests. `cdylib` with `lto = true`, `panic = "abort"`.

**Discipline (inherited from CLAUDE.md):**
- Strict TDD — failing test first, minimal implementation, green, refactor.
- Never smoke tests — every test must verify actual content/behavior by reading produced PDF bytes.
- Zero warnings (`TreatWarningsAsErrors` true in both .NET projects).
- Version bump BEFORE the first PR of each milestone.
- Feature branches off `develop`; merge `feature/*` → `develop` → `main` with tag on main only with explicit authorization.

---

## Upstream verification (2026-04-21)

All 26 gaps confirmed available in `oxidize-pdf 2.5.4` source:

| Module / File in core | Features unlocked |
|---|---|
| `actions/` (goto, uri, launch, named, form) | DOC-014 |
| `viewer_preferences.rs` | DOC-015 |
| `structure/destination.rs` + `structure/name_tree.rs` | DOC-017 |
| `page_labels/` | DOC-018 |
| `structure/tagged.rs` + `structure/marked_content.rs` | DOC-019, PAGE-009 |
| `writer/pdf_writer::WriterConfig` | DOC-020 |
| `semantic/entity.rs` + `semantic/export.rs` | DOC-021 |
| `forms/{button,choice,signature}_widget.rs` + `forms/field.rs` | PAGE-008, FORM-007, FORM-008 |
| `page.rs::Page::from_parsed()` *(to confirm in plan M5)* | PAGE-010 |
| `coordinate_system.rs` | PAGE-011 |
| `text/layout.rs::ColumnLayout` | TXT-014 |
| `text/validation.rs` | TXT-016 |
| `graphics/calibrated_color.rs`, `lab_color.rs`, `color_profiles.rs` | GFX-014, GFX-015, GFX-019 |
| `graphics/patterns.rs`, `shadings.rs`, `form_xobject.rs` | GFX-016, GFX-017, GFX-018 |
| `graphics/transparency.rs`, `soft_mask.rs`, `clipping.rs` | GFX-020, GFX-021, GFX-024 |
| Graphics context helpers (`draw_text`, `draw_image`) | GFX-022, GFX-023 |

No upstream work required; pure FFI/.NET surface work.

---

## Milestone map

```
M1  v0.8.0  Document-level metadata       (5 features)  [small]   [shipped]
M2  v0.9.0  Forms — write path            (3 features)  [medium]  [shipped]
M3          Color spaces                  (3 features)  [medium]
M4          Patterns, gradients, xobjects (8 features)  [large]
M5          Page editing & coord systems  (2 features)  [medium]
M6          Accessibility + text advanced (5 features)  [large]
────────────────────────────────────────────────────────
Total                                     26 features
```

Dependencies:
- **M4 depends on M3** — patterns/shadings consume color-space types defined in M3.
- **M6 (Tagged PDF for existing PDFs) benefits from M5** — but M6 can ship tagging on newly-built docs without M5.
- All other milestones independent and reorderable.

---

## M1 — Document-level metadata (v0.8.0)

**Rationale for going first:** small surface, zero graphics complexity, high RAG/accessibility value for existing users. Unblocks outline/destination features that will be consumed by M5/M6.

**Features (5):**

| ID | Feature | Core API | Wrapper surface (draft) |
|---|---|---|---|
| DOC-014 | Set open action | `document::set_open_action(Action)` | `PdfDocument.SetOpenAction(PdfOpenAction)` with static constructors `GoTo(int page, PdfDestination? dest)`, `Uri(string)` |
| DOC-015 | Viewer preferences | `ViewerPreferences` | `PdfDocument.SetViewerPreferences(PdfViewerPreferences)` with builder: `HideToolbar`, `HideMenubar`, `FitWindow`, `PageLayout`, `PageMode`, `PrintScaling`, `Duplex` |
| DOC-017 | Named destinations | `structure::Destination` + `NameTree` | `PdfDocument.AddNamedDestination(string name, PdfDestination dest)` |
| DOC-018 | Page labels | `page_labels::PageLabel` | `PdfDocument.SetPageLabels(IReadOnlyList<PdfPageLabelRange>)` |
| DOC-020 | Save with WriterConfig | `writer::WriterConfig` | `PdfSaveOptions` (Compression, ObjectStreams, XrefStreams, PdfVersion) passed to `SaveAsync(path, options)` and `ToBytesAsync(options)` |

**Artifacts to produce in the M1 plan:**
- FFI: `native/src/document_metadata.rs` (or fold into existing `document.rs`) adding 10–12 exported functions (all open-action variants, ViewerPrefs JSON in, named-dest add, page-labels JSON in, save-with-config).
- .NET: `Native/NativeMethods.cs` extensions; new public types `PdfOpenAction`, `PdfViewerPreferences`, `PdfDestination`, `PdfPageLabelRange`, `PdfSaveOptions`.
- Tests: minimum one real-reproduction test per feature that produces a PDF, re-parses it, and asserts the metadata byte-level or via `oxidize-pdf` reader.
- `CHANGELOG.md` + version bumps (`0.7.2` → `0.8.0`).

**Ship criteria:** 5/5 features green, round-trip tests pass, 0 warnings, PR `feature/m1-document-metadata` → `develop`.

---

## M2 — Forms: write path (v0.9.0)

**Rationale:** explicitly in the original issue body as "Interactive forms — create/fill"; widely requested in the .NET space. Moderate complexity (needs appearance streams).

**Features (3):**

| ID | Feature | Core API | Wrapper surface (draft) |
|---|---|---|---|
| PAGE-008 | Add form widgets to page | `page.add_form_widget(Widget, Rect)` | `PdfPage.AddFormField(PdfFormField field, PdfRectangle bounds)` |
| FORM-007 | Create form fields | `forms::{TextField, CheckBox, RadioGroup, ComboBox, ListBox, PushButton}` | Builder-style constructors per type with validation rules, defaults, appearance overrides |
| FORM-008 | Fill form fields on existing PDF | `forms::FormData::set_field_value` | `PdfDocument.OpenAsync(path)` + `doc.FillField(name, value)` + `doc.SaveAsync(output)` |

**Artifacts:**
- FFI: `native/src/forms_write.rs` with handle-based builder for each field type + `form_fill_*` for existing docs.
- .NET: `Forms/PdfFormField.cs` (abstract) + 6 concrete types; `PdfDocument.FillField`, `PdfPage.AddFormField`.
- Tests: create each field type; fill + re-read each; AcroForm dictionary present in output.
- Version: `0.9.0`.

---

## M3 — Color spaces

**Rationale:** prerequisite for M4 (patterns/shadings need `PdfColor` to support Lab/CalRGB/ICC). Small surface, high precision-requirement use cases (print workflows, accessibility).

**Features (3):**

| ID | Feature | Core API | Wrapper surface |
|---|---|---|---|
| GFX-014 | CalRGB / CalGray | `graphics::CalibratedColorSpace` | `PdfColor.CalRgb(whitePoint, gamma, matrix, values)` |
| GFX-015 | Lab colors | `graphics::LabColor` | `PdfColor.Lab(whitePoint, l, a, b)` |
| GFX-019 | ICC color profiles | `graphics::IccColorProfile::load(bytes, channels)` | `PdfDocument.AddIccProfile(string name, byte[] icc, int channels)` + `PdfColor.Icc(string profileName, double[] values)` |

**Artifacts:**
- FFI: extend `native/src/graphics.rs` with color-space constructors returning opaque `PdfColorHandle`.
- .NET: extend `PdfColor` static factories; `PdfDocument.AddIccProfile`.
- Tests: fill a rectangle with each color space; re-parse; assert color-space object in content stream.
- Version: assigned at release time.

---

## M4 — Patterns, gradients, transparency, xobjects

**Rationale:** largest graphics block. Depends on M3 colors. High visual-fidelity value (charts, branded reports).

**Features (8):**

| ID | Feature |
|---|---|
| GFX-016 | Tiling patterns |
| GFX-017 | Axial / radial shadings (gradients) |
| GFX-018 | FormXObject / templates |
| GFX-020 | Transparency groups |
| GFX-021 | Soft masks |
| GFX-022 | Draw text from graphics context |
| GFX-023 | Draw image from graphics context |
| GFX-024 | Clip ellipse / arbitrary path |

**Artifacts:**
- FFI: new `native/src/graphics_advanced.rs` module; 15+ exports.
- .NET: `Graphics/PdfPattern.cs`, `PdfShading.cs`, `PdfFormXObject.cs`, `PdfTransparencyGroup.cs`, `PdfSoftMask.cs`; extend `PdfGraphicsContext` with `DrawText`, `DrawImage`, `ClipEllipse`, `ClipPath`.
- Tests: render each pattern/gradient; FormXObject reuse scenario; transparency + soft mask combined; visually-verifiable assertions via content-stream inspection.
- Version: assigned at release time.

**Risk flag:** this is the largest milestone — allow split into M4a (patterns + shadings + xobjects) and M4b (transparency + soft mask + gfx context helpers) if implementation cost exceeds estimate.

---

## M5 — Page editing and custom coordinate systems

**Rationale:** unlocks editing existing PDFs (massive user ask) and is prerequisite for tagging existing PDFs in M6.

**Features (2):**

| ID | Feature | Core API | Wrapper surface |
|---|---|---|---|
| PAGE-010 | Convert parsed page → editable | `Page::from_parsed(ParsedPage)` | `PdfPage.FromExisting(PdfDocument source, int pageIndex)` returning an editable `PdfPage` |
| PAGE-011 | Coordinate system transforms | `coordinate_system::CoordinateSystem` | `PdfPage.SetCoordinateSystem(PdfCoordinateOrigin origin, double unitScale)` |

**Artifacts:**
- FFI: `native/src/page_edit.rs` — roundtrip parsed page to editable, apply edits, save.
- .NET: `PdfPage.FromExisting`, `PdfCoordinateOrigin` enum (TopLeft, BottomLeft, Custom), unit scale.
- Tests: open real PDF → add overlay text → save → re-parse → verify both original content and overlay.
- Version: assigned at release time.

---

## M6 — Accessibility, semantic, text advanced

**Rationale:** lowest immediate ROI, highest complexity; defer to last. Completes PDF/UA path.

**Features (5):**

| ID | Feature | Core | Wrapper |
|---|---|---|---|
| DOC-019 | Structure tree (Tagged PDF) | `structure::tagged::StructTree` | `PdfDocument.EnableTaggedPdf(PdfStructTree tree)` |
| DOC-021 | Semantic entities | `semantic::Entity` | `PdfDocument.AddSemanticEntities(IEnumerable<PdfSemanticEntity>)` |
| PAGE-009 | Marked content | `structure::marked_content::MarkedContent` | `PdfPage.BeginMarkedContent(tag, properties)` / `.EndMarkedContent()` |
| TXT-014 | Column layout | `text::layout::ColumnLayout` | `PdfFlowLayout.WithColumns(int count, double gap)` |
| TXT-016 | Text validation | `text::validation::validate_*` | `PdfTextValidation.Validate(PdfDocument)` returning warnings |

**Artifacts:** new `Accessibility/` namespace in .NET; extend layout; add validation report type.

**Ship criteria:** PDF/UA conformance verifiable on a sample tagged doc. Version: assigned at release time.

---

## Sequencing

```
shipped     v0.8.0  M1 Document metadata
shipped     v0.9.0  M2 Forms write
next        M3      Color spaces
later       M4      Advanced graphics
later       M5      Page editing
later       M6      Accessibility + text
```

Version labels are assigned per release (at the time a milestone is cut),
not in advance — historical attempts to pre-bind milestone → version
created collisions when an unrelated MINOR shipped between milestones.
M1/M2 shown with their actual shipped versions; M3–M6 intentionally
unbound.

After M6 reaches 100% parity, the next bump is the stable-API major
(v1.0). The exact MINOR labels for M3–M6 are decided when each is
released.

---

## Decision points before writing per-milestone detailed plans

1. **Milestone order confirmation.** Proposed: M1 → M2 → M3 → M4 → M5 → M6. Any reordering (e.g. M2 before M1 if forms are customer-blocking)?
2. **M4 split decision.** Should M4 be pre-split into M4a/M4b in the roadmap, or keep single-shot with conditional split on overrun?
3. **v1.0 cutover.** After M6, bump to v1.0 or continue 0.x?
4. **Issue tracking.** Close #13 + open one issue per milestone, or leave #13 as umbrella and open sub-issues blocked by #13?

Once decisions 1–4 are made, the next step is producing `2026-MM-DD-m1-document-metadata.md` — the first executable TDD plan.

---

## Self-review

- [x] **Spec coverage:** all 26 gaps from `docs/FEATURE_PARITY.md` mapped to a milestone. FORM-007/008 explicitly scheduled (issue #13 body requirement).
- [x] **No placeholders in this roadmap** — this file is the index; detailed TDD steps deliberately deferred to per-milestone plans.
- [x] **Upstream support verified** — every feature traced to a file in `oxidize-pdf 2.5.4`.
- [x] **Dependency call-outs** — M3 → M4, M5 → M6(tagging existing PDFs).
- [x] **Versioning policy** — milestone → version is no longer pre-bound; assigned at release time. Historical entries for M1 (v0.8.0) and M2 (v0.9.0) retained as facts.
