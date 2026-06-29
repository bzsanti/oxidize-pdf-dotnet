using System.Reflection;
using System.Text;
using Docnet.Core;
using Docnet.Core.Models;

namespace OxidizePdf.NET.Benchmarks.Adapters;

/// <summary>Drives Docnet.Core (a PDFium native wrapper).</summary>
public sealed class DocnetAdapter : IPdfExtractorAdapter
{
    public string Name => "Docnet.Core";
    public string License => "MIT (PDFium BSD)";

    public string Version =>
        typeof(DocLib).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(DocLib).Assembly.GetName().Version?.ToString()
        ?? "unknown";

    public ExtractResult Extract(byte[] pdfBytes)
    {
        // DocLib.Instance is a process-wide PDFium singleton — never disposed here.
        // Dimensions are required by the API but irrelevant to text extraction.
        using var docReader = DocLib.Instance.GetDocReader(pdfBytes, new PageDimensions(1080, 1920));
        int pageCount = docReader.GetPageCount();
        var sb = new StringBuilder();
        for (int i = 0; i < pageCount; i++)
        {
            using var pageReader = docReader.GetPageReader(i);
            sb.Append(pageReader.GetText());
        }
        return new ExtractResult(pageCount, sb.ToString());
    }
}
