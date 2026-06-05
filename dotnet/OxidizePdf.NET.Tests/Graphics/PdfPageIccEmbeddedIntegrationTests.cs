using OxidizePdf.NET.Graphics;
using OxidizePdf.NET.Tests.TestHelpers;
using Xunit;

namespace OxidizePdf.NET.Tests.Graphics;

/// <summary>
/// Tests for the embedded-profile ICC path: AddIccColorSpace(name, IccProfile)
/// followed by SetFillColorIcc. This path is a .NET superset not in the Python bridge.
/// </summary>
public class PdfPageIccEmbeddedIntegrationTests
{
    // Minimal valid ICC-like data — real viewers would reject this, but the
    // PDF writer embeds it verbatim without validating the ICC structure.
    private static byte[] MinimalIccData() => new byte[128];

    [Fact]
    [Trait("Category", "Integration")]
    public void AddIccColorSpace_ThenSetFillColorIcc_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        var profile = new IccProfile("EmbeddedRGB", MinimalIccData(), IccColorSpace.Rgb);
        Assert.Same(page, page.AddIccColorSpace("EmbeddedRGB", profile)
            .SetFillColorIcc("EmbeddedRGB", new double[] { 0.5, 0.3, 0.8 }));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void AddIccColorSpace_NullProfile_Throws()
    {
        using var page = PdfPage.A4();
        Assert.Throws<ArgumentNullException>(() => page.AddIccColorSpace("EmbeddedRGB", null!));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void IccEmbedded_FilledRect_PdfContainsICCBasedResource()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        var profile = new IccProfile("EmbeddedRGB", MinimalIccData(), IccColorSpace.Rgb);
        page.AddIccColorSpace("EmbeddedRGB", profile)
            .SetFillColorIcc("EmbeddedRGB", new double[] { 0.5, 0.3, 0.8 })
            .DrawRect(50, 50, 200, 200)
            .Fill();
        doc.AddPage(page);
        var pdfText = ContentStreamHelper.ToLatin1(doc.SaveToBytes());
        Assert.Contains("ICCBased", pdfText);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void IccEmbedded_ContentStream_ContainsCsAndScOperators()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        var profile = new IccProfile("EmbeddedRGB", MinimalIccData(), IccColorSpace.Rgb);
        page.AddIccColorSpace("EmbeddedRGB", profile)
            .SetFillColorIcc("EmbeddedRGB", new double[] { 0.5, 0.3, 0.8 })
            .DrawRect(50, 50, 200, 200)
            .Fill();
        doc.AddPage(page);
        var pdfBytes = doc.SaveToBytes();

        // The embedded ICC profile occupies stream 0; the page content stream is stream 1.
        // We scan all streams for the one containing PDF graphics operators.
        string? contentStream = null;
        for (int idx = 0; idx < 10; idx++)
        {
            var candidate = ContentStreamHelper.DecompressContentStreamAt(pdfBytes, idx);
            if (candidate == null) break;
            if (candidate.Contains("/EmbeddedRGB cs") && candidate.Contains("sc\n"))
            {
                contentStream = candidate;
                break;
            }
        }

        Assert.NotNull(contentStream);
        Assert.Contains("/EmbeddedRGB cs", contentStream);
        Assert.Contains("sc\n", contentStream);
    }
}
