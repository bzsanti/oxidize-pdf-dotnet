# Release Notes — v0.14.0

**Release Date:** 2026-06-12
**Previous Version:** v0.13.0
**Type:** MINOR (new feature, backward compatible)

## Summary

FORM-008 closes the forms gap on existing PDFs: AcroForm fields on an
already-serialized PDF can now be filled and persisted via an ISO 32000-1
§7.5.6 incremental update, built on upstream `oxidize-pdf` 2.15.0's
`IncrementalFormFiller` (upstream #318, previously blocked, now unblocked).
The native core is upgraded 2.14.0 → 2.15.0, which additionally repairs two
text-extraction defects (recovery past malformed content operators, and
recursion into Form XObjects). All additions are backward compatible.

## New Features

### FORM-008 — Fill AcroForm fields on existing PDFs
- **`PdfOperations.FillFormFieldsAsync(byte[] pdfBytes, IReadOnlyDictionary<string, string> fields)`** —
  fills AcroForm fields on an already-serialized PDF and returns the updated
  bytes, appending an ISO 32000-1 §7.5.6 incremental update (core
  `IncrementalFormFiller`, upstream oxidizePdf #318). Unlike
  `PdfDocument.FillField` (in-process builder fields only), this works on any
  parsed PDF produced elsewhere (Acrobat, pdftk, ReportLab, …). The base bytes
  are preserved verbatim as the output prefix; `/AcroForm/NeedAppearances` is
  set so compliant viewers regenerate the field appearance on open. Throws
  `PdfExtractionException` when a named field does not exist.

## Bug Fixes

### Text extraction (via core 2.15.0, #319)
- Text past a malformed content operator is now recovered instead of dropped.
- Text inside Form XObjects is now extracted (the extractor recurses into
  them), recovering content previously missing from some invoices and forms.
  Confirmed via A/B on the good-energy invoice fixture (+953 characters
  recovered, text previously lost inside a Form XObject).

## Changed

- Upgraded the native `oxidize-pdf` core from 2.14.0 to **2.15.0**.

## Breaking Changes

None. All additions are backward compatible.

## Changelog (since v0.13.0)

- `faf17fd` Merge pull request #49 from bzsanti/feature/upstream-2.15.0
- `cd834b1` chore(release): bump version to 0.14.0 (FORM-008 + core 2.15.0)
- `ca3f242` feat(form-008): fill AcroForm fields on existing PDFs via incremental update
