# Session handoff — 2026-10-14

Active increment #95 on `feat/rust-parity-raw-streams`: PdfStreams object/page
inspection plus bounded strict/explicit-recovery decoding. Local validation and
quality/security review complete; platform CI must pass before develop integration.
No automatic PR (#81), release or worktree.

Acceptance/evidence and resolved findings: docs/FEATURE_PARITY.md. 25 focused .NET
cases, 228 native tests, formatting/Clippy pass. A compiled disposable mutation
ignoring decoded budgets fails both strict/recovery cases as intended. Both
Kripteia analyses completed; C# syntax coverage limitations manually supplemented.
Core 5.4.2 strict `/Filter []` data-loss behavior is rejected explicitly; raw access
and explicit recovery remain available. Encoded budgets stop accumulation early.

Next delivery steps: push feature, await all 14 platform CI jobs, document exact
validated SHA, fast-forward develop, close #95 and check its #81 box. Then #97
(named images) can reuse document-aware bounded decoding and #96 resource lookup.
Completed previously: #105 and #96. Core 5.4.2 / managed 0.19.0 / native 0.14.0;
test-only PKCS 10.0.13. Preserve original cleanup history and no-worktree choice.

Evidence: /tmp/raw-streams-*.log and /tmp/raw-streams-mutant/.
