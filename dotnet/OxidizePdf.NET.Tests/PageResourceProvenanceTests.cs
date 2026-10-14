using System.Text;
using OxidizePdf.NET.Models;

namespace OxidizePdf.NET.Tests;

public class PageResourceProvenanceTests
{
    // Independent tree: root 2 -> ancestor 3 -> pages 4 and 5. Shared Resources is 6.
    private static byte[] Pdf(string root = "", string parent = "", string page = "",
        string parentLink = "2 0 R", string pageLink = "3 0 R", string shared = "<< /Font 7 0 R >>")
    {
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Kids [3 0 R] /Count 2 /MediaBox [0 0 100 200] {root} >>",
            $"<< /Type /Pages /Parent {parentLink} /Kids [4 0 R 5 0 R] /Count 2 {parent} >>",
            $"<< /Type /Page /Parent {pageLink} {page} >>",
            "<< /Type /Page /Parent 3 0 R /Resources << >> >>",
            shared,
            "<< /Fz 8 7 R /Fa << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>"
        ];
        using var output = new MemoryStream();
        void Write(string text) => output.Write(Encoding.ASCII.GetBytes(text));
        Write("%PDF-1.4\n");
        var offsets = new List<long>();
        for (int i = 0; i < objects.Length; i++)
        {
            offsets.Add(output.Position);
            Write($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = output.Position;
        Write($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets) Write($"{offset:D10} 00000 n \n");
        Write($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return output.ToArray();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectOwnershipDoesNotDependOnDictionaryIndirection(bool indirect)
    {
        var bytes = Pdf(root: "/Resources << /Font << /ParentFont 8 0 R >> >>",
            page: indirect ? "/Resources 6 0 R" : "/Resources << /Font 7 0 R >>");
        var result = await new PdfExtractor().GetPageResourceProvenanceAsync(bytes, 1);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(new PdfIndirectObjectId(4, 0), result.PageReference);
        Assert.Equal(PageResourceOrigin.Direct, result.Origin);
        Assert.Equal(new PdfIndirectObjectId(4, 0), result.SourceReference);
        Assert.Equal(0, result.AncestorDepth);
        Assert.Equal(indirect ? new PdfIndirectObjectId(6, 0) : (PdfIndirectObjectId?)null, result.ResourceDictionaryReference);
        Assert.Equal(new[] { "Font" }, result.ResolvedResources.ResourceKeys);
        Assert.Equal(new[] { "Fa", "Fz" }, result.ResolvedResources.FontNames);
        Assert.False(result.ResolvedResources.HasXObjects);
        Assert.Collection(result.Entries,
            entry => { Assert.Equal("Font", entry.Category); Assert.Equal("Fa", entry.Name); Assert.Null(entry.Reference); },
            entry => { Assert.Equal("Font", entry.Category); Assert.Equal("Fz", entry.Name); Assert.Equal(new PdfIndirectObjectId(8, 7), entry.Reference); });
        // Entry objects are intentionally not loaded: reference 8 7 R need not resolve.
    }

    [Theory]
    [InlineData(false, false, 2, 2)]
    [InlineData(false, true, 2, 2)]
    [InlineData(true, false, 3, 1)]
    [InlineData(true, true, 3, 1)]
    public async Task InheritedOwnershipIdentifiesNearestDeclaringNode(bool nearer, bool indirect, uint owner, int depth)
    {
        var declaration = indirect ? "/Resources 6 0 R" : "/Resources << /Font 7 0 R >>";
        var bytes = Pdf(root: nearer ? "/Resources << /Font << /Ignored 8 0 R >> >>" : declaration,
            parent: nearer ? declaration : "");
        var result = await new PdfExtractor().GetPageResourceProvenanceAsync(bytes, 1);
        Assert.Equal(PageResourceOrigin.Inherited, result.Origin);
        Assert.Equal(new PdfIndirectObjectId(owner, 0), result.SourceReference);
        Assert.Equal(depth, result.AncestorDepth);
        Assert.Equal(indirect ? new PdfIndirectObjectId(6, 0) : (PdfIndirectObjectId?)null, result.ResourceDictionaryReference);
        Assert.Equal(new[] { "Fa", "Fz" }, result.ResolvedResources.FontNames);
        Assert.Equal(new PdfIndirectObjectId(4, 0), result.PageReference);
    }

    [Fact]
    public async Task EmptyOverridesAndAbsenceRemainDistinctWithoutCrossPageLeakage()
    {
        var extractor = new PdfExtractor();
        var absent = await extractor.GetPageResourceProvenanceAsync(Pdf(), 1);
        Assert.Equal(PageResourceOrigin.None, absent.Origin);
        Assert.Null(absent.SourceReference);
        Assert.Null(absent.ResourceDictionaryReference);
        Assert.Null(absent.AncestorDepth);
        Assert.Empty(absent.Entries);
        Assert.Empty(absent.ResolvedResources.ResourceKeys);
        var bytes = Pdf(root: "/Resources 6 0 R");
        var inherited = await extractor.GetPageResourceProvenanceAsync(bytes, 1);
        var empty = await extractor.GetPageResourceProvenanceAsync(bytes, 2);
        Assert.Equal(PageResourceOrigin.Inherited, inherited.Origin);
        Assert.Equal(2, empty.PageNumber);
        Assert.Equal(new PdfIndirectObjectId(5, 0), empty.PageReference);
        Assert.Equal(PageResourceOrigin.Direct, empty.Origin);
        Assert.Equal(new PdfIndirectObjectId(5, 0), empty.SourceReference);
        Assert.Equal(0, empty.AncestorDepth);
        Assert.Empty(empty.Entries);
        Assert.Empty(empty.ResolvedResources.FontNames);
        var masked = await extractor.GetPageResourceProvenanceAsync(Pdf(root: "/Resources 6 0 R", parent: "/Resources << >>"), 1);
        Assert.Equal(new PdfIndirectObjectId(3, 0), masked.SourceReference);
        Assert.Equal(PageResourceOrigin.Inherited, masked.Origin);
        Assert.Empty(masked.ResolvedResources.ResourceKeys);
    }

    [Fact]
    public async Task ResolvedInventoryIncludesNamedCategoriesAndPreservesFlattenedApi()
    {
        const string resources = "/Resources << /XObject << /Im2 88 9 R /Fm1 << /Type /XObject >> >> /Font << /F1 8 0 R >> /ExtGState << /GS1 90 0 R >> /ColorSpace << /Cs1 /DeviceRGB >> /Pattern << /P1 91 0 R >> /Shading << /Sh1 92 0 R >> /Properties << /Pr1 93 0 R >> /ProcSet [/PDF /Text] /Custom 99 0 R >>";
        var extractor = new PdfExtractor();
        var bytes = Pdf(page: resources);
        var original = (byte[])bytes.Clone();
        var result = await extractor.GetPageResourceProvenanceAsync(bytes, 1);
        var old = await extractor.GetPageResourcesAsync(bytes, 1);
        Assert.Equal(old.FontNames, result.ResolvedResources.FontNames);
        Assert.Equal(old.ResourceKeys, result.ResolvedResources.ResourceKeys);
        Assert.Equal(old.HasXObjects, result.ResolvedResources.HasXObjects);
        Assert.True(result.ResolvedResources.HasXObjects);
        Assert.Equal(new[] { "ColorSpace/Cs1", "ExtGState/GS1", "Font/F1", "Pattern/P1", "Properties/Pr1", "Shading/Sh1", "XObject/Fm1", "XObject/Im2" }, result.Entries.Select(e => e.Category + "/" + e.Name));
        Assert.Equal(new PdfIndirectObjectId(88, 9), result.Entries.Last().Reference);
        Assert.Null(result.Entries[0].Reference);
        Assert.Equal(original, bytes);
    }

    [Theory]
    [InlineData("/Resources null")]
    [InlineData("/Resources 42")]
    [InlineData("/Resources 99 0 R")]
    [InlineData("/Resources << /Font 42 >>")]
    [InlineData("/Resources << /XObject 99 0 R >>")]
    public async Task InvalidResourceDeclarationsFailInsteadOfFallingBack(string declaration)
    {
        var error = await Assert.ThrowsAsync<PdfExtractionException>(() => new PdfExtractor().GetPageResourceProvenanceAsync(
            Pdf(root: "/Resources 6 0 R", page: declaration), 1));
        Assert.Contains("resources for page 1", error.Message);
    }

    [Theory]
    [InlineData("3 0 R", "3 0 R", "Cycle")]
    [InlineData("2 0 R", "4 0 R", "Cycle")]
    [InlineData("null", "3 0 R", "Parent")]
    [InlineData("2 0 R", "99 0 R", "Parent")]
    [InlineData("2 0 R", "6 0 R", "Parent")]
    public async Task RequiredMalformedAncestryIsRejected(string parentLink, string pageLink, string message)
    {
        var error = await Assert.ThrowsAsync<PdfExtractionException>(() => new PdfExtractor().GetPageResourceProvenanceAsync(
            Pdf(parentLink: parentLink, pageLink: pageLink, shared: "42"), 1));
        Assert.Contains(message, error.Message);
        // A valid direct declaration needs no ancestry inspection, even with a cyclic Parent.
        var direct = await new PdfExtractor().GetPageResourceProvenanceAsync(Pdf(page: "/Resources << >>", pageLink: "4 0 R"), 1);
        Assert.Equal(PageResourceOrigin.Direct, direct.Origin);
    }

    [Fact]
    public async Task TraversalEntryAndNameBudgetsHaveExactBoundaries()
    {
        var extractor = new PdfExtractor();
        var inherited = Pdf(root: "/Resources << /Font << /F1 8 0 R >> >>");
        var exact = new PageResourceReadOptions { MaxAncestorDepth = 2, MaxEntries = 2, MaxNameBytes = 12 };
        var result = await extractor.GetPageResourceProvenanceAsync(inherited, 1, exact);
        Assert.Equal("F1", Assert.Single(result.Entries).Name);
        foreach (var limits in new[] {
            new PageResourceReadOptions { MaxAncestorDepth = 1 },
            new PageResourceReadOptions { MaxEntries = 1 },
            new PageResourceReadOptions { MaxNameBytes = 11 } })
        {
            var error = await Assert.ThrowsAsync<PdfExtractionException>(() => extractor.GetPageResourceProvenanceAsync(inherited, 1, limits));
            Assert.Contains("limit exceeded", error.Message);
        }
        var empty = await extractor.GetPageResourceProvenanceAsync(Pdf(page: "/Resources << >>"), 1,
            new PageResourceReadOptions { MaxAncestorDepth = 0, MaxEntries = 0, MaxNameBytes = 0 });
        Assert.Equal(PageResourceOrigin.Direct, empty.Origin);
        Assert.Empty(empty.Entries);
    }

    [Fact]
    public async Task ValidationAndSnapshotOwnershipReachTheRealFfi()
    {
        var extractor = new PdfExtractor();
        var bytes = Pdf(page: "/Resources 6 0 R");
        await Assert.ThrowsAsync<ArgumentNullException>(() => extractor.GetPageResourceProvenanceAsync(null!, 1));
        await Assert.ThrowsAsync<ArgumentException>(() => extractor.GetPageResourceProvenanceAsync([], 1));
        await Assert.ThrowsAsync<ArgumentException>(() => new PdfExtractor(bytes.Length - 1).GetPageResourceProvenanceAsync(bytes, 1));
        foreach (int index in new[] { -1, 0 })
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => extractor.GetPageResourceProvenanceAsync(bytes, index));
        foreach (int index in new[] { 3, int.MaxValue })
            await Assert.ThrowsAsync<PdfExtractionException>(() => extractor.GetPageResourceProvenanceAsync(bytes, index));
        await Assert.ThrowsAsync<PdfExtractionException>(() => extractor.GetPageResourceProvenanceAsync([1, 2, 3], 1));
        await Assert.ThrowsAsync<OperationCanceledException>(() => extractor.GetPageResourceProvenanceAsync(bytes, 1, cancellationToken: new CancellationToken(true)));
        foreach (var limits in new[] {
            new PageResourceReadOptions { MaxAncestorDepth = -1 },
            new PageResourceReadOptions { MaxAncestorDepth = 257 },
            new PageResourceReadOptions { MaxEntries = -1 },
            new PageResourceReadOptions { MaxNameBytes = -1 } })
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => extractor.GetPageResourceProvenanceAsync(bytes, 1, limits));
        var options = new PageResourceReadOptions();
        var pending = new PdfExtractor(bytes.Length).GetPageResourceProvenanceAsync(bytes, 1, options);
        Array.Fill(bytes, (byte)0);
        options.MaxEntries = 0;
        options.MaxNameBytes = 0;
        var result = await pending;
        Assert.Equal(PageResourceOrigin.Direct, result.Origin);
        Assert.Equal(new[] { "Fa", "Fz" }, result.ResolvedResources.FontNames);
    }
}
