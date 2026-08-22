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
    public void ConicShading_EmitsType1DictAndShOperator()
    {
        var pdf = BuildAndDump(page =>
        {
            page.AddConicShading("Cone", 100, 100, 0, 200, 0, 200, new[]
            {
                new GradientStop(0.0, 1.0, 0.0, 0.0),
                new GradientStop(1.0, 0.0, 0.0, 1.0),
            });
            page.PaintShading("Cone");
        });

        Assert.Contains("/ShadingType 1", pdf);
        Assert.Contains("/Cone sh", pdf);
    }

    [Fact]
    public void GouraudMeshShading_EmitsType4DictAndShOperator()
    {
        var pdf = BuildAndDump(page =>
        {
            page.AddGouraudMeshShading("Mesh", new[]
            {
                new GouraudVertex(0, 50, 50, 1, 0, 0),
                new GouraudVertex(0, 250, 50, 0, 1, 0),
                new GouraudVertex(0, 150, 250, 0, 0, 1),
            }, 0, 300, 0, 300);
            page.PaintShading("Mesh");
        });

        Assert.Contains("/ShadingType 4", pdf);
        Assert.Contains("/Mesh sh", pdf);
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

    [Fact]
    public void NewShadings_RejectInvalidGeometryColorsFlagsAndBits()
    {
        using var page = PdfPage.A4();
        var stops = new[]
        {
            new GradientStop(0, 0, 0, 0),
            new GradientStop(1, 1, 1, 1)
        };
        Assert.Throws<ArgumentException>(() =>
            page.AddConicShading("C", 0, 0, 1, 0, 0, 1, stops));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            page.AddConicShading("C", 0, 0, 0, 1, 0, 1,
                [new GradientStop(0, double.NaN, 0, 0), stops[1]]));

        var vertices = new[]
        {
            new GouraudVertex(0, 0, 0, 1, 0, 0),
            new GouraudVertex(0, 1, 0, 0, 1, 0),
            new GouraudVertex(0, 0, 1, 0, 0, 1)
        };
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            page.AddGouraudMeshShading("M", vertices, 0, 1, 0, 1, bitsPerFlag: 3));
        Assert.Throws<ArgumentException>(() =>
            page.AddGouraudMeshShading("M", [vertices[0], vertices[1] with { Flag = 3 }, vertices[2]], 0, 1, 0, 1));
    }
}
