# Release decision — 0.18.0

The user explicitly authorized releasing the current state on 2026-09-27,
accepting upstream issues #633 and #634 until a subsequent official fix release.
This supersedes the historical release-blocking conclusion below. The four
regression tests remain enabled and failing on official 5.1.5. Release package
version is 0.18.0 and native FFI version is 0.14.0.

# Upstream 5 migration

Active work, 2026-09-27. Starting core: 4.6.0. Target: official 5.1.5.
Confirmed published and non-yanked in the public Cargo index:
https://index.crates.io/ox/id/oxidize-pdf
Published SHA-256:
`d4082ffd3731febbea1803d2f46b0e03ae0ebc5e4b8ff2c1ded7160cb3f9bac7`.

The manifest now pins registry `=5.1.5`, with no `[patch.crates-io]`.
The user objected to local upstream patches; do not modify upstream code or
reinstate a patched dependency without explicit permission. Both local patches
and the vendored copy have been withdrawn from the project. Historical
validation below against patched 5.1.4 does not establish correctness on 5.1.5.

## Upstream reports

Both failures were also reproduced in a standalone Rust binary against the
unmodified published 5.1.5 crate, independently of the .NET wrapper. Issues
opened at the user's explicit request:

- Outline links: https://github.com/bzsanti/oxidizePdf/issues/633
- Metadata policy: https://github.com/bzsanti/oxidizePdf/issues/634

Each issue includes the Rust reproduction and source-level diagnosis.

## Official 5.1.5 validation — current status

- `cargo build --offline` succeeded using the registry dependency.
- Targeted .NET bookmark/structural run: 16 passed, 4 failed.
- Two mixed nested/sibling bookmark writer round trips fail because the
  resulting outline contains a cycle or duplicate item.
- Reconstructive extract and split retain the title while reporting metadata
  discarded. Merge passes the same metadata-policy test.
- These tests remain enabled and unchanged; the migration is incomplete.
- Runtime source comparison between published 5.1.4 and 5.1.5 finds only
  `src/signatures/certificate.rs` changed: certificate verification uses
  `oxidize_webpki::ALL_VERIFICATION_ALGS`. The two defective files are unchanged.
- Logs: `/tmp/oxidize-official-515-build.log` and
  `/tmp/oxidize-official-515-regressions.log`.
- Withdrawn patch backup (not a build input): `/tmp/oxidize-withdrawn-patches-c8xcs33f`.

## Acceptance inventory

- [x] Pin Cargo dependency and resolve lockfile to official 5.1.5.
- [x] Native `cargo build --offline` passes with official 5.1.5.
- [x] Verify the latest published upstream version externally.
- [x] Expose link-annotation URI extraction and unreliable figure-text opt-in.
      Three behavioral tests pass (URI action opt-in and figure text with/without
      layout). URI schemes are not filtered; actions are never executed.
      Preserved the old FFI struct and export; a v2 export takes the new flags.
- [x] Expose extracted fragment rendering mode (including invisible OCR text).
      `PdfExtractor.ExtractTextFragmentsAsync` returns positioned runs, font
      metadata, structure tag, MCID, mode and truncation state for a one-based
      page number. Tests cover all eight modes, byte limits and missing pages.
- [x] Incremental highlight annotation enumeration and mutations.
      `GetHighlightsAsync` and `EditHighlightsAsync` expose geometry, RGB,
      opacity, contents and stable IDs. The actual 5.1.4 API offers Add/Remove
      only; replacement is an atomic Remove/Add batch with a new ID.
      Prefix preservation, appearance round trips, replacement, invalid
      geometry/pages and encrypted-document rejection are covered by tests.
      Upstream's highlight editor does not enforce DocMDP; this is documented
      explicitly rather than claiming certification-policy enforcement.
- [x] Incremental FreeText annotation enumeration and mutations.
      `GetFreeTextAnnotationsAsync` / `EditFreeTextAnnotationsAsync` expose
      rectangle, Unicode contents, default appearance and alignment. Update
      preserves object identity. Nine tests cover the lifecycle, all alignments,
      unrelated Highlight retention, invalid fields/conflicts, encryption and
      DocMDP P=1/P=2 rejection versus P=3 allowance.
