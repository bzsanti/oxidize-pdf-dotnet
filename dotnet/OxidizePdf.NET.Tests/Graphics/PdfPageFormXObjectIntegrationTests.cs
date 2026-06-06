using System.Text;
using OxidizePdf.NET.Graphics;
using OxidizePdf.NET.Tests.TestHelpers;
using Xunit;

namespace OxidizePdf.NET.Tests.Graphics;

/// <summary>
/// GFX-018 Form XObjects. Verifies a reusable form is registered in
/// /Resources/XObject with /Subtype /Form, and that invoking it emits the
/// "/name Do" operator in the page content stream.
/// </summary>
public class PdfPageFormXObjectIntegrationTests
{
    // Form content: a 50x50 blue square. Raw PDF content-stream operators.
    private static byte[] FormContent() =>
        Encoding.ASCII.GetBytes("0.0 0.0 1.0 rg\n0 0 50 50 re\nf\n");

    private static PdfFormXObject BlueSquare() => new(
        x: 0, y: 0, width: 50, height: 50, content: FormContent());

    [Fact]
    [Trait("Category", "Integration")]
    public void FormXObject_AddedToPage_ResourceDictContainsXObjectForm()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.AddFormXObject("Fm1", BlueSquare())
            .InvokeXObject("Fm1");
        doc.AddPage(page);

        var pdfText = ContentStreamHelper.ToLatin1(doc.SaveToBytes());
        Assert.Contains("/XObject", pdfText);
        Assert.Contains("/Subtype", pdfText);
        Assert.Contains("/Form", pdfText);
        Assert.Contains("Fm1", pdfText);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void FormXObject_Invoked_ContentStreamContainsDoOperator()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.AddFormXObject("Fm1", BlueSquare())
            .InvokeXObject("Fm1");
        doc.AddPage(page);

        var stream = ContentStreamHelper.DecompressAllContentStreams(doc.SaveToBytes());
        Assert.Contains("/Fm1 Do", stream);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void FormXObject_HasRequiredBBox()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.AddFormXObject("Fm1", BlueSquare())
            .InvokeXObject("Fm1");
        doc.AddPage(page);

        // /BBox is a required entry for a Form XObject (ISO 32000-1 §8.10.2).
        var pdfText = ContentStreamHelper.ToLatin1(doc.SaveToBytes());
        Assert.Contains("/BBox", pdfText);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void FormXObject_ReusedAcrossTwoInvocations_BothDoOperatorsEmitted()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.AddFormXObject("Fm1", BlueSquare())
            .InvokeXObject("Fm1")
            .Translate(100, 100)
            .InvokeXObject("Fm1");
        doc.AddPage(page);

        // A single registered form invoked twice — the hallmark of XObject reuse.
        var stream = ContentStreamHelper.DecompressAllContentStreams(doc.SaveToBytes());
        var first = stream.IndexOf("/Fm1 Do", StringComparison.Ordinal);
        Assert.True(first >= 0, "first /Fm1 Do not found");
        var second = stream.IndexOf("/Fm1 Do", first + 1, StringComparison.Ordinal);
        Assert.True(second > first, "second /Fm1 Do not found — form not reused");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void AddFormXObject_NullForm_Throws()
    {
        using var page = PdfPage.A4();
        Assert.Throws<ArgumentNullException>(() => page.AddFormXObject("Fm1", null!));
    }
}
