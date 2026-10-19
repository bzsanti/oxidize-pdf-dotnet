# Inspecting and editing existing tagged PDFs

```csharp
var before = await PdfTaggedOperations.InspectAsync(pdfBytes);
var paragraph = before.Elements.First(e => e.StructureType == "P");
PdfTaggedMutation[] changes =
[
    new PdfTaggedMutation.SetElementAttribute(paragraph.Object, "Alt",
        new PdfObjectValue("Text", "Alternative description"))
];
var plan = await PdfTaggedOperations.PlanAsync(pdfBytes, changes);
var edited = await PdfTaggedOperations.EditAsync(pdfBytes, changes);
byte[] updatedPdf = edited.PdfBytes;
```

Inspection returns element identities and parents, structure types, text
attributes, original dictionary keys and values, role and class maps, artifacts,
ParentTree entries, observed MCIDs, and stable findings. `Valid` reflects the
checks implemented by upstream, not complete PDF/UA certification. Warnings
can remain even when `Valid` is true.

Five mutation types are supported: CreateElement, SetElementAttribute,
ReparentElement, AssociateMcid, and SetParentTreeEntry. IDs always refer to
existing indirect objects. A null value removes an attribute or ParentTree
entry; `new PdfObjectValue("Null")` instead stores a PDF null object.
AssociateMcid requires the MCID to already exist in page content. Protected
structural dictionary keys must be changed using their dedicated operations.

Planning reports affected object identities and normalized mutations without
writing a PDF. Editing appends a revision and preserves the input bytes as an
exact prefix. The result includes the plan plus inspection before and after.
Inspect `ValidationAfter` for remaining or introduced findings; successful
editing does not certify the resulting document. Encrypted inputs, invalid
references/cycles, and disallowed certified-document edits are rejected.

Dictionary and plan inspection values use typed JSON: `type` and optional
`value`. PDF strings are returned as `Bytes` with base64 payloads to preserve
encoding; references contain `[objectNumber, generation]`. Arrays and
dictionaries recurse through the same representation. Stream inspection
includes the dictionary and raw base64 stream data. Stream values cannot be
supplied as mutations through `PdfObjectValue`.

Optional `PdfTaggedLimits` bounds visited objects, nesting depth, inspected
entries, and decoded content bytes. Defaults match upstream. Cancellation
prevents queued work; it does not interrupt an active native operation.

## Preflight and explicit preparation

`PreflightAsync(pdf)` runs the core's unambiguous derived-index recovery in memory,
then returns validation findings. It returns no prepared bytes and does not invent
missing language or descriptions. Unlike `InspectAsync`, it checks editing policy
and rejects ambiguous ownership, forbidden edits and unsupported recovery.

```csharp
var preflight = await PdfTaggedOperations.PreflightAsync(pdfBytes);
var preparation = new PdfTaggedPreparationOptions
{
    RecoverIndexes = true,
    DocumentLanguage = "es-ES" // Only if the catalog language is missing.
};
PdfTaggedMutation[] descriptions = preflight.Findings
    .Where(f => f.Code == "MissingAlternateText" && f.Object.HasValue)
    .Select(f => (PdfTaggedMutation)new PdfTaggedMutation.SetElementAttribute(
        f.Object!.Value, "Alt", new PdfObjectValue("Text", "Author-supplied description")))
    .ToArray();
var plan = await PdfTaggedOperations.PlanPreparedAsync(pdfBytes, descriptions, preparation);
var prepared = await PdfTaggedOperations.EditPreparedAsync(pdfBytes, descriptions, preparation);
byte[] ownedBytes = prepared.Edit.PdfBytes;
```

Each description must come from the author and describe that particular element;
the placeholder above is not an accessibility repair recommendation. Language is
passed through without inference or tag normalization. Empty/blank language and
any existing nonempty catalog language are rejected. Existing descriptions cannot
be overwritten by prepared operations; ordinary `EditAsync` retains its existing
explicit editing behavior.

Preparation is opt-in: `RecoverIndexes` defaults to false and `DocumentLanguage`
to null. Prepared methods require at least one selected step. Recovery runs first,
then optional language preparation, then mutations. `PreparationObjects` contains
the exact sorted/deduplicated object identities changed during preparation,
including xref-stream objects and original generations of retired unreachable
zero-offset entries. `MutationPlan` (or `prepared.Edit.Plan`) contains only the
subsequent mutation plan. Neither inventory replaces the other.

A successful `PlanPreparedAsync` does **not** certify final validity. The core
permits planning explicit metadata corrections while findings remain.
`EditPreparedAsync`, including an empty mutation list, only returns bytes when
final core validation succeeds. Its bytes preserve the original input as an exact
prefix, including any preparation revisions. `Edit.ValidationBefore` describes
the prepared baseline; call `InspectAsync` separately to inspect original bytes.
This validity check is not a complete PDF/UA certificate.

Preflight/prepared native failures use `PdfTaggedException.Error`; request or
structure failures include ambiguity, validation limits and incomplete final
metadata. Encryption and prohibited certification edits use PermissionDenied.
Managed shape validation uses argument exceptions. Existing methods keep their
signatures, request schemas and `PdfExtractionException` behavior.

PDF bytes, mutations, attributes and limit settings are snapshotted before
queueing, and results own their arrays. No caller files are written. Cancellation
only prevents queued execution. Selected `PdfTaggedLimits` govern tagged validation;
core 5.4.2 recovery uses its own traversal safeguards before validation and does
not accept these configurable limits. This API does not promise a total memory or
CPU budget for preparation. Streaming/resource-budget work remains separate (#100).