- [x] Incremental Ink annotation enumeration and mutations.
      `GetInkAnnotationsAsync` / `EditInkAnnotationsAsync` expose ordered stroke
      coordinates, Gray/RGB/CMYK color, width, opacity and stable identity.
      Add/Update/Remove are atomic; Update preserves unrelated dictionary keys.
      Eight tests cover all color spaces, prefix and identity preservation,
      conflicts, geometry limits, unrelated FreeText retention, encryption,
      and all three DocMDP permission levels.
- [x] Incremental geometric annotation enumeration and mutations.
      `GetGeometricAnnotationsAsync` / `EditGeometricAnnotationsAsync` expose
      Line, Square, Circle, Polygon and PolyLine, all ten line endings,
      Gray/RGB/CMYK stroke/fill, width, dashes and opacity. Twenty tests cover
      every shape and ending, lifecycle, identity and prefix preservation,
      unrelated Ink retention, invalid styles/geometry/conflicts, encryption
      and all DocMDP permission levels.
- [x] Policy-driven structural operations: planning and reports for merge,
      extract and split; exact-permutation reorder; atomic Move/Rotate/Delete/
      Duplicate/Insert batches. `PdfExistingDocumentOperations` requires an
      explicit preservation/reconstruction policy for semantic operations and
      returns per-input roles, selections, structure dispositions and object
      mutation reports. Input bytes are staged in private RAII temporary
      directories; reports expose stable input indexes instead of paths.
      Both optional policies (first-input metadata and secondary page labels)
      are covered. Original legacy wrapper methods remain available.
- [x] Incremental signing preparation/finalization and custom visible
      appearances, including images and fitting validation.
- [x] Bounded semantic comparison and revision attribution.
- [x] Tagged-PDF inspection, validation and incremental editing.
- [x] Outline/bookmark reading: all eight destination modes, named GoTo
      resolution, styles, bounds and preorder parent indexes. The mixed-tree
      writer regression remains unresolved on official 5.1.5.
- [x] Incremental OCR layers and dry-run policy support.
- [x] Security-grade redaction and explicit masking-risk reports.
- [x] Regression validation for upgraded extraction and signature verification.
- [ ] Full native and .NET tests, lint, docs and version metadata review.

Existing user-owned edits in `.claude`, `SESSION_SUMMARY.md`, benchmark results,
and `kripteia.toml` are outside this migration and must be preserved.

## Historical validation against locally patched 5.1.4

- Native: 219 tests passed against 5.1.4; Clippy with `-D warnings` passed.
- .NET targeted: 12 tests passed (Upstream514, Upstream460, ABI layout).
- Full .NET suite before fragment API addition: 893 passed, 0 failed.
- Fragment API targeted suite: 6 passed, including ActualText substitution
  and graphics-state restoration of rendering mode. Clippy remains clean.
- Debug native library rebuilt and copied to the ignored Linux runtime asset.
- `native/Cargo.lock` is ignored by repository policy; it was resolved locally,
  while the tracked manifest has an exact `=5.1.4` pin.
- MSBuild needs local sockets and therefore requires elevated execution in this
  environment. A plain sandbox test command can exit with no output; verbose
  output showed `SocketException (13): Permission denied`.
- crates.io API returned 403 and docs.rs browser response was stale. The public
  Cargo index succeeded and confirmed 5.1.4, including the matching checksum.

## Resolved upstream defect: outline writer

The upstream writer reserved IDs in preorder but selected sibling links by
sibling count rather than subtree size. This produced duplicate links when a
parent with children was followed by a sibling. Both root and child lists are
corrected in `native/vendor/oxidize-pdf/src/writer/pdf_writer/mod.rs`.

The published 5.1.4 package is vendored through `[patch.crates-io]`; the global
Cargo cache was not modified. `native/vendor/README.md` records provenance,
the published checksum and removal conditions. The exact source change is in
`native/patches/oxidize-pdf-5.1.4-outline-links.patch`. A later metadata-policy fix also changes `operations/semantic_preservation.rs`;
a byte comparison confirms these are the only two changed runtime source files.
The MIT license from the upstream repository is included.

Validation after patch: 11 targeted .NET tests passed, including the original
failing round trip, a deeper mixed-sibling tree, independently assembled trees,
all view modes, extraction modes, ActualText and byte limits. Cargo build,
Clippy for the wrapper and cargo fmt --check pass. Cargo now exposes 16 existing
upstream AES generic-array deprecation warnings because this is a local patch
rather than a registry dependency; they are not suppressed.
Native unit suite after patch: 219 passed, 0 failed. Log:
`/tmp/oxidize-native-514-tests.log`. No test process remains running.

