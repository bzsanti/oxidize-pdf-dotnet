using OxidizePdf.NET;

namespace OxidizePdf.NET.Benchmarks.Adapters;

/// <summary>Drives OxidizePdf.NET's PdfExtractor.</summary>
public sealed class OxidizeAdapter : IPdfExtractorAdapter
{
    private readonly PdfExtractor _extractor = new();

    public string Name => "OxidizePdf.NET";
    public string License => "MIT";
    public string Version => PdfExtractor.Version;

    public ExtractResult Extract(byte[] pdfBytes)
    {
        // Time ONLY the text extraction — the operation a real text-extraction user
        // performs, and the deliverable being compared across libraries. We do NOT
        // call GetPageCountAsync: it is a separate FFI entry point that pins and
        // parses the whole PDF a second time (oxidize_extract_text and
        // oxidize_get_page_count share nothing), and the ms/page metric uses the
        // reference adapter's (PdfPig) page count for every library, so Oxidize's
        // own count is never used. PageCount is therefore left at 0.
        //
        // The library's API is async; the benchmark contract is synchronous and
        // single-threaded per call, so block here deliberately.
        string text = _extractor.ExtractTextAsync(pdfBytes).GetAwaiter().GetResult();
        return new ExtractResult(0, text);
    }
}
