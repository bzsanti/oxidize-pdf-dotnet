# Session handoff — 2026-10-14

Completed #97: PdfImages.ExtractAsync selects one page/resource name, with explicit
nested Form paths, source identity, full declared metadata and original/bounded
output bytes. Implementation `0eb2c6fc06db0d87108f7feebd77835b61937db4` passed all
14 platform CI jobs before integration into develop:
https://github.com/bzsanti/oxidize-pdf-dotnet/actions/runs/38630658308
The closure commit changes documentation only; product/test/contract/workflow
identity with the validated SHA is verified. No automatic PR or package release.

Output: packed samples after stream decoding or standalone JPEG passthrough, with
explicit format metadata. No implicit PNG/color/mask transformation or JPEG
integrity guarantee. Pixel limits check PDF dimensions. Missing/non-image/
unsupported resources fail explicitly; unrelated images are not extracted. Form
lookup uses only the supplied path; absent Resources inherits its enclosing
context, empty Resources masks it, invalid Resources and cycles fail.

Validation: 58 focused .NET cases (19 named-image cases), 230 native tests,
format/Clippy and both Kripteia analyses. The compiled disposable pixel-limit
mutation fails as intended. Shared #95 decoder and #96 lookup regressions pass;
ancestor limits have an internal typed error without changing the old API. Full
acceptance/review evidence and limitations: docs/FEATURE_PARITY.md.

Next: #103 (all merge metadata source modes), then #102 (complete split options
and output naming). Both concern operation metadata; implement merge's explicit
source/custom policies first, preserving structural-policy distinctions and
without claiming XMP equivalence from Info copying. Issue bodies were read in
/tmp/named-images-next103.json and /tmp/named-images-next102.json; refresh before
implementation. #102 output patterns must not imply file-write authorization.

Prior increments #105/#96/#95 are complete. Core 5.4.2, managed 0.19.0, native
0.14.0; test-only PKCS 10.0.13. Active integration branch: develop. Preserve the
no-worktree choice and existing cleanup history.
Evidence: /tmp/named-images-*.log, /tmp/named-images-ci-result.json and
/tmp/named-images-mutant/.
