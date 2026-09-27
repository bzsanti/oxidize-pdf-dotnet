using System.Text.Json;

namespace OxidizePdf.NET.Tests;

public class GeometricEditingTests
{
    private static byte[] BlankPdf(bool encrypt = false)
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        doc.AddPage(page);
        if (encrypt) doc.Encrypt("user", "owner");
        return doc.SaveToBytes();
    }

    private static PdfGeometricGeometry Geometry(PdfGeometricKind kind) => kind switch
    {
        PdfGeometricKind.Line => new() { Kind = kind, Start = [60, 60], End = [120, 120], StartEnding = PdfAnnotationLineEnding.None, EndEnding = PdfAnnotationLineEnding.OpenArrow },
        PdfGeometricKind.Square or PdfGeometricKind.Circle => new() { Kind = kind, Rectangle = [60, 60, 120, 120] },
        PdfGeometricKind.Polygon => new() { Kind = kind, Vertices = [[60, 60], [120, 60], [100, 120]] },
        _ => new() { Kind = kind, Vertices = [[60, 60], [120, 60], [100, 120]], StartEnding = PdfAnnotationLineEnding.Circle, EndEnding = PdfAnnotationLineEnding.ClosedArrow }
    };

    [Theory]
    [InlineData(PdfGeometricKind.Line)]
    [InlineData(PdfGeometricKind.Square)]
    [InlineData(PdfGeometricKind.Circle)]
    [InlineData(PdfGeometricKind.Polygon)]
    [InlineData(PdfGeometricKind.PolyLine)]
    public async Task AllGeometries_RoundTripAndPreserveIdentity(PdfGeometricKind kind)
    {
        var pdf = BlankPdf();
        Assert.Empty(await PdfOperations.GetGeometricAnnotationsAsync(pdf));
        var geometry = Geometry(kind);
        var style = new PdfGeometricStyle { StrokeColor = [0.2, 0.4, 0.6], FillColor = [0, 0.5, 0, 0], Width = 2, DashPattern = [3, 2], Opacity = 0.7 };
        var added = await PdfOperations.EditGeometricAnnotationsAsync(pdf,
            [new PdfGeometricMutation.Add(0, geometry, style)]);
        Assert.True(added.PdfBytes.AsSpan().StartsWith(pdf));
        var note = Assert.Single(added.Annotations);
        Assert.Equal(JsonSerializer.Serialize(geometry), JsonSerializer.Serialize(note.Geometry));
        Assert.Equal(JsonSerializer.Serialize(style), JsonSerializer.Serialize(note.Style));
        var updated = await PdfOperations.EditGeometricAnnotationsAsync(added.PdfBytes,
            [new PdfGeometricMutation.Update(note.Id, geometry, new PdfGeometricStyle { StrokeColor = [0.3], Width = 4 })]);
        Assert.True(updated.PdfBytes.AsSpan().StartsWith(added.PdfBytes));
        var listed = Assert.Single(await PdfOperations.GetGeometricAnnotationsAsync(updated.PdfBytes));
        Assert.Equal(note.Id, listed.Id);
        Assert.Equal(new[] { 0.3 }, listed.Style.StrokeColor);
        Assert.Equal(4, listed.Style.Width);
        Assert.Null(listed.Style.DashPattern);
        Assert.Null(listed.Style.Opacity);
        Assert.Null(listed.Style.FillColor);
        var removed = await PdfOperations.EditGeometricAnnotationsAsync(updated.PdfBytes,
            [new PdfGeometricMutation.Remove(note.Id)]);
        Assert.True(removed.PdfBytes.AsSpan().StartsWith(updated.PdfBytes));
        Assert.Empty(await PdfOperations.GetGeometricAnnotationsAsync(removed.PdfBytes));
    }

    public static IEnumerable<object[]> Endings => Enum.GetValues<PdfAnnotationLineEnding>().Select(e => new object[] { e });

    [Theory]
    [MemberData(nameof(Endings))]
    public async Task EveryLineEnding_RoundTrips(PdfAnnotationLineEnding ending)
    {
        var geometry = Geometry(PdfGeometricKind.Line);
        geometry.StartEnding = ending;
        geometry.EndEnding = ending;
        var result = await PdfOperations.EditGeometricAnnotationsAsync(BlankPdf(),
            [new PdfGeometricMutation.Add(0, geometry, new PdfGeometricStyle())]);
        var read = Assert.Single(await PdfOperations.GetGeometricAnnotationsAsync(result.PdfBytes));
        Assert.Equal(ending, read.Geometry.StartEnding);
        Assert.Equal(ending, read.Geometry.EndEnding);
    }

    [Fact]
    public async Task InvalidStyleGeometryAndConflictsAreRejected()
    {
        var pdf = BlankPdf();
        var geometry = Geometry(PdfGeometricKind.Square);
        await Assert.ThrowsAsync<ArgumentException>(() => PdfOperations.EditGeometricAnnotationsAsync(pdf,
            [new PdfGeometricMutation.Add(0, geometry, new PdfGeometricStyle { DashPattern = [0, 0] })]));
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfOperations.EditGeometricAnnotationsAsync(pdf,
            [new PdfGeometricMutation.Add(0, new() { Kind = PdfGeometricKind.Square, Rectangle = [100, 100, 50, 50] }, new())]));
        var added = await PdfOperations.EditGeometricAnnotationsAsync(pdf, [new PdfGeometricMutation.Add(0, geometry, new())]);
        var id = Assert.Single(added.Annotations).Id;
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfOperations.EditGeometricAnnotationsAsync(added.PdfBytes,
            [new PdfGeometricMutation.Remove(id), new PdfGeometricMutation.Remove(id)]));
        Assert.Single(await PdfOperations.GetGeometricAnnotationsAsync(added.PdfBytes));
    }

    [Fact]
    public async Task OtherAnnotationsAreRetainedAndEncryptedInputsRejected()
    {
        var ink = await PdfOperations.EditInkAnnotationsAsync(BlankPdf(), [new PdfInkMutation.Add(0, [[10, 20, 30, 40]], [0])]);
        var geometric = await PdfOperations.EditGeometricAnnotationsAsync(ink.PdfBytes,
            [new PdfGeometricMutation.Add(0, Geometry(PdfGeometricKind.Circle), new())]);
        Assert.Equal(Assert.Single(ink.Annotations).Id, Assert.Single(await PdfOperations.GetInkAnnotationsAsync(geometric.PdfBytes)).Id);
        var encrypted = BlankPdf(true);
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfOperations.GetGeometricAnnotationsAsync(encrypted));
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfOperations.EditGeometricAnnotationsAsync(encrypted,
            [new PdfGeometricMutation.Add(0, Geometry(PdfGeometricKind.Circle), new())]));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public async Task DocMdpIsEnforced(int permission, bool allowed)
    {
        var pdf = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "fixtures", "signatures", "docmdp_p2_rsa.pdf"));
        // Synthetic policy variants, not cryptographic verification.
        var offset = pdf.AsSpan().IndexOf(System.Text.Encoding.ASCII.GetBytes("/P 2"));
        Assert.True(offset >= 0);
        pdf[offset + 3] = (byte)('0' + permission);
        var mutation = new PdfGeometricMutation.Add(0, Geometry(PdfGeometricKind.Circle), new());
        if (allowed)
        {
            var result = await PdfOperations.EditGeometricAnnotationsAsync(pdf, [mutation]);
            Assert.True(result.PdfBytes.AsSpan().StartsWith(pdf));
            Assert.Single(result.Annotations);
        }
        else
        {
            var error = await Assert.ThrowsAsync<PdfExtractionException>(() => PdfOperations.EditGeometricAnnotationsAsync(pdf, [mutation]));
            Assert.Contains("DocMDP", error.Message);
        }
    }
}
