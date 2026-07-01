# PDF Extraction Benchmark Results

## Environment

- Machine: mojotest
- OS: Ubuntu 24.04.3 LTS
- Runtime: .NET 8.0.26
- Corpus: `/home/santi/repos/BelowZero/oxidizePdf/fixtures` (802 PDFs)

  - OxidizePdf.NET oxidize-pdf-ffi v0.11.1 (MIT)
  - PdfPig 0.1.8-alpha-001+6ebcf528e098f0b88a4588e9d710a994c9c251e0 (MIT)
  - iText7 9.6.0 (AGPL)
  - Docnet.Core 2.6.0+03191d3a3eb27efa8b542bd637489b388d1ba316 (MIT (PDFium BSD))

## Speed

Measured on the **common-success subset**: 727 of 802 PDFs every library parsed with status Ok. ms/page uses the reference page count.

| Library | Median ms/page | PDFs/sec | Sample |
|---|---:|---:|---:|
| OxidizePdf.NET | 1.96 | 24.08 | 727 |
| PdfPig | 3 | 8.41 | 727 |
| iText7 | 6 | 3.04 | 727 |
| Docnet.Core | 3.5 | 5.27 | 727 |

## Robustness

Over the full corpus (802 PDFs), per library.

| Library | Total | % Ok | % Empty | % Error | % Timeout |
|---|---:|---:|---:|---:|---:|
| OxidizePdf.NET | 802 | 93.5% | 3.6% | 2.9% | 0% |
| PdfPig | 802 | 94.4% | 5% | 0.6% | 0% |
| iText7 | 802 | 91.4% | 5% | 3.5% | 0.1% |
| Docnet.Core | 802 | 94.4% | 5% | 0.5% | 0.1% |

## Capability matrix

Qualitative feature comparison (hand-authored, not measured).

| Capability | OxidizePdf.NET | PdfPig | iText7 | Docnet.Core |
|---|:---:|:---:|:---:|:---:|
| Plain text extraction | ✓ | ✓ | ✓ | ✓ |
| Heading detection | ✓ | ✗ | ✗ | ✗ |
| Table extraction | ✓ | ✗ | ✗ | ✗ |
| Reading order / multi-column | ✓ | ✗ | ✗ | ✗ |
| RAG chunking with page citations | ✓ | ✗ | ✗ | ✗ |
