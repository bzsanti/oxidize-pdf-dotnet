using System.Text;

namespace OxidizePdf.NET.Tests;

public class PdfDocumentViewerPreferencesTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void SetViewerPreferences_HideToolbarFitWindow_EmbedsDict()
    {
        using var doc = new PdfDocument();
        doc.AddPage(PdfPage.A4());
        doc.SetViewerPreferences(new PdfViewerPreferences
        {
            HideToolbar = true,
            FitWindow = true,
        });

        byte[] bytes = doc.SaveToBytes();
        string text = Encoding.Latin1.GetString(bytes);

        Assert.Contains("/ViewerPreferences", text);
        Assert.Contains("/HideToolbar true", text);
        Assert.Contains("/FitWindow true", text);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetViewerPreferences_PageLayoutTwoColumnLeft_Written()
    {
        using var doc = new PdfDocument();
        doc.AddPage(PdfPage.A4());
        doc.SetViewerPreferences(new PdfViewerPreferences { PageLayout = PdfPageLayout.TwoColumnLeft });

        string text = Encoding.Latin1.GetString(doc.SaveToBytes());

        Assert.Contains("/PageLayout /TwoColumnLeft", text);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetViewerPreferences_PageModeFullScreen_Written()
    {
        using var doc = new PdfDocument();
        doc.AddPage(PdfPage.A4());
        doc.SetViewerPreferences(new PdfViewerPreferences { PageMode = PdfPageMode.FullScreen });

        string text = Encoding.Latin1.GetString(doc.SaveToBytes());

        Assert.Contains("/PageMode /FullScreen", text);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetViewerPreferences_DuplexFlipLongEdge_Written()
    {
        using var doc = new PdfDocument();
        doc.AddPage(PdfPage.A4());
        doc.SetViewerPreferences(new PdfViewerPreferences { Duplex = PdfDuplex.DuplexFlipLongEdge });

        string text = Encoding.Latin1.GetString(doc.SaveToBytes());

        Assert.Contains("/Duplex /DuplexFlipLongEdge", text);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetViewerPreferences_PrintScalingNone_Written()
    {
        using var doc = new PdfDocument();
        doc.AddPage(PdfPage.A4());
        doc.SetViewerPreferences(new PdfViewerPreferences { PrintScaling = PdfPrintScaling.None });

        string text = Encoding.Latin1.GetString(doc.SaveToBytes());

        Assert.Contains("/PrintScaling /None", text);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetViewerPreferences_NullArgument_ThrowsArgumentNullException()
    {
        using var doc = new PdfDocument();
        doc.AddPage(PdfPage.A4());

        Assert.Throws<ArgumentNullException>(() => doc.SetViewerPreferences(null!));
    }
}
