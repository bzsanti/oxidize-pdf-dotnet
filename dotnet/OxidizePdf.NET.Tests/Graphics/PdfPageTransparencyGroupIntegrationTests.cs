using System.Text;
using OxidizePdf.NET.Graphics;
using OxidizePdf.NET.Tests.TestHelpers;
using Xunit;

namespace OxidizePdf.NET.Tests.Graphics;

/// <summary>
/// GFX-020 Transparency groups. Verifies that a Form XObject carrying a
/// transparency group emits a real <c>/Group &lt;&lt; /S /Transparency &gt;&gt;</c>
/// dictionary (ISO 32000-1 §11.4.5) with the isolated/knockout/colour-space
/// attributes preserved in the written PDF.
/// </summary>
public class PdfPageTransparencyGroupIntegrationTests
{
    private static byte[] FormContent() =>
        Encoding.ASCII.GetBytes("0.0 0.0 1.0 rg\n0 0 50 50 re\nf\n");

    private static PdfFormXObject GroupedForm(PdfTransparencyGroup group) => new(
        x: 0, y: 0, width: 50, height: 50, content: FormContent(), group: group);

    [Fact]
    [Trait("Category", "Integration")]
    public void TransparencyGroup_IsolatedKnockout_EmitsGroupDictWithAttributes()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        var group = new PdfTransparencyGroup(colorSpace: "DeviceRGB", isolated: true, knockout: true);
        page.AddFormXObject("Fm1", GroupedForm(group))
            .InvokeXObject("Fm1");
        doc.AddPage(page);

        var pdfText = ContentStreamHelper.ToLatin1(doc.SaveToBytes());
        Assert.Contains("/Group", pdfText);
        Assert.Contains("/Transparency", pdfText);
        Assert.Contains("/CS", pdfText);
        Assert.Contains("/DeviceRGB", pdfText);
        // /I and /K are emitted as booleans only when true (ISO 32000-1 §11.4.5).
        Assert.Contains("/I true", pdfText);
        Assert.Contains("/K true", pdfText);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void TransparencyGroup_NonIsolatedNonKnockout_OmitsIAndKButKeepsGroup()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        var group = new PdfTransparencyGroup(colorSpace: "DeviceGray", isolated: false, knockout: false);
        page.AddFormXObject("Fm1", GroupedForm(group))
            .InvokeXObject("Fm1");
        doc.AddPage(page);

        var pdfText = ContentStreamHelper.ToLatin1(doc.SaveToBytes());
        Assert.Contains("/Group", pdfText);
        Assert.Contains("/Transparency", pdfText);
        Assert.Contains("/DeviceGray", pdfText);
        // When false, the optional /I and /K entries must NOT be written.
        Assert.DoesNotContain("/I true", pdfText);
        Assert.DoesNotContain("/K true", pdfText);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void FormXObject_WithoutGroup_DoesNotEmitTransparencyGroup()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        // No group supplied — backward-compatible with GFX-018.
        var form = new PdfFormXObject(x: 0, y: 0, width: 50, height: 50, content: FormContent());
        page.AddFormXObject("Fm1", form).InvokeXObject("Fm1");
        doc.AddPage(page);

        var pdfText = ContentStreamHelper.ToLatin1(doc.SaveToBytes());
        Assert.Contains("/Form", pdfText);
        Assert.DoesNotContain("/Transparency", pdfText);
    }

    [Fact]
    public void PdfTransparencyGroup_NullColorSpace_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new PdfTransparencyGroup(colorSpace: null!, isolated: true, knockout: false));
    }

    [Fact]
    public void PdfTransparencyGroup_EmptyColorSpace_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new PdfTransparencyGroup(colorSpace: "", isolated: true, knockout: false));
    }
}