## Highlight API validation

Four .NET HighlightEditingTests passed. Native build/check and wrapper Clippy
passed; the previously recorded upstream AES deprecation warnings remain.
Native module: `native/src/highlights.rs`; managed API:
`PdfOperations.Highlights.cs`, `PdfHighlight.cs`. No test process is running.
All new incremental annotation groups and structural operations are exposed. Next: incremental signing.

## FreeText API validation — 2026-09-27

Nine FreeTextEditingTests pass. The preceding combined FreeText/Highlight run
passed all 10 tests before the three DocMDP cases were added. Wrapper Clippy
passes, with the same 16 dependency deprecation warnings already recorded.
The policy fixture is copied unchanged from the published upstream package;
tests explicitly label modified P variants as synthetic policy tests, not
signature-validity tests. No test process remains running.

## Ink API validation — 2026-09-27

Eight InkEditingTests pass; native build and wrapper Clippy pass. The same
upstream dependency deprecation warnings remain. Native `ink.rs` and managed
`PdfOperations.Ink.cs` validate 1..4096 strokes, finite coordinate pairs and at
most one million points before invoking the upstream editor. Color arity
preserves the original DeviceGray/RGB/CMYK space rather than converting it.
There is no public Ink metadata mutation API in 5.1.4; unrelated metadata keys
are preserved by upstream Update. No test process remains running.

## Geometric annotation validation — 2026-09-27

Twenty GeometricEditingTests pass. Native build and wrapper Clippy pass;
existing dependency deprecation warnings remain. Geometry DTOs reject fields
that do not belong to their shape, preserve every upstream style option and
use strict native shape/color/width/dash/opacity validation. The managed model
uses named geometry fields without relying on JSON discriminator ordering,
which keeps deserialization compatible with all supported .NET targets.
No test process remains running.

## Structural operations validation — 2026-09-27

Fifteen ExistingDocumentOperationsTests plus five BookmarkReadingTests pass.
These compare complete plan/execution reports, inspect output page content and
counts, test every page mutation, exact/identity permutations, prefix and
bookmark preservation, explicit semantic loss, metadata and page-label policy,
encryption and all DocMDP levels. Native suite: 220 passed, including a parser
check that rotating page 1 leaves page 0 unchanged.

A second local upstream patch corrects reconstructive extraction/split:
`native/patches/oxidize-pdf-5.1.4-metadata-policy.patch`. Previously the legacy
extractor retained metadata regardless of the selected policy, contradicting
the semantic report. The patch passes the chosen metadata disposition into
PageExtractionOptions. The failure was reproduced before the fix and both
metadata policies now pass for merge, extract and split.

Wrapper Clippy passed (`/tmp/oxidize-existing-clippy.log`), with the existing
dependency deprecation warnings. No build or test process remains running.

## Incremental signing — official 5.1.5

Managed `PdfSigning` and disposable `PdfPreparedSignature` now expose the
native preparation, completion, and appearance-layout APIs. SafeHandle pins
session lifetime during completion; private keys remain outside the wrapper.
Options include existing/new fields, certification, field locks, custom typed
PDF dictionary values, and visible text/RGB watermark artwork.

Ten IncrementalSigningTests pass, including real RSA detached CMS creation and
independent .NET cryptographic verification of the container extracted from
the final PDF. Other cases cover exact covered ranges, prior-byte preservation,
existing-field occupancy, DocMDP levels, FieldMDP All, typed entries, malformed
and oversized containers, retries, disposal, encryption, and appearance fitting.
Synthetic DER fixtures in policy tests are explicitly not cryptographic tests.
See `docs/INCREMENTAL_SIGNING.md` for the provider workflow and limitations.
Test-only dependency: System.Security.Cryptography.Pkcs 10.0.11.
Log: `/tmp/oxidize-signing-tests.log`.

## Semantic comparison — official 5.1.5

`PdfSemanticComparison.CompareAsync` exposes all upstream limits, difference
classes/paths, chronological revision fingerprints and object changes,
unreachable object identities, and equivalent-object pairs. Six targeted tests
pass: identical inputs, text/metadata changes, annotation revision attribution,
serialization-only changes, encrypted/malformed rejection, and resource bounds
for objects, depth, canonical data, decoded streams, text, and revision count.
Native build, wrapper Clippy with `-D warnings`, and diff whitespace checks pass.
Logs: `/tmp/oxidize-compare-tests.log`, `/tmp/oxidize-compare-clippy.log`.
Usage and interpretation: `docs/SEMANTIC_COMPARISON.md`.

