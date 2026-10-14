# Active increment — named image XObjects (#97), 2026-10-14

| Requirement / source | State / evidence | Blocker / next action |
| --- | --- | --- |
| READ-015 page/name selection and source identity | Demonstrated | Exact names, aliases, two images, inherited/indirect resources and identities |
| Encoded/decoded metadata and existing count/pixel budgets | Demonstrated | Owned payload/sample data, ordered dictionary, pixel/byte exact boundaries |
| Inherited/missing/non-image/unsupported-filter behavior | Demonstrated | Typed errors; unrelated corrupt/unsupported resources are not extracted |
| Explicit nested Forms; preserve batch APIs | Demonstrated | Two-level paths, nearest/absent/empty Resources, cycles and batch PNG regression |
| Typed contracts, real FFI, parity gate, quality/security and CI | Local evidence complete; platform CI pending | Require all 14 jobs before integration |

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
feature branch based on `0498e5c`, including the current changes. No runtime
dependency or core version change. Local review/verification is complete; platform
CI remains the integration blocker. No automatic PR or package release.

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
   (`native/src/named_images.rs:37`), while retaining the existing public provenance
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
was an authorized implementation step. Pending: all 14 platform CI jobs on the
implementation revision. Evidence: `/tmp/named-images-{native-tests,dotnet-tests,
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
