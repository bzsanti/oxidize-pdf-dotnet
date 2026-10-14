# Session handoff — 2026-10-14

Completed parity increment #105: `PdfExtractor.GetPageGeometryAsync` exposes
existing-page rotation and MediaBox/CropBox without changing the tuple API.
Implementation: `bb7a6e9`; test-only PKCS security correction: `f5baa0e`.
Validated for integration into `develop` without an automatic PR, as required by #81.
Current tracking: [FEATURE_PARITY.md](docs/FEATURE_PARITY.md).

Validation: 225 native tests, Clippy, 15 focused geometry/contract/ABI cases;
after the test-only PKCS update, 28 geometry/contract/signing/slot cases pass.
All 14 platform CI jobs pass on `f5baa0e`:
https://github.com/bzsanti/oxidize-pdf-dotnet/actions/runs/38545842115
The first CI attempt discovered two newly published vulnerabilities in test-only
PKCS 10.0.11; upgraded to patched 10.0.13 without suppressing NuGet audits.
Final handoff edits are documentation only and reuse this exact code evidence.

Next increment: #96 (resource provenance), then #97 (named images), then #95
(bounded raw streams). Core `inherited_resources` may contain resolved direct
resource references too, so provenance must inspect dictionary ownership and
ancestor references, not infer origin from that field alone. Keep the existing
flattened resource API. Remaining acceptance stays in #81 and its open children.

Repository baseline cleanup is complete: stale upstream-4.0.0 branch removed;
pending work preserved in `8d83a2a` and reconciled in `de37d12`.
Core 5.4.2 / managed 0.19.0 / native FFI 0.14.0. No new release. No worktrees.