## Existing tagged PDFs — official 5.1.5

`PdfTaggedOperations` provides bounded inspection, dry-run planning, and all
five incremental mutations. Reports preserve dictionaries as typed PDF values,
role/class maps, artifacts, MCIDs, ParentTree entries, stable findings, changed
objects and normalized mutations, plus validation before/after edits.

Nine TaggedEditingTests pass, covering all mutation types, exact input-prefix
preservation, plan/execution agreement, Unicode attributes/removal, cycles,
missing MCIDs, ParentTree restoration, encrypted inputs, limits, and all three
DocMDP policies for both planning and editing. A combined run also passed all
ten signing tests after sharing the typed PDF value converter (`PdfObjectValue`).
Native build, Clippy with `-D warnings`, fmt and diff checks pass.
Logs: `/tmp/oxidize-tagged-policy-tests.log`, `/tmp/oxidize-tagged-tests.log`,
`/tmp/oxidize-tagged-clippy.log`. Usage: `docs/TAGGED_PDF_EDITING.md`.

## Incremental OCR — official 5.1.5

`PdfOcrLayer.PlanAsync` and `ApplyAsync` expose all OcrLayerPage/Fragment inputs
and plan fields. Recognition remains caller-provided; coordinates are PDF user
space. No implicit confidence filtering or image-coordinate conversion occurs.
Seven OcrLayerTests pass: Unicode extraction and invisible render mode, retained
original content/input prefix, plan/execution equality, duplicate-layer skip,
empty-page no-op, invalid requests/encryption, and all DocMDP permission levels
for planning and execution. Native build and wrapper Clippy pass.
Log: `/tmp/oxidize-ocr-tests.log`. Usage: `docs/INCREMENTAL_OCR.md`.

## Redaction — official 5.1.5

`PdfRedaction.RemoveIrreversiblyAsync` and `MaskVisuallyAsync` expose distinct
upstream semantics, entity filtering, visual placeholders, and the complete
risk report. Secure removal never falls back to masking and documents its
restricted ASCII whole-operand support and reconstructive behavior.
Four RedactionTests pass: target removal with unrelated text retained and
metadata dropped; visual-mask recoverability and placeholder; unsupported,
ambiguous, partial and non-ASCII targets rejected; empty selection semantics.
Native build, wrapper Clippy, format and whitespace checks pass.
Log: `/tmp/oxidize-redaction-tests.log`. Usage: `docs/REDACTION.md`.
All implementation inventory blocks are now exposed. Full regression, supported
framework builds, documentation/version audit, and upstream defects remain.

## Global validation — 2026-09-27

- Full native suite: **220 passed, 0 failed** against registry 5.1.5.
- Full .NET suite: **993 total, 989 passed, 4 failed**, including corpus groups.
  The only failures are the two bookmark writer round trips (#633), and
  reconstructive extract/split metadata discard (#634). Tests remain enabled.
- Managed library builds for **net8.0, net9.0, net10.0**: zero warnings/errors.
- Latest wrapper Clippy `--no-deps -- -D warnings`, cargo fmt and diff checks pass.
- Validation used the Linux x64 debug native runtime; other platform binaries
  have not been rebuilt or validated locally and no release has been published.
- README links all new feature guides. CHANGELOG describes the integration as
  unreleased; FEATURE_PARITY separates the validated 5.x delta from its stale
  historical table. Structural-operation policy documentation was added.
- Version review: registry core is exactly 5.1.5. Existing package version
  0.16.1 and FFI crate version 0.13.0 were left unchanged; assigning a published
  release version is not part of this uncommitted integration. Existing exports
  and the 72-byte extraction-options ABI remain compatible.
- GitHub issues #633 and #634 were rechecked and remain OPEN.

Logs: `/tmp/oxidize-515-full-rust.log`, `/tmp/oxidize-515-full-dotnet.log`,
`/tmp/oxidize-515-framework-build.log`. All these test/build processes completed.

The goal is **not complete**: the official dependency still fails four required
regressions. No patches, test skips, or weakened assertions were introduced to
hide these failures. Resolve via an official upstream fix, or an explicitly
user-approved alternative, before claiming the migration verified.
