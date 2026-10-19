using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace OxidizePdf.NET.Tests;

public class XmpTests
{
    // Independent pre-existing PDF with Info, arbitrary catalog data and accurate xrefs.
    private static byte[] Pdf(string? xml = null, bool compressed = false, int? certification = null)
    {
        byte[] data = Encoding.UTF8.GetBytes(xml ?? "");
        if (compressed)
        {
            using var output = new MemoryStream();
            using (var z = new ZLibStream(output, CompressionLevel.Optimal, true)) z.Write(data);
            data = output.ToArray();
        }
        var objects = new byte[][] {
            Encoding.ASCII.GetBytes($"<< /Type /Catalog /Pages 2 0 R /Custom (retain-me) {(xml is null ? "" : "/Metadata 5 0 R")} {(certification is null ? "" : "/Perms << /DocMDP 6 0 R >>")} >>"),
            Encoding.ASCII.GetBytes("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
            Encoding.ASCII.GetBytes("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 321 456] >>"),
            Encoding.ASCII.GetBytes("<< /Title (original-info) /Author (original-author) >>"),
            [..Encoding.ASCII.GetBytes($"<< /Type /Metadata /Subtype /XML /Length {data.Length} {(compressed ? "/Filter /FlateDecode" : "")} >>\nstream\n"), ..data, ..Encoding.ASCII.GetBytes("\nendstream")],
            Encoding.ASCII.GetBytes(certification is null ? "null" : $"<< /Type /Sig /ByteRange [0 999999 1000001 1] /Contents <0000> /Reference [<< /TransformMethod /DocMDP /TransformParams << /P {certification ?? 1} /V /1.2 >> >>] >>") };
        using var pdf = new MemoryStream();
        void Write(string s) => pdf.Write(Encoding.ASCII.GetBytes(s));
        Write("%PDF-1.7\n"); var offsets = new List<long>();
        for (int i = 0; i < objects.Length; i++) { offsets.Add(pdf.Position); Write($"{i + 1} 0 obj\n"); pdf.Write(objects[i]); Write("\nendobj\n"); }
        long xref = pdf.Position; Write($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (long offset in offsets) Write(offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
        Write($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R /Info 4 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return pdf.ToArray();
    }
    private static PdfXmpMetadata AllProperties => new()
    {
        Title = "日本 & <title>\r",
        Creator = ["Zoë", "", "作者"],
        Description = "description & >",
        Keywords = ["one", "dos & tres"],
        CreateDate = "2024-01-02T03:04:05Z",
        ModifyDate = "2025-02-03T04:05:06Z",
        CreatorTool = "tool",
        PdfAIdentifier = new(3, PdfXmpConformance.U, "a<&", "c>"),
        DocumentId = "uuid:doc",
        InstanceId = "uuid:instance",
    };
    [Fact]
    public void AllPropertiesAndIdentifierEscapingRoundTripThroughCore()
    {
        var original = AllProperties;
        var xml = PdfXmp.Serialize(original);
        var recovered = PdfXmp.Parse(xml);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(recovered));
        var rdf = PdfXmp.SerializeIdentifier(original.PdfAIdentifier!);
        Assert.Contains("a&lt;&amp;", rdf); Assert.Contains("c&gt;", rdf);
        Assert.True(PdfXmp.ValidateForPdfA(original).IsValid);
        Assert.False(PdfXmp.ValidateForPdfA(new()).IsValid);
        Assert.NotEmpty(PdfXmp.ValidateForPdfA(new()).Message!);
    }
    [Fact]
    public void NamespaceAliasesMalformedXmlAndLimitsAreDistinguished()
    {
        var xml = PdfXmp.Serialize(AllProperties).Replace("rdf:", "r:").Replace("xmlns:rdf=", "xmlns:r=");
        Assert.Equal(AllProperties.Title, PdfXmp.Parse(xml).Title);
        foreach (var bad in new[] { "<x:xmpmeta/>", "<root>", "<!DOCTYPE x [<!ENTITY e SYSTEM 'file:///etc/passwd'>]><x>&e;</x>" })
            Assert.Equal(PdfXmpErrorKind.InvalidInput, Assert.Throws<PdfXmpException>(() => PdfXmp.Parse(bad)).Kind);
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfXmp.Parse(new string('x', 1_048_577)));
        Assert.Throws<PdfXmpException>(() => PdfXmp.Parse(PdfXmp.Serialize(new() { Title = "\0" })));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IncrementalWritePreservesPrefixInfoAndReadsLatestPacket(bool compressed)
    {
        var baseXml = PdfXmp.Serialize(new() { Title = "old-xmp" });
        var original = Pdf(baseXml, compressed);
        Assert.Equal(baseXml, PdfXmp.Read(original)!.Xml);
        var updated = PdfXmp.Write(original, AllProperties);
        Assert.Equal(original, updated[..original.Length]);
        Assert.Equal(JsonSerializer.Serialize(AllProperties), JsonSerializer.Serialize(PdfXmp.Read(updated)!.Metadata));
        Assert.Equal("original-info", (await new PdfExtractor().ExtractMetadataAsync(updated)).Title);
        var again = PdfXmp.Write(updated, new() { Title = "last" });
        Assert.Equal(updated, again[..updated.Length]); Assert.Equal("last", PdfXmp.Read(again)!.Metadata.Title);
        Assert.Contains("/Custom <72657461696E2D6D65>", Encoding.ASCII.GetString(again[updated.Length..]));
    }
    [Fact]
    public void MissingMetadataIsNotMalformedMetadataAndWriteCanInsertIt()
    {
        var original = Pdf(); Assert.Null(PdfXmp.Read(original));
        Assert.Equal("new", PdfXmp.Read(PdfXmp.Write(original, new() { Title = "new" }))!.Metadata.Title);
        Assert.Throws<PdfXmpException>(() => PdfXmp.Read(Pdf("<broken>")));
        Assert.Throws<PdfXmpException>(() => PdfXmp.Read(Pdf(new string('x', 1_048_577), true)));
        Assert.Throws<PdfXmpException>(() => PdfXmp.Write(original, new() { Title = "\0" }));
        Assert.Null(PdfXmp.Read(original));
    }
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void GenericMetadataWritesRejectEveryCertificationLevel(int permission)
    {
        var pdf = Pdf(certification: permission); var snapshot = pdf.ToArray();
        Assert.Equal(PdfXmpErrorKind.PermissionDenied, Assert.Throws<PdfXmpException>(() => PdfXmp.Write(pdf, AllProperties)).Kind);
        Assert.Equal(snapshot, pdf);
        Assert.NotNull(PdfXmp.Read(PdfXmp.Write(Pdf(), AllProperties))); // uncertified negative control
    }
}
