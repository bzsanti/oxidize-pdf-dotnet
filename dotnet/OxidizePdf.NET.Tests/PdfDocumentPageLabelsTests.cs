using System.Text;

namespace OxidizePdf.NET.Tests;

public class PdfDocumentPageLabelsTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void SetPageLabels_RomanThenDecimal_EmbedsPageLabelsDict()
    {
        using var doc = new PdfDocument();
        for (int i = 0; i < 6; i++)
            doc.AddPage(PdfPage.A4());

        doc.SetPageLabels(PdfPageLabels.Create()
            .AddRange(0, PdfPageLabelStyle.LowercaseRoman)
            .AddRange(3, PdfPageLabelStyle.DecimalArabic));

        string text = Encoding.Latin1.GetString(doc.SaveToBytes());

        Assert.Contains("/PageLabels", text);
        Assert.Contains("/Nums", text);
        Assert.Contains("/S /r", text);  // lowercase roman
        Assert.Contains("/S /D", text);  // decimal arabic
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetPageLabels_WithPrefixAndStart_EmbedsPrefixAndStartValue()
    {
        using var doc = new PdfDocument();
        doc.AddPage(PdfPage.A4());
        doc.AddPage(PdfPage.A4());

        doc.SetPageLabels(PdfPageLabels.Create()
            .AddRange(0, PdfPageLabelStyle.DecimalArabic, prefix: "Chapter ", startAt: 5));

        string text = Encoding.Latin1.GetString(doc.SaveToBytes());

        Assert.Contains("/P (Chapter ", text);
        Assert.Contains("/St 5", text);
        Assert.Contains("/S /D", text);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetPageLabels_UppercaseRoman_EmitsUppercaseStyle()
    {
        using var doc = new PdfDocument();
        doc.AddPage(PdfPage.A4());

        doc.SetPageLabels(PdfPageLabels.Create()
            .AddRange(0, PdfPageLabelStyle.UppercaseRoman));

        string text = Encoding.Latin1.GetString(doc.SaveToBytes());

        Assert.Contains("/S /R", text);  // uppercase roman
    }

    [Fact]
    public void SetPageLabels_NoRanges_ThrowsArgumentException()
    {
        using var doc = new PdfDocument();
        doc.AddPage(PdfPage.A4());

        Assert.Throws<ArgumentException>(() => doc.SetPageLabels(PdfPageLabels.Create()));
    }

    [Fact]
    public void SetPageLabels_Null_ThrowsArgumentNullException()
    {
        using var doc = new PdfDocument();
        doc.AddPage(PdfPage.A4());

        Assert.Throws<ArgumentNullException>(() => doc.SetPageLabels(null!));
    }
}
