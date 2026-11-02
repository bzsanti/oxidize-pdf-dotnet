# OxidizePdf.NET 0.21.0

Adopts core 5.5.6 and adds optional exact token counting (#104, #122).
`PdfTokenCounter` exposes counter discovery and typed owned counts. Both pipeline
chunker configs can select WordProxy or Cl100kBase; the default remains WordProxy.
Normal packages include cl100k_base. Custom native builds can omit tiktoken and
return explicit unavailable errors without silently estimating.

Core signed-field restrictions are now enforced, while the core also permits dedicated invisible
timestamps under P1. The timestamp preparation API is not exposed by this binding. Chunk budgets use the selected counter and retain
explicit oversized exceptions for indivisible content. Existing managed APIs
are preserved. See docs/UPSTREAM-5.5.6.md for limits and provenance.

Full Rust/.NET parity (#81, core #710) remains incomplete.
