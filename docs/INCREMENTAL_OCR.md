# Incremental OCR text layers

`PdfOcrLayer` accepts recognized words from your OCR provider. It does not run
recognition, rasterize pages, or convert image coordinates automatically.

```csharp
PdfOcrPage[] pages =
[
    new()
    {
        PageIndex = 0,
        Language = "es",
        Fragments =
        [
            new()
            {
                Text = "Reconocido",
                Region = [50, 500, 100, 20],
                Confidence = 0.99,
                ReadingOrder = 0
            }
        ]
    }
];
var plan = await PdfOcrLayer.PlanAsync(pdfBytes, pages);
var result = await PdfOcrLayer.ApplyAsync(pdfBytes, pages);
```

Regions are `[x, y, width, height]` in PDF user-space coordinates, with positive
width and height. Map your provider's image coordinates, origin, rotation, and
scale before submitting them. Page indexes are zero-based. Fragments must have
strictly increasing reading-order indexes, nonempty text, and finite confidence
between zero and one. Confidence is stored as recognition metadata; this API
does not filter low-confidence words for you.

The plan identifies changed pages, added streams and font resources, and pages
skipped because an oxidize-pdf OCR layer already exists. Each changed page gets
an isolated font resource and an invisible text stream. Unicode is retained for
logical extraction. Existing page text and resources remain in the document.

Applying appends one incremental revision and preserves every original byte.
Reapplying to a page that already has an oxidize-pdf OCR layer skips that page;
this detection is specific to upstream's own layer marker, not all third-party
OCR. Empty fragments add no objects. If every page is skipped or empty, output
bytes equal the input. Requests with duplicate or nonexistent pages fail.

Both planning and execution enforce upstream permissions. Encrypted inputs and
DocMDP-certified PDFs forbidding content edits are rejected. Cancellation
prevents queued work but does not interrupt a running native operation.
