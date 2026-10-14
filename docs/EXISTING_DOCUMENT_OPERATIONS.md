# Existing-document operations and policies

`PdfExistingDocumentOperations` exposes `PlanMergeAsync`/`MergeAsync`,
`PlanExtractAsync`/`ExtractAsync`, and `PlanSplitAsync`/`SplitAsync`. Each requires
an explicit `PdfExistingDocumentPolicy`:

- `PreserveBase`: preserves supported base-document structure and rejects
  incompatible secondary structures.
- `PreserveBaseWithFirstInputPageLabels`: explicitly keeps first-input labels.
- `Reconstruct`: reconstructs selected pages with reported semantic losses.
- `ReconstructWithFirstInputMetadata`: reconstructs with first-input metadata.

```csharp
var plan = await PdfExistingDocumentOperations.PlanExtractAsync(
    pdfBytes, [0, 2], PdfExistingDocumentPolicy.PreserveBase);
var result = await PdfExistingDocumentOperations.ExtractAsync(
    pdfBytes, [0, 2], PdfExistingDocumentPolicy.PreserveBase);
byte[] extracted = result.Outputs.Single();
```

All selections and page-mutation indexes are zero-based. Mutations support
Move, Rotate, Delete, Duplicate and Insert, with sequential indexes within an
atomic batch. Reorder requires an exact permutation. Their planning counterparts
return reports without output PDFs.

Results include output PDFs, semantic-preservation reports by input index,
selected pages and structure dispositions, and object changes where applicable.
Inputs are staged in private temporary directories that are removed after the
operation. Native staging paths are not returned to callers.

Known upstream 5.1.5 issue: reconstructive extraction and split retain source
metadata while reporting it as discarded ([#634](https://github.com/bzsanti/oxidizePdf/issues/634)).
Do not rely on that policy to remove sensitive metadata until upstream fixes it.
Reconstructive merge passes the corresponding metadata-policy tests.
