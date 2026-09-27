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
