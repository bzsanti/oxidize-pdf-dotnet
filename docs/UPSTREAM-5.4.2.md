# Core 5.4.2 update

Status: local validation passed; platform CI pending. Tracking: https://github.com/bzsanti/oxidize-pdf-dotnet/issues/79

Pins the official Rust core to exactly 5.4.2 while preserving the binding API and feature flags. Includes the standard-font, text layout, document writing and graphics serialization improvements delivered by core PR #704 (issues #700–#703). Improvements are workload-dependent: the upstream fractional-only uncompressed graphics benchmark takes 3.6% longer; no universal speedup is claimed.

Acceptance: build and regression tests against the registry package, review the dependency changes, and pass platform CI before integration. Package publication is separate from this dependency update.

## Validation and review

220 native tests and 1008 .NET/KernelMemory tests passed on net10.0. Clippy, Rust formatting and changed C# whitespace formatting passed. The extraction FFI is byte-identical to published main. The only runtime adjustment replaces chunks_exact(2) with as_chunks::<2>().0.iter() for Rust 1.99 Clippy; the existing even-coordinate guard makes both iterations equivalent. This API is supported by the declared Rust 1.88 minimum. The carriage-return fixture now defines its characters using ToUnicode; all three expected policy outputs remain unchanged. A native mutation mapping ReplaceWithSpace to Remove was rejected by the test, and the restored native library passes the full suite.

Reviewed contract: preserve the public binding API and enabled core features while consuming the official 5.4.2 registry artifact. Existing tests exercise real native PDF generation/extraction, errors and binding boundaries. Upstream #704 performance/serialization validation is reused; no new universal performance claim is made.

Kripteia: native parser tests score 96 (53 tests), changed C# tests score 97 (7 tests). Security lists existing FFI entry points, not new unsafe code. The C# parser reports a partial syntax tree, so the changed fixture and xref/stream lengths were manually checked. Native configuration tests call real FFI through helper functions; no-production-call heuristic findings are false positives. Null/empty/bad-JSON guards are distinct boundary cases covered nearby. The corrected CR fixture has an executed negative control; no assertion was weakened.

The source base is published 0.18.0 main f2c42a2. develop lacked that release; its only divergent commit is the previous 0.17.0 merge-back. This update incorporates both histories and preserves the published API implementation. No unrelated local migration files are included.

Evidence: `docs/evidence/core-5.4.2/`. Remaining acceptance: platform CI and integration into develop. PyPI/NuGet versions and tags are unchanged; package publication is separate.

CI follow-up: Rust 1.99 adds chunks_exact_to_as_chunks. The equivalent coordinate iterator adjustment passes all 220 native tests, Clippy, and the focused managed ink tests. Kripteia was repeated for ink.rs and InkEditingTests.cs; the existing FFI guard and coordinate limits are unchanged. This is a CI compatibility correction within #79.
