using System.Text;

namespace OxidizePdf.NET.Tests;

public class PdfDocumentSaveOptionsTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void SaveToBytes_ModernOptions_WritesPdf15WithXRefStreams()
    {
        using var doc = new PdfDocument();
        doc.AddPage(PdfPage.A4());

        byte[] bytes = doc.SaveToBytes(PdfSaveOptions.Modern());
        string text = Encoding.Latin1.GetString(bytes);

        Assert.StartsWith("%PDF-1.5", text);
        Assert.Contains("/Type /XRef", text);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SaveToBytes_LegacyOptions_WritesPdf14WithoutXRefStreams()
    {
        using var doc = new PdfDocument();
        doc.AddPage(PdfPage.A4());

        byte[] bytes = doc.SaveToBytes(PdfSaveOptions.Legacy());
        string text = Encoding.Latin1.GetString(bytes);

        Assert.StartsWith("%PDF-1.4", text);
        Assert.DoesNotContain("/Type /XRef", text);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SaveToBytes_DefaultOptions_WritesPdf17()
    {
        using var doc = new PdfDocument();
        doc.AddPage(PdfPage.A4());

        byte[] bytes = doc.SaveToBytes(PdfSaveOptions.Default());
        string text = Encoding.Latin1.GetString(bytes);

        Assert.StartsWith("%PDF-1.7", text);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SaveToBytes_CustomVersion_WritesThatVersion()
    {
        using var doc = new PdfDocument();
        doc.AddPage(PdfPage.A4());

        byte[] bytes = doc.SaveToBytes(new PdfSaveOptions { PdfVersion = "1.6" });
        string text = Encoding.Latin1.GetString(bytes);

        Assert.StartsWith("%PDF-1.6", text);
    }

    [Fact]
    public void SaveToBytes_NullOptions_ThrowsArgumentNullException()
    {
        using var doc = new PdfDocument();
        doc.AddPage(PdfPage.A4());

        Assert.Throws<ArgumentNullException>(() => doc.SaveToBytes((PdfSaveOptions)null!));
    }
}
