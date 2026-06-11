using System.Text;
using OxidizePdf.NET;
using OxidizePdf.NET.Models;

namespace OxidizePdf.NET.Tests;

/// <summary>
/// Behavioral tests for PAGE-011 (custom screen-space coordinate system). Each
/// test switches a page to screen-space (top-left origin, Y down) and asserts
/// the exact Y-flip transformation matrix emitted into the content stream, plus
/// that subsequent draw operations compose after it. Y-flip mirrors text
/// glyphs, so coverage is on shape/line/path draw ops per the ship criterion.
/// </summary>
public class PdfPageCoordinateSystemTests
{
    private static readonly PdfSaveOptions Uncompressed = new() { CompressStreams = false };

    private static string BuildAndDump(double width, double height, Action<PdfPage> draw)
    {
        byte[] bytes;
        using (var doc = new PdfDocument())
        using (var page = new PdfPage(width, height))
        {
            draw(page);
            doc.AddPage(page);
            bytes = doc.SaveToBytes(Uncompressed);
        }
        return Encoding.Latin1.GetString(bytes);
    }

    [Fact]
    public void BeginScreenSpace_NullScale_Throws()
    {
        using var page = new PdfPage(200, 300);
        Assert.Throws<PdfExtractionException>(() => page.BeginScreenSpace(0.0));
    }

    [Fact]
    public void BeginScreenSpace_EmitsExactYFlipMatrixForPageHeight()
    {
        var pdf = BuildAndDump(200, 300, page =>
        {
            page.BeginScreenSpace();
            page.MoveTo(10, 20).LineTo(110, 20).Stroke();
        });

        // 300-high page, scale 1.0 → top-left origin maps to PDF standard via
        // [1 0 0 -1 0 300]: user (x,y) -> (x, 300 - y).
        Assert.Contains("1.00 0.00 0.00 -1.00 0.00 300.00 cm", pdf);

        // The CTM must be emitted before the draw operators it governs.
        var cmIndex = pdf.IndexOf("1.00 0.00 0.00 -1.00 0.00 300.00 cm", StringComparison.Ordinal);
        var moveIndex = pdf.IndexOf("10.00 20.00 m", StringComparison.Ordinal);
        Assert.True(cmIndex >= 0 && moveIndex > cmIndex,
            "screen-space CTM must precede the draw operations");
    }

    [Fact]
    public void BeginScreenSpace_ScaleFactor_AppliesToBothAxesAndHeightOffset()
    {
        var pdf = BuildAndDump(100, 100, page =>
        {
            page.BeginScreenSpace(2.0);
            page.MoveTo(0, 0).LineTo(10, 10).Stroke();
        });

        // scale 2.0 on a 100-high page: [2 0 0 -2 0 200].
        Assert.Contains("2.00 0.00 0.00 -2.00 0.00 200.00 cm", pdf);
    }

    [Fact]
    public void BeginScreenSpace_ThreeDrawOps_AllComposeUnderCoordinateSwitch()
    {
        // Ship criterion #2: coordinate origin switch tested on >= 3 drawing ops.
        var pdf = BuildAndDump(200, 300, page =>
        {
            page.BeginScreenSpace();
            // Op 1: horizontal line near the top in screen-space.
            page.MoveTo(10, 20).LineTo(110, 20).Stroke();
            // Op 2: diagonal line.
            page.MoveTo(10, 40).LineTo(110, 140).Stroke();
            // Op 3: closed triangular path.
            page.MoveTo(50, 50).LineTo(50, 150).LineTo(150, 150).ClosePath().Stroke();
        });

        // Coordinate switch present.
        Assert.Contains("1.00 0.00 0.00 -1.00 0.00 300.00 cm", pdf);

        // Each draw op contributes deterministic operands, proving all three are
        // emitted in the screen-space coordinate space (operands are untransformed;
        // the CTM applies the flip at render time).
        Assert.Contains("10.00 20.00 m", pdf);    // op 1 move
        Assert.Contains("110.00 20.00 l", pdf);   // op 1 line
        Assert.Contains("110.00 140.00 l", pdf);  // op 2 line
        Assert.Contains("h\n", pdf);              // op 3 close path

        // Three stroked shapes → three `S` operators after the coordinate switch.
        var cmIndex = pdf.IndexOf("0.00 -1.00 0.00 300.00 cm", StringComparison.Ordinal);
        var afterCm = pdf[cmIndex..];
        var strokeCount = afterCm.Split("\nS\n").Length - 1;
        Assert.True(strokeCount >= 3, $"expected >= 3 stroke ops after the CTM, found {strokeCount}");
    }
}
