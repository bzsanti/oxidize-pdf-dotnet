# OxidizePdf.NET 0.20.1

Updates the published Rust core from 5.5.0 to 5.5.4 without changing the managed API.

- Includes core fixes for redaction, form permissions, page trees, OCR geometry and Unicode, and PDF generation.
- Updates ECDSA dependencies through oxidize-webpki 0.1.1 to resolve RUSTSEC-2026-0387. The existing RSA advisory exception remains unchanged; this is not a claim of an advisory-free dependency graph.
- Restores the normal regression check for rejecting incremental form filling under DocMDP P=1.
- Validates OCR output with two streams per changed page while preserving original content.

Targets .NET 8/9/10 and the existing eight native runtime identifiers. Local core-adoption validation passed 275 native, 1,228 managed and 15 Kernel Memory tests; adoption PR #112 passed platform, package and security CI. Release validation is tracked in #113.

Batch Rotate/Compress remain unsupported by the managed binding. Missing OCR and other parity APIs are still tracked separately; updating the core does not expose them automatically. See [compatibility details](docs/UPSTREAM-5.5.4.md).

Install: `dotnet add package OxidizePdf.NET --version 0.20.1`.
