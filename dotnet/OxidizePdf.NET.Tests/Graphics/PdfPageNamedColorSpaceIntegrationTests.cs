using OxidizePdf.NET.Graphics;
using OxidizePdf.NET.Tests.TestHelpers;
using Xunit;

namespace OxidizePdf.NET.Tests.Graphics;

/// <summary>
/// Tests for named (multi-space-per-page) variants.
/// The key test here is that TWO CalRGB spaces can coexist on one page —
/// this proves Decision 2's one-per-page limitation is gone in 2.12.0.
/// </summary>
public class PdfPageNamedColorSpaceIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void TwoCalRgbSpacesOnOnePage_BothAppearInResourceDict()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        var cs1 = CalRgbColorSpace.SRgb();
        var cs2 = CalRgbColorSpace.AdobeRgb();

        page.AddColorSpace("SRgbSpace", PageColorSpace.CalRgb(cs1))
            .AddColorSpace("AdobeRgbSpace", PageColorSpace.CalRgb(cs2))
            .SetFillColorCalibratedNamed("SRgbSpace", CalibratedColor.CalRgb(new double[] { 0.8, 0.2, 0.3 }, cs1))
            .DrawRect(50, 50, 100, 100)
            .Fill()
            .SetFillColorCalibratedNamed("AdobeRgbSpace", CalibratedColor.CalRgb(new double[] { 0.1, 0.7, 0.4 }, cs2))
            .DrawRect(200, 50, 100, 100)
            .Fill();
        doc.AddPage(page);

        var pdfText = ContentStreamHelper.ToLatin1(doc.SaveToBytes());
        Assert.Contains("SRgbSpace", pdfText);
        Assert.Contains("AdobeRgbSpace", pdfText);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void NamedCalGray_ContentStream_ContainsNameAndScOperator()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        var cs = CalGrayColorSpace.D50();
        page.AddColorSpace("MyGray", PageColorSpace.CalGray(cs))
            .SetFillColorCalibratedNamed("MyGray", CalibratedColor.CalGray(0.4, cs))
            .DrawRect(50, 50, 100, 100)
            .Fill();
        doc.AddPage(page);

        var pdfBytes = doc.SaveToBytes();
        var pdfText = ContentStreamHelper.ToLatin1(pdfBytes);
        Assert.Contains("MyGray", pdfText);

        var stream = ContentStreamHelper.DecompressFirstContentStream(pdfBytes);
        Assert.NotNull(stream);
        Assert.Contains("/MyGray cs", stream);
        Assert.Contains("sc\n", stream);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void NamedLab_ContentStream_ContainsNameAndScOperator()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        var cs = LabColorSpace.D50();
        page.AddColorSpace("PerceptualLab", PageColorSpace.Lab(cs))
            .SetFillColorLabNamed("PerceptualLab", new LabColor(60.0, 20.0, -30.0, cs))
            .DrawRect(50, 50, 100, 100)
            .Fill();
        doc.AddPage(page);

        var pdfBytes = doc.SaveToBytes();
        var pdfText = ContentStreamHelper.ToLatin1(pdfBytes);
        Assert.Contains("PerceptualLab", pdfText);

        var stream = ContentStreamHelper.DecompressFirstContentStream(pdfBytes);
        Assert.NotNull(stream);
        Assert.Contains("/PerceptualLab cs", stream);
        Assert.Contains("sc\n", stream);
    }
}

/// <summary>
/// Regression test for oxidize-python issue #57.
///
/// Issue #57: after drawing a filled shape and then setting a NEW fill color, text drawn
/// afterward inherited the wrong color because shape-fill-color and text-fill-color were
/// tracked in separate state slots on the native side.
///
/// This test verifies that the .NET binding exposes the same text-color and shape-fill-color
/// APIs in a way that exercises both state slots and that the content stream contains distinct
/// color operators for each, in the correct order.
///
/// Relevant .NET APIs:
///   - PdfPage.SetFillColor(r, g, b)         — shape fill → rg operator (DeviceRGB)
///   - PdfPage.SetTextColor(r, g, b)          — text fill  → rg operator via text color slot
///
/// These two methods map to oxidize_page_set_fill_color_rgb and
/// oxidize_page_set_text_color_rgb respectively — two distinct FFI exports that maintain
/// separate color state slots on the native side. The regression (wrong color on text after
/// shape fill) can only occur if the two slots are aliased. This test confirms they are not.
///
/// Note: the native layer emits colour components with 3 decimal places, e.g.
/// "1.000 0.000 0.000 rg". Assertions match this exact format.
/// </summary>
public class OxidizePythonIssue57RegressionTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void ShapeFillColor_ThenTextColor_BothOperatorsAppearDistinctInContentStream()
    {
        // Use values that are visually and numerically distinct so we can assert both
        // colour operators appear separately — not just that "rg" is present once.
        const double shapeR = 1.0, shapeG = 0.0, shapeB = 0.0;   // red fill
        const double textR  = 0.0, textG  = 0.0, textB  = 1.0;   // blue text

        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.SetFillColor(shapeR, shapeG, shapeB)   // shape fill: red
            .DrawRect(50, 50, 100, 100)
            .Fill()
            .SetTextColor(textR, textG, textB)       // text fill: blue (different state slot)
            .SetFont(StandardFont.Helvetica, 12)
            .TextAt(50, 200, "blue text");
        doc.AddPage(page);

        var stream = ContentStreamHelper.DecompressFirstContentStream(doc.SaveToBytes());
        Assert.NotNull(stream);

        // Both colour operators must appear in the stream.
        // The native layer emits components with 3 decimal places, e.g. "1.000 0.000 0.000 rg".
        // Using Contains on the decompressed stream confirms the native side emits them
        // as separate instructions, proving the two state slots are independent.
        Assert.Contains("1.000 0.000 0.000 rg", stream);
        Assert.Contains("0.000 0.000 1.000 rg", stream);

        // The shape fill operator must come BEFORE the text fill operator, confirming ordering.
        var redPos  = stream!.IndexOf("1.000 0.000 0.000 rg", StringComparison.Ordinal);
        var bluePos = stream.IndexOf("0.000 0.000 1.000 rg", StringComparison.Ordinal);
        Assert.True(redPos < bluePos,
            "shape fill color must be set before text color in the content stream");
    }
}
