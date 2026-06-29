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
        // The library's API is async; the benchmark contract is synchronous and
        // single-threaded per call, so block here deliberately.
        int pageCount = _extractor.GetPageCountAsync(pdfBytes).GetAwaiter().GetResult();
        string text = _extractor.ExtractTextAsync(pdfBytes).GetAwaiter().GetResult();
        return new ExtractResult(pageCount, text);
    }
}
