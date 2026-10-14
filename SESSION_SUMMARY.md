# Session handoff — 2026-10-14

Active #97 on feat/rust-parity-named-images: PdfImages.ExtractAsync selects one
page/resource name, with explicit nested Form paths, resource identity, full
metadata and original/bounded output bytes. Local implementation/review complete;
all platform CI must pass before develop integration. No automatic PR or release.

Output contract: packed PDF samples after bounded stream decoding, or standalone
JPEG passthrough. No implicit PNG/color/mask transformation or JPEG integrity
claim. Declared dimensions enforce the existing pixel limit. Missing/non-image/
unsupported resources fail explicitly; unrelated resources are not extracted.
See docs/FEATURE_PARITY.md for complete criteria, limits and review evidence.

Validation: 58 focused .NET cases (19 named-image cases), 230 native tests,
format/Clippy checks and both Kripteia analyses. The compiled disposable pixel-limit
mutation fails as intended. Shared #95 decoder and #96 resource helper regressions
pass; ancestor limits now have an internal typed error without changing the old API.

Next delivery steps: push feature, await all 14 CI jobs, record exact validated SHA,
fast-forward develop, close #97 and update #81. Prior increments #105/#96/#95 are
complete. Core 5.4.2, managed 0.19.0, native 0.14.0; test-only PKCS 10.0.13.
Preserve the no-worktree choice and existing cleanup history.

Evidence: /tmp/named-images-*.log and /tmp/named-images-mutant/.
