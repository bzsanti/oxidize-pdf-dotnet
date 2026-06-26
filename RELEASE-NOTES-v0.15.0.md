# Release Notes — v0.15.0

**Release Date:** 2026-06-26
**Previous Version:** v0.14.0
**Type:** MINOR (new feature, backward compatible)

## Summary

Exposes the upstream `oxidize-pdf` 3.0.0 **CID-keyed positioned-glyph-run**
write API (issue #358) to .NET, and upgrades the native core from 2.15.0 to
**3.0.1** (a major upstream release). A caller can now register a CID-keyed
(CID = glyph id) TrueType font and draw a pre-shaped, positioned glyph run —
expressing ligatures and per-glyph kerning/offset that the Unicode-keyed text
path cannot — while keeping the text extractable via the emitted `ToUnicode`
CMap. All additions are backward compatible.

## New Features

### CID-keyed positioned glyph runs (#358)
- **`PdfDocument.AddCidKeyedFont(string name, byte[] fontData, CidFontMapping mapping)`** —
  registers a CID-keyed (CID = glyph id under `CIDToGIDMap = Identity`)
  TrueType font. `CidFontMapping` carries the required `CidToGid` map and the
  `CidToUnicode` / `CidToUnicodeStr` maps that feed the emitted `ToUnicode`
  CMap (a multi-character string, e.g. an `fi` ligature glyph, takes
  precedence). The maximum CID is derived automatically. Only TrueType
  (CIDFontType2) fonts are supported; an OpenType/CFF font is rejected.
- **`PdfPage.ShowCidArray(string fontName, double size, IReadOnlyList<CidGlyph> glyphs, double x, double y)`** —
  selects the registered font and emits a `TJ` array at `(x, y)`. Each
  `CidGlyph` carries `Cid`, `Adjust` (post-glyph advance kern) and `XOffset`
  (per-glyph horizontal offset that does not consume advance, for mark /
  diacritic attachment). The caller supplies an already-shaped run (e.g. from a
  shaper such as `rustybuzz`); the core performs no shaping.

## Changed

- Upgraded the native `oxidize-pdf` core from **2.15.0** to **3.0.1**. Notable
  upstream improvements inherited beyond the CID-keyed write API:
  - Bounded-memory lenient parsing for damaged / large PDFs — peak memory is
    now O(window) instead of O(file) on recovery paths (#339).
  - Cross-reference-stream double-decode fix: the strict reader
    (`PdfReader::new`) now works for every xref-stream PDF, including
    linearized / government documents (#341).
  - PDF/A validation fix for Flate-compressed XMP `/Metadata` (#346).
  - Manual stream-reconstruction now preserves non-Flate filters
    (`/DCTDecode`, `/LZWDecode`, filter arrays, `/DecodeParms`) on damaged
    files instead of silently dropping them (#351).

## Bug Fixes

None specific to the .NET layer; the core upgrade above carries the upstream
fixes.

## Breaking Changes

None at the .NET API surface. All additions are backward compatible.

## Changelog (since v0.14.0)

- `10dbb25` feat: expose CID-keyed positioned glyph runs (issue #358) via FFI+C#
- `753037b` chore: bump oxidize-pdf to 3.0.1
- `d31c820` chore: bump oxidize-pdf to 2.16.0
