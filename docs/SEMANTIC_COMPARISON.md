# Semantic PDF comparison

```csharp
var result = await PdfSemanticComparison.CompareAsync(originalPdf, editedPdf,
    new PdfSemanticComparisonLimits
    {
        MaxObjects = 100000,
        MaxRevisions = 128,
        MaxDecodedStreamBytes = 64 * 1024 * 1024
    }, cancellationToken);

foreach (var change in result.Differences)
    Console.WriteLine($"{change.Class}: {change.Path}: {change.Description}");

foreach (var revision in result.RightRevisions)
    Console.WriteLine($"Revision {revision.Index}: {revision.ObjectChanges.Length} object changes");
```

`SemanticallyEqual` concerns the semantic domains supported by upstream. This
is an object-graph and logical-text comparison, not pixel rendering. The
`Visual` classification describes changes in page-appearance data. A `Security`
difference does not itself decide signature validity or certificate trust.

Differences have stable logical paths and categories: Visual, Textual,
Structural, Metadata, Security, or SerializationOnly. Object numbering,
dictionary order, stream encodings, and volatile timestamps are normalized.
A serialization-only difference can coexist with `SemanticallyEqual = true`.

Revision arrays are chronological. Each revision contains its xref offset,
normalized state fingerprint (hexadecimal SHA-256), and physical object changes
(Added, Replaced, Freed). The result also includes equivalent object pairs and
latest in-use objects unreachable from Root and Info. Object identities consist
of object number and generation; they are local to each input document.

Limits apply independently to each input. The defaults match upstream:
100,000 objects, depth 256, 256 MiB decoded streams, 256 MiB canonical data,
64 MiB extracted text, 10,000 unreachable objects, and 1,024 revisions.
Malformed, encrypted, undecodable, and over-limit inputs throw
`PdfExtractionException`; partial comparisons are not returned as equality.
Cancellation prevents queued work but does not interrupt an active native call.
