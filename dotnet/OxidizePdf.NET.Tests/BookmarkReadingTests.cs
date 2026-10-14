using System.Text;
using OxidizePdf.NET.Models;

namespace OxidizePdf.NET.Tests;

public class BookmarkReadingTests
{
    [Fact]
    public async Task ReadBookmarks_PreservesHierarchyStyleAndResolvedPages()
    {
        using var doc = new PdfDocument();
        using var first = PdfPage.A4();
        using var second = PdfPage.A4();
        doc.AddPage(first).AddPage(second);
        var outline = new PdfOutline();
        outline.AddItem(new PdfOutlineItem("Parent", 0)
        {
            IsBold = true, IsOpen = false,
            Children = [new PdfOutlineItem("Child", 1) { IsItalic = true }]
        });
        outline.AddItem(new PdfOutlineItem("Sibling", 0));
        doc.SetOutline(outline);
        var pdf = doc.SaveToBytes();
        var extractor = new PdfExtractor();
        var items = await extractor.GetBookmarksAsync(pdf);
        Assert.Equal(new[] { "Parent", "Child", "Sibling" }, items.Select(x => x.Title));
        Assert.Null(items[0].ParentIndex);
        Assert.True(items[0].IsBold);
        Assert.False(items[0].IsOpen);
        Assert.Equal(0, items[1].ParentIndex);
        Assert.True(items[1].IsItalic);
        Assert.Equal(1u, items[1].Destination!.PageIndex);
        Assert.Null(items[2].ParentIndex);
        await Assert.ThrowsAsync<PdfExtractionException>(() => extractor.GetBookmarksAsync(pdf,
            new BookmarkReadOptions { MaxItems = 1 }));
    }

    [Fact]
    public async Task ReadBookmarks_DeepSiblingListsRoundTripWithoutDuplicateLinks()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        doc.AddPage(page);
        var outline = new PdfOutline();
        outline.AddItem(new PdfOutlineItem("Root", 0)
        {
            Children = [
                new PdfOutlineItem("BranchA", 0) { Children = [new PdfOutlineItem("LeafA", 0)] },
                new PdfOutlineItem("BranchB", 0) { Children = [new PdfOutlineItem("LeafB", 0)] },
                new PdfOutlineItem("BranchC", 0)
            ]
        });
        outline.AddItem(new PdfOutlineItem("Root2", 0) { Children = [new PdfOutlineItem("Leaf2", 0)] });
        doc.SetOutline(outline);
        var items = await new PdfExtractor().GetBookmarksAsync(doc.SaveToBytes());
        Assert.Equal(new[] { "Root", "BranchA", "LeafA", "BranchB", "LeafB", "BranchC", "Root2", "Leaf2" }, items.Select(i => i.Title));
        Assert.Equal(new int?[] { null, 0, 1, 0, 3, 0, null, 6 }, items.Select(i => i.ParentIndex));
    }

    [Fact]
    public async Task ReadBookmarks_ValidNestedTreeKeepsParentIndexesAndClosedState()
    {
        var pdf = Assemble(new[]
        {
            "<< /Type /Catalog /Pages 2 0 R /Outlines 4 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>",
            "<< /Type /Outlines /First 5 0 R /Last 7 0 R /Count 2 >>",
            "<< /Title (Parent) /Parent 4 0 R /First 6 0 R /Last 6 0 R /Count -1 /Next 7 0 R /F 2 /Dest [3 0 R /Fit] >>",
            "<< /Title (Child) /Parent 5 0 R /F 1 /Dest [3 0 R /Fit] >>",
            "<< /Title (Sibling) /Parent 4 0 R /Prev 5 0 R /Dest [3 0 R /Fit] >>"
        });
        var extractor = new PdfExtractor();
        var items = await extractor.GetBookmarksAsync(pdf);
        Assert.Equal(new[] { "Parent", "Child", "Sibling" }, items.Select(x => x.Title));
        Assert.Null(items[0].ParentIndex);
        Assert.False(items[0].IsOpen);
        Assert.True(items[0].IsBold);
        Assert.Equal(0, items[1].ParentIndex);
        Assert.True(items[1].IsItalic);
        Assert.Null(items[2].ParentIndex);
        await Assert.ThrowsAsync<PdfExtractionException>(() => extractor.GetBookmarksAsync(pdf,
            new BookmarkReadOptions { MaxItems = 1 }));
    }

    [Fact]
    public async Task ReadBookmarks_ResolvesEveryDestinationViewAndNamedGoTo()
    {
        var modes = new[] { "/XYZ null 12 1.5", "/Fit", "/FitH 13", "/FitV 14", "/FitR 1 2 3 4", "/FitB", "/FitBH 16", "/FitBV 17" };
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R /Outlines 4 0 R /Dests << /named [3 0 R /FitBV 17] >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>",
            "<< /Type /Outlines /First 5 0 R /Last 12 0 R /Count 8 >>"
        };
        for (var i = 0; i < modes.Length; i++)
        {
            var prev = i == 0 ? "" : $"/Prev {i + 4} 0 R";
            var next = i == 7 ? "" : $"/Next {i + 6} 0 R";
            var dest = i == 7 ? "/A << /S /GoTo /D /named >>" : $"/Dest [3 0 R {modes[i]}]";
            objects.Add($"<< /Title (Item{i}) /Parent 4 0 R {prev} {next} {dest} /F 3 /C [0.1 0.2 0.3] >>");
        }
        var items = await new PdfExtractor().GetBookmarksAsync(Assemble(objects));
        Assert.Equal(8, items.Count);
        Assert.Equal(Enum.GetValues<BookmarkViewMode>(), items.Select(x => x.Destination!.ViewMode));
        Assert.All(items, item =>
        {
            Assert.Equal(0u, item.Destination!.PageIndex);
            Assert.True(item.IsBold && item.IsItalic);
            Assert.Equal(new[] { 0.1, 0.2, 0.3 }, item.Color);
        });
        Assert.Null(items[0].Destination!.Left);
        Assert.Equal(1.5, items[0].Destination!.Zoom);
        Assert.Equal(12, items[0].Destination!.Top);
        Assert.Equal(2, items[4].Destination!.Bottom);
        Assert.Equal(3, items[4].Destination!.Right);
        Assert.Equal(17, items[7].Destination!.Left);
    }

    [Fact]
    public async Task ReadBookmarks_EmptyOutlineAndInvalidLimits()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        doc.AddPage(page);
        var pdf = doc.SaveToBytes();
        var extractor = new PdfExtractor();
        Assert.Empty(await extractor.GetBookmarksAsync(pdf));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => extractor.GetBookmarksAsync(pdf,
            new BookmarkReadOptions { MaxDepth = 257 }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => extractor.GetBookmarksAsync(pdf,
            new BookmarkReadOptions { MaxNameTreeNodes = 0 }));
    }

    private static byte[] Assemble(IReadOnlyList<string> objects)
    {
        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append(i + 1).Append(" 0 obj\n").Append(objects[i]).Append("\nendobj\n");
        }
        var xref = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets) pdf.Append(offset.ToString("D10")).Append(" 00000 n \n");
        pdf.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }
}
