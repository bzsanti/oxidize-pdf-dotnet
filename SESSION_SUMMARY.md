# Session handoff — 2026-10-14

Completed #103: MergeWithMetadataAsync exposes all four core MetadataMode variants
with typed source/custom options, defaults and failures. Implementation
`7a9ac2a99a47bbc3910e85a94f511f8363997555` passed all 14 platform CI jobs before
integration into develop:
https://github.com/bzsanti/oxidize-pdf-dotnet/actions/runs/38641793379
Closure changes documentation only; product/test/contract/workflow identity with
the validated SHA is verified. No automatic PR or package publication.

Existing overloads remain source-compatible. Inputs/options are snapshotted and
result bytes owned. Private RAII temporary inputs; original-list source indexes;
Info-only semantics do not imply XMP or structural preservation. Core 5.4.2
Unicode serialization needed a bounded repair of newly generated Info. All source
modes/remerge and independent qpdf checks pass. Broader legacy writer evidence is
recorded on #87; do not claim unrelated APIs are fixed.

Validation: 38 focused .NET cases (16 new), all 232 native tests, formatting/Clippy,
parity/ABI, both Kripteia analyses and a compiled negative control forcing source
index zero (two expected output assertion failures). Full acceptance/review and
limitations: docs/FEATURE_PARITY.md. Evidence /tmp/merge-metadata-*.log,
/tmp/merge-metadata-ci-result.json, /tmp/merge-metadata-mutant and qpdf artifacts.

Next: #102 (complete split options and output naming semantics). Output naming
must not imply authority to write caller-selected files. Continue with the core
5.4.2 contract, existing APIs and structural-policy distinctions. Refresh issue
body before implementation.

Prior #105/#96/#95/#97 complete. Core 5.4.2, managed 0.19.0, native 0.14.0;
test-only PKCS 10.0.13. Active integration branch: develop. Preserve the
no-worktree choice and existing cleanup history.
