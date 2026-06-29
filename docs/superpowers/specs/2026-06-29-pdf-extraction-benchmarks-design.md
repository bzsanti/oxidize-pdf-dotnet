# PDF Extraction Benchmark Harness — Design

**Date:** 2026-06-29
**Status:** Approved (design)
**Adoption lever:** #4, sub-project 1 of 3 (benchmark harness). Sub-projects 2 (comparative
post) and 3 (awesome-list referrals) are separate, later phases that depend on this one.

## Problem

`OxidizePdf.NET`'s positioning ("native speed" + "MIT, no AGPL friction") rests on a
**stale, unverifiable** claim: the README cites "3,000–4,000 pages/second, based on
oxidize-pdf **v1.6.4** benchmarks" — the core is now 3.0.4, and the number is not against
any competitor. Adoption lever #4 needs a fresh, reproducible, **honest** benchmark
comparing `OxidizePdf.NET` against the mainstream .NET PDF-extraction libraries, on a real
corpus, that can back a comparative post and refresh the README.

The honesty risk is the whole point: a benchmark that flatters oxidize by cherry-picking
PDFs or conflating speed with robustness would backfire. The design's safeguards exist to
prevent exactly that.

## Scope

This spec covers **only the harness** — the runnable tool that produces the numbers. The
comparative post and the awesome-dotnet/awesome-rag submissions are out of scope here
(later sub-projects; the post depends on this harness's output, the submissions are outreach
needing explicit authorization).

## Competitors

`OxidizePdf.NET` is measured against three mainstream .NET extraction libraries:

- **PdfPig** (`UglyToad.PdfPig`) — MIT, pure C#. The popular extraction baseline (MIT-vs-MIT).
- **iText7** (`itext7`) — AGPL / commercial. Feature-rich; anchors the "MIT, no AGPL friction" angle.
- **Docnet.Core** — PDFium native wrapper; a native-backed comparison point for "native speed".

PDFsharp is **excluded** — its text extraction is weak, so including it would look like padding
against a weak target.

## Architecture

