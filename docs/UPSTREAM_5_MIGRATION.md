# Session closed — 2026-10-13

## Outcome and repository state

The requested dependency update, parity investigation and issue creation are
complete. Full Rust/.NET parity is **not** implemented: remaining work is tracked
by [#81](https://github.com/bzsanti/oxidize-pdf-dotnet/issues/81) and the 20 concrete
issues [#86–#105](https://github.com/bzsanti/oxidize-pdf-dotnet/issues?q=is%3Aissue%20is%3Aopen%20%22feat%28parity%29%22).
Every new issue's title/body and parent link was read back and verified. The
per-issue mapping is in `docs/FEATURE_PARITY.md`; publication is separate.

- Checkout: `/home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet`.
- Branch: `feature/upstream-4.0.0`; HEAD: `d7c40caf8fac5d9eacb78e80f71e5086fa803f7c`.
- This is an older dirty checkout, **not** remote develop. Inspected remote commit:
  `adb94995899bdc57f1d622431a9c6510dd055edd`.
- Current session changes: exact core pin 5.1.5 → 5.4.2 in `native/Cargo.toml`,
  local ignored lockfile/runtime rebuild, additions/corrections to README,
  CHANGELOG, FEATURE_PARITY and this existing migration handoff. Those files
  already contained earlier work; the entire diff does not belong to this session.
- The other modified/untracked source/tests/docs, `.claude` changes, deleted
  `.claude/settings.local.json`, SESSION_SUMMARY, AGENTS.md, benchmark results
  and kripteia.toml predated this work and were preserved. Ownership is not
  inferred from their untracked status. Managed/native package versions were
  not reassigned (local managed 0.16.1 / FFI 0.13.0).
- No stage, commit, branch change, push, PR or release. External issue mutations
  occurred only during the explicitly authorized issue-creation task, not closure.
- All validation and issue-creation processes started here finished. No process
  or service is intentionally left running. No benchmarks were run.

## Validation and interpretation

Reuse the detailed commands/logs below: Rust **220/220**, .NET **992/993**, old
bookmark/metadata regression families **20/20**, Clippy/fmt pass, net8/net9/net10
builds pass without warnings. Closing `git diff --check` passes; no broad suite
was repeated just for closure. Cross-platform packaging was not validated here.

The sole local .NET failure is the old CR fixture, not an unresolved remote bug.
Remote #79 already adds explicit ToUnicode mappings, retains all three expected
CR-policy results, and records a rejected mutation. This is source/documentation
evidence from remote develop, not a new local test run. NEW-001/002/004/005 and
text recovery in NEW-006 are also implemented remotely; raw stream access remains
#95. Do not file duplicate issues or patch the upstream decoder from the older
B1 diagnosis below. The historical validation remains accurate for local sources.

## Next session

1. Read #81 and the chosen child issue, then inspect current remote develop.
2. Reconcile this checkout's pre-existing changes before integrating anything.
   Prefer a separate ordinary clone if isolation is needed; the earlier handoff
   prohibits worktrees. Do not reset this checkout or copy the old integration
   wholesale over current develop.
3. Implement one agreed capability against the current source, with its complete
   options/results/errors, behavioral/boundary tests and parity contract entry.
   Acceptance is the child issue's criteria plus #81's quality/security/platform
   requirements, not an API count. Issue creation does not close implementation.
4. If resuming this local checkout specifically, first reconcile the already
   corrected CR fixture and five implemented APIs; the local suite still has
   the documented failure until that source reconciliation occurs.

Useful read-only entry points:

```bash
git status --short
gh issue view 81 --repo bzsanti/oxidize-pdf-dotnet
gh issue view 86 --repo bzsanti/oxidize-pdf-dotnet
gh api repos/bzsanti/oxidize-pdf-dotnet/commits/develop --jq .sha
```

Evidence retained: `/tmp/oxidize-parity-issues/` (20 exact bodies, manifest,
creation journal and verified remote snapshots), `/tmp/oxidize-dotnet-develop-audit/`
and its tarball (inspected source), `/tmp/oxidize-542-repro/`, the Cargo index,
source delta and `/tmp/oxidize-542-*.log` files listed below. Network and MSBuild
may need sandbox escalation; no credentials or private document data were added.

## Closing cleanup

Start: `2026-10-13T19:46:28.645434+00:00`.
Five-day cutoff: `2026-10-08T19:46:28.645434+00:00`.
Read-only inventory with resolved paths, mtimes, counts and sizes:
`/tmp/oxidize-close-20261013-inventory.json`.

Inspected only the three session-owned directories above plus these exact files:
`/tmp/oxidize-upstream-index.jsonl`, `/tmp/oxidize-515-542.diff`,
`/tmp/oxidize-dotnet-develop-audit.tar.gz`,
`/tmp/oxidize-dotnet-issues-before.json`, `/tmp/oxidize-rust-issues-before.json`,
`/tmp/oxidize-dotnet-issues-precreate.json`, and the seven build/test/lint/repro logs
with prefix `/tmp/oxidize-542-` recorded under Validation below.
All **429 inventoried files (14,721,053 bytes)** were newer than the cutoff and
preserved. Temporary files deleted: **0**; temporary bytes reclaimed: **0**.
No uncertain candidate was selected for removal, and no broad shared-cache or
`/tmp` scan/purge was performed. Unrelated older artifacts were not inspected.
New closure inventory/hash/sweep logs are also retained and are newer than cutoff.

Cargo workspace resolved to this repository's `native/Cargo.toml`.
Installed tool: `cargo-sweep-sweep 0.8.0`. Both commands succeeded:

```bash
cargo sweep --dry-run --time 5 /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native
cargo sweep --time 5 /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native
```

Preview and actual cleanup were confined to `native/target`, each reporting
**1.79 GiB**. Cargo-sweep does not report a file/directory deletion count here;
no count was inferred. Logs: `/tmp/oxidize-close-20261013-sweep-preview.log` and
`/tmp/oxidize-close-20261013-sweep.log`. Cleanup failures: **none**.
A before/after SHA-256 check confirmed every tracked file unchanged by cleanup;
all inventoried evidence still exists. The handoff was then updated and checked.

---

# Remote-state reconciliation — 2026-10-13

Subsequent issue triage inspected remote develop at `adb94995899bdc57f1d622431a9c6510dd055edd`.
The local B1 evidence below is real for this checkout, but **is not an outstanding
remote release blocker**: #79 already corrected the fixture with explicit ToUnicode
while retaining all three policy expectations and a discriminating negative control.
See remote `docs/UPSTREAM-5.4.2.md`. Five of the eight new API gaps are also already
implemented there. The checkout was preserved; no synchronization was performed.

The remaining source-confirmed gaps now have 20 implementation issues (#86–#105),
indexed in [FEATURE_PARITY.md](FEATURE_PARITY.md) and umbrella
[#81](https://github.com/bzsanti/oxidize-pdf-dotnet/issues/81). No duplicate upstream
CR issue was created. Historical local validation below retains its original scope.

# Upstream update — 2026-10-13

Scope: adopt the latest official Rust release and audit .NET parity; preserve
existing local work. No upstream patches or new feature implementation.
The [public Cargo index](https://index.crates.io/ox/id/oxidize-pdf) confirms 5.4.2
(published 2026-10-11), not yanked. Published checksum:
`eb85baf2cf24b9ba56fface490e5d04bb9827ed33ce21e9f8f5e028d6ee34713`.

| Requirement | Evidence / state | Blocker / next action |
|---|---|---|
| Latest official dependency | Demonstrated: exact `=5.4.2`, offline Cargo resolution/build passes; Linux runtime rebuilt | None; local Cargo.lock is ignored by existing repository policy |
| Existing behavior retained | Not fully met: Rust 220/220; .NET 992/993 | B1: WinAnsi CR/LF compatibility below |
| Previous #633/#634 failures | Demonstrated resolved: unchanged BookmarkReadingTests + ExistingDocumentOperationsTests, 20/20 | Closed on official 5.4.2 |
| Current Rust parity analysis | Demonstrated: current managed/FFI source compared with published Rust 5.1.5–5.4.2 delta | Eight new/expanded API gaps and broader backlog in FEATURE_PARITY.md; implementation outside this request |
| Build/lint | Demonstrated: Clippy all targets with warnings denied, fmt, net8/net9/net10 builds | None |

## B1 — existing carriage-return contract

`Upstream460Tests.ExtractionOptions_ApplyReadingOrderAndAllCarriageReturnPolicies`
fails at line 114. The PDF fixture uses Helvetica/WinAnsi with
`(A\015B\015\012C) Tj`. Remove policy expected `AB\nC`; actual text is
`A\uFFFDB\uFFFD\uFFFDC`. The wrapper still passes all three CR policies to the
Rust `TextExtractor`. The new font resolution path treats undefined font codes
as replacement characters before text sanitization; postprocessing replacement
characters in .NET cannot safely recover which were CR versus LF or other codes.

Independently reproduced without FFI/.NET in `/tmp/oxidize-542-repro`, against
the official 5.4.2 crate: Remove, ReplaceWithSpace and NormalizeLineEnding all
return `A\uFFFDB\uFFFD\uFFFDC`. Source and PDF fixture are retained there;
`/tmp/oxidize-542-repro.log` records the run. This confirms the behavior is in
the upstream extraction path rather than managed serialization or the ABI.

This is a compatibility failure against the existing wrapper contract, not yet
an adjudication that the new PDF encoding behavior violates the specification.
Keep the test unchanged and enabled. Next action: reconcile the upstream decoding
contract with the existing CR policy, obtain an official compatible fix (or an
explicitly approved contract change), then rerun this test and full regressions.
No local upstream patch, lossy replacement workaround, issue submission or release
was performed. Publication remains blocked by B1; updating the dependency and
analyzing parity does not establish release acceptance.

## Validation on current sources

- `cargo build --offline --manifest-path native/Cargo.toml`: passed;
  `/tmp/oxidize-542-build.log`. Debug Linux binary copied into the ignored runtime
  asset used by the managed tests.
- `cargo test --offline --manifest-path native/Cargo.toml`: **220 passed**;
  `/tmp/oxidize-542-rust-tests.log`.
- `cargo clippy --offline --manifest-path native/Cargo.toml --all-targets -- -D warnings`:
  passed; `/tmp/oxidize-542-clippy.log`. `cargo fmt --check`: passed.
- Full .NET suite (`--no-restore`): **993 total, 992 passed, 1 failed, 0 skipped**;
  `/tmp/oxidize-542-dotnet-tests.log`. The sole failure is B1.
- Unchanged previous regression families (`--no-build --no-restore --filter
  'FullyQualifiedName~BookmarkReadingTests|FullyQualifiedName~ExistingDocumentOperationsTests'`):
  **20 passed**; `/tmp/oxidize-542-prior-regressions.log`.
- Managed build (`--no-restore`): **net8/net9/net10 pass, zero warnings/errors**;
  `/tmp/oxidize-542-frameworks.log`.
- `git diff --check`: passed. Cross-platform native packaging was not run.

Review scope: dependency adoption and documentation, retaining all existing tests
and ABI sources. Regression evidence covers existing FFI behavior, validation,
malformed-input and policy tests; it is not an exhaustive security audit of the
new core. No new exports, unsafe blocks or optional features were added. The
upstream public-key recipient API remains unsupported/fail-closed, not a usable
cryptographic capability. Prior user changes and package versions were preserved.
The checkout was not reset to published main, committed, or published.

The historical release/handoff below remains provenance, not current validation.

# Session handoff — 2026-09-27

**Closed after successful 0.18.0 publication.** The user explicitly accepted
upstream #633/#634 for this release and requested a subsequent release once
fixed. Do not reintroduce local upstream patches or weaken/skip regression tests.
No build, test, or publication process is intentionally left running.

## Local state and ownership

- Original checkout: `/home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet`.
  Branch `feature/upstream-4.0.0`, HEAD
  `d7c40caf8fac5d9eacb78e80f71e5086fa803f7c`. Still dirty: it contains the
  integration source and documentation, but its version metadata remains the
  pre-release local state (managed 0.16.1 / FFI 0.13.0).
- Do not mistake those local versions for the published artifact. Release
  metadata was prepared in a separate clone, not by resetting this checkout.
- Clean release clone: `/tmp/oxidize-dotnet-release-018`, branch
  `release/0.18.0`, commit `a1ed6a61a0adc1dcb50a5518a0859ff158f27944`.
  Its PR #78 was merged as `f2c42a2411db287732374b127677d57bde698cc8`, tagged
  `v0.18.0`. Published managed version 0.18.0 / native FFI 0.14.0 / core 5.1.5.
- Pre-existing/user-owned changes preserved: `.claude/commands/start-session.md`,
  deleted `.claude/settings.local.json`, `SESSION_SUMMARY.md`,
  `docs/superpowers/specs/benchmark-results/`, and `kripteia.toml`.
  None were included in the release. The older SESSION_SUMMARY was not overwritten;
  this migration document is the authoritative handoff for the current session.
- Closing did not stage, commit, push, switch branches or update external systems.
  No new benchmarks were run; existing benchmark files are not release evidence.

## Validation evidence

- `cargo test --offline --manifest-path native/Cargo.toml`: 220 passed.
  `/tmp/oxidize-515-full-rust.log`.
- `dotnet test dotnet/OxidizePdf.NET.Tests/OxidizePdf.NET.Tests.csproj --no-restore`:
  993 total, 989 passed, 4 failed. `/tmp/oxidize-515-full-dotnet.log`.
- `dotnet build dotnet/OxidizePdf.NET/OxidizePdf.NET.csproj --no-restore`:
  net8/net9/net10 succeeded, zero warnings/errors.
  `/tmp/oxidize-515-framework-build.log`.
- Release Linux binary targeted tests: 103 total, 99 passed, the same 4 failed,
  zero skipped. `/tmp/oxidize-018-release-smoke.log`.
- The four failures are two mixed nested/sibling bookmark round trips (#633),
  and reconstructive extraction/split metadata-discard cases (#634).
- Release workflow 36340623272 succeeded on all eight native RIDs and both
  publication jobs. NuGet index and downloaded package were independently
  checked; package contents and SHA-256 are recorded below.
- Closing validation: `git diff --check` passed; no broad suite rerun.

## Next session

Start from published `main`/`v0.18.0`, preserving the original checkout's personal
changes before any local synchronization. No worktrees: they are prohibited.
Check whether #633/#634 have an official published fix; adopt that version
without vendor patches, then run:

```bash
gh issue view 633 --repo bzsanti/oxidizePdf
gh issue view 634 --repo bzsanti/oxidizePdf
cargo build --manifest-path native/Cargo.toml
cp native/target/debug/liboxidize_pdf_ffi.so dotnet/OxidizePdf.NET/runtimes/linux-x64/native/liboxidize_pdf_ffi.so
dotnet test dotnet/OxidizePdf.NET.Tests/OxidizePdf.NET.Tests.csproj --filter 'FullyQualifiedName~BookmarkReadingTests|FullyQualifiedName~ExistingDocumentOperationsTests'
```

Acceptance for the follow-up: all four previously failing tests pass unchanged
against the official dependency, then full Rust/.NET regression and supported
framework builds pass. Prepare the next release with notes identifying both fixes.
In this environment MSBuild requires execution outside the sandbox for local
sockets; network commands may also need escalation.

## Closing cleanup

Inventory started: `2026-09-27T19:47:14.083902+00:00`. Five-day cutoff: `2026-09-22T19:47:14.083902+00:00`.

| Resolved project-owned location | Files preserved | Bytes | Older than cutoff |
|---|---:|---:|---:|
| `/tmp/oxidize-018-linux-artifact` | 1 | 11231888 | 0 |
| `/tmp/oxidize-018-published` | 1 | 38578398 | 0 |
| `/tmp/oxidize-515-issue-repro` | 4 | 48540 | 0 |
| `/tmp/oxidize-withdrawn-patches-c8xcs33f` | 998 | 19804893 | 992 |

All inventoried files were preserved as validation/reproduction or withdrawn-patch
history referenced by this handoff. The old timestamps in the withdrawn vendor
backup originate from packaged source files; age alone is not deletion authority.
This backup is not a build dependency and is not in the release. No shared cache,
repository root, or broad `/tmp` scan/purge was performed. The release clone was
preserved as a repository. Other ownership-uncertain items were not removed.
Detailed mtime inventory: `/tmp/oxidize-close-018-inventory.json`.

Cargo workspace resolved by `cargo locate-project --workspace --manifest-path
native/Cargo.toml` to this repository's `native/Cargo.toml`. Installed tool:
`cargo-sweep-sweep 0.8.0`. Commands:

```bash
cargo sweep --dry-run --time 5 /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native
cargo sweep --time 5 /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native
```

Both succeeded, confined to `native/target`, reporting nothing to clean.
**Deleted: 0 files / 0 directories. Reclaimed: 0 bytes. Cleanup failures: none.**
All referenced evidence and tracked content were retained.

---

# Published release — 0.18.0

On 2026-09-27 the user explicitly authorized publishing the current integration
with upstream issues #633 and #634 accepted until a later fix release.
This supersedes the historical blocking conclusion below.

- PR #78 merged: https://github.com/bzsanti/oxidize-pdf-dotnet/pull/78
- Release commit: f2c42a2411db287732374b127677d57bde698cc8
- Tag/release: https://github.com/bzsanti/oxidize-pdf-dotnet/releases/tag/v0.18.0
- NuGet: https://www.nuget.org/packages/OxidizePdf.NET/0.18.0
- Release workflow 36340623272: all eight native builds, NuGet publication,
  and GitHub release succeeded.
- NuGet flat-container index confirms 0.18.0 available.
- Downloaded package verified: eight native RIDs, net8.0/net9.0/net10.0 assemblies,
  version 0.18.0, FFI 0.14.0, and all sixteen checked new native exports.
- Linux release binary smoke tests: 99 passed, only the four accepted upstream
  regressions failed; no skipped tests. Package binary matches the tested artifact.
- Package SHA-256: f87b9b49e7f0d586020b5b056e0a83f61a6e1b5f2321724387c4beec68c1b930
- Release prepared in separate clone `/tmp/oxidize-dotnet-release-018`; original
  checkout, branch and unrelated user-owned edits were preserved. No worktrees.
- No patches to the official upstream source were used.

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
