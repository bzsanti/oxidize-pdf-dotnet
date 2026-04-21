using System.Text;

namespace OxidizePdf.NET.Tests;

public class PdfDocumentOpenActionTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void SetOpenAction_GoToPageWithFit_EmbedsGoToAction()
    {
        using var doc = new PdfDocument();
        doc.AddPage(PdfPage.A4());
        doc.AddPage(PdfPage.A4());
        doc.SetOpenAction(PdfOpenAction.GoTo(
            pageIndex: 1,
            destination: PdfDestination.Fit()));

        byte[] bytes = doc.SaveToBytes();
        string text = Encoding.Latin1.GetString(bytes);

        Assert.Contains("/OpenAction", text);
        Assert.Contains("/S /GoTo", text);
        Assert.Contains("/Fit", text);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetOpenAction_Uri_EmbedsUriAction()
    {
        using var doc = new PdfDocument();
        doc.AddPage(PdfPage.A4());
        doc.SetOpenAction(PdfOpenAction.Uri("https://example.com/"));

        byte[] bytes = doc.SaveToBytes();
        string text = Encoding.Latin1.GetString(bytes);

        Assert.Contains("/S /URI", text);
        Assert.Contains("https://example.com/", text);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetOpenAction_XyzWithZoom_EmbedsCoordinates()
    {
        using var doc = new PdfDocument();
        doc.AddPage(PdfPage.A4());
        doc.SetOpenAction(PdfOpenAction.GoTo(0, PdfDestination.Xyz(0, left: 100, top: 500, zoom: 1.5)));

        byte[] bytes = doc.SaveToBytes();
        string text = Encoding.Latin1.GetString(bytes);

        Assert.Contains("/XYZ", text);
        Assert.Contains("100", text);
        Assert.Contains("500", text);
        Assert.Contains("1.5", text);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetOpenAction_CalledTwice_LastWriteWins()
    {
        using var doc = new PdfDocument();
        doc.AddPage(PdfPage.A4());
        doc.SetOpenAction(PdfOpenAction.Uri("https://first.example/"));
        doc.SetOpenAction(PdfOpenAction.Uri("https://second.example/"));

        byte[] bytes = doc.SaveToBytes();
        string text = Encoding.Latin1.GetString(bytes);

        Assert.DoesNotContain("https://first.example/", text);
        Assert.Contains("https://second.example/", text);
    }
}
