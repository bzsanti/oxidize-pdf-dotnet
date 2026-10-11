using System.IO.Compression;
using System.Text;
using OxidizePdf.NET.Models;

namespace OxidizePdf.NET.Tests;

public class TextRecoveryTests
{
    // Independent PDF fixture with controlled zlib integrity and font mappings.
    private static byte[] Pdf(string content, bool missingChecksum = false, bool omitted = false, bool form = false)
    {
        var encoded = Encoding.ASCII.GetBytes(content);
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true)) zlib.Write(encoded);
        var stream = compressed.ToArray();
        if (missingChecksum) stream = stream[..^4];
        if (omitted) stream = [0, 1, 2, 3];
        byte[] Stream(byte[] data, string extra) => Encoding.ASCII.GetBytes($"<< /Length {data.Length} {extra} >>\nstream\n")
            .Concat(data).Concat(Encoding.ASCII.GetBytes("\nendstream")).ToArray();
        var cmap = Encoding.ASCII.GetBytes("begincmap\n1 begincodespacerange\n<00> <FF>\nendcodespacerange\n1 beginbfrange\n<00> <7F> <0000>\nendbfrange\n1 beginbfchar\n<01> <00A0>\nendbfchar\nendcmap");
        var objects = new List<byte[]>
        {
            Encoding.ASCII.GetBytes("<< /Type /Catalog /Pages 2 0 R /Version /1.7 >>"),
            Encoding.ASCII.GetBytes("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
            Encoding.ASCII.GetBytes("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] /Resources << /Font << /F1 4 0 R >> " + (form ? "/XObject << /Fm1 7 0 R >> " : "") + ">> /Contents 5 0 R >>"),
            Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding /ToUnicode 6 0 R >>"),
            form ? Stream(Encoding.ASCII.GetBytes("/Fm1 Do"), "") : Stream(stream, "/Filter /FlateDecode"),
            Stream(cmap, "")
        };
        if (form) objects.Add(Stream(stream, "/Type /XObject /Subtype /Form /BBox [0 0 200 300] /Resources << /Font << /F1 4 0 R >> >> /Filter /FlateDecode"));
        using var pdf = new MemoryStream();
        void Write(string text) => pdf.Write(Encoding.ASCII.GetBytes(text));
        Write("%PDF-1.4\n");
        var offsets = new List<long>();
        for (int i = 0; i < objects.Count; i++)
        {
            offsets.Add(pdf.Position);
            Write($"{i + 1} 0 obj\n"); pdf.Write(objects[i]); Write("\nendobj\n");
        }
        long xref = pdf.Position;
        Write($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (long offset in offsets) Write($"{offset:D10} 00000 n \n");
        Write($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return pdf.ToArray();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecoveryIsExplicitAndReportsContentsOrFormIdentity(bool form)
    {
        var bytes = Pdf("BT /F1 12 Tf 10 100 Td (Recovered) Tj ET", missingChecksum: true, form: form);
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfTextExtraction.ExtractAsync(bytes));
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfTextExtraction.ExtractAsync(bytes, parserOptions: PdfParserOptions.Tolerant()));
        var result = Assert.Single(await PdfTextExtraction.RecoverAsync(bytes, 4096));
        Assert.Equal(0u, result.PageIndex);
        Assert.Equal("Recovered", result.Text.Text.Trim());
        Assert.False(result.Text.Truncated);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(PdfRecoveryAction.Recovered, diagnostic.Action);
        Assert.Equal(PdfFlateRecoveryKind.Unverified, diagnostic.Kind);
        Assert.Equal(0ul, diagnostic.FilterIndex);
        Assert.NotEmpty(diagnostic.Error);
        if (form)
        {
            Assert.Equal(PdfRecoveryLocation.FormXObject, diagnostic.Location);
            Assert.Equal("Fm1", diagnostic.FormName);
            Assert.Equal(new uint[] { 7, 0 }, diagnostic.ObjectId);
            Assert.Null(diagnostic.ContentsIndex);
        }
        else
        {
            Assert.Equal(PdfRecoveryLocation.PageContents, diagnostic.Location);
            Assert.Equal(0ul, diagnostic.ContentsIndex);
            Assert.Null(diagnostic.FormName);
            Assert.Null(diagnostic.ObjectId);
        }
    }

    [Fact]
    public async Task OmissionIsReportedAndResourceLimitIsNotDowngraded()
    {
        var omitted = Assert.Single(await PdfTextExtraction.RecoverAsync(Pdf("", omitted: true), 4096));
        Assert.Empty(omitted.Text.Text);
        var diagnostic = Assert.Single(omitted.Diagnostics);
        Assert.Equal(PdfRecoveryAction.Omitted, diagnostic.Action);
        Assert.Null(diagnostic.Kind);
        Assert.Null(diagnostic.FilterIndex);
        Assert.NotEmpty(diagnostic.Error);
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfTextExtraction.RecoverAsync(Pdf("BT /F1 12 Tf (Too long) Tj ET"), 2));
    }

    [Fact]
    public async Task NormalizationZeroBudgetAndFragmentMetadataAreExposed()
    {
        var bytes = Pdf("0.2 0.3 0.4 rg /Span << /MCID 5 >> BDC BT /F1 12 Tf 10 100 Td (A\\001B) Tj ET EMC");
        var options = new PdfTextExtractionOptions { PreserveLayout = true, TrackSpaceDecisions = true };
        var original = Assert.Single(await PdfTextExtraction.ExtractAsync(bytes, options, pageIndex: 0));
        Assert.Contains("A\u00a0B", original.Text.Text);
        Assert.Empty(original.Diagnostics);
        var fragment = Assert.Single(original.Text.Fragments);
        Assert.Equal(5u, fragment.MarkedContentId);
        Assert.Equal("Span", fragment.StructureTag);
        Assert.Equal(TextRenderingMode.Fill, fragment.RenderMode);
        Assert.Equal(PdfExtractedColorSpace.Rgb, fragment.Color!.Space);
        Assert.Equal(3, fragment.Color.Components.Length);
        // Core content operators retain f32 precision before exposing f64 colors.
        Assert.Equal(0.2, fragment.Color.Components[0], precision: 6);
        Assert.Equal(0.3, fragment.Color.Components[1], precision: 6);
        Assert.Equal(0.4, fragment.Color.Components[2], precision: 6);
        Assert.NotNull(fragment.SpaceDecisions);
        options.NormalizeNonBreakingSpaces = true;
        Assert.Contains("A B", Assert.Single(await PdfTextExtraction.ExtractAsync(bytes, options)).Text.Text);
        options.MaxExtractedBytes = 0;
        var empty = Assert.Single(await PdfTextExtraction.ExtractAsync(bytes, options));
        Assert.Empty(empty.Text.Text);
        Assert.True(empty.Text.Truncated);
        Assert.Equal("1.7", await PdfTextExtraction.GetEffectiveVersionAsync(bytes));
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfTextExtraction.ExtractAsync(bytes, pageIndex: 1));
    }

    [Fact]
    public void BuildIdentificationControlsSerializedMetadata()
    {
        byte[] Write(BuildIdentification policy)
        {
            using var doc = new PdfDocument();
            using var page = PdfPage.A4();
            Assert.Equal(BuildIdentification.Enabled, doc.BuildIdentification);
            doc.SetBuildIdentification(policy).SetCreator("Application").AddPage(page);
            Assert.Equal(policy, doc.BuildIdentification);
            return doc.SaveToBytes();
        }
        var enabled = Encoding.Latin1.GetString(Write(BuildIdentification.Enabled));
        var disabled = Encoding.Latin1.GetString(Write(BuildIdentification.Disabled));
        foreach (var key in new[] { "/oxidize-pdf-build", "/oxidize-pdf-edition", "/oxidize-pdf-features" })
        {
            Assert.Contains(key, enabled);
            Assert.DoesNotContain(key, disabled);
        }
        Assert.Contains("Application", disabled);
    }

    [Theory]
    [InlineData(CarriageReturnHandling.Remove, "AB\nC")]
    [InlineData(CarriageReturnHandling.ReplaceWithSpace, "A B\nC")]
    [InlineData(CarriageReturnHandling.NormalizeLineEnding, "A\rB\nC")]
    public async Task EveryCarriageReturnPolicyReachesTheNewExtractor(CarriageReturnHandling policy, string expected)
    {
        var bytes = Pdf("BT /F1 12 Tf 10 100 Td (A\\015B\\012C) Tj ET");
        var result = Assert.Single(await PdfTextExtraction.ExtractAsync(bytes, new() { CarriageReturnHandling = policy }));
        Assert.Equal(expected, result.Text.Text);
    }
}
