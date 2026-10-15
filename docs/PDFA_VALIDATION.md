# PDF/A validation

`PdfAValidation.ValidateAsync` exposes the **checks implemented by oxidize-pdf
5.4.2**, with levels `A1a`, `A1b`, `A2a`, `A2b`, `A2u`, `A3a`, `A3b`, `A3u`.
The default target is `A1b`. No PDF/A-4 level or PDF/A converter is exposed by
this core release.

```csharp
var report = await PdfAValidation.ValidateAsync(pdfBytes,
    new PdfAValidationOptions { Level = PdfALevel.A2u });
foreach (var error in report.Errors)
    Console.WriteLine($"{error.Kind}: {error.Message}");
```

`IsValid` means the core returned no conformance findings. It is **not independent,
comprehensive ISO certification**. Do not use this result alone to certify an
archive. Conformance errors are normal successful results with `IsValid=false`.
Parser and operation failures throw `PdfAException`; `Kind=ParseError` includes
both initial PDF parsing and references that cannot be resolved during validation.
An operation failure does not return a partial conformance report.

Use `PdfAValidationOptions.Password` to unlock an encrypted reader for inspection.
Null leaves the reader locked; an empty string explicitly tries an empty password.
Missing/wrong credentials produce a `ParseError` operation failure. After successful
unlocking, the report still includes `EncryptionForbidden`; unlocking never changes
the input or makes encryption PDF/A-compatible. Passwords are held in managed/native
request strings during the operation; this API does not promise secure erasure.

All 16 declared conformance error variants and their payloads are represented by
`PdfAValidationError`. Use `Kind` to select applicable nullable fields: font name,
location, identifier details, color space, external reference type, object
description, actual/required version, embedded-file name/missing field or action
type. Preserve `Message` for human diagnostics; do not parse it for classification.
Locations and object descriptions retain native numbering and formatting.

All four declared warning variants are represented by `PdfAValidationWarning`,
including unsigned 64-bit file sizes. **5.4.2's validator never emits warnings**;
the binding consequently returns an empty array instead of manufacturing them.
Some declared conformance variants likewise have no producer in this version.
Operation errors preserve all five native `PdfAError` categories; the `Validation`
category carries its structured finding separately.

`CollectAllErrors` defaults to true and is forwarded to the native validator.
**The core stores this setting but never reads it.** False currently returns the
same complete report as true. It is neither a first-error guarantee nor a runtime
budget. The binding does not truncate reports to disguise this limitation.

## Actual coverage and limits

The core checks encryption, its PDF header version compatibility rules, catalog
XMP identification, selected JavaScript and external actions, page-resource
transparency and sampled LZW filters for part 1, attachments for parts 1/2,
font embedding declarations and selected color spaces/output intents.

These checks are partial:

- Version 1.4 is required for part 1; the core accepts 1.4 through 1.7 for
  parts 2/3, although their `required` diagnostic label is 1.7.
- XMP extraction uses regular expressions for selected element forms, not full
  XML/RDF validation. Unreadable metadata may become `XmpMetadataMissing`;
  parseable packets without recognized identity become `XmpMissingPdfAIdentifier`.
- Accessible levels do not establish full tagged-PDF accessibility. Font checks
  require ToUnicode only for A levels, with encoding exceptions; U levels are
  not a comprehensive Unicode validation. Font program presence is not a font
  integrity check.
- Page checks index the root Kids array rather than comprehensively traversing
  nested page trees, and inspect direct page resources. Inherited resources,
  nested Forms and all content operators are not comprehensively audited.
- Output-intent detection checks declarations, not ICC profile validity. Part 3
  attachments bypass the attachment prohibition without comprehensive metadata
  validation. Action and LZW checks do not scan every object in the document.
  A dangling optional action reference can resolve to null and be skipped;
  not every malformed reference becomes an operation error.

The API is read-only, uses no temporary files and does not execute PDF actions.
It snapshots bytes and settings before scheduling; callers own the returned
arrays/strings and retain their inputs. No native handle escapes. Each native
JSON allocation is freed exactly once, including failure responses.

Input size is limited to 100 MiB. `PdfParserOptions` controls the initial reader;
internal validator stream decoders can use their own core defaults. Parser
warnings are separate from PDF/A warnings and are not included in this report.
This API does not promise a total allocation, decompression or execution-time
budget. Cancellation prevents queued work but does not interrupt an active native
call. General bounded streaming is tracked separately in #100.

Fixtures in `PdfAValidationTests` are independently authored minimal PDFs which
are accepted/rejected by this validator at every level. They are intentionally
not labelled externally certified files. Compressed/uncompressed XMP, mismatched
identity, rule differences and operation failures exercise the real FFI. Native
unit tests cover declared variants which the current validator never emits.
