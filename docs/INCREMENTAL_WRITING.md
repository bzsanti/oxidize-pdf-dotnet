# Incremental document writing

`PdfDocument.SaveIncrementalToBytes(basePdf, options)` appends a revision to an
existing PDF and returns an owned byte array. It preserves the entire original
prefix. The supplied `PdfDocument` contains the new pages and, when requested,
replacement Info metadata. The call is synchronous; keep exclusive access to the
document and base array until it returns. Neither input is consumed.

```csharp
using var changes = new PdfDocument();
using var page = PdfPage.A4();
page.SetFont(StandardFont.Helvetica, 12).TextAt(50, 700, "New page");
changes.AddPage(page);
changes.SetTitle("Título 東京");
byte[] updated = changes.SaveIncrementalToBytes(baseBytes, new()
{
    PageMode = PdfIncrementalPageMode.AppendPages,
    InfoPolicy = PdfIncrementalInfoPolicy.Replace,
    Writer = new PdfSaveOptions { CompressStreams = true }
});
```

- `AppendPages` (default) keeps all original pages before the supplied pages.
- `ReplaceLeadingPages` replaces the first N base pages with N supplied pages,
  keeps any remaining base pages, and extends the document if N exceeds its page
  count. This is whole-page content replacement, not a drawing overlay. Core keeps
  original page dictionary metadata when replacing existing pages.
- `Preserve` (default) retains the exact Info object reference and generation,
  including absence. Supplied document metadata and identification policy do not
  affect the preserved dictionary.
- `Replace` writes the supplied document's six standard text fields, dates and
  build-identification policy. It does not merge original custom Info fields.
  Unicode in all six fields is encoded as PDF text. An empty supplied document
  can replace Info without adding/removing pages.

The base catalog is authoritative: its XMP and other catalog entries remain.
Replacement Info does not synchronize XMP. Other document-level settings in the
supplied document (encryption, outlines, page labels, viewer preferences, etc.)
are not applied by this workflow. Use the APIs dedicated to those operations.
The base trailer's two file identifiers are retained when present.

All five production `WriterConfig` fields are mapped. `Writer.UseXrefStreams`
and `UseObjectStreams` must both be false because core 5.4.2 supports classic
xref output for this workflow; the base may use a stream xref. `CompressStreams`
controls new streams. `PdfVersion` is passed through but does not alter the
preserved base header or effective version. `incremental_update` is set to true
by this operation. Its default settings match core `WriterConfig::incremental()`.
Existing `SaveToBytes()` and `SaveToBytes(PdfSaveOptions)` remain full saves with
`incremental_update=false` and retain their previous behavior.

Invalid managed arguments raise argument exceptions. Native failures raise
`PdfIncrementalSaveException` with `Error`: `InvalidInput`, `InvalidPdf`,
`EncryptedBase`, `Io`, `Serialization` or `NativeFailure`. `Serialization` includes
unsupported page/catalog structure rejected by the core after initial parsing.
No bytes are returned on failure. Encrypted bases are rejected. Prefix retention
alone does not establish signature validity or permission to edit a certified
PDF; callers must apply their signing/certification policy before using this
low-level writer. This API has no configurable total memory or CPU budget.

Core 5.4.2 takes a base pathname, so the binding uses a private RAII temporary
directory and removes it on success, error and unwinding. No caller-supplied path
is used. The returned bytes are copied before freeing their native buffer; the
SafeHandle keeps the supplied document alive throughout the native call.

Two bounded core workarounds apply only to this new workflow: replacement Info
with non-ASCII text gets a generated UTF-16BE Info revision, and the final generated
trailer receives the original ID pair that the legacy core writer omits. Neither
rewrites source bytes. The existing merge/split four-field repair and full-save
semantics remain unchanged. Remove these workarounds when upstream covers them.
