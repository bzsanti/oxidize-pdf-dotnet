using System.Text;
using OxidizePdf.NET;
using OxidizePdf.NET.Models;

namespace OxidizePdf.NET.Tests;

/// <summary>
/// Behavioral tests for Tagged PDF: PAGE-009 (marked content) and DOC-019
/// (structure tree). Pages are serialized uncompressed and the emitted content
/// stream / catalog objects are asserted directly.
/// </summary>
public class PdfTaggedContentTests
{
    private static readonly PdfSaveOptions Uncompressed = new() { CompressStreams = false };

    [Fact]
    public void BeginMarkedContent_EmitsBdcEmc_AndReturnsMcid()
    {
        byte[] bytes;
        int mcid;
        using (var doc = new PdfDocument())
        using (var page = new PdfPage(300, 400))
        {
            mcid = page.BeginMarkedContent("P");
            page.DrawTextAt(StandardFont.Helvetica, 12, 20, 360, "TAGGED_TEXT");
            page.EndMarkedContent();
            doc.AddPage(page);
            bytes = doc.SaveToBytes(Uncompressed);
        }

        Assert.Equal(0, mcid);
        var pdf = Encoding.Latin1.GetString(bytes);
        Assert.Contains("/P <</MCID 0>> BDC", pdf);
        Assert.Contains("EMC", pdf);
        Assert.Contains("TAGGED_TEXT", pdf);
        Assert.True(pdf.IndexOf("BDC", StringComparison.Ordinal) < pdf.IndexOf("EMC", StringComparison.Ordinal),
            "BDC must precede EMC");
    }

    [Fact]
    public void EndMarkedContent_WithoutBegin_Throws()
    {
        using var page = new PdfPage(300, 400);
        Assert.Throws<PdfExtractionException>(() => page.EndMarkedContent());
    }

    [Fact]
    public void SetStructureTree_ProducesTaggedPdf_WithStructTreeRootAndElements()
    {
        // Ship criterion: PDF/UA basis — a tagged document with a structure tree
        // whose element references the page's marked content (MCID).
        byte[] bytes;
        using (var doc = new PdfDocument())
        using (var page = new PdfPage(595, 842))
        {
            int mcid = page.BeginMarkedContent("P");
            page.DrawTextAt(StandardFont.Helvetica, 12, 72, 770, "ACCESSIBLE");
            page.EndMarkedContent();
            doc.AddPage(page);

            var tree = new PdfStructureTree();
            int root = tree.AddRoot("Document");
            tree.AddChild(root, "P", lang: "en-US", actualText: "ACCESSIBLE",
                mcids: new[] { (0, mcid) });
            doc.SetStructureTree(tree);

            bytes = doc.SaveToBytes(Uncompressed);
        }

        var pdf = Encoding.Latin1.GetString(bytes);
        Assert.Contains("/StructTreeRoot", pdf);                       // catalog reference + object
        Assert.Contains("/MarkInfo", pdf);                            // marked as tagged
        Assert.Contains("/StructElem", pdf);                          // structure elements
        Assert.True(pdf.Contains("/S /P") || pdf.Contains("/S/P"), "must emit the /P element");
        Assert.Contains("/MCID 0", pdf);                              // links to marked content
    }

    [Fact]
    public void SetStructureTree_EmptyTree_Throws()
    {
        using var doc = new PdfDocument();
        Assert.Throws<ArgumentException>(() => doc.SetStructureTree(new PdfStructureTree()));
    }

    [Fact]
    public void StructureTree_AddChildBeforeRoot_Throws()
    {
        var tree = new PdfStructureTree();
        // AddChild with no root yet → invalid parent index.
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.AddChild(0, "P"));
    }

    [Fact]
    public void StructureTree_SecondRoot_Throws()
    {
        var tree = new PdfStructureTree();
        tree.AddRoot("Document");
        Assert.Throws<InvalidOperationException>(() => tree.AddRoot("Document"));
    }
}
