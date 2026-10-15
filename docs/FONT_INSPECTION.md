# CID and font inspection

`PdfFonts.InspectAsync` inspects one exact font resource on a **zero-based** page.
It accepts PDF string bytes (the bytes of `Tj` or a string item inside `TJ`), not
text, Unicode code points, CIDs or glyph IDs. Existing
`PdfExtractor.GetResolvedFontResourceAsync` remains unchanged and uses its existing
one-based page numbering.

```csharp
PdfFontInspection font = await PdfFonts.InspectAsync(
    pdfBytes, pageIndex: 0, resourceName: "F1",
    characterCodes: new byte[] { 0x41, 0x42 },
    cids: new uint[] { 100, 101 });

foreach (var glyph in font.Glyphs)
{
    // Each identity is independent; null means the core has no mapping.
    Console.WriteLine($"{Convert.ToHexString(glyph.SourceCode)}: " +
        $"CID={glyph.Cid}, GID={glyph.GlyphId}, text={glyph.Unicode}, advance={glyph.Advance}");
}
```

The core's public resolved-font decoder determines character-code boundaries,
code-to-CID mapping, CID-to-GID mapping, complete Unicode sequences and effective
horizontal/vertical advance. Glyph order is source-code order. `Unicode` is a
string, preserving supplementary characters, ligatures and variation selectors;
null differs from GID/CID zero and from an empty string. Explicit `ToUnicode`
remains authoritative even when a particular code is absent. Collection fallback
is decided by the core, including registry/encoding compatibility checks.

`CidSystemInfo` reports the descendant's declared Registry, Ordering and
Supplement. It is metadata, not a claim that the core used that collection.
Resource lookup uses the core's effective inherited page resources. Names are
exact decoded resource names without a syntactic leading slash; no trimming or
case folding is performed.

`VerticalMetrics` is null for simple fonts. For CID fonts it contains:

- `DefaultAdvance`: the second value in `DW2`, defaulting to -1000.
- `Overrides`: explicit per-CID advances from `W2`; last duplicate wins.
- `Ranges`: inclusive uniform ranges in declaration order, never expanded.

`VerticalAdvances` answers the supplied CID queries in order, retaining duplicates.
Like core 5.4.2, explicit overrides take precedence over all ranges; otherwise the
last matching range wins, followed by the default. Advances are in glyph units
(1/1000 em). They describe pen movement; W2/DW2 drawing-origin coordinates are
validated but are not exposed as advances. CID queries use uint, matching the
core's advance lookup; values above 65535 receive the default. Parsed W2 CIDs
must be within 0..65535. Horizontal font glyph advances remain horizontal even
when vertical tables are queried separately.

Core's `CidVerticalWidths` is inside a private module without a public re-export,
and resolved-font tables are private. The binding therefore reads compact W2/DW2
through public parser objects, while glyph decoding uses the public core directly.
Malformed vertical tables fail explicitly, including under tolerant parser
options; inspection does not present a silently recovered partial table as complete.

For collection-only lookup, no PDF is needed:

```csharp
var lookup = PdfFonts.LookupCollection("Japan1", new ushort[] { 100, 1133, 0 });
// Supported collections: CNS1, GB1, Japan1, Korea1, KR (case-sensitive).
// CID 1133 includes a supplementary variation selector; CID 0 has no text.
```

`UnicodeScalar` is populated only when the complete mapping is one scalar.
Multi-scalar/missing mappings have a null scalar. Unknown orderings return
`Supported=false` and null mappings. Missing CIDs, including CID 0 (.notdef),
return null Unicode. This fallback lookup does not override a font's ToUnicode.

`PdfFontInspectionOptions` bounds character-code bytes (default 4096/max 65536),
CID queries (4096/max 4096), W2 entries processed including duplicates
(4096/max 65536), conservative vertical range-lookup work
(1,000,000/max 16,777,216), and decoded Unicode response bytes
(1 MiB/max 16 MiB). Zero budgets are allowed. Work is conservatively calculated as
(code-byte count + requested CID count) times (vertical range count + 1).
The Unicode limit is checked after core decoding. These are not total CPU/memory
budgets for parsing, embedded-font loading or internal core CMap resolution.
PDF input is capped at 100 MiB; font names at 1024 UTF-8 bytes. Collection lookup
accepts at most 4096 CIDs and a 64-byte UTF-8 ordering.

PDF bytes, code bytes, CID queries and settings are snapshotted before queued
work. Cancellation prevents queued execution but cannot interrupt native code.
Returned objects/arrays/dictionaries are caller-owned; no native handles escape.
Native JSON is released once after deserialization, including failures.
Argument exceptions identify invalid managed arguments/limit configuration.
`PdfFontException.Error` distinguishes `InvalidInput`, `InvalidPdf`, `MissingFont`,
`LimitExceeded` and `NativeFailure`. Parser settings are explicit; the default
matches `PdfParserOptions`. Creation APIs and existing font-resource models keep
their prior signatures and behavior.
