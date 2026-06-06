using System.Text;
using System.Text.RegularExpressions;
using OxidizePdf.NET;
using OxidizePdf.NET.Graphics;
using OxidizePdf.NET.Tests.TestHelpers;
using Xunit;

namespace OxidizePdf.NET.Tests.Graphics;

/// <summary>
/// GFX-021 Soft masks. A soft mask is applied through an ExtGState whose
/// <c>/SMask</c> entry references a Form XObject acting as the mask source
/// (ISO 32000-1 §11.6.4.3). Verifies the written <c>/SMask</c> dictionary
/// (type, subtype, indirect <c>/G</c> reference) and the <c>gs</c> operator
/// in the content stream.
/// </summary>
public class PdfPageSoftMaskIntegrationTests
{
    // A mid-grey square serves as a luminosity/alpha mask source.
    private static byte[] MaskContent() =>
        Encoding.ASCII.GetBytes("0.5 0.5 0.5 rg\n0 0 100 100 re\nf\n");

    private static PdfFormXObject MaskForm() => new(
        x: 0, y: 0, width: 100, height: 100, content: MaskContent());

    [Fact]
    [Trait("Category", "Integration")]
    public void SoftMask_Luminosity_EmitsSMaskDictWithIndirectGroupAndGsOperator()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.AddFormXObject("Mask1", MaskForm())
            .ApplySoftMask(PdfSoftMask.Luminosity("Mask1"));
        doc.AddPage(page);

        var bytes = doc.SaveToBytes();
        var pdfText = ContentStreamHelper.ToLatin1(bytes);
        Assert.Contains("/SMask", pdfText);
        Assert.Contains("/S /Luminosity", pdfText);
        Assert.Contains("/Type /Mask", pdfText);
        // /G must be an indirect reference to the registered FormXObject, not a /Name.
        Assert.Matches(@"/G\s+\d+\s+0\s+R", pdfText);

        var stream = ContentStreamHelper.DecompressAllContentStreams(bytes);
        Assert.Matches(@"/\w+\s+gs", stream);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SoftMask_Alpha_EmitsAlphaSubtype()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.AddFormXObject("Mask1", MaskForm())
            .ApplySoftMask(PdfSoftMask.Alpha("Mask1"));
        doc.AddPage(page);

        var pdfText = ContentStreamHelper.ToLatin1(doc.SaveToBytes());
        Assert.Contains("/SMask", pdfText);
        Assert.Contains("/S /Alpha", pdfText);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SoftMask_None_EmitsNoneSubtypeWithoutRegisteredForm()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        // A None mask disables masking and needs no group source.
        page.ApplySoftMask(PdfSoftMask.None());
        doc.AddPage(page);

        var pdfText = ContentStreamHelper.ToLatin1(doc.SaveToBytes());
        Assert.Contains("/SMask", pdfText);
        Assert.Contains("/S /None", pdfText);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SoftMask_UnregisteredGroup_SaveThrows()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        // Reference a form that was never registered: the writer must refuse to
        // emit a spec-invalid /G /Name token (ISO 32000-1 Table 144).
        page.ApplySoftMask(PdfSoftMask.Luminosity("DoesNotExist"));
        doc.AddPage(page);

        Assert.ThrowsAny<Exception>(() => doc.SaveToBytes());
    }

    [Fact]
    public void PdfSoftMask_NullGroupReference_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PdfSoftMask.Luminosity(null!));
        Assert.Throws<ArgumentNullException>(() => PdfSoftMask.Alpha(null!));
    }

    [Fact]
    public void PdfSoftMask_EmptyGroupReference_Throws()
    {
        Assert.Throws<ArgumentException>(() => PdfSoftMask.Luminosity(""));
        Assert.Throws<ArgumentException>(() => PdfSoftMask.Alpha(""));
    }
}
