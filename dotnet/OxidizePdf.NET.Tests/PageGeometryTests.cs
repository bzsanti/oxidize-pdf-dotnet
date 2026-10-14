using System.Text;
using OxidizePdf.NET.Models;

namespace OxidizePdf.NET.Tests;

public class PageGeometryTests
{
    // Independent page tree: root -> intermediate Pages -> two leaves.
    private static byte[] Pdf(string root = "", string parent = "", string page = "")
    {
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Kids [3 0 R] /Count 2 {root} >>",
            $"<< /Type /Pages /Parent 2 0 R /Kids [4 0 R 5 0 R] /Count 2 {parent} >>",
            $"<< /Type /Page /Parent 3 0 R {page} >>",
            "<< /Type /Page /Parent 3 0 R /MediaBox [0 0 40 70] /Rotate 180 >>"
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

    private static void Box(PageGeometryBox box, double left, double bottom, double right, double top)
    {
        Assert.Equal(left, box.Left);
        Assert.Equal(bottom, box.Bottom);
        Assert.Equal(right, box.Right);
        Assert.Equal(top, box.Top);
    }

    [Theory]
    [InlineData(0, 200, 300)]
    [InlineData(90, 300, 200)]
    [InlineData(180, 200, 300)]
    [InlineData(270, 300, 200)]
    public async Task RotationAndUnrotatedBoxesPreserveLegacyDimensions(int rotation, double width, double height)
    {
        var bytes = Pdf(page: $"/MediaBox [-10 20 190 320] /CropBox [0 30 180 300] /Rotate {rotation}");
        var original = (byte[])bytes.Clone();
        var extractor = new PdfExtractor();
        var geometry = await extractor.GetPageGeometryAsync(bytes, 1);
        Assert.Equal(1, geometry.PageNumber);
        Assert.Equal(rotation, geometry.Rotation);
        Assert.Equal(width, geometry.Width);
        Assert.Equal(height, geometry.Height);
        Box(geometry.MediaBox, -10, 20, 190, 320);
        Box(Assert.IsType<PageGeometryBox>(geometry.CropBox), 0, 30, 180, 300);
        Assert.Equal((width, height), await extractor.GetPageDimensionsAsync(bytes, 1));
        Assert.Equal(original, bytes);
    }

    [Fact]
    public async Task InheritedGeometryUsesNearestAncestorAndPageOverrides()
    {
        var bytes = Pdf(root: "/MediaBox [0 0 600 800] /CropBox [5 6 550 700] /Rotate 90",
            parent: "/MediaBox [-10 20 190 320] /CropBox [0 30 180 300] /Rotate 270");
        var extractor = new PdfExtractor();
        var first = await extractor.GetPageGeometryAsync(bytes, 1);
        Assert.Equal(270, first.Rotation);
        Assert.Equal(300, first.Width);
        Assert.Equal(200, first.Height);
        Box(first.MediaBox, -10, 20, 190, 320);
        Box(Assert.IsType<PageGeometryBox>(first.CropBox), 0, 30, 180, 300);
        var second = await extractor.GetPageGeometryAsync(bytes, 2);
        Assert.Equal(2, second.PageNumber);
        Assert.Equal(180, second.Rotation);
        Box(second.MediaBox, 0, 0, 40, 70);
        var overridden = await extractor.GetPageGeometryAsync(Pdf(
            root: "/MediaBox [0 0 600 800] /CropBox [5 6 550 700] /Rotate 90",
            page: "/MediaBox [10 20 110 220] /CropBox [11 21 99 199] /Rotate 0"), 1);
        Assert.Equal(0, overridden.Rotation);
        Box(overridden.MediaBox, 10, 20, 110, 220);
        Box(Assert.IsType<PageGeometryBox>(overridden.CropBox), 11, 21, 99, 199);
    }

    [Fact]
    public async Task MissingCropAndRotationRemainDistinctFromExplicitValues()
    {
        var geometry = await new PdfExtractor().GetPageGeometryAsync(Pdf(page: "/MediaBox [10 20 110 220]"), 1);
        Assert.Null(geometry.CropBox);
        Assert.Equal(0, geometry.Rotation);
        Assert.Equal(100, geometry.Width);
        Assert.Equal(200, geometry.Height);
        Box(geometry.MediaBox, 10, 20, 110, 220);
    }

    [Fact]
    public async Task InvalidInputIndexesLimitsAndCancellationAreRejected()
    {
        var extractor = new PdfExtractor();
        var bytes = Pdf(page: "/MediaBox [0 0 200 300]");
        await Assert.ThrowsAsync<ArgumentNullException>(() => extractor.GetPageGeometryAsync(null!, 1));
        await Assert.ThrowsAsync<ArgumentException>(() => extractor.GetPageGeometryAsync([], 1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => extractor.GetPageGeometryAsync(bytes, 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => extractor.GetPageGeometryAsync(bytes, -1));
        await Assert.ThrowsAsync<ArgumentException>(() => new PdfExtractor(bytes.Length - 1).GetPageGeometryAsync(bytes, 1));
        await Assert.ThrowsAsync<PdfExtractionException>(() => extractor.GetPageGeometryAsync(bytes, 3));
        await Assert.ThrowsAsync<PdfExtractionException>(() => extractor.GetPageGeometryAsync(bytes, int.MaxValue));
        await Assert.ThrowsAsync<PdfExtractionException>(() => extractor.GetPageGeometryAsync([1, 2, 3], 1));
        await Assert.ThrowsAsync<OperationCanceledException>(() => extractor.GetPageGeometryAsync(bytes, 1, new CancellationToken(true)));
        Assert.Equal(200, (await new PdfExtractor(bytes.Length).GetPageGeometryAsync(bytes, 1)).Width);
    }

    [Fact]
    public async Task QueuedInspectionOwnsAnInputSnapshot()
    {
        var bytes = Pdf(page: "/MediaBox [0 0 200 300] /Rotate 90");
        var pending = new PdfExtractor().GetPageGeometryAsync(bytes, 1);
        Array.Fill(bytes, (byte)0);
        var geometry = await pending;
        Assert.Equal(90, geometry.Rotation);
        Assert.Equal(300, geometry.Width);
        Assert.Equal(200, geometry.Height);
    }
}
