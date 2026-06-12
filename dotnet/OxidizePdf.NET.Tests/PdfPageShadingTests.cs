using System.Text;
using OxidizePdf.NET;
using OxidizePdf.NET.Models;

namespace OxidizePdf.NET.Tests;

/// <summary>
/// Behavioral tests for axial/radial gradient shadings (GFX-017). Each test
/// builds a page that defines a shading and paints it bounded by a clip rect,
/// serializes WITHOUT compression, and asserts the emitted shading dictionary
/// (an indirect object) and the `sh` paint operator in the content stream.
/// </summary>
public class PdfPageShadingTests
{
    private static readonly PdfSaveOptions Uncompressed = new() { CompressStreams = false };

    private static string BuildAndDump(Action<PdfPage> draw)
    {
        byte[] bytes;
        using (var doc = new PdfDocument())
        using (var page = PdfPage.A4())
        {
            draw(page);
            doc.AddPage(page);
            bytes = doc.SaveToBytes(Uncompressed);
        }

        // PDF tokens are ASCII; Latin1 round-trips every byte without loss.
        return Encoding.Latin1.GetString(bytes);
    }

    [Fact]
    public void AxialShading_EmitsType2DictAndShOperator()
    {
        var pdf = BuildAndDump(page =>
        {
            page.AddAxialShading("Grad1", 50, 50, 250, 50, new[]
            {
                new GradientStop(0.0, 1.0, 0.0, 0.0),
                new GradientStop(1.0, 0.0, 0.0, 1.0),
            });
            page.SaveGraphicsState()
                .ClipRect(50, 50, 200, 100)
                .PaintShading("Grad1")
                .RestoreGraphicsState();
        });

        Assert.Contains("/ShadingType 2", pdf);
        Assert.Contains("/Coords", pdf);
        Assert.Contains("/Grad1 sh", pdf);
    }

    [Fact]
    public void RadialShading_EmitsType3DictAndShOperator()
    {
        var pdf = BuildAndDump(page =>
        {
            page.AddRadialShading("Glow", 150, 150, 0, 150, 150, 80, new[]
            {
                new GradientStop(0.0, 1.0, 1.0, 1.0),
                new GradientStop(1.0, 0.0, 0.0, 0.0),
            });
            page.SaveGraphicsState()
                .ClipRect(70, 70, 160, 160)
                .PaintShading("Glow")
                .RestoreGraphicsState();
        });

        Assert.Contains("/ShadingType 3", pdf);
        Assert.Contains("/Glow sh", pdf);
    }

    [Fact]
    public void Shading_IsReferencedFromPageResources()
    {
        var pdf = BuildAndDump(page =>
        {
            page.AddAxialShading("G", 0, 0, 100, 0, new[]
            {
                new GradientStop(0.0, 0.0, 0.0, 0.0),
                new GradientStop(1.0, 1.0, 1.0, 1.0),
            });
            page.PaintShading("G");
        });

        Assert.Contains("/Shading", pdf);
    }

    [Fact]
    public void AxialShading_ExtendFlags_RoundTrip()
    {
        var pdf = BuildAndDump(page =>
        {
            page.AddAxialShading("E", 0, 0, 100, 0, new[]
            {
                new GradientStop(0.0, 0.0, 0.0, 0.0),
                new GradientStop(1.0, 1.0, 1.0, 1.0),
            }, extendStart: true, extendEnd: true);
            page.PaintShading("E");
        });

        Assert.Contains("/Extend [true true]", pdf);
    }

    [Fact]
    public void AddAxialShading_NullName_Throws()
    {
        using var page = PdfPage.A4();
        Assert.ThrowsAny<ArgumentException>(() =>
            page.AddAxialShading(null!, 0, 0, 1, 0, new[]
            {
                new GradientStop(0.0, 0, 0, 0),
                new GradientStop(1.0, 1, 1, 1),
            }));
    }

    [Fact]
    public void AddAxialShading_OneStop_Throws()
    {
        using var page = PdfPage.A4();
        Assert.Throws<ArgumentException>(() =>
            page.AddAxialShading("G", 0, 0, 1, 0, new[] { new GradientStop(0.0, 0, 0, 0) }));
    }

    [Fact]
    public void PaintShading_NullName_Throws()
    {
        using var page = PdfPage.A4();
        Assert.ThrowsAny<ArgumentException>(() => page.PaintShading(null!));
    }
}
