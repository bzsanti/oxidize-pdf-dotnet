# Release integration — 0.20.0, 2026-10-19

User authorized integration and publication after validation. PR #109 merged into
develop as 8cb68853a7ab54ebc777bdd8e02a55bdeeb84eea. Its exact source head
5de06f4568ae529d196fdddc778ce0581a7bf182 passed all 15 PR CI jobs in
[run 39310679115](https://github.com/bzsanti/oxidize-pdf-dotnet/actions/runs/39310679115):
eight native builds, four managed platforms, lint/native regression tests, security
and the complete NuGet gate. PackageReference consumers passed on .NET 8/9/10.
The initial package checker incorrectly assumed a fixed nuspec XML namespace;
corrected parsing passes the complete package and rejects a wrong-version control.

The earlier validation ledger below records the state before those checks. Its
pending-CI statements are superseded by this evidence. Release integration changes
only documentation; the release PR to main must also pass CI before tagging.
Core #794/#795/#797 remain explicitly excluded from completed-capability claims.

# Release candidate validation — 0.20.0, 2026-10-19

User authorized continuation through release validation, not publication.
Scope: implemented capabilities and previously integrated changes since 0.19.0;
#89/#92/#98/#104 and unsupported batch operations retain the blockers below.

| Requirement | Evidence / state | Remaining gate |
| --- | --- | --- |
| Source/review | Existing real-FFI, mutation, Rust/C# security/test analysis reused for unchanged implementation; package source mapping and artifact paths reviewed | CI on exact commit |
| Candidate metadata | Managed version 0.20.0 and changelog; Release solution: 1228 library + 15 Kernel Memory tests pass | Platform/package validation |
| Platform portability | Eight native targets, four managed test platforms configured | PR CI on exact candidate commit |
| Package consumption | Local NuGet consumer passes on .NET 8/10; local incomplete RID package correctly rejected; source mapping pins OxidizePdf.NET to candidate feed | CI must verify all eight fresh native assets, three TFMs and .NET 9 runtime |
| Known limitations | Core #794/#795/#797 explicitly documented; batch fail-before-work tested | These operations excluded from completion claims; no weakened acceptance |
| Publication | Not authorized by this validation request | No release tag, NuGet push or release workflow dispatch |

Local evidence: /tmp/oxidize-020-tests.log, /tmp/oxidize-020-consumer.log,
/tmp/oxidize-020-layout-local.log, /tmp/oxidize-020-format.log. Packaging smoke has
no xUnit tests (Kripteia reports zero test files); its executable exit status is the
behavioral evidence. Security scan completed with no findings; manual review covers
NuGet source selection and package layout. No source or package publishing credentials
are used by the new CI validation job. Historical results below remain historical.

# Implementation scope — all unblocked issues, 2026-10-19

User selected all released-fix candidates (#91/#92/#93/#98/#100/#101).
This section supersedes the historical status and next actions below. Core remains
exactly 5.5.0. Four binding surfaces are implemented locally; full issue acceptance
and platform CI remain open. No replacement core or arbitrary JavaScript execution.

| Requirement / source | Current evidence | Blocker / next action |
| --- | --- | --- |
| #91 XMP | Full supported model, parse/serialize/identity RDF/validation, catalog read and incremental write; real-FFI compressed metadata, Unicode, malformed input, prefix/Info preservation and DocMDP rejection pass | Platform CI; limited core model does not retain unknown properties or synchronize Info; validation only covers supported identity checks |
| #93 calculations | Both engines, all definition variants, values/events/settings/formats/order export; typed ordered operations and errors pass real FFI | Platform CI; JavaScript definition retains core placeholder behavior, no script execution; exported calculation order is not claimed topological |
| #100 lazy/streaming | Seekable managed Stream callbacks, real pages and typed objects, effective options/cache stats, SafeHandle lifetime and disposal; partial reads, nonzero initial position, cancellation and error recovery tested | Platform CI; bounded caches are not a process memory cap; cancellation checkpoints do not interrupt CPU-only core work |
| #101 batch | Core worker pool, stable item indices/results, progress, custom callbacks, merge/extract/split file readback, concurrency/stop/timeout/error contracts | Core [#797](https://github.com/bzsanti/oxidizePdf/issues/797): Rotate/Compress merely copy input; binding rejects the entire request before work. Full operation acceptance needs upstream correction. Nonzero memory cap rejected; Rayon/performance engines unavailable |
| #92 incremental forms | ReadOnly regression passes; new certification counterexample retained | Core [#794](https://github.com/bzsanti/oxidizePdf/issues/794): filler accepts DocMDP P=1. Same fixture is rejected by core IncrementalObjectEditor with PermissionDenied. Need public policy enforcement in filler before remaining field/appearance acceptance |
| #98 annotation creation | Earlier PDF payload probes pass | Core [#795](https://github.com/bzsanti/oxidizePdf/issues/795): public PolylineAnnotation.with_endings requires private polygon::LineEndingStyle, incompatible with exported LineEndingStyle. Need core public type/export fix before complete options binding |
| #89 / #104 | Prior independent investigations retained below | Outside released-fix implementation set; no inferred fix or closure |

## Contracts and review

Calculation requests are bounded to 256 ordered steps/1 MiB, with finite inputs,
expression/depth/precision limits and bounded outputs. XMP decoding is limited to
1 MiB; writing uses the public incremental editor and preserves Info unchanged.
Lazy/streaming borrow readable seekable streams exclusively, start at offset zero,
and retain callbacks until native disposal. Default leaveOpen=true; explicit
ownership disposes the input. Native calls are serialized and reentrancy rejected.
Managed callback exceptions cannot unwind through Rust. Failed streaming reads
fault the session rather than silently advancing. Typed object traversal is bounded.
Batch callbacks may run on native threads; all workers join before return. Timeouts
are completion budgets, not interruption or rollback; cancellation is pre-start.
Callers own output-path collision policy. Unsupported batch operations fail before
any callback or file operation, instead of reporting a successful copy as rotation.

Manual review corrected swallowed managed I/O exceptions during lenient native
initialization and initial-position handling. An ownership mutation in a disposable
copy reverses leaveOpen behavior: the real-FFI disposal test fails at its ownership
assertion; production passes. Initial mutant build fixture omissions were corrected
and are not counted as mutation evidence. Test/security Kripteia scans ran for both
languages (Rust 97, C# 95); C# has 43 degraded syntax trees, and Rust enumerates
unsafe FFI sites. These tools do not certify the upstream core.

Validation: native 252 units + 22 passing probes, one ignored known DocMDP failure;
explicit ignored-probe execution reproduces the failure with passing controls.
Full managed run: 1,227 pass/one failure (nonzero initial stream position). After
fixing native initialization, the affected integrated suite passes all 39 cases;
unchanged full-suite results are reused. Clippy all targets/features denies warnings,
format and ABI/parity gates pass. Batch guard/split verification is recorded in
/tmp/unblocked-batch-final.log. Platform CI is still required before integration.

Evidence: /tmp/unblocked-native.log, /tmp/unblocked-managed-full.log,
/tmp/unblocked-managed-final.log, /tmp/unblocked-clippy-final.log,
/tmp/unblocked-format-final.log, /tmp/unblocked-boundaries-final.log,
/tmp/unblocked-ownership-mutant.log, /tmp/unblocked-review-{rust,csharp}-final.log.
#92 reproducer: native/tests/incremental_form_core_contract.rs,
/tmp/form92-policy550.log. #98 external-consumer compiler evidence:
/tmp/core550-annotation-access.log (E0308), private module confirmed in core source.
#101 source evidence: core 5.5.0 src/batch/worker.rs Rotate/Compress branches.
On user request, registered core #794/#795/#797 after checking for duplicates,
with reproducers/evidence, acceptance criteria and downstream issue links.
No issues closed, commits, push or PR performed. Existing dirty handoff and probes preserved. Next action: platform CI for implemented surfaces and
upstream corrections for the three incomplete contracts; do not mark full parity.

# Active continuation — core 5.5.0, 2026-10-19

Official [v5.5.0](https://github.com/bzsanti/oxidizePdf/releases/tag/v5.5.0)
verified (published 2026-10-19). Exact native pin and parity contract now use
5.5.0; local ignored Cargo.lock resolves the registry package and roxmltree 0.21.1.
Existing feature selection and public APIs are preserved. Earlier evidence below
is historical, superseded only where this section gives current evidence.

| Requirement / source | State / evidence | Blocker / next action |
| --- | --- | --- |
| Adopt released fixes | Demonstrated locally: 251 native units + 22 core regression cases pass | Platform CI/integration pending |
| #91 XMP | Six probes pass including all five previous failures | Full model/document integration remains unimplemented |
| #92 incremental forms | ReadOnly fix passes direct-core and real-FFI mixed-batch rejection; writable control preserves prefix/readback | Signature/certification/field-lock policy, complete field/error/appearance acceptance remain pending |
| #93 calculations | Three probes pass; public value/event accessors verify values, order, old/new values and timestamps | Implement typed binding and remaining arithmetic/error contracts |
| #98 annotations | Four final-PDF probes pass including attachment reference and MIME readability | Full constructors/options plus payload readback and FFI acceptance remain pending |
| #100/#101 streaming/batch | Six probes pass including real pages, timeout and stop-on-error | Full binding, effective memory options, optional features and RID acceptance pending; #758 now CLOSED |
| #89 OCR | Upstream #708 still OPEN, refreshed from GitHub | Existing upstream geometry/provider dependency remains |
| #104 exact tokens | Previous budget failure not retested; release does not establish correction | Reconstruct missing /tmp prototype and verify selected-counter budgets |

## Compatibility and #92 continuation

Rebuilt linux-x64 debug native library and used it in the real managed suite.
Initial full run: 1,185 passed / 10 failed out of 1,195. All failures were traced:
5.5.0 rejects invalid PDF/A identities at XML parse time; validator maps these to
XmpMetadataMissing. Part mismatch fixtures now use another *valid* identity,
retaining exact mismatch payload assertions. Missing-identity XML now declares its
namespace; a new test separately checks invalid identity/malformed XML rejection.
Parser #754 rejects dangling references instead of fabricating null: named-image
errors now correctly expect InvalidData; dangling PDF/A OpenAction must throw
ParseError rather than pass validation. No production fallback or relaxed
assertion was introduced. Final affected-suite run: all 55 pass; the other 1,141
cases passed in the full run and their sources are unchanged.

Added #92 real-FFI regression: a batch containing writable and ReadOnly fields
throws, input remains unchanged, and a subsequent writable-only fill succeeds
with exact prefix/value and retained ReadOnly flag. Included in parity gate.
The gate's core mutation now derives the current pin instead of hardcoding 5.4.2.
Removed 13 obsolete ignore attributes from the core probes. These are core
regressions, not evidence of six completed binding capabilities.

## Review and validation

Scope: dependency adoption, existing core probes, managed compatibility fixtures,
ReadOnly FFI regression, gate and API documentation. Reviewed the form boundary's
input copy, native result/error propagation and finally-based buffer release;
no FFI layout/ownership changes. Historical 5.4.2 failures provide RED for the
newly active core regression cases. Current GREEN: 273 native tests, focused
55 managed tests, all-target/all-feature Clippy with warnings denied. Full-suite
results reused only for unchanged cases. Platform CI and release remain pending.

Both Kripteia analyses executed for Rust and C#: Rust score 97 (273 tests/35 files),
C# score 95 (1,046 tests/132 files in the final scan). Rust security enumerated 297
existing unsafe/FFI usages without secret/taint findings; C# reported no security
findings with 41 degraded syntax trees. Telemetry readonly warnings did not prevent
analysis. Scores/unsafe counts are not approval of the entire upstream core.
Manual review found stale diagnostics and a hardcoded mutation, corrected above.
Remaining capability requirements are recorded in the table, not waived by upgrade.

Evidence: /tmp/core550-native-tests.log, /tmp/core550-native-final.log,
/tmp/core550-calculation-access.log, /tmp/core550-dotnet-tests.log,
/tmp/core550-focused.log, /tmp/core550-clippy-final.log, /tmp/core550-format.log,
/tmp/core550-review-rust.log, /tmp/core550-review-csharp-final.log and
/tmp/core550-format-final.log.
No commit, push, PR, issue closure or publication. Existing dirty handoff/probes
preserved. Next implementation increment: #91 full XMP integration or #93 typed
calculations, whose previously blocking core contracts now pass; #92 remains
open until its remaining policy and complete behavioral requirements are proven.

# Verification — streaming #100 and batch #101, 2026-10-17

Scope is feasibility/dependency verification requested by the user, not full
binding implementation. Baseline core 5.4.2, develop 3457d2; earlier probes and
tracking preserved. No core replacement or dependency upgrade.

| Requirement / source | State / evidence | Verification / stop condition |
| --- | --- | --- |
| #100 real lazy pages/objects | 5.4.2 constructor hardcodes zero pages; upstream #725 is CLOSED | Confirmed count mismatch (1 versus 0); adopt released correction |
| #100 real streaming contents/bounds | 5.4.2 PageStreamer fabricates pages/content; upstream #724 is CLOSED | Confirmed three fabricated A4 pages versus one 321x456 page |
| #100 effective memory options | Upstream #758 remains OPEN | Reuse issue evidence; no claim of process-wide memory cap |
| #101 per-item outcomes/order and concurrency | Worker pool retains indices and fixed worker count | Passed mixed outcomes, exact names/order, observed <=2 workers |
| #101 timeout/resource options | Worker ignores memory_limit and job_timeout | Confirmed 30ms success at 1ms timeout; memory remains source-only evidence |
| #101 stop_on_error | Custom-job branch does not set cancellation | Confirmed second side effect executes after serial failure; core #776 |
| #101 optional performance/rayon | Native build omits both | Source inventory only; no optional-feature runtime/packaging claim |

Verification completed: ordinary reader returns one page; lazy returns zero;
streamer returns three A4 pages for a single 321x456 page. Upstream #724/#725
are closed but their corrections are absent from pinned 5.4.2; #758 remains open.
Do not infer that a suitable newer release was tested or adopted.

Batch mixed-outcome/order/two-worker-bound control passes. Timeout 1ms with a
30ms job reports success; serial stop_on_error runs the second callback after
failure. Core https://github.com/bzsanti/oxidizePdf/issues/776 owns these failures
and the source-only unused memory-limit finding. Optional performance/rayon
paths and RID builds are not runtime-validated and remain pending.

Probe native/tests/stream_batch_core_contract.rs; log /tmp/stream-batch-core.log.
Explicit run: two controls pass, four assertions fail. Normal run: two pass and
four known failures ignored, not passing acceptance. Format/focused Clippy pass.
Fixed in-memory PDF and finite custom callbacks; longest sleep is 30ms, no
external scripts or caller-selected file output. Full implementation review,
real-FFI acceptance and platform CI remain pending. #100/#101 updated; no product
changes, core replacement or dependency bump.

# Active investigation — annotation creation (#98), 2026-10-17

Baseline integrated develop 3457d2; core pinned 5.4.2. Existing new-document
constructors live in native/src/annotations.rs and PdfPage.cs; incremental
editors are distinct and do not demonstrate creation parity.

| Public core family/options | Existing .NET creation coverage | Remaining requirement |
| --- | --- | --- |
| URI/GoTo Link; Text icon/open; Stamp standard/custom | Selected constructors present | Full action/destination/common option inventory before implementation |
| Line endings/interior; Square/Circle border effects | Basic geometry and colors present | Missing subtype/common options |
| Highlight/Underline/StrikeOut, Squiggly and arbitrary QuadPoints | First three rectangle constructors present | Squiggly, quad geometry and markup options |
| FreeText DA/quadding/rich text/default style | No new-document constructor | Typed configuration and final dictionary tests |
| Polygon/Polyline vertices, border, opacity, endings | No new-document constructor | Geometry validation and configuration |
| Ink strokes and bounds | No new-document constructor | Typed strokes and malformed geometry tests |
| Popup parent/open/color/flags | No new-document constructor | Verify valid writer-assigned parent relationships |
| FileAttachment filename/data/MIME/icon | No new-document constructor | Confirmed payload discard; core #774 blocks acceptance |
| Common contents/subject/name/date/flags/border/color | Partial specialized setters only | Full supported typed options and serialization |
| Other AnnotationType variants (Sound/Movie/etc.) | Enum-only is not functional creation | Do not claim unsupported variants as complete |

Feasibility investigation completed: core #774 blocks attachment acceptance.
Final PDF /FS/EF/F is a direct Type/Length dictionary, not an indirect stream;
source discards the payload. Adding MIME text/plain produces an empty parsed
annotation dictionary (observed failure, exact MIME root cause not established).
FreeText contents/alignment and Ink coordinates survive serialization/readback.
Probe: native/tests/annotation_creation_core_contract.rs. Explicit run: two pass,
two acceptance failures; normal run: two pass/two ignored. Format and focused
Clippy pass. Log /tmp/annotations98-core.log. Initial get_page harness error was
corrected by Pages/Kids traversal before recording this evidence.
Upstream https://github.com/bzsanti/oxidizePdf/issues/774 contains full reproducer.
The diagnostic uses fixed in-memory data only, executes no attachments/actions,
and adds no dependency. Full FFI/managed implementation, real-FFI tests, parity
gate, quality/security review and platform CI remain pending. Core repairs stay
upstream; no binding-side replacement writer.

# Active investigation — calculation engine (#93), 2026-10-17

Pinned core 5.4.2, integrated develop 3457d2. Reusing the previous observation
about inaccessible system values/events; no replacement engine is authorized.

| Requirement / source | State / evidence | Blocker / next action |
| --- | --- | --- |
| Typed calculations/results (#93) | Blocked: external consumer produces six E0616 errors; no public alternative getters in 5.4.2 | Core #771: typed result/event accessors required |
| Dependencies/order/cycles (#93) | Lower engine value/order and direct system/cycle controls pass; system two-hop event assertion fails (2 vs 3) | Core #771: propagate transitive dependencies; exact system value readback awaits accessor |
| Missing references/arithmetic errors (#93) | Pending | Inventory semantic limits after feasibility gate |
| Complete FFI/models/ownership, tests, parity, quality/security and platform CI (#93) | Not implemented | Required for completion; never infer parity from diagnostic controls |

Feasibility investigation completed: upstream blocker
https://github.com/bzsanti/oxidizePdf/issues/771 includes both reproductions.
Runtime evidence: native/tests/calculation_core_contract.rs and
/tmp/calculations93-core.log (two controls pass, one acceptance assertion fails).
Normal run: two pass, one known failure ignored; not passing #93 acceptance.
Compile evidence: /tmp/calculations93-access and /tmp/calculations93-access.log.
The initial no-compression harness failed dependency compilation; corrected with
compression before recording E0616. No product dependency change.
Formatting, focused Clippy and diff checks pass. Probes use only fixed in-memory
values and existing dependencies; no PDF scripts or external runtime executed.
Full implementation quality/security review and platform CI remain pending.
Core changes stay upstream; partial lower-engine support alone cannot close #93.

# Active investigation — incremental forms (#92), 2026-10-17

Based on integrated develop 3457d2, pinned core 5.4.2. #94 is closed and checked
in #81. Existing FillFormFieldsAsync already exposes the incremental core filler;
the issue body overlooked this API. No replacement API is required merely to
expose fill_many. User strategy remains: core defects must be repaired upstream.

| Requirement / source | State / evidence | Blocker / next action |
| --- | --- | --- |
| Existing-byte single/batch filling (#92) | Existing managed/FFI API found in PdfOperations.cs/forms.rs | Audit full behavior before extending surface |
| Prefix preservation and batch failure (#92) | Two direct-core controls pass | Not real-FFI acceptance; probe in native/tests/incremental_form_core_contract.rs |
| Read-only fields (#92) | Confirmed failure: /Ff 1 accepts mutation | Upstream oxidizePdf#770; adopt released correction before acceptance |
| Invalid fields and signature/lock policy (#92) | Pending | Separate investigation; no unsupported claim of preservation |
| Appearances, typed errors, ownership, parity and review/CI (#92) | Pending | Required before acceptance; no completion claim |

Explicit run: two controls pass and ReadOnly rejection fails; normal run: two pass,
one known failure ignored. Formatting and focused Clippy pass. Reproducer is
attached to https://github.com/bzsanti/oxidizePdf/issues/770 and #92 is updated.
Evidence: /tmp/incremental-form-core-20261017.log.

The new probe uses fixed in-memory data and existing dependencies, with no external
resources or executable PDF actions. It is a core diagnostic, not FFI acceptance.

# Active increment — form validation (#94), 2026-10-16

Based on develop d47a087, after merging #107 and closing #99. Explicit validation
uses core 5.4.2 without changing the dependency or existing FillField behavior.

| Requirement / source | State / evidence | Blocker / next action |
| --- | --- | --- |
| All rules, masks, settings, values and results (#94) | Demonstrated locally | Typed models and synchronous real-core Validate/ValidateMany |
| Custom rules and ordered diagnostics (#94) | Demonstrated locally | Snapshots, pure managed predicates, core Custom outcomes; mutation caught |
| Unicode/empty/multiple-rule failures (#94) | Demonstrated locally | Six behavioral fixtures plus native boundary cases |
| Existing FillField behavior (#94) | Demonstrated locally | Explicit invocation; regression covers reading, creation and filling |
| Bounds, parity and quality/security (#94) | Demonstrated locally | 45 managed cases, 251 native units, Clippy and review below |
| Platform CI (#94/#81) | Demonstrated on 2d84db0 | PR #108, run 39054241586: all 14 jobs pass, pull_request event. Local full format check also passes. Merged via PR #108 as 3457d2 on 2026-10-17. |

## Validation usage and contract

```csharp
var result = PdfFormValidation.Validate(new PdfFieldValidation(
    "email", new PdfFormValue.Text("person@example.com"),
    new PdfFieldValidator(new PdfValidationRule[] {
        new PdfValidationRule.Required(), new PdfValidationRule.Email()
    })));
if (!result.IsValid)
    foreach (var error in result.Errors)
        Console.WriteLine(error.Message);
```

Call validation explicitly before the application's existing fill operation.
ValidateMany returns independent requests in input order, including duplicate names.
No document, JavaScript, external resource or persistent validation cache is involved.
Rules and request data are snapshotted before invoking pure managed custom predicates;
predicate exceptions propagate before native validation. No managed delegate pointer
crosses FFI. Results are owned managed data; native output is always freed in finally.

Core limits are explicit in XML docs and regression fixtures: all seven settings
are stored but unused; conditions except Always evaluate false; Group is unused;
Length counts UTF-8 bytes; formatting is not validation. Mask failures are Format,
while email/URL/phone/date/time/card rule failures are Custom. Warnings are empty
and Details is null. These are core contracts, not stronger binding guarantees.

Limits: 256 requests, 128 rules/field, 1,024 total rules, 1 MiB input JSON,
65,536 UTF-8 bytes/value, 1,024 bytes/name, 4,096 bytes/pattern or message, 32 number
decimals, and 8 MiB output JSON. Custom placeholder is one Unicode scalar.
Managed argument failures or PdfFormValidationException abort the whole call;
field validation failures are returned diagnostics. Native callers must supply
valid buffers; lengths are bounded before dereferencing.

## Quality review — form validation (#94)

### Resumen ejecutivo

Reviewed nine implementation/test/contract/tracking files against d47a087 plus
the working increment. Core validation.rs and calculations::FieldValue are the
behavioral sources. No remaining confirmed implementation finding. Local review
is complete; the formatting-only correction 2d84db0 resolves the prior CI whitespace
failure. The exact CI format command passes locally (537 files, zero changes);
non-whitespace test content is identical. All 14 PR CI jobs pass on 2d84db0
(run 39054241586); merged via PR #108 as 3457d2 on 2026-10-17. Earlier XMP probes and
the blocked #104 prototype are outside this increment.

### Contratos y evidencia

FormValidationTests uses the real native library for every rule, every mask/layout,
default settings, ordered failures, Unicode, empty values, callback snapshots,
exceptions and request limits. Existing form read/create/fill tests and parity/ABI
gates pass: 45 cases in /tmp/form-validation-final.log. Native tests pass 251 unit
cases (/tmp/form-validation-native.log); the unrelated XMP integration probe has
one passing control and five explicitly ignored upstream defects. Native boundary
tests check null/zero/oversized/invalid UTF-8 input and no output on a late invalid
request. Formatting and all-target/all-feature Clippy with denied warnings pass
(/tmp/form-validation-clippy.log).

A disposable compiled copy in /tmp/form-validation-mutant replaces custom predicate
results with true. Three fixtures fail for the intended reasons: missing Custom
diagnostic, falsely valid rejected value, and uncalled predicate. The other three
pass; the production six pass. Log: /tmp/form-validation-mutant.log. Final added
bound assertions also pass in the production suite.

### Hallazgos

No unresolved findings in the increment. During implementation, missing enum XML
documentation was corrected; test expectations were reconciled with core's Custom
diagnostic mapping before acceptance. Core limitations above remain documented,
not silently implemented in the binding.

### Calidad de Tests (Kripteia)

Mandatory script ran on native/src and dotnet: Rust score 97, 251 tests/30 files;
C# score 95, 1,044 tests/132 files. New native tests score 100 (2), new managed tests
score 100 (6). Final changed test-file reanalysis also scores 100. Existing worst
Rust test is ffi_guard_unit_swallows_panic (65); unrelated historical assertions
were not expanded into this issue. Logs: /tmp/form-validation-qr-rust.log,
/tmp/form-validation-qr-csharp.log and /tmp/form-validation-qr-final-tests.log.
No score substitutes for the real-FFI and mutation evidence above.

### Análisis de Seguridad (Kripteia Security)

Both mandatory security analyses executed. Rust lists 297 unsafe/FFI usages,
including oxidize_form_validate; manual inspection confirms null checks, bounded
read, panic containment and one owned allocation/free path. No reported secret
or taint-flow finding. C# reports no security issues, with degraded syntax parsing
for 41 existing files (none of the new validation files). Read-only telemetry DB
warnings did not prevent either analysis. Managed callbacks execute only supplied
application delegates synchronously. JSON does not execute expressions or scripts.

### Métricas

- Files reviewed: 9, plus core contracts and existing consumers.
- Unresolved findings: 0.
- Product files modified by review: 0; implementation corrections and tests above.
- Pending verification: PR platform matrix and integration.

# Blocked investigation — exact token counting (#104), 2026-10-16

User authorized independent binding work after merging #107. #99 is closed and
checked in #81; develop integration is `d47a087`. #104 uses existing core 5.4.2
TokenCounter APIs, without upstream fixes. #93/#94 remain separate capabilities.

| Requirement / source | State / evidence | Blocker / next action |
| --- | --- | --- |
| Word proxy and cl100k_base selection (#104) | Pending | Typed count API and explicit counter provenance; preserve defaults |
| Optional build/discovery/unavailable errors (#104) | Pending | Cargo feature plus feature-enabled/disabled native verification |
| Applicable semantic/hybrid chunker integration (#104) | Pending | Forward selection to core with_token_counter before chunking |
| Independent pinned oracle and chunk budgets (#104) | Failed core budget probe | Core joins a and b into 3 tokens at budget 2, reporting oversized=false; correction/adoption required |
| Ownership, errors, parity, quality/security (#104) | Pending | Real-FFI boundaries and integrated review |
| Platform CI/integration (#104) | Pending | PR-based CI before integration |

#104 prototype is preserved in `/tmp/tokens104-prototype`, not in the product tree.
Direct core probe: `/tmp/tokens-core-budget.log`; Python tiktoken 0.12.0 confirms
`a` and `b` cost one each while `a\nb` costs three. No core correction or acceptance
claim. Exploratory managed URL-fixture failures concern key/value display text,
not this independent core budget defect.

# Completed increment — arbitrary clipping (#99), 2026-10-16

Selected independent work after the user assigned upstream defects to the core.
Core 5.4.2 exposes `ClippingPath` and `GraphicsContext::set_clipping_path`.
#92 was inspected but not selected: its policy acceptance needs further core
investigation; no new defect/completion claim is made for it.

| Requirement / source | State / evidence | Blocker / next action |
| --- | --- | --- |
| Typed move/line/cubic/close and both winding rules (#99) | Demonstrated locally | Bounded path builder; complete validation before applying existing core operation |
| Holes and save/restore scope (#99) | Demonstrated locally | Real-FFI operators, Poppler pixels and isolated wrong-rule mutation |
| Finite coordinates, invalid/empty paths, ownership (#99) | Demonstrated locally | All six native coordinates, invalid commands/counts/rule, unchanged-page assertion, managed snapshots and lifetime tests |
| Existing convenience APIs, parity and quality/security (#99) | Demonstrated locally | 23 focused .NET cases, 249 native unit cases, formatting/Clippy and review below |
| Platform CI before integration (#99/#81) | Demonstrated: 14/14 jobs passed on `a7e1934` | Merged as d47a087; #99 closed and checked in #81 |

## Custom clipping usage and contract

```csharp
var path = new PdfClippingPath()
    .MoveTo(10, 10).LineTo(90, 10).LineTo(90, 90).LineTo(10, 90).ClosePath()
    .MoveTo(30, 30).LineTo(70, 30).LineTo(70, 70).LineTo(30, 70).ClosePath();
page.SaveGraphicsState()
    .ClipPath(path, PdfWindingRule.EvenOdd)
    .DrawRect(0, 0, 100, 100).Fill()
    .RestoreGraphicsState();
```

Coordinates are PDF points, serialized by core to three decimals. Both contours
above have the same orientation: even-odd leaves a hole, nonzero fills the center.
The default rule is nonzero. CurveTo takes two control points and an endpoint.
Open subpaths close implicitly; a move-only/zero-area path is valid and clips to
an empty region. An empty command list is rejected. Line/curve/close require a
current point. End any prior unfinished drawing path before applying a custom
clip, as with core clipping operations. Use graphics-state save/restore to restore
the effective PDF clip; this change does not alter ClearClipping or convenience APIs.

The mutable builder is not thread-safe. Calls copy up to 65,536 commands and
retain no builder reference; SafeHandle marshalling keeps the page alive for the
native call. Non-finite coordinates/unknown rules are argument errors, missing
MoveTo/command overflow are invalid-operation errors, and consumed/disposed pages
retain existing lifetime errors. Native callers receive existing typed error codes
and last-error text. Invalid commands are rejected before page mutation. No core
dependency, public existing signature or workflow changes.

## Quality review — custom clipping (#99)

### Resumen ejecutivo

Reviewed the eight implementation/test/probe/contract/tracking files for #99
against develop `73bf34c` plus the final working changes. The earlier #91 probes
and pre-existing handoff are separate. Local implementation and review complete;
all 14 platform CI jobs passed on `a7e1934`. User-authorized
[PR #107](https://github.com/bzsanti/oxidize-pdf-dotnet/pull/107) is merged as d47a087.
[CI 38891979618](https://github.com/bzsanti/oxidize-pdf-dotnet/actions/runs/38891979618)
was triggered by `pull_request`; #99 is closed and checked in #81.
No package publication performed.

### Contratos y evidencia

`ClippingPathTests` crosses the real managed/native boundary and checks ordered
geometry, both winding operators, q/Q painting scope, cubic coordinates, snapshots,
consumed/disposed pages, invalid paths and the exact accepted command cap.
`graphics::custom_clip_tests` verifies the fixed ABI layout, null/count/rule/kind
errors, all six coordinates against NaN and both infinities, and unchanged page
operations after a late invalid command. Oversized counts are rejected before
reading a one-command buffer. No arbitrary invalid pointer is dereferenced.

All 249 native unit tests pass; the separate earlier XMP probe has one passing
control and five explicitly ignored upstream failures, not #99 acceptance cases.
All 23 focused managed cases pass, including ellipse/transform regression and
parity/ABI gates. All-target/all-feature Clippy with warnings denied and formatting
pass. Logs: `/tmp/clipping-native-test.log`, `/tmp/clipping-dotnet-test.log`,
`/tmp/clipping-clippy.log`. The first .NET attempt failed at sandbox MSBuild socket
creation; the permitted retry compiled and passed.

`python3 tools/clipping_render_probe.py native/target/debug/liboxidize_pdf_ffi.so`
renders real-FFI PDFs with Poppler at 72 dpi. Center/ring/outside/restored pixels
are respectively 0/0/255/0 for nonzero and 255/0/255/0 for even-odd (0 is black).
The probe uses private temporary files, trusted explicit library selection, no
shell and a 30-second renderer timeout. A disposable compiled managed copy in
`/tmp/clipping-rule-mutant` forces rule zero while still using real FFI: the
even-odd assertion fails for missing `W*`, with three controls passing. Log:
`/tmp/clipping-mutant-test.log`. Production sources were not mutated.

### Hallazgos

1. **Existing current path participates in PDF clipping**:
   `dotnet/OxidizePdf.NET/PdfPage.cs:2297` — the core appends path operators;
   unfinished preceding geometry can participate. The new API explicitly requires
   ending prior drawing paths, preserves core semantics, and documents q/Q scope.
   This does not require a core change or modifying existing convenience methods.
2. **Pointer validity remains a native caller obligation**:
   `native/src/graphics.rs:576` — finite/count validation cannot establish raw
   pointer allocation validity. The safety contract requires an aligned initialized
   array and live exclusive page; managed marshalling supplies the array and holds
   the page SafeHandle. Bounds are checked before constructing the slice.

Both are explicit contract boundaries, not unresolved implementation blockers.

### Calidad de Tests (Kripteia)

Actual Rust analysis: score 97, 249 tests across 29 files. Managed test analysis:
score 95, 1,028 tests across 125 files; all four new ClippingPathTests score 100.
Existing repeated-dispose tests score 65 and remain outside this increment. The
production-only C# and Python runs detect zero tests, so their score 100 supplies
no test evidence. Logs: `/tmp/clipping-kripteia-{rust,csharp,tests,python}.log`.

### Análisis de Seguridad (Kripteia Security)

Rust reports 296 unsafe usages, predominantly existing FFI exports, including the
new boundary. No additional taint/secret/dangerous-function category was reported.
C# production/tests and Python report no security issues; C# has partial/error
syntax-tree degradation on existing files, so these scans do not prove absence of
defects. Telemetry writes failed on the readonly database but both analyses ran.
Manual review covered native pointer/count/layout checks, complete validation
before mutation, SafeHandle lifetime, bounded copying, error paths and the probe's
filesystem/process behavior. No new dependencies or external input execution.

### Métricas

- Files reviewed: 8 (#99 scope).
- Findings: 2 contract boundaries documented above; no unresolved local blocker.
- Product files modified by the review: 0.
- Pending actions: PR review, integration and issue closure. Platform CI complete.

# Active investigation — XMP (#91), 2026-10-16

Issue criteria refreshed from GitHub against local develop `73bf34c`; core remains
pinned to 5.4.2. No XMP binding or completion claim has been introduced.

User decision: corrections belong to the core; do not implement a replacement
parser in this binding. Open core issues searched on 2026-10-16: existing
[#741](https://github.com/bzsanti/oxidizePdf/issues/741) covers RDF attribute-form
identifiers and namespace handling. Created
[#763](https://github.com/bzsanti/oxidizePdf/issues/763) for the additional
property-loss, XML parsing and escaping defects, with all six self-contained
probes and a cross-reference to #741. #91 remains blocked on corrected core
adoption plus the downstream implementation/acceptance work below.

| Requirement / source | State / evidence | Blocker / next action |
| --- | --- | --- |
| Preserve supported model properties through parse/serialize (#91) | Failed: keywords disappear; title entities remain escaped | Correct/adopt core parser and serializer; rerun `xmp_core_contract` |
| Namespaces and malformed XML (#91) | Failed: equivalent namespace prefix loses title; unclosed XML succeeds | Namespace-aware XML validation/parsing required upstream or an explicitly designed adapter |
| Safe serialization of supported identifier fields (#91) | Failed: amendment text can insert XML elements | Escape all textual fields; retain injection regression |
| Supported document integration and Info/XMP policy (#91) | Pending: writer generates XMP from Info, not from the PDF/A XMP model | Establish supported full-model write path and explicit consistency semantics before binding |
| Typed FFI, ownership, bounded inputs, parity gate, review and platform CI (#91) | Not implemented | Remains required after core strategy is settled; CI via PR targeting develop/main |

Reproduction: `cargo test --offline --manifest-path native/Cargo.toml --test
xmp_core_contract -- --include-ignored`. Six compiled tests call the actual pinned
core: five fail their acceptance assertions, one Unicode/identifier control passes.
The five known-failure probes are explicitly ignored in ordinary test runs; this
is an upstream diagnostic suite, **not passing #91 acceptance or FFI evidence**.
The ordinary run passes the control and reports five ignored tests. No production
source, dependency, public API or workflow was changed. Formatting passed.

Source inspection: `pdfa/xmp.rs` implements the model named by #91 using regex
extraction; `to_xml` omits keywords and escapes only some fields. The separate
`metadata::XmpMetadata` has a different property model and parser; substituting it
is not automatically equivalent to the issue's PDF/A model. `Document::create_xmp_metadata`
and `writer/pdf_writer/mod.rs` generate the catalog metadata stream from Info.
This establishes the default writer behavior, not a claim that every possible
low-level integration path is unavailable. Read integration, compressed stream
limits, full-model write support and policy still require implementation evidence.

The new probes use fixed, small in-memory strings and existing dependencies; they
do not write files, execute PDF actions or expose an FFI. The identifier regression
captures XML property injection, not OS command execution. Full implementation
quality/security review and platform CI remain pending. #89's previously recorded
OCR blocker is independent and remains open.

# Completed increment — PDF/A validation (#90), 2026-10-15

| Requirement / source | State / evidence | Blocker / next action |
| --- | --- | --- |
| All eight core levels and collect-all-errors setting | Demonstrated locally, including compiled negative control | Core 5.4.2 stores but never reads collect-all; documented and tested as such |
| Structured errors/warnings and operation failures | Demonstrated locally | Exhaustive variant mapping; distinguish parser failure from findings and unlock encrypted readers explicitly |
| Valid/invalid fixtures per level; actual validator coverage | Demonstrated locally | Independently authored PDFs, compressed/uncompressed XMP and level-specific violations; no certification or conversion claim |
| Ownership, boundary behavior and compatibility | Demonstrated locally | Snapshots, typed failures, native boundaries and real FFI; existing OCR regression passes |
| Parity gate, quality/security review and platform CI | Demonstrated: local checks and 14/14 CI jobs pass on `39100ed` | None for #90; package publication separate |

## Quality review — PDF/A validation (#90)

### Resumen ejecutivo

Reviewed ten changed implementation/test/contract/documentation/probe files over
develop `324235d`, excluding preexisting session notes. The new API wraps the
pinned validator and an optional reader unlock; no dependency or existing API
changes. [Public contract and limitations](PDFA_VALIDATION.md). Local acceptance
passes, and all 14 platform CI jobs pass on implementation
`39100ed1125e6f1ea11489a5cc37228bf814e70c`:
https://github.com/bzsanti/oxidize-pdf-dotnet/actions/runs/38808943719
Documentation-only closure preserves product/test/contract/workflow identity with
that validated SHA. The separate #89 upstream
blocker is documented below and is not represented as completed implementation.

### Contratos y evidencia

35 focused .NET cases pass: 21 new PDF/A cases, the parity/ABI gates and existing
OCR-layer regression. Each of eight levels has matching and mismatched XMP tests,
including compressed metadata. Distinctive findings retain font/action/location,
version and identifier payloads. Rule differences cover transparency, ToUnicode
and attachments. Conformance failures return reports; malformed PDF/page trees,
locked readers and wrong passwords produce typed operation failures. Successful
unlock retains EncryptionForbidden. Invalid settings, native JSON, cancellation,
input snapshots and recovery after failures exercise the real FFI.

All 248 native tests pass. Three new tests cover declared warnings/unemitted
variants, Unicode/u64 payloads, operation error categories, null/UTF-8/impossible
length boundaries and rejection before dereferencing over-budget input. Clippy
with all targets/features and warnings denied passes; format checks are part of
the final verification and pass. Logs: `/tmp/pdfa-*.log`.

An isolated compiled managed copy forces all requests to A1b while continuing to
call the real native validator. Seven level cases fail their validity assertions;
the A1b control passes. This is a behavioral failure, not a setup/compile failure.
Production sources/runtime are unchanged. Evidence: `/tmp/pdfa-level-mutant` and
`/tmp/pdfa-mutant-test.log`. The subsequent optional-password branch does not
change the no-password requests used by this control; its evidence remains
applicable and the final baseline passes all eight cases.

Manual review traced all enum/payload mappings, parser/validator error paths,
unlocking, snapshot timing, allocation ownership and limits. Output pointers are
cleared before validation; byte limits precede slice construction; every native
JSON response is freed in finally. No native handles, caller-controlled file
writes or PDF action execution are introduced. Credentials are not logged or
returned, but are ordinary request strings with no secure-erasure guarantee.
The 100 MiB input cap is not a global parser/decompression/runtime budget; #100
retains that separate capability. The OCR probe uses explicit trusted library
selection, private temporary files, no shell, a Poppler timeout and one native
free. Its expected geometric failure is not counted as a passing test.

### Hallazgos

1. **Collect setting is a core no-op**: native/src/pdfa.rs:134 and
   dotnet/OxidizePdf.NET/PdfAValidation.cs:78 — forwarding false cannot cause early
   termination in 5.4.2. Preserve the complete report and document this behavior;
   both values are compared through FFI. A future core change requires renewed
   evidence, not artificial report truncation.
2. **Partial core coverage and silent optional references**:
   dotnet/OxidizePdf.NET.Tests/PdfAValidationTests.cs:124 — the first test assumed
   a dangling OpenAction would throw; the core resolves it to null and skips it.
   A malformed required Pages reference demonstrates the actual parser failure.
   Both behaviors are now asserted and the coverage guide records this gap.
   Core warnings have no producer; their full variants are unit-tested at the
   native mapping boundary rather than fabricated by the validator.
3. **Locked readers cannot return a complete report**: native/src/pdfa.rs:127 —
   the core initially notices encryption but then fails reading the locked
   catalog. Add explicit Password support via public reader.unlock; null/wrong
   credentials remain ParseError, successful unlocking still reports encryption.
   Real encrypted FFI fixtures verify both paths. No document is decrypted to disk.
4. **Existing OCR layer geometry fails acceptance**: tools/ocr_geometry_probe.py:90
   — independent bounds are 160.02 pt for a 20 pt request. This blocks #89 and
   belongs to upstream #708; it does not affect the read-only PDF/A wrapper.
   Preserve #89 as open and require actual geometry/rendering evidence after the
   core correction. The old layer usage guide now discloses the limitation.

No unresolved binding defect remains within #90's explicit core-coverage scope.
This conclusion does not certify the core's complete ISO conformance coverage.

### Calidad de Tests (Kripteia)

Rust: score 97, 248 tests/29 files; new pdfa module 3 tests, score 100. C#: score
95, 1034 methods/130 files; new PDF/A file 7 methods, score 99. Lowest existing
Rust item is ffi_guard_unit_swallows_panic (65); existing managed model-only tests
are among lower scores. These unrelated observations are not #90 blockers.
Python probe scan reports 0 tests/0 test files (score 100 therefore supplies no
behavioral evidence); its actual subprocess/FFI run is the evidence above.

### Análisis de Seguridad (Kripteia Security)

Rust reports 295 unsafe usages, predominantly FFI boundaries; no emitted secret
or taint finding. C# reports no security issues, with 41 files degraded by its
syntax parser, including PdfAValidationTests. Manual review covers this limitation.
Python reports no security issues. All scans complete despite a read-only
telemetry database warning; telemetry is not the analysis result. Scores and
absence of findings do not substitute for the ownership/credential review.

### Métricas

- Files reviewed in this increment: 10, plus relevant pinned core producers.
- Confirmed findings: 4, reconciled above; #89 remains separately blocked.
- Product changes made by review itself: 0; reader-unlock correction belongs to
  the authorized implementation and was revalidated.
- Pending verification for #90: none; all 14 configured platform CI jobs pass.
- CI workflow preference: the user explicitly allowed this existing feature-push
  run to finish, but future increments must use PR-triggered CI targeting
  develop/main. No workflow change or new CI run is part of this closure.

## OCR #89 — upstream acceptance blocker

Issue #89 remains open. Pinned core 5.4.2 has the unresolved defects in
[upstream #708](https://github.com/bzsanti/oxidizePdf/issues/708), confirmed OPEN
on 2026-10-15. The optional Tesseract provider emits zero-sized regions and
dimensions, heuristic confidence and an ASCII-only default whitelist. Its
availability method returns true without invoking the executable. Its image
method does not implement preprocessing, regions or timeout options.

The searchable layer also fails independent geometry acceptance even with
valid caller-produced recognition. The committed probe calls our real
`oxidize_ocr_layer` FFI, requires exact original-prefix preservation and exact
text `El`, then measures the actual word using Poppler:

```sh
python3 tools/ocr_geometry_probe.py dotnet/OxidizePdf.NET/runtimes/linux-x64/native/liboxidize_pdf_ffi.so
```

Current result: requested width **20 pt**, actual width **160.02 pt**; exit 1
at the geometric assertion. This is an expected failing acceptance probe, not
a passing regression test. It requires an existing platform build and
`pdftotext`; no recognition engine is needed for this isolated counterexample.
It uses private temporary files and frees the native response once.

Next action for #89: adopt a released core correction for #708, then implement
the provider/converter binding, real recognition fixtures, optional RID packaging
and CI. Geometry acceptance must include rotations and displaced CropBox, actual
selectable bounds and unchanged visible rendering; metadata-only checks cannot
close it. Provider/preprocessing/discovery behavior also requires validation.
No OCR capability is marked complete or added to the passing parity inventory.
Existing layer APIs are retained; their current tests prove text/prefix behavior,
not alignment. Work proceeds to independent #90 without changing the core pin
or maintaining a separate OCR engine in the binding.

# Completed increment — CID Unicode and vertical metrics (#88), 2026-10-15

| Requirement / source | State / evidence | Blocker / next action |
| --- | --- | --- |
| Five Adobe collections, complete sequences and missing mappings | Demonstrated locally, including negative control | All five collections, unknown collection/CID, supplementary and multi-scalar results |
| Character-code/CID/GID/Unicode separation | Demonstrated locally | Handwritten code 0x41 → CID 100 → GID 7 → fi; absent mappings remain null |
| Vertical defaults, ranges, overrides and per-CID advance | Demonstrated locally | Compact full-CID range, default/override/reverse-range precedence and core advance agreement |
| Bounded work, typed failures, ownership and compatibility | Demonstrated locally, including guard mutation | Limits, native boundaries, typed failures and queued snapshots; old font APIs pass regression |
| Behavioral contract, review/security and platform CI | Demonstrated: local evidence and 14/14 CI jobs pass on `016a171` | None; package publication separate |

Source: #88 and pinned core 5.4.2. The issue's CidVerticalWidths is declared pub
inside private text::extraction_cmap and is not re-exported. Public resolved fonts
provide effective decoded advances but keep their tables private. Therefore the
binding will inspect W2/DW2 through public parser objects, with compact ranges and
explicit bounds; Unicode/character decoding stays in the public core decoder.
The new PdfFonts API is additive; existing ResolvedFontResource and extraction
APIs remain compatible. The finite scope is collection lookup and requested font
inspection, not a font renderer or font-creation rewrite. Package publication is
separate. Existing uncommitted session notes are preserved.

## Quality review — CID font inspection (#88)

### Resumen ejecutivo

Reviewed eight changed files over develop `53eac54`: additive managed API/models,
native export/module, independent behavioral tests, parity contract and two docs.
Existing extraction/font-creation and resolved-resource methods remain unchanged.
Local acceptance and all 14 platform CI jobs pass on final implementation
`016a17153a7cb68dd8a59d54601c5d3eb4945ab1`:
https://github.com/bzsanti/oxidize-pdf-dotnet/actions/runs/38799415815
Documentation-only closure preserves product/test/contract/workflow identity with
that validated implementation.
See [public contract and examples](FONT_INSPECTION.md).

### Contratos y evidencia

56 focused .NET cases pass (32 new), covering collection lookup and inspection,
legacy font creation/resources, ABI exports and parity gates. Fixtures are authored
independently of the PDF writer, with inherited resources, an explicit code→CID
CMap, a CID→GID stream, ToUnicode and indirect W2/DW2 arrays. Literal expected
values distinguish character bytes, CIDs, GIDs, Unicode and advances. Tests cover
horizontal/vertical CJK fallback, ligatures, supplementary characters and variation
selectors; registry/ordering authority and ToUnicode missing-map precedence; absent
CID/GID/Unicode, valid GID zero; and simple fonts with no CID vertical table.

Vertical assertions independently compare raw defaults/overrides/ranges and query
results with the public core's decoded advances. A 0..65535 range remains one
record. Explicit overrides win over later ranges; the last matching range wins
otherwise. Default -1000 and custom DW2 -800, out-of-domain advance queries,
truncated/descending/overflowing metrics, exact names/pages, malformed PDF,
truncated codes, cancellation, query/entry/work/Unicode limits and mutable queued
inputs are covered. Ownership is exercised through real FFI with no mocks.

All 245 native tests pass, including three new boundary/semantic tests. They cover
complete Unicode versus scalar-only values, unmapped CIDs, invalid JSON/unknown
fields, out-of-range CIDs, collection limits, null pointers, impossible lengths,
invalid UTF-8, cleared output pointers, duplicate range budget accounting, compact
full-CID ranges and nonfinite metrics. Clippy with warnings denied and format
checks pass. Evidence: /tmp/cid-inspection-*.log.

An isolated compiled native copy substitutes scalar-only collection lookup and
removes entry/work guards. Ten selected real-FFI cases produce six intended
failures: four lost multi-scalar mappings and two missing limit exceptions. The
single supplementary-scalar mapping and unrelated limits still pass. Production
sources/runtime are unchanged; the complete baseline passes. Evidence:
/tmp/cid-inspection-mutant-test.log and /tmp/cid-inspection-mutant.

Manual review traced request serialization and snapshots, parser/resource resolution,
collection authority, core glyph decoding, compact-table parsing, lookup precedence,
JSON error envelopes and disposal. Each output buffer is freed once in finally,
including typed failures. Native pointer/length checks precede slice construction;
outputs are cleared before validation. Resource names are carried in escaped JSON,
not interpolated into PDF syntax or paths. No file writes or native handles escape.
Limits bound supplied queries, retained/processed vertical entries and vertical
range-lookup work. Unicode response limits are explicitly post-decoding, not a
promise to bound all allocations made by the core.

### Hallazgos

1. **Private core vertical model**: native/src/font_inspection.rs:114 — the issue's
   cited CidVerticalWidths is public only inside a private module, and its fields
   are private in ResolvedFontResource. A direct export cannot compile against
   pinned 5.4.2. The bounded public-parser W2/DW2 adapter exposes compact pen advances
   and is cross-checked against public decode_glyphs. Malformed tables fail explicitly
   instead of representing a partially recovered table as complete. Replace this
   adapter with a core table accessor when upstream makes one public.

2. **CI toolchain lint**: native/src/font_inspection.rs:171 — CI uses Rust 1.99,
   whose new chunks_exact_to_as_chunks lint rejects constant-sized chunks_exact
   under warnings-as-errors; local Rust 1.96 did not flag it. Use
   as_chunks::<3>().0.iter(), supported by the project's Rust 1.88 minimum.
   The preceding remainder validation remains unchanged, so iteration visits
   exactly the same triples. The first CI run 38776084208 is not acceptance
   evidence; the corrected implementation passes the final platform run above. All 245
   native tests and 32 CID .NET cases pass again against the rebuilt library, as
   does Clippy with all targets/features. The one-line iterator substitution does
   not change the guards or Unicode selection exercised by the isolated mutant;
   that evidence and the completed security analysis remain applicable.

No confirmed defect remains in #88. CIDSystemInfo is declared metadata, not proof
of effective collection fallback; actual Unicode is decided by the core. Full PDF
parsing, embedded-font loading and internal CMap processing are not governed by
the vertical-query work counter. This scope is explicit in options/docs; broader
resource budgets remain in #100. No renderer or writing API changes are needed for
these inspection guarantees. Earlier font-resource APIs retain their old contracts.

### Calidad de Tests (Kripteia)

Both mandatory scripts completed. Rust: score 97, 245 tests/28 files; new font
module: three tests, score 100. C#: 95, 1027 methods/129 files; FontInspectionTests:
12 methods, score 99. The budget theory's assert/setup ratio is flagged (90), but
its two assertions check both rejection and typed error per budget; the compiled
negative control proves that the entry/work guards are reached. Existing worst
scores (65) are unchanged panic/disposal tests, outside #88.

### Análisis de Seguridad (Kripteia Security)

Rust lists 294 unsafe usages (mostly existing FFI declarations), with no emitted
secret/taint-flow findings. C# emits no security findings, with degraded parsing
in 39 files including the new API/tests. Manual review covers their syntax,
marshalling, snapshots, JSON and output ownership. Both tools warn that telemetry
cannot write its read-only database but complete their analyses. Tool results do
not certify global memory/time limits or unreviewed core internals.

### Métricas

- Archivos revisados: 8 changed files, plus core font/collection/parser consumers and existing font-resource APIs.
- Hallazgos totales: 2, resolved within scope as described.
- Archivos de producto modificados por la revisión: 1 (CI lint compatibility).
- Verificaciones pendientes: none for #88 acceptance; package publication is separate.

# Completed increment — incremental writer and Info policy (#87), 2026-10-15

| Requirement / source | State / evidence | Blocker / next action |
| --- | --- | --- |
| All five core WriterConfig fields and a real base workflow | Demonstrated locally | Explicit byte-base append/leading-page replacement; incremental_update is selected by operation |
| Preserve/Replace Info, including Unicode | Demonstrated locally, including negative control | Original reference/generation/absence retained; six replacement fields encoded correctly |
| Exact prefix and valid revisions | Demonstrated locally | Independent fixture, exact prefix, each xref entry and Prev, repeated revisions, stream-xref base and trailer IDs |
| Typed failures and ownership; unchanged SaveToBytes | Demonstrated locally | SafeHandle, checked copy/free, native boundaries, malformed/encrypted bases, unsupported structure and legacy-save regression |
| Contract gate, quality/security and platform CI | Demonstrated: local checks/review and 14/14 CI jobs pass on `c49dcca` | None; package publication separate |

Source: #87 and pinned core 5.4.2 WriterConfig/PdfWriter. The additive synchronous
API supplies base bytes separately from the live PdfDocument containing new pages
and replacement metadata. Core supports classic output xrefs only; header/version
remain those of the base. Replacement affects the first N pages, retains the tail,
and is not a content overlay. Other document-level settings do not replace the
base catalog. Existing full-save APIs remain unchanged. The core path-only API
requires private RAII temporary storage. No automatic PR or package publication.

The core also drops the base trailer ID. A bounded final generated-trailer repair
will retain its original byte-string pair, after any generated Info repair; this
is necessary for valid incremental trailer continuity and never rewrites the base.

## Quality review — incremental writer (#87)

### Resumen ejecutivo

Scope: changes over develop `46f86c6`, including six product files, one new test
file, parity contract and two documentation files. The additive native export and
managed method use core 5.4.2 directly. Existing save signatures and behavior are
unchanged. Local review and all 14 platform CI jobs pass on implementation
`c49dcca520fef3669787d3c3788627e1a3028406`:
https://github.com/bzsanti/oxidize-pdf-dotnet/actions/runs/38768393315
Documentation-only closure preserves product/test/contract/workflow identity with
the validated implementation.
See [public contract and usage](INCREMENTAL_WRITING.md).

### Contratos y evidencia

61 focused .NET cases pass, including 12 new incremental-writer cases plus legacy
saves, merge/split Info repair, ABI symbol resolution and reflection-based parity
gates. Handwritten fixtures verify original generation-3 Info, absence of Info,
Unicode and custom source metadata, XMP reference, two identifiable base pages,
binary file IDs, append/leading-page replacement and untouched tail. Assertions
check the original prefix, every new classic xref entry's object/generation/offset,
each Prev pointer and repeated revisions. A handwritten stream-xref base is read
and extended. New-stream compression is observed in bytes and extracted text.
Replace covers all six Unicode fields and both identification policies; metadata-
only revisions, malformed IDs/PDF, invalid options, encryption, unsupported Annots
and disposed handles are covered. Failed writes leave the document reusable.

242 native tests pass, including three new boundary tests: malformed/unknown JSON
and enums, invalid writer combinations/version, null outputs/inputs, invalid UTF-8,
zero/impossible slice lengths, cleared outputs and document reuse after failure.
Clippy with warnings denied and format checks pass. Logs: /tmp/incremental-writer-*.log.

A compiled isolated copy always selects Replace and omits Unicode repair. Four
real-FFI cases fail for the intended reasons: two wrong Info-reference/absence
assertions and two mojibake strings. Production sources/runtime remain unchanged;
the baseline 61-case suite passes. Evidence: /tmp/incremental-writer-mutant-test.log
and /tmp/incremental-writer-mutant. This discriminates policy and encoding behavior,
not merely call success or output size.

Manual review traced all five WriterConfig fields, request validation, core append
and replacement paths, generated-only repairs, output ownership and failure mapping.
The native call holds a SafeHandle reference; checked managed copying frees the
buffer in finally. Slice length checks precede dereferencing, errors clear outputs,
and the private temporary directory cleans up on return/error/unwind. The generated
trailer repair inserts only hex byte strings after the final xref, leaving all
object/xref offsets and the entire original prefix intact. Both parsers and byte
assertions verify the resulting revisions. Existing four-field merge/split repairs
retain their field selection; their regression cases pass.

### Hallazgos

1. **Unicode replacement Info**: native/src/incremental_writer.rs:119 — core
   5.4.2 writes non-ASCII text as raw UTF-8 in PDF 1.x literals. The existing bounded
   generated-Info helper now accepts an internal field list; only this new workflow
   selects all six fields. Preserved source Info is never reinterpreted. Mutation
   evidence demonstrates the corruption without this correction.
2. **Trailer identifiers**: native/src/incremental_writer.rs:130 — the legacy
   core writer drops base /ID. Restore the original two byte strings in the final
   generated trailer, using hex encoding and preserving all offsets. Binary-ID and
   repeated-revision assertions cover the repair, including after Unicode repair.
3. **Recoverable malformed fixture**: dotnet/OxidizePdf.NET.Tests/IncrementalWriterTests.cs:198 —
   an initial test assumed a wrong Pages reference must fail; the core parser
   recovers it. The unsupported fixture now supplies a stream as Annots, which the
   writer explicitly rejects. The prior failure was a test-contract assumption,
   not evidence of an implementation fix.

No remaining confirmed defect within #87. Full-save Unicode behavior remains
unchanged as required by the issue's compatibility constraint. Core callback-style
overlay is not exposed by this page-document workflow; content overlays use their
existing operation APIs. Certification permission enforcement and configurable
total resource budgets are not claimed by this low-level core API; use the signing
policy APIs and track broader resource controls in #100. New document-level catalog
settings are outside this workflow and explicitly documented, rather than silently
represented as applied. No package publication is part of this delivery.

### Calidad de Tests (Kripteia)

Both mandatory scripts ran. Rust: 97, 242 tests/27 files; the new module has three
tests, score 97. C#: 95, 1015 methods/128 files; IncrementalWriterTests has eight
methods, score 100. Counts include theories once, unlike executed test-case counts.
Existing worst offenders include ffi_guard_unit_swallows_panic and Dispose_Twice_
DoesNotThrow (65); they are unchanged and outside #87. Scores supplement the
independent fixtures and compiled negative control.

### Análisis de Seguridad (Kripteia Security)

Rust reports 293 unsafe usages, mostly FFI declarations, and no emitted secret or
taint-flow findings. C# reports no security issues, with degraded parsing in 37
files including IncrementalWriterTests. Manual inspection covers the changed C#
syntax and the full native FFI boundary. The telemetry database is read-only; both
analyses completed and emitted their reports. A clean scan is not a guarantee
about unrestricted resource use or permission to modify signed PDFs.

### Métricas

- Archivos revisados: 10 changed files, plus core writer/parser and existing ABI consumers.
- Hallazgos totales: 3, reconciled above.
- Archivos de producto modificados por la revisión: 0; the unsupported test fixture was corrected.
- Verificaciones pendientes: none for #87 acceptance; package publication is separate.

# Completed increment — tagged preflight and preparation (#86), 2026-10-15

| Requirement / source | State / evidence | Blocker / next action |
| --- | --- | --- |
| Core 5.4.2 preflight and opt-in preparation | Demonstrated locally | Three additive methods; ordinary editing regression retained |
| Full preparation inventory separate from mutation plan | Demonstrated locally | Exact classic/stream-xref inventories, retired generation 3 and independent mutation inventory |
| Prefix, ownership, permissions, ambiguity and final validation | Demonstrated locally, including negative control | Real-FFI handwritten fixtures, policy/validation rejection and input snapshots |
| Typed options/results/errors and existing API compatibility | Demonstrated locally | Argument validation, typed native errors, legacy signatures/schema/exception retained |
| Contract gate, quality/security and platform CI | Demonstrated: local checks/review and 14/14 CI jobs pass on `7d7a5c0` | None; package publication separate |

Source: refreshed #86, first pending high-priority entry in #81, and pinned core
`writer/incremental_tagged_pdf.rs`. See [usage and precise contracts](TAGGED_PDF_EDITING.md#preflight-and-explicit-preparation).
Preflight performs recovery internally but returns only findings. Prepared methods
require explicit opt-in and preserve the core's strict final validation; ordinary
editing intentionally remains diagnostic. Recovery runs before language setup.
No automatic PR, dependency/version change, file output or package publication.
The existing uncommitted session handoff is preserved.

## Quality review — tagged preparation increment

### Resumen ejecutivo

Reviewed changes over develop `0b8905e`: additive preflight/prepared managed APIs,
options/results/errors, existing tagged FFI dispatch, behavioral tests and parity
contract. The core recovery/editor is used directly; no reconstruction or custom
repair algorithm is added. Local behavior and review are complete, including a compiled negative control;
all 14 platform CI jobs pass on `7d7a5c0aab8d7661cf30fd79d527c6d29df92def`:
https://github.com/bzsanti/oxidize-pdf-dotnet/actions/runs/38731361842
Documentation-only closure preserves product/test/contract/workflow identity with
the validated implementation.

### Contratos y evidencia

30 focused .NET cases pass (14 new), including classic and stream xrefs, sparse
missing derived indexes, missing author metadata, exact object inventories,
original generations of retired entries, repeated preparation, prefix preservation,
original content/private values, Unicode descriptions, language conflicts,
ambiguous MCID ownership, forbidden structural keys, blank descriptions,
encryption and all three DocMDP permission values. Input arrays/mutations/limits
are mutated after return to verify queued snapshots. Existing ordinary editor
semantics remain covered, including returning diagnostic invalid reports.

All 239 native tests pass, including three new tagged boundary cases. They reject
unknown/conflicting preparation fields, malformed JSON, null pointers, impossible
lengths and invalid UTF-8 with a cleared output. A language-only FFI test reparses
the output independently of report serialization to assert exact es-ES language
and preservation of an unrelated catalog value. Clippy with warnings denied and
format checks pass. Evidence is in /tmp/tagged-preparation-*.log.

A compiled disposable native copy omits with_recovered_indexes. Both inventory
cases then fail because absent ParentTree object 7 was not recovered, and the
already-indexed fixture fails Assert.Throws because invalid prepared bytes escape
without the recovery builder's strict final-validation guard. These are functional
failures through real FFI, not compilation/setup failures. Baseline tests pass;
production sources/runtime were not modified. Artifacts: /tmp/tagged-preparation-mutant
and /tmp/tagged-preparation-mutant-test.log.

Manual review traced options through JSON, recovery/language builders, planning,
strict apply, serialization and disposal. Preparation object identities are copied
without reinterpretation; subsequent plans are separate. The input prefix is
preserved by the core incremental writer. No native handles escape, pinned input
and returned CString are released once, and the output is cleared before failures.
Core policy rejects encryption/certification before preparation; incomplete final
validation never reaches serialization. Request serialization and byte copying
finish before Task.Run. Existing native error codes and managed exception types
are retained for old actions, and new actions expose typed error categories.

### Hallazgos

1. **Queued mutable input**: dotnet/OxidizePdf.NET/PdfTaggedOperations.cs:76 — the
   previous call captured caller PDF bytes directly. A snapshot now isolates queued
   work, while serialized edits/limits also retain their values. The real-FFI
   mutation-after-return test validates this boundary.
2. **Legacy request schema**: dotnet/OxidizePdf.NET/PdfTaggedOperations.cs:72 — an
   early implementation added preparation:null to ordinary calls, incompatible with
   the earlier native deny_unknown_fields schema. The field is now omitted for
   ordinary calls; old method signatures, payload shape and exception types remain.
3. **Failure output reset and slice length**: native/src/tagged_existing.rs:272 —
   the earlier FFI returned on null input before clearing writable output, and did
   not guard impossible slice lengths. Reset now precedes checks and lengths are
   bounded before from_raw_parts; direct FFI tests cover both failures.

No remaining confirmed product defect within #86. Configurable validation limits
do not govern the earlier core recovery traversal; core supplies its own safeguards.
This is documented rather than represented as a total budget; broader streaming and
resource controls belong to #100 and are not required for core contract parity.

### Calidad de Tests (Kripteia)

The skill script ran both analyses. Rust: score 97, 239 tests/26 files; changed
native tagged module: 3 tests, score 97. C#: score 95, 1007 tests/127 files;
TaggedPreparationTests: 10 methods, score 100. Existing worst offenders include
ffi_guard_unit_swallows_panic and Dispose_Twice_DoesNotThrow tests (65); these
remain outside this increment. Tool scores do not replace independent fixtures
or assertions discriminating inventory, policy and validation behavior.

### Análisis de Seguridad (Kripteia Security)

Rust lists 292 unsafe usages, primarily existing FFI declarations, with no emitted
secret or taint-flow findings. C# reports no security issues and degraded syntax
analysis in 36 files, including PdfTaggedOperations and TaggedPreparationTests.
Affected pointer/lifetime/policy paths were therefore inspected manually. Telemetry
could not write its sandboxed database, but both analyses completed; no scan is
claimed as proof of safety.

### Métricas

- Changed files reviewed: 7 implementation/test/contract/documentation files, with core/editor and existing model consumers traced.
- Confirmed findings: 3, corrected within this increment.
- Product files modified by the final read-only review: 0.
- Pending verification: none within #86; package publication is separate.

# Completed increment — split options (#102), 2026-10-15

| Requirement / source | State / evidence | Blocker / next action |
| --- | --- | --- |
| Core 5.4.2 defaults and metadata/optimize options | Demonstrated locally | Real-FFI Info assertions, including Unicode, absence and discard; optimization explicitly no-op |
| Named byte results without caller filesystem authority | Demonstrated locally | Private RAII directory; portable pattern checks; two compiled negative controls |
| Existing modes/APIs, owned bytes and typed failures | Demonstrated locally | All four modes, repeated ranges, snapshots, malformed input, page bounds and FFI output reset |
| Contract gate, review/security and platform CI | Demonstrated: local checks/review and 14/14 CI jobs pass on `4191f2a` | None; package publication remains separate |

Source: refreshed GitHub #102 and pinned core `operations/split.rs` (5.4.2).

```csharp
var parts = await PdfOperations.SplitNamedAsync(pdf, new PdfSplitOptions
{
    Mode = PdfSplitMode.Ranges,
    Ranges = [(0, 2), (4, 4)],
    OutputPattern = "part-{n}-{start}-{end}.pdf",
    PreserveMetadata = true,
    Optimize = false
});
// parts[0].FileName == "part-1-1-3.pdf"; parts[0].Bytes are caller-owned.
```

The existing SplitAsync overloads and four modes remain available. Options-based
splits now snapshot input bytes and selectors before queueing. Cancellation affects
queued work only. SplitNamedAsync returns independent owned byte arrays with
suggested filenames; callers decide whether/where to persist them. No filename
from the request enters filesystem operations. Private RAII temporary storage
cleans inputs and partial outputs on success, errors and unwinding.

Defaults match core: PreserveMetadata=true, Optimize=false, OutputPattern="page_{}.pdf".
Only Title/Author/Subject/Keywords are copied; writer-generated Info may remain
when preservation is false. XMP/custom Info and structural preservation are outside
this reconstructing contract; use policy-driven PdfExistingDocumentOperations for
those guarantees. Generated Unicode Info uses the existing bounded UTF-16BE repair
from #103. Core 5.4.2 never reads Optimize: true is an explicit no-op, and named
results report OptimizationApplied=false. No size reduction is promised.

Names use one-based numbers: {n} is output ordinal, {} is page for SinglePages or
start-end for range-based modes (including 2-2 for one-page ranges). {page} is valid
only for SinglePages; {start}/{end} only for other modes. Unsupported tokens,
separators, absolute paths, control characters, Windows device names and trailing
space/dot are rejected; each resolved name is limited to 200 UTF-8 bytes. Named
results reject duplicate names after lowercase comparison; use {n} for repeated
ranges. Constant patterns are valid for one output. Existing byte-list results
continue to allow repeated ranges. SplitAt retains core ordering and ignores zero
and out-of-bounds points; it does not sort or deduplicate.

Managed shape validation throws argument exceptions; native failures throw
PdfSplitException (derived from PdfExtractionException), with InvalidInput,
InvalidPdf, Io, Serialization or NativeFailure. C ABI symbol/signature and default
JSON array-of-base64 response remain unchanged; optional named=true selects named
objects. Unknown/irrelevant legacy JSON fields retain their prior ignored behavior.
No general parser resource budget, immediate cancellation or global writer fix is
claimed. No dependency/package version changes.

## Quality review — split options increment

### Resumen ejecutivo

Scope: #102 changes over develop `808aaef8`, including new split implementation,
managed API/results/tests, shared generated-Info helper visibility and parity
contract. No unrelated operations were redesigned. Acceptance is supported by real FFI, compiled negative controls and all 14 platform
CI jobs on `4191f2a81be0cf060a6e53a6549733a4b81a14d1`:
https://github.com/bzsanti/oxidize-pdf-dotnet/actions/runs/38708577065
Documentation-only closure preserves product/test/contract/workflow identity with
that validated commit. No automatic PR or package publication.

### Contratos y evidencia

236 native tests and 123 focused .NET cases pass. Regression includes split options, existing
operations, metadata merge, structural policies, parity and ABI. Assertions inspect
serialized PDF metadata/text/page counts using an independent handwritten source
fixture, plus ordering, duplicate ranges, owned buffers, snapshots, cancellation,
invalid patterns, invalid PDF and partial range failure/recovery. Native tests
exercise malformed JSON, zero/oversized chunk size, reversed/empty ranges, null
pointers, invalid UTF-8, impossible lengths and legacy JSON defaults. Clippy with
warnings denied and whitespace checks pass.

Two compiled disposable negative controls in /tmp/split-options-mutant fail for
intended assertions: bypassing portable-name validation accepts ../escape-{n}.pdf
(return code 0 instead of InvalidArgument); passing the caller pattern to core
writes split-<unique>-1.pdf into the mutant working directory, detected by the
filesystem isolation assertion. Production sources/runtime remain unchanged by
these experiments. Evidence: /tmp/split-options-mutant.log and
/tmp/split-options-isolation-mutant.log. Baseline logs use /tmp/split-options-*.

Manual review traced all request fields through JSON/core/naming/result decoding,
source snapshots, thread-local errors, pointer preconditions and output reset,
exactly-once buffer release, private paths and partial-error cleanup. Naming is
metadata even if validation regresses: core receives only trusted internal tokens.
The Unicode repair's implementation is unchanged; #103 regression remains relevant.

### Hallazgos

1. **Zero chunk core loop**: native/src/split_options.rs:89 — core can loop forever
   for zero and overflow for enormous chunks. Resolved by managed/native bounds;
   native FFI tests reject zero and usize::MAX before core dispatch.
2. **Temporary path ownership**: native/src/split_options.rs:111 — old split used shared
   timestamp paths and removed only successfully-read outputs. Replaced for this
   options operation by private RAII storage and unique ordinal paths, retaining
   duplicates without overwrite. Isolation mutation and partial-failure tests pass.
3. **Portable device names**: native/src/split_options.rs:44 — simple separator checks
   miss Windows reserved names, including superscript device digits and padded
   stems. Explicit rejection and real-FFI examples now cover these cases.
4. **Enum contract representation**: docs/parity-contract.json:383 — initial Mode
   default used text instead of the gate's numeric representation. Corrected to 0;
   the initial 119-pass/1-fail run was a contract registration failure, not split
   behavior. The subsequent 120-case run passed before additional boundary cases.

No unresolved implementation finding in scope. Legacy no-options split and other
old temporary-file helpers remain outside #102; the updated options API does not
claim their cleanup behavior has changed. Broader writer issues remain in #87.

### Calidad de Tests (Kripteia)

Both skill-script analyses executed. Rust: score 97, 236 tests/25 files; new
split_options.rs: 4 tests, score 98. C#: score 95, 997 tests/126 files; new
SplitOptionsTests: 6 methods, score 100. Existing worst offenders include
ffi_guard_unit_swallows_panic (65), PdfDocumentBuilderTests.Dispose_Twice_DoesNotThrow
(65) and PdfFlowLayoutTests.Dispose_Twice_DoesNotThrow (65). The tool's suggestions
about hardcoded expectations do not invalidate independent behavioral oracles.

### Análisis de Seguridad (Kripteia Security)

Rust reports 292 unsafe usages, mainly existing FFI declarations; no secret or
taint-flow findings emitted. Changed pointer handling was reviewed manually.
C# reports no security issues, with degraded syntax analysis in 34 files, including
PdfOperations.cs and MergeMetadataTests.cs. Manual tracing covers these affected
boundaries; the scan is supporting evidence, not proof. Telemetry writes were
read-only in the sandbox, but both analyses completed successfully.

### Métricas

- Reviewed: 10 implementation/test/contract files plus this documentation.
- Confirmed findings: 4, resolved within the authorized increment.
- Product files modified by the final read-only review: 0.
- Pending verification: none within #102. Package publication is separate.

# Completed increment — merge metadata modes (#103), 2026-10-14

| Requirement / source | State / evidence | Blocker / next action |
| --- | --- | --- |
| OPS-013 / core 5.4.2 MetadataMode: four variants, default FromFirst | Demonstrated | Additive MergeWithMetadataAsync; legacy overload/default-literal tests |
| Source identity, custom fields, invalid indexes and empty selections | Demonstrated | 16 real-FFI cases, including third input, page order and zero-page source |
| Info copying vs structural preservation/XMP | Demonstrated | Structural-policy regression, source XMP/private Info omitted by reconstruction |
| Owned result, snapshot inputs, typed failures and cleanup | Demonstrated | Invalid arguments/PDF/pages, FFI reset tests, private RAII temporary inputs |
| Unicode values survive core serialization | Demonstrated | UTF-16BE repair of generated Info; all source modes/remerge and independent qpdf |
| Parity gate, quality/security and platform CI before integration | Demonstrated: local checks/review and all 14 CI jobs pass on `7a9ac2a` | None |

`PdfOperations.MergeWithMetadataAsync(inputs, options, ct)` returns an owned PDF
byte array. `PdfMergeMetadataOptions.Mode` supports FromFirst (default),
FromDocument (requires zero-based SourceIndex), Custom (requires Custom fields),
and None. Irrelevant fields, unknown modes and invalid source indexes are rejected.
The distinct method name preserves `MergeAsync(inputs, default)` overload resolution.

```csharp
var merged = await PdfOperations.MergeWithMetadataAsync(inputs,
    new PdfMergeMetadataOptions { Mode = PdfMergeMetadataMode.FromDocument, SourceIndex = 2 });
var custom = await PdfOperations.MergeWithMetadataAsync(inputs,
    new PdfMergeMetadataOptions { Mode = PdfMergeMetadataMode.Custom,
        Custom = new PdfMergeMetadata { Title = "Informe", Author = "Equipo" } });
```

Only Title, Author, Subject and Keywords are copied. Null custom fields are
omitted; empty strings are values. None does not remove writer-generated Info.
SourceIndex addresses the original input list independently of selected pages;
a zero-page input can supply metadata. Empty input lists and explicit empty page
lists are rejected. Source Info reading retains the core's best-effort behavior:
missing/unreadable fields are omitted, not taken from another input.

This reconstructing API does not preserve XMP, bookmarks, forms or arbitrary Info
keys. `PdfExistingDocumentOperations` and its preservation reports/policies remain
separate: ReconstructWithFirstInputMetadata is also not XMP preservation.
Input bytes, page selectors and options are snapshotted before scheduling;
cancellation affects queued work only. Core file-based merge uses private temporary
inputs with RAII cleanup. No caller-specified files are written. PdfMergeException
(derived from PdfExtractionException) categorizes native failures; managed argument
validation uses standard argument exceptions. No total parser resource-budget or
interruptibility guarantee is introduced.

## Quality review — merge metadata increment

### Resumen ejecutivo

Reviewed implementation `7a9ac2a99a47bbc3910e85a94f511f8363997555` against `fb48c77`,
core 5.4.2, managed 0.19.0 and native 0.14.0. Local review/verification and all
14 platform CI jobs are complete before integration. No dependency/version change,
automatic PR or package publication. Two implementation findings below are fixed.

### Contratos y evidencia

38 focused .NET cases pass: 16 merge-metadata cases, existing structural-operation
regression, parity contract and ABI exports. Independent hand-written PDF fixtures
contain distinguishable source Info, page labels in content, private Info and XMP.
Assertions read serialized results through real FFI, including non-first source,
page range/list order and duplicates, Unicode/empty/null custom values, missing
source Info, zero-page metadata source, invalid indexes/combinations, malformed
PDF, page bounds, cancellation and snapshots of mutable caller inputs.

All 232 native tests pass, including malformed JSON, negative/out-of-range source
indexes, conflicting mode fields, base64/empty inputs, empty/reversed page ranges,
null pointers, invalid UTF-8 and resetting each writable output independently.
Denied-warning Clippy, Rust/.NET formatting and whitespace checks pass.

A compiled disposable copy in `/tmp/merge-metadata-mutant` forces SourceIndex to
zero: the selected-input theory fails for indexes 1 and 2 on `second-title` and
`third-title` versus actual `first-title`; index 0 still passes. The reviewed tree
and runtime are unchanged. The Unicode test initially failed on mojibake before
the repair and now passes for all source modes and a second merge. Independently,
`qpdf --check` finds no syntax/stream errors and qpdf JSON decodes all four custom
Unicode fields exactly, including supplementary characters and escaped syntax.
Evidence: `/tmp/merge-metadata-*.log` and `/tmp/merge-metadata-qpdf.json`.

### Hallazgos

1. **Ignored fields on fieldless native modes**: `native/src/merge_metadata.rs:22` —
   Serde's unit variants accepted an irrelevant index despite deny_unknown_fields.
   Resolved with empty struct variants; the real FFI now rejects the request and
   leaves outputs null/zero. Existing legacy JSON types keep their accepted schema.
2. **Core writer corrupts Unicode Info**: `native/src/merge_metadata.rs:113` —
   core 5.4.2 writes String metadata as raw UTF-8 in PDF 1.x, which its reader
   correctly interprets as PDFDocEncoding. The new operation repairs only its
   freshly generated Info using a single incremental revision: non-ASCII selected
   fields become UTF-16BE hex strings, all remaining generated Info bytes remain
   intact, names are escaped and xref/root/size/previous-offset are explicit.
   It rejects unexpected generated value/trailer shapes. There is no arbitrary
   input editor, global writer patch or XMP conversion. The pinned core fix can
   later replace this bounded workaround. Legacy writer Unicode behavior remains
   outside this increment; record for the writer/Info audit in #87, without
   claiming the new repair fixes unrelated APIs.

No unresolved finding within #103. Manual review traced option validation, core
selection, stable input indexes, snapshot serialization, thread-local error fetch,
pointer preconditions, panic containment, checked output length and exactly-once
buffer release even on managed copy failure. Only internally generated paths enter
filesystem writes. Temp-directory destruction follows success/error/unwind. The
Info revision accepts only the fresh, unencrypted classic-xref writer shape and
string fields of the pinned core; arbitrary input dictionaries are never copied
into its syntax. Partial-failure cleanup is reasoned from RAII, not fault-injected
filesystem failures. Legacy temporary-path behavior was not changed; new calls
use the private-directory pattern already used by structural operations.

### Calidad de Tests (Kripteia)

Rust score 97, 232 detected tests/24 files; new boundary tests score 100. C# score
95, 991 detected tests/125 files; new class score 100 (11 methods/16 cases). Prior
worst scores are in unchanged panic/disposal tests, already tracked by prior
reviews. Scores do not establish provenance or Unicode correctness; actual output
assertions, the negative control and the independent PDF reader provide that evidence.

### Análisis de Seguridad (Kripteia Security)

Rust lists 292 unsafe usages across the native tree; new FFI pointers, ownership
and error paths were manually reviewed and boundary-tested. No new secret,
taint-flow or dangerous-function finding. C# reports no security issues with
partial/error syntax in 34 files, including the new test class; compilation and
manual review supplement that coverage limitation. Both mandatory analyses
completed; their telemetry warnings do not prevent analysis.

### Métricas

- Changed implementation/test/contract files reviewed: 8, plus supporting consumers/core.
- Confirmed findings: 2, both resolved before the final read-only review.
- Product files modified during the final read-only review: 0.
- Pending verification: none.
- Platform CI: all 14 jobs pass on the implementation SHA:
  https://github.com/bzsanti/oxidize-pdf-dotnet/actions/runs/38641793379
- The closure commit changes documentation only; implementation/test/contract
  tree identity with the validated SHA is verified before pushing develop.

# Completed increment — named image XObjects (#97), 2026-10-14

| Requirement / source | State / evidence | Blocker / next action |
| --- | --- | --- |
| READ-015 page/name selection and source identity | Demonstrated | Exact names, aliases, two images, inherited/indirect resources and identities |
| Encoded/decoded metadata and existing count/pixel budgets | Demonstrated | Owned payload/sample data, ordered dictionary, pixel/byte exact boundaries |
| Inherited/missing/non-image/unsupported-filter behavior | Demonstrated | Typed errors; unrelated corrupt/unsupported resources are not extracted |
| Explicit nested Forms; preserve batch APIs | Demonstrated | Two-level paths, nearest/absent/empty Resources, cycles and batch PNG regression |
| Typed contracts, real FFI, parity gate, quality/security and CI | Demonstrated: all local checks/review and all 14 CI jobs pass on `0eb2c6f` | None |

Use #96 resource ownership lookup and #95 document-aware bounded decoding. Output
is a resource extraction, not a rendered PNG: decoded samples retain packing,
color-space/Decode/mask metadata; JPEG payloads remain JPEG and are not decoded.
This avoids reimplementing the private batch PNG converter or processing unrelated
images. Explicit Form paths resolve each Form's resource dictionary; an absent
Form Resources uses the current enclosing dictionary, while an empty one masks it.
No automatic Form search, inline-image scan, graphics-state transform, mask
composition or palette/color conversion. These transformations are outside the named-resource contract; existing batch
extraction remains unchanged. Budgets apply after core parsing, not to its total allocations.

## Quality review — named image increment

### Resumen ejecutivo

Scope: `named_images.rs`, shared resource/stream helpers and module registration,
`PdfImages.cs`, independent real-FFI fixtures and parity contract. Source is the
implementation `0eb2c6fc06db0d87108f7feebd77835b61937db4`. No runtime
dependency or core version change. Local review/verification and all platform CI
are complete. No automatic PR or package release.

### Contratos y evidencia

`PdfImages.ExtractAsync` selects one zero-based page and exact decoded PDF name.
An explicit list of Form names traverses resources, not drawing operators. No
recursive search or batch extraction is used. Each successful call consumes one
image count slot. Effective page Resources comes from #96; absent Form Resources
inherits the enclosing dictionary, present empty Resources masks it, invalid
Resources errors, and a repeated Form object in the path is rejected. Resource
owner, dictionary, image and all Form identities are retained where indirect.

Encoded payload and decoded packed samples remain separate, with full declared
metadata (including ordered Filter/DecodeParms, ColorSpace, Decode and masks).
PNG conversion, palette/color processing, Decode mapping and mask composition are
not applied. Standalone DCT is explicit JPEG passthrough: it does not claim JPEG
integrity, header dimensions or decoded pixels. Pixel bounds check declared PDF
width × height using u64 arithmetic, as in the existing core image limits. No
image sample-geometry or whole-document validity claim is made. Unsupported
bounded filters, including chained DCT, fail explicitly rather than skip the image.

The stream decoder and recovery diagnostics are the same path as #95, including
indirect predictors and the strict empty-Filter guard. Recovery is opt-in. Input,
Form names, nested options and parser settings are snapshotted before scheduling;
returned bytes/JSON values are owned. Cancellation affects queued work only. The
100 MiB input cap and post-parse budgets are not a whole-parser time/memory sandbox.

58 focused .NET cases pass: 19 named-image cases, 10 raw streams, 20 provenance,
6 parity contract, 1 ABI and 2 existing batch-bound cases. The new batch regression
also confirms two valid PNG outputs while named selection returns only requested
samples with MaxImages=1. All 230 native tests, denied-warning Clippy, Rust/.NET
format checks and whitespace checks pass. A compiled copy in
`/tmp/named-images-mutant` replaces the caller's pixel limit with long.MaxValue;
`ImageCountPixelAndByteLimitsAreEnforcedBeforeDecode` fails at its expected Throws
assertion (no exception). The original passes; reviewed source/runtime was not
mutated. A second assertion checks the pixel guard before a corrupt Flate decoder.

### Hallazgos

1. Preserve budget categories across shared helpers: the initial ancestor helper
   returned only text, and the metadata helper used stream-oriented error codes.
   Mapping both generically would mislabel inspection limits as invalid data or
   stream decoding errors. Resolved with internal `ResourceLookupError::DepthLimit`
   (`native/src/resource_provenance.rs:35`) and `inspection_error`
   (`native/src/named_images.rs:41`), while retaining the existing public provenance
   messages and actual decoder classifications. Real-FFI ancestor-limit and
   metadata-limit tests verify LimitExceeded, with no invented stream error for
   metadata inspection. Existing provenance regression passes.

No unresolved finding. A dangling reference that core resolves to PDF null is
rejected as NotImage/NotForm; the wrapper does not invent a parser failure for it.
Manual review covered selectors and shadowing, shared-helper consumers, checked
integer bounds, per-chain cycle limits, metadata budgets before recursive JSON,
output reset, raw pointer/length preconditions, panic containment, same-thread
native error retrieval, CString transfer/free and pinned-input finally cleanup.
The test-only JPEG marker payload deliberately demonstrates passthrough, not an
unperformed JPEG integrity check. Unrelated resources are not decoded or validated.

### Calidad de Tests (Kripteia)

Rust score 97, 230 tests/23 files; new native tests score 100. C# score 95,
980 detected tests/124 files; new image class score 98 (12 methods/19 cases).
The two claims that expression-bodied theories do not call production code are
false positives: both await `Fails`, which calls `PdfImages.ExtractAsync` through
real FFI and asserts the exact error category. A duplicated Compress fixture
helper is intentional standard ZLibStream setup for independent PDF fixtures;
it does not duplicate the production decoder or weaken the oracle. Prior worst
scores remain in unchanged panic/disposal tests and their existing review backlog.

### Análisis de Seguridad (Kripteia Security)

Rust reports 291 unsafe usages across the native tree; the new FFI's ownership
and boundary invariants were manually traced and boundary-tested. C# reports no
security issues but partial/error syntax in 33 files, including the new API/tests;
compilation/manual review supplement that coverage limitation. Both mandatory
analyses completed successfully despite read-only telemetry warnings. No new
secret, taint-flow or dangerous-function finding was reported for this increment.

### Métricas

Seven changed implementation/test/contract files reviewed plus core dependencies
and existing FFI/bootstrap helpers. One contract-mapping finding corrected before
final verification; zero unresolved. Review passes were read-only; the correction
was an authorized implementation step. All 14 platform CI jobs passed before
integration on the implementation revision:
https://github.com/bzsanti/oxidize-pdf-dotnet/actions/runs/38630658308
The closure commit only updates documentation; product/test/contract/workflow
identity with the validated SHA is verified, so redundant CI is skipped. Evidence: `/tmp/named-images-{native-tests,dotnet-tests,
clippy,mutation}.log`, `/tmp/named-images-kripteia-{rust,csharp}.log` and disposable
mutation directory. Package publication is separate.

# Completed increment — raw streams and bounded decoding (#95), 2026-10-14

| Requirement / source | State / evidence | Blocker / next action |
| --- | --- | --- |
| READ-017 raw bytes, ordered Filter/DecodeParms and identity | Demonstrated | Object/page reads preserve payload, typed declared metadata and identity |
| Explicit bounded decoding/recovery | Demonstrated | Strict/recovery behavior, indirect predictors, all diagnostics, exact stage limits |
| Complete inputs/ownership, unchanged legacy API | Demonstrated | Snapshot/cancellation/legacy tests and native pointer/length checks |
| Behavior, failure and boundary evidence | Demonstrated | 25 focused .NET cases, 228 native tests; compiled budget mutant rejected |
| Parity gate, quality/security, all platform CI before integration | Demonstrated: local review and all 14 CI jobs pass on `0e38e76` | None |

Scope: expose core 5.4.2 stream semantics; zero-based page/content selectors in
`PdfStreams` follow `PdfTextExtraction`. Raw bytes are the parser's encoded payload,
not a physical file slice. Inspection budgets apply after core parsing; decoding
bounds every filter stage according to core, not total parser allocation/time.
References in declared metadata remain references. Recovery is opt-in and never
converts errors or budget exhaustion into successful truncated output. #97 remains
separate and will consume this document-aware decoding foundation.

## Quality review — raw streams increment

### Resumen ejecutivo

Scope: `native/src/streams.rs`, module registration and shared ParserSettings;
`PdfStreams.cs`, real-FFI tests and parity contract, plus core 5.4.2 stream/objects/
filter/recovery code and existing native error/free/bootstrap helpers. No new
runtime dependency. Existing extraction entry points are unchanged. Source is the
implementation `0e38e7610144ce35cb7e172343a5a70068eb77af`; platform acceptance is complete.

### Contratos y evidencia

Owned encoded payloads and declared ordered metadata are separate from decoded
results. Reference metadata is intentionally unresolved; document-aware decoding
resolves DecodeParms (tested with a PNG predictor that changes output). Strict
errors do not become recovery; opt-in recovery reports both Unverified and
Incomplete with the exact filter index and source error. Invalid Flate, predictor
failures, unsupported bounded filters and non-Flate budget errors preserve core
classification. No operation returns partial success after a budget failure.

Limits apply per filter stage for decoding and across returned streams for encoded
payload/metadata. Node/name/depth/reference/count boundaries are exercised through
real FFI, including stopping at an exhausted payload budget before resolving a
later invalid reference. Core parsing and its caches precede these budgets: this
is not a whole-parser memory/time sandbox. Input is capped at 100 MiB. Cancellation
only prevents queued native calls. Results own their byte arrays and JSON values.

Validation: 25 focused cases (10 stream cases, 8 text recovery, 6 contract, 1 ABI),
228 native tests, Clippy with denied warnings, Rust/.NET format and whitespace
checks. In `/tmp/raw-streams-mutant`, forcing MaxDecodedBytes to 16 MiB in the
request producer makes BOTH strict/recovery exact-limit cases fail because no
exception was thrown; original sources pass. The mutation compiled and loaded
the real native library, and did not modify the reviewed runtime or product tree.

### Hallazgos

1. Aggregate encoded-byte checks originally happened after collecting every
   resolved stream. Repeated references could accumulate copies before rejection.
   Corrected in `native/src/streams.rs:83` (`as_stream`, called by `select`) by consuming the budget before
   retaining each stream; the early-stop test distinguishes this behavior.
2. Core 5.4.2 `filters.rs::decode_stream_with_limit` returns empty bytes for a
   nonempty payload with `/Filter []`. The new strict path rejects that case with
   DecodeError (`native/src/streams.rs:245`) instead of publishing silent data loss. Raw inspection remains
   available; explicit core recovery preserves the bytes. Tested and documented.
   Upstream correction belongs to a future core upgrade; this increment fails
   explicitly and does not require patching the pinned core or changing legacy APIs.

Both findings are resolved within this increment; no unresolved local finding.
Manual review traced NUL/pointer/length preconditions, output reset, panic guard,
owned CString transfer/free, same-thread error retrieval, managed snapshots,
reference cycles, bounded metadata traversal before recursive JSON conversion,
checked budget subtraction and error classification. No external I/O is added.

### Calidad de Tests (Kripteia)

Rust score 97, 228 tests/22 files; new native tests score 100. C# score 95,
968 detected tests/123 files; new class score 100 (9 methods/10 expanded cases).
Existing lowest-score panic/disposal tests belong to the prior review backlog;
they are unchanged and do not determine this API's acceptance. Independent PDF
fixtures and the compiled negative control supplement the heuristic scores.

### Análisis de Seguridad (Kripteia Security)

Rust reports 290 unsafe usages across the native tree; the new FFI was manually
traced above. C# reports no security issues, with partial/error syntax coverage
in 31 files, including the new API/tests; compilation and manual inspection cover
those limitations. Both test-quality and security analyses completed; read-only
telemetry warnings do not stop either analysis. No new secret, taint-flow or
dangerous-function finding was reported for this increment.

### Métricas

Six changed implementation/test/contract files reviewed plus existing helpers
and core dependencies. Two findings corrected; zero unresolved. Review passes
were read-only; corrections were a separate authorized implementation step.
All 14 platform CI jobs passed on the implementation SHA before integration:
https://github.com/bzsanti/oxidize-pdf-dotnet/actions/runs/38613769172
The closure commit changes documentation only and verifies product/test/contract/
workflow identity with that SHA; it skips redundant CI. No package publication.
Evidence: `/tmp/raw-streams-{native-tests,dotnet-tests,clippy,mutation}.log`,
`/tmp/raw-streams-kripteia-{rust,csharp}.log` and disposable mutation directory.

# Completed increment — resource provenance (#96), 2026-10-14

Implementation: `72c880f`.
[Successful platform CI](https://github.com/bzsanti/oxidize-pdf-dotnet/actions/runs/38555998665)
covers eight native builds, four .NET test jobs, lint and security. All passed.
#96 meets its implementation/integration gate; no package release is requested.

| Requirement | State / evidence | Blocker / closure evidence |
| --- | --- | --- |
| Direct/inherited/absent resource identity | Implemented by `GetPageResourceProvenanceAsync` | Local behavior tests pass |
| Resolved view without altering old API | Demonstrated: sorted entries, references and resolved summary | Old flattened API comparison and empty override tests pass |
| Bounded malformed-input behavior | Demonstrated: cycle/type/reference errors and exact limits | Native boundary tests and real-FFI rejection cases pass |
| Real behavior and parity enforcement | Demonstrated: 20 resource cases + existing geometry/contract/ABI | 35 focused tests pass; 226 native tests pass |
| Quality/security and platform CI | Local review complete and all 14 platform jobs passed | None |

Use Rust 5.4.2 parsed-page/object APIs. `inherited_resources` alone cannot identify
provenance because the core also uses it for resolved direct references. Walk only
the parent chain needed to find the nearest `/Resources`; never merge dictionaries
across ancestors. A present but invalid dictionary errors; an empty one masks parents.
The new resolved view lists standard named resource categories without loading
referenced resource contents. ProcSet/custom non-dictionary categories remain keys.
#97 can reuse the effective-resource lookup. Next implement #95 before #97:
core image extraction prepares entire page batches and its preparation helper is
private; the public document-aware bounded stream decoder should be exposed once
and reused for named-image selection, avoiding a second decoding path.

### Quality review — resource provenance increment

Scope: native `resource_provenance.rs` and module registration; managed
`GetPageResourceProvenanceAsync`, P/Invoke, models/options; 20 real-FFI cases,
parity contract and existing pinned-JSON/free/error helpers. Core 5.4.2 object
lookup and page inheritance were inspected as the source of truth. No new
runtime dependency or changes to existing resource APIs.

Contracts/evidence: independent PDF trees distinguish direct inline/indirect,
nearest ancestor/root, absent versus empty Resources and per-page overrides.
Standard categories produce sorted names and declared object/generation references;
referenced resource contents are deliberately not loaded (including a declared
unresolvable entry reference). ProcSet/custom category values are not interpreted.
Malformed required parents/resources/categories fail; ancestors beyond the owner
are irrelevant. Limits bound this inspection after core page parsing, not the
core parser's earlier work. All returned names count against the UTF-8 budget,
including repeated category/font names; JSON syntax/escaping is excluded.

Local validation: 35 focused .NET cases pass (20 provenance, 8 geometry, 6 contract,
1 ABI); all 226 native tests pass; Clippy with denied warnings, Rust formatting,
.NET formatting and diff whitespace checks pass. The entry-budget negative control
was compiled in `/tmp/resource-provenance-mutant`: replacing only managed
MaxEntries forwarding with 100,000 makes
`TraversalEntryAndNameBudgetsHaveExactBoundaries` fail at the expected
Throws assertion (no exception). Its original source/runtime passed. The mutant
used separate managed binaries and never modified the reviewed tree/runtime.

Findings: no unresolved finding in this increment. Manual review covered output
reset before errors, valid-pointer preconditions, checked integer narrowing,
panic containment, same-thread error retrieval, successful CString ownership
transfer and finally-based freeing, cloned input/options before scheduling,
parent-cycle detection and budget checks before accumulating reported names.
The parser's own lenient recovery/caches remain governed by the core and file-size
cap; the wrapper does not claim a whole-parser allocation/time bound. Raw resource
references are identities, not a claim that the referenced image/font is valid.
These explicit limits avoid misleading completeness/security guarantees.

Kripteia test quality: Rust 97, 226 tests/21 files; C# 95, 959 detected tests/122
files. The new C# test class scored 100 (8 methods, 20 expanded cases). The new
native boundary test scored 90 with a false "happy path only" claim: it explicitly
asserts NullPointer, InvalidArgument, PdfParseError and cleared outputs. Its
multiple assertions report actual return codes at individual call sites. Existing
worst scores (Rust panic guard 65 and C# disposal tests 65) concern unchanged code;
the existing review backlog owns them, not the acceptance of this read-only API.

Kripteia security: Rust reported 289 unsafe usages across the FFI, including the
new entry point, manually traced as above. C# reported no security issues but
partial/error syntax in 29 files, including the new model/test files; manual
inspection and compilation supplement that incomplete analysis. Both analyses
completed successfully despite read-only telemetry warnings. No new secret,
taint-flow or dangerous-function finding was emitted for this increment.

Metrics: 7 changed code/test/contract files reviewed plus existing FFI helpers and
core lookup dependencies; 0 unresolved findings; 0 product files modified by the
read-only review. Platform CI passed all 14 jobs on `72c880f` before integration.
Subsequent edits only update documentation; runtime/test/configuration trees are
verified byte-identical to this validated revision, so the closure commit skips
redundant CI execution.
Evidence: `/tmp/resource-provenance-native-tests.log`,
`/tmp/resource-provenance-kripteia-rust.log`,
`/tmp/resource-provenance-kripteia-csharp.log`, and the disposable mutation above.


# Completed increment — page geometry (#105), 2026-10-14

Implementation commits: `bb7a6e9` and `f5baa0e`.
[Successful platform CI](https://github.com/bzsanti/oxidize-pdf-dotnet/actions/runs/38545842115):
eight native platform builds, four .NET test jobs, lint and security, all passed.
No package release was requested. #105 meets its implementation/integration gate.

Sequence: existing-page geometry (#105), then resource provenance (#96) before
named images (#97) and bounded stream access (#95). Geometry is independent;
resource identity/lookup should be shared by the later reader capabilities.
Remaining issues stay open under #81; no total parity claim.

| Requirement | Evidence / state | Blocker / closure evidence |
| --- | --- | --- |
| Typed geometry, preserve tuple API | Implemented: `GetPageGeometryAsync` / `oxidize_get_page_geometry` | Demonstrated: local tests and platform CI pass |
| Indexing and ownership | Demonstrated: one-based indexes, configured size cap, owned snapshot | Invalid indexes/input, cancellation and snapshot tests pass |
| Real FFI behavior | Demonstrated: 8 geometry cases through real FFI | Four rotations, nearest inheritance/page override, absent crop/rotation, bounds and ownership |
| Contract, quality/security, platform CI | Contract gate, local review and all 14 CI jobs pass | None |

Scope follows Rust 5.4.2 `ParsedPage`: MediaBox and CropBox are the typed boxes
supported by the parser. BleedBox/TrimBox/ArtBox are not parsed geometry fields.
No normalization of raw rotation beyond the core's behavior is introduced.

### CI blocker discovered and correction

Initial CI run [38544224542](https://github.com/bzsanti/oxidize-pdf-dotnet/actions/runs/38544224542)
passed eight native builds, lint/security and three .NET platforms. macOS stopped
at restore: test-only `System.Security.Cryptography.Pkcs` 10.0.11 was flagged by
NU1902/NU1903 for advisories published 2026-10-13. Upgrade that test dependency
to 10.0.13, the patched version for both
[GHSA-2fmv-rv2c-59x7](https://github.com/advisories/GHSA-2fmv-rv2c-59x7) and
[GHSA-846f-f764-76mx](https://github.com/advisories/GHSA-846f-f764-76mx).
This is necessary to pass the required security/platform gate; the product
library has no PKCS dependency. No audit suppression or test weakening.
Restore and 28 targeted geometry/contract/signing/slot tests pass, including
real CMS creation and CheckSignature. Platform CI subsequently passed all 14 jobs.
The source-level geometry review below remains applicable; only the test project
package reference changes. Initial failure log: `/tmp/geometry-ci-failed.log`.

### Quality review — geometry increment

Scope: `native/src/parser.rs`, managed `NativeMethods.cs`, `PdfExtractor.cs`,
`Models/PageGeometry.cs`, `PageGeometryTests.cs`, parity contract and CI trigger.
Reviewed consumers: existing pinned JSON helper, thread-local error retrieval,
`oxidize_free_string`, Rust `ParsedPage` and inheritance resolution. Existing
APIs remain unchanged. No architecture expansion. The subsequent test-only PKCS security update is documented above.

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
findings; 0 product files changed during the read-only review. Platform validation passed all 14 jobs on `f5baa0e`. Subsequent changes
are documentation only; runtime, tests, dependency pins and CI configuration
remain byte-identical to the validated revision. Full logs are retained in `/tmp/geometry-kripteia-rust.log`,
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
| NEW-007 / medium | `writer/pdf_writer/mod.rs`: `IncrementalInfoPolicy` | Implemented in #87: explicit incremental base bytes, append/leading-page replacement, Preserve/Replace Info; see [contract](INCREMENTAL_WRITING.md) |
| NEW-008 / low | `fonts/resolved.rs`, `text/cid_to_unicode.rs`, private `text/extraction_cmap.rs` | Implemented in #88: five Adobe collections, complete glyph identities/Unicode and compact vertical tables; [contract](FONT_INSPECTION.md) |

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
| DOC-020 | Save with config (WriterConfig) | yes | Existing full-save configuration plus explicit base-document incremental workflow (#87); classic incremental output only |
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
