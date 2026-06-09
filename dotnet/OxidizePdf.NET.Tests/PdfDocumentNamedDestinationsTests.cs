using System.Text;
using System.Text.RegularExpressions;

namespace OxidizePdf.NET.Tests;

public class PdfDocumentNamedDestinationsTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void AddNamedDestination_SingleName_EmbedsNameTree()
    {
        using var doc = new PdfDocument();
        doc.AddPage(PdfPage.A4());
        doc.AddPage(PdfPage.A4());
        doc.AddNamedDestination("chapter-1", PdfDestination.Fit(1));

        string text = Encoding.Latin1.GetString(doc.SaveToBytes());

        Assert.Contains("/Names", text);
        Assert.Contains("chapter-1", text);
        Assert.Contains("/Fit", text);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void AddNamedDestination_DuplicateName_LastWriteWins()
    {
        using var doc = new PdfDocument();
        doc.AddPage(PdfPage.A4());
        doc.AddPage(PdfPage.A4());

        // Two writes with distinguishable destinations: only the second should persist.
        doc.AddNamedDestination("target", PdfDestination.Xyz(0, left: 111, top: 222, zoom: 3.5));
        doc.AddNamedDestination("target", PdfDestination.Xyz(1, left: 333, top: 444, zoom: 4.5));

        string text = Encoding.Latin1.GetString(doc.SaveToBytes());

        // The first write's distinguishable values must be gone; only the second write persists.
        Assert.DoesNotContain("111", text);
        Assert.DoesNotContain("222", text);
        Assert.DoesNotContain("3.5", text);
        Assert.Contains("333", text);
        Assert.Contains("444", text);
        Assert.Contains("4.5", text);
    }

    [Fact]
    public void AddNamedDestination_NullOrWhitespaceName_Throws()
    {
        using var doc = new PdfDocument();
        doc.AddPage(PdfPage.A4());

        Assert.Throws<ArgumentNullException>(() => doc.AddNamedDestination(null!, PdfDestination.Fit(0)));
        Assert.Throws<ArgumentException>(() => doc.AddNamedDestination("", PdfDestination.Fit(0)));
        Assert.Throws<ArgumentException>(() => doc.AddNamedDestination("   ", PdfDestination.Fit(0)));
    }

    [Fact]
    public void AddNamedDestination_NullDestination_ThrowsArgumentNullException()
    {
        using var doc = new PdfDocument();
        doc.AddPage(PdfPage.A4());

        Assert.Throws<ArgumentNullException>(() => doc.AddNamedDestination("label", null!));
    }
}