New console project `dotnet/OxidizePdf.NET.Benchmarks` (`net8.0`, `<IsPackable>false</IsPackable>`).
The competitor packages are dev-only `PackageReference`s in this project — they never ship in
the published `OxidizePdf.NET` / connector packages (so iText7's AGPL never touches them).

### Components

1. **`IPdfExtractorAdapter`** — the common contract every library is driven through:
   ```csharp
   public sealed record ExtractResult(int PageCount, string Text);

   public interface IPdfExtractorAdapter
   {
       string Name { get; }      // e.g. "OxidizePdf.NET"
       string License { get; }   // e.g. "MIT"
       string Version { get; }   // the library's package version
       // Extract plain text from EVERY page, concatenated. The identical operation
       // for every adapter — this is what makes the comparison apples-to-apples.
       ExtractResult Extract(byte[] pdfBytes);
   }
   ```
   One adapter each: `OxidizeAdapter`, `PdfPigAdapter`, `IText7Adapter`, `DocnetAdapter`.
   Each adapter does the minimal "all-pages plain text" extraction in its library's idiom.

2. **`BenchmarkRunner`** — iterates the corpus; for each (adapter, pdf) records a
   `FileResult { adapter, file, pageCount, elapsedMs, status, textLength }` where
   `status ∈ { Ok, Error, Timeout, Empty }`. A per-file timeout (default 30s) runs each
   extraction on a cancellable task; a file exceeding it is recorded `Timeout`, never aborting
   the run. Exceptions are caught per file and recorded `Error`. `Empty` = succeeded but
   `textLength == 0` (catches silent-failure modes like the AES-empty bug).

3. **`ResultsAggregator`** — turns `FileResult`s into the reported metrics (below).

4. **`Program`** — CLI: `--corpus <dir>` (default `../../../fixtures` relative to the repo,
   overridable), `--timeout <seconds>`, `--out <dir>`. Discovers `*.pdf` under the corpus dir.

### Metrics & honesty safeguards (the crux)

Speed and robustness are reported **separately**, never merged into one number:

- **Speed — on the common-success subset only.** The set of PDFs that **every** adapter
  extracted with status `Ok` is computed first. Speed metrics (`median ms/page`, `PDFs/sec`)
  are reported **only over that common subset**, so a library cannot look fast by silently
  skipping the hard PDFs. The common-subset size is reported alongside (e.g. "speed measured
  on 612 of 802 PDFs all four libraries parsed").
- **Robustness — over the full corpus, per library.** `% Ok`, `% Empty`, `% Error`,
  `% Timeout` over all 802. This is where "library X returns empty for N% of these real PDFs"
  surfaces.
- **Page count** for `ms/page` uses a single reference page count per PDF (PdfPig's, as a
  neutral third party — not oxidize's own) so the denominator is identical across adapters.
- **Environment block** in the output: machine, OS, .NET runtime version, and each adapter's
  reported library `Version`, plus the corpus path and file count — so the run is reproducible
  and the numbers are contextualized.

### Capability matrix (qualitative, hand-authored)

A separate Markdown table — NOT a computed score — listing structure-aware capabilities
(plain text, **heading detection**, **table extraction**, **reading-order / multi-column**,
**RAG chunking with page citations**) as ✓/✗ per library. Honest as a feature comparison; it
makes no quantitative quality claim where there is no ground truth. (The quality dimension is
deliberately robustness + capabilities, not a fidelity score — see the brainstorming decision.)

## Data flow

```
corpus dir ──> discover *.pdf
for each pdf, for each adapter:
   read bytes ──> adapter.Extract (on a task with timeout)
            ──> FileResult { pageCount, elapsedMs, status, textLength }
ResultsAggregator:
   common-success subset ──> speed (median ms/page, PDFs/sec)  [subset only]
   full corpus           ──> robustness (% Ok/Empty/Error/Timeout)  [per adapter]
output ──> results.json (env + all FileResults + aggregates)
       ──> results.md   (speed table + robustness table + capability matrix stub)
```

## Output

- **`results.json`** — environment block, every `FileResult`, and the aggregates. The raw
  record, so the numbers can be re-derived and audited.
- **`results.md`** — a human-readable summary: the speed table (common subset), the robustness
  table (full corpus), and the hand-authored capability matrix. This is the artifact the post
  (sub-project 2) draws from.

## Error handling

- Per-file timeout → `Timeout` status; the run continues.
- Per-file exception → `Error` status with the exception type recorded; the run continues.
- A library that fails to load / throws at construction → the whole adapter is reported as
  unavailable in the output (not a silent omission), and the run proceeds with the rest.
- Empty corpus / bad `--corpus` path → fail fast with a clear message before any work.

## Testing (no smoke tests)

- **Per-adapter test:** each adapter, run on a known fixture (a small PDF with known text),
  asserts `PageCount >= 1` and that `Text` contains expected real substrings — verifying the
  adapter genuinely extracts content, not that it merely returned. (`sample.pdf` from the test
  fixtures, plus the adapter's own library.)
- **Aggregator test:** feed synthetic `FileResult`s where adapters disagree on which files
  succeeded; assert the common-success subset is the intersection, that speed is computed over
  only that subset, and that robustness percentages are over the full set. This is the test
  that proves the honesty safeguard works.
- **Timeout/error-isolation test:** a `FileResult` stream containing an `Error` and a `Timeout`
  aggregates correctly without throwing.

## Out of scope (YAGNI)

- No per-commit CI run (802 PDFs × 4 libraries is heavy; this is a manual/occasional run).
- No charts/visualization (the post handles presentation).
- No ground-truth-labeled quality scoring (rejected in brainstorming as not defensible without
  labeling; robustness + capability matrix is the quality story).
- No publishing the corpus or the post here — those are later sub-projects.

## Open follow-ups (later sub-projects)

1. Comparative post drawing on `results.md` (sub-project 2) — and refreshing the stale README
   "v1.6.4 / 3,000–4,000 pages/s" claim with the new numbers.
2. awesome-dotnet / awesome-rag submissions (sub-project 3) — outreach, needs explicit authorization.
