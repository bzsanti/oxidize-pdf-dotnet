# Rust core 5.5.3 compatibility

Tracking: #111. Development pins the registry dependency `oxidize-pdf = "=5.5.3"`.
This is a source dependency update, not a new Python/NuGet package publication.
Existing package versions and public binding surface remain unchanged.

The previous pin was 5.5.0. Core 5.5.1 and 5.5.2 include stricter redaction and
form-permission checks, page-label/font/URI/annotation corrections and changes
to batch operation behavior. Core 5.5.3 adds page-tree consistency, OCR geometry
and Unicode corrections, and generation allocation/metric improvements.
See the upstream [5.5.1](https://github.com/bzsanti/oxidizePdf/blob/v5.5.1/docs/releases/5.5.1.md),
[5.5.2](https://github.com/bzsanti/oxidizePdf/blob/v5.5.2/docs/releases/5.5.2.md)
and [5.5.3](https://github.com/bzsanti/oxidizePdf/blob/v5.5.3/docs/releases/5.5.3.md)
compatibility notes. Core benchmark percentages are not binding benchmarks.

The dependency pin does not expose missing OCR APIs or complete the remaining
parity issues. Existing unsupported operations remain explicit. Historical
upstream blocker references must be re-evaluated against 5.5.3 before working
on their corresponding binding issues; they are not closure evidence by themselves.

## Validation

Registry 5.5.3 is published and not yanked. Both the exact pin and registry
checksum are verified: `ac23cc2913977d436332f433faa48686cbd9b096c7f45f36de8d8278d8715b5a`.
Only the core package changes in dependency resolution. No local path override
remains. Local build and regression checks passed; multiplatform CI is tracked by the adoption PR.

The .NET batch bridge continues rejecting Rotate and Compress before work.
Core now implements rotation, but enabling the binding operation requires its
own acceptance under #101. Compression remains unsupported in the core.

For incremental OCR layers, `StreamsAdded` now reports two streams per changed
page: a graphics-state guard before existing content and the OCR stream that
restores the state before adding invisible text. Resource count remains one per
changed page. The original PDF prefix, original text and invisible Unicode OCR
text remain regression requirements. The previously ignored DocMDP P=1 form-fill
rejection probe now passes and runs in the normal native test suite. This does
not close the remaining form/OCR binding issues.

Local .NET 10 validation: 1,228 binding tests and 15 Kernel Memory tests passed.
Native tests include the re-enabled DocMDP rejection; Clippy with warnings denied
and Rust formatting passed. The native lockfile remains local under the existing
repository ignore policy; the manifest pins the registry core exactly. Multiplatform
CI builds and checks the actual NuGet runtime assets before any package publication.

[Review and validation evidence](evidence/core-5.5.3/review.md).
