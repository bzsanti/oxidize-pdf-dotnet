# Removing text and masking it visually

The two operations have deliberately separate names:

```csharp
PdfRedactionEntity[] entities =
[
    new()
    {
        Id = "customer-name",
        Type = "personName",
        Content = "EXAMPLE NAME",
        Bounds = new() { Page = 1, X = 50, Y = 700, Width = 150, Height = 20 }
    }
];
var result = await PdfRedaction.RemoveIrreversiblyAsync(pdfBytes, entities, ["personName"]);
```

Entity bounds use PDF coordinates and **one-based** page numbers. Types are
upstream camelCase names such as `text`, `personName`, `email`, or a custom
string. Only entities matching the explicitly selected types are processed.

`RemoveIrreversiblyAsync` invokes upstream's restricted secure text engine.
It accepts exact, nonempty ASCII matches occupying a complete literal Tj
operand or all concatenated literal strings in one TJ array. It preserves text
advance when replacing removed glyph data. It rebuilds the document, drops
prior revisions and auxiliary document-level data, and checks the output for
remaining target text. It does not preserve signatures or arbitrary document
structure. It is not a general rectangle-based removal engine for arbitrary PDFs.

Unsupported content fails without output: examples include annotations,
XObjects, inline images, marked content, abbreviated text-showing operators,
ambiguous matches, non-ASCII targets, and malformed streams. An empty matching
selection also fails. There is no fallback to visual masking.

`MaskVisuallyAsync` draws black rectangles and optionally white placeholder
text. Underlying source content can remain extractable. Its report always
identifies `VisualMask`, sets `IsIrreversible` false, and includes residual risks.
An empty selection returns unchanged bytes with the same explicit risk report.
This legacy operation reconstructs pages; it is not an incremental editor or
a guarantee of document-structure preservation.

Both methods return output bytes and the upstream report: mode, irreversible
status, residual risks, affected one-based pages, count, and processed entity
IDs/types. Cancellation prevents queued work but does not interrupt an active
native operation. The targeted tests exercise removal, preserved unrelated
text, metadata dropping, masking recoverability, and refusal without fallback.
