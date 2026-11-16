# Core 5.6.1 in bridge 0.23.0

The exact registry dependency is `oxidize-pdf =5.6.1`. Strict extraction through
`PdfTextExtraction.ExtractAsync` with `PdfParserOptions.Strict()` resolves valid
indirect stream lengths and rejects invalid values. The core also allocates
modern object-stream identifiers from the document's object namespace.

This release includes the generation and batch changes integrated in PR #128.
Default native packages retain performance, Rayon and exact token counting;
the existing feature-disabled configurations remain supported. See
[generation contracts](GENERATION.md) and [the capability ledger](FEATURE_PARITY.md).

The extraction controls and compatibility limits described for [5.6.0](UPSTREAM-5.6.0.md)
remain applicable. Publication and clean NuGet consumers are tracked in #129;
full API parity remains outside this release.
