# Core 5.4.2 update

Status: local validation passed; platform CI pending. Tracking: https://github.com/bzsanti/oxidize-pdf-dotnet/issues/79

Pins the official Rust core to exactly 5.4.2 while preserving the binding API and feature flags. Includes the standard-font, text layout, document writing and graphics serialization improvements delivered by core PR #704 (issues #700–#703). Improvements are workload-dependent: the upstream fractional-only uncompressed graphics benchmark takes 3.6% longer; no universal speedup is claimed.

Acceptance: build and regression tests against the registry package, review the dependency changes, and pass platform CI before integration. Package publication is separate from this dependency update.

## Validation and review

220 native tests and 1008 .NET/KernelMemory tests passed on net10.0. Clippy, Rust formatting and changed C# whitespace formatting passed. Native FFI implementation is byte-identical to published main. The carriage-return fixture now defines its characters using ToUnicode; all three expected policy outputs remain unchanged. A native mutation mapping ReplaceWithSpace to Remove was rejected by the test, and the restored native library passes the full suite.

Reviewed contract: preserve the public binding API and enabled core features while consuming the official 5.4.2 registry artifact. Existing tests exercise real native PDF generation/extraction, errors and binding boundaries. Upstream #704 performance/serialization validation is reused; no new universal performance claim is made.

Kripteia: native parser tests score 96 (53 tests), changed C# tests score 97 (7 tests). Security lists existing FFI entry points, not new unsafe code. The C# parser reports a partial syntax tree, so the changed fixture and xref/stream lengths were manually checked. Native configuration tests call real FFI through helper functions; no-production-call heuristic findings are false positives. Null/empty/bad-JSON guards are distinct boundary cases covered nearby. The corrected CR fixture has an executed negative control; no assertion was weakened.

The source base is published 0.18.0 main f2c42a2. develop lacked that release; its only divergent commit is the previous 0.17.0 merge-back. This update incorporates both histories and preserves the published API implementation. No unrelated local migration files are included.

Evidence: `docs/evidence/core-5.4.2/`. Remaining acceptance: platform CI and integration into develop. PyPI/NuGet versions and tags are unchanged; package publication is separate.
