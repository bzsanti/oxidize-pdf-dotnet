using System.Reflection;
using System.Text;
// Alias: UglyToad.PdfPig.PdfDocument collides with OxidizePdf.NET.PdfDocument,
// which is in scope via the parent namespace.
using PigDocument = UglyToad.PdfPig.PdfDocument;

namespace OxidizePdf.NET.Benchmarks.Adapters;

/// <summary>Drives UglyToad.PdfPig.</summary>
public sealed class PdfPigAdapter : IPdfExtractorAdapter
{
    public string Name => "PdfPig";
    public string License => "MIT";

    public string Version =>
        typeof(PigDocument).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(PigDocument).Assembly.GetName().Version?.ToString()
        ?? "unknown";

    public ExtractResult Extract(byte[] pdfBytes)
    {
        using var doc = PigDocument.Open(pdfBytes);
        var sb = new StringBuilder();
        foreach (var page in doc.GetPages())
        {
            sb.Append(page.Text);
        }
        return new ExtractResult(doc.NumberOfPages, sb.ToString());
    }
}
