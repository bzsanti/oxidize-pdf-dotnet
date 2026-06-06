using OxidizePdf.NET;
using OxidizePdf.NET.Tests.TestHelpers;
using Xunit;

namespace OxidizePdf.NET.Tests.Graphics;

/// <summary>
/// GFX-024 Elliptical clipping. Verifies that <see cref="PdfPage.ClipEllipse"/>
/// emits the ellipse path (move + four Bézier curves + close) followed by the
/// <c>W n</c> clip operators, and rejects non-positive radii.
/// </summary>
public class PdfPageClipEllipseIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void ClipEllipse_EmitsPathAndClipOperatorsInContentStream()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.ClipEllipse(300.0, 400.0, 100.0, 50.0);
        // Draw something inside the clip so the page has content.
        page.DrawTextAt(StandardFont.Helvetica, 12.0, 280.0, 395.0, "clipped");
        doc.AddPage(page);

        var stream = ContentStreamHelper.DecompressAllContentStreams(doc.SaveToBytes());
        // MoveTo the top of the ellipse: cx, cy+ry = 300, 450 (three-decimal serialisation).
        Assert.Contains("300.000 450.000 m", stream);
        // Four cubic Bézier quarters.
        Assert.Equal(4, CountOccurrences(stream, " c\n"));
        // Clip with nonzero winding, then end the path without painting (the
        // canonical "W n" clip idiom emitted on consecutive lines).
        Assert.Contains("W\nn\n", stream);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void ClipEllipse_RightmostExtentCurveUsesKappaControlPoints()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        // cx=200, cy=300, rx=80, ry=40. KAPPA*rx = 44.183, KAPPA*ry = 22.091.
        // First quarter ends at the rightmost point (cx+rx, cy) = (280, 300).
        page.ClipEllipse(200.0, 300.0, 80.0, 40.0);
        page.DrawTextAt(StandardFont.Helvetica, 10.0, 190.0, 295.0, ".");
        doc.AddPage(page);

        var stream = ContentStreamHelper.DecompressAllContentStreams(doc.SaveToBytes());
        // First curve: cx+kx cy+ry, cx+rx cy+ky, cx+rx cy
        //            = 244.183 340.000 280.000 322.091 280.000 300.000 c
        Assert.Contains("244.183 340.000 280.000 322.091 280.000 300.000 c", stream);
    }

    [Theory]
    [InlineData(0.0, 50.0)]
    [InlineData(50.0, 0.0)]
    [InlineData(-10.0, 50.0)]
    [InlineData(50.0, -10.0)]
    public void ClipEllipse_NonPositiveRadius_Throws(double rx, double ry)
    {
        using var page = PdfPage.A4();
        Assert.Throws<ArgumentOutOfRangeException>(() => page.ClipEllipse(100.0, 100.0, rx, ry));
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }
}
