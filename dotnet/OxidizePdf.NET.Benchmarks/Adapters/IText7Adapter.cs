using System.Reflection;
using System.Text;
using iText.Kernel.Pdf.Canvas.Parser;
// Aliases: iText's PdfReader / PdfDocument collide with OxidizePdf.NET types
// in scope via the parent namespace.
using ITextReader = iText.Kernel.Pdf.PdfReader;
using ITextDocument = iText.Kernel.Pdf.PdfDocument;

namespace OxidizePdf.NET.Benchmarks.Adapters;

/// <summary>Drives iText7 (itext7). AGPL-licensed; dev-only dependency.</summary>
public sealed class IText7Adapter : IPdfExtractorAdapter
{
    public string Name => "iText7";
    public string License => "AGPL";

    public string Version =>
        typeof(ITextDocument).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(ITextDocument).Assembly.GetName().Version?.ToString()
        ?? "unknown";

    public ExtractResult Extract(byte[] pdfBytes)
    {
        using var stream = new MemoryStream(pdfBytes);
        using var reader = new ITextReader(stream);
        using var pdf = new ITextDocument(reader);

        int pageCount = pdf.GetNumberOfPages();
        var sb = new StringBuilder();
        for (int i = 1; i <= pageCount; i++)
        {
            // pdf.GetPage(i) returns an iText PdfPage; passed directly so the
            // type name never needs to be written (avoids the PdfPage clash).
            sb.Append(PdfTextExtractor.GetTextFromPage(pdf.GetPage(i)));
        }
        return new ExtractResult(pageCount, sb.ToString());
    }
}
