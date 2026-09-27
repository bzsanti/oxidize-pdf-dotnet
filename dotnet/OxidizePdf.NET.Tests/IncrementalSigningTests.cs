using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

namespace OxidizePdf.NET.Tests;

public class IncrementalSigningTests
{
    private static byte[] Pdf()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        doc.AddPage(page);
        return doc.SaveToBytes();
    }

    [Fact]
    public async Task ExternalCmsSignsExactRangesAndPreservesOriginalRevision()
    {
        var original = Pdf();
        using var prepared = await PdfSigning.PrepareAsync(original, new());
        Assert.True(prepared.PreparedPdf.AsSpan().StartsWith(original));
        Assert.Equal(prepared.BytesToDigest, Covered(prepared.PreparedPdf, prepared.ByteRange));
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Wrapper signing test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        var cms = new SignedCms(new ContentInfo(prepared.BytesToDigest), detached: true);
        cms.ComputeSignature(new CmsSigner(cert));
        var encoded = cms.Encode();
        var final = prepared.Complete(encoded);
        Assert.Equal(final, prepared.Complete(encoded));
        Assert.True(final.AsSpan().StartsWith(original));
        Assert.Equal(prepared.PreparedPdf.Length, final.Length);
        Assert.Equal(prepared.BytesToDigest, Covered(final, prepared.ByteRange));
        var verifier = new SignedCms(new ContentInfo(Covered(final, prepared.ByteRange)), detached: true);
        var gapStart = checked((int)(prepared.ByteRange[0][0] + prepared.ByteRange[0][1]));
        var embedded = Convert.FromHexString(System.Text.Encoding.ASCII.GetString(final, gapStart + 1, encoded.Length * 2));
        Assert.Equal(encoded, embedded);
        verifier.Decode(embedded);
        verifier.CheckSignature(verifySignatureOnly: true); // Self-signed test certificate; no trust-chain claim.
    }

    [Fact]
    public async Task InvalidContainerCanBeRetriedAndDisposalRejectsCompletion()
    {
        var prepared = await PdfSigning.PrepareAsync(Pdf(), new());
        Assert.Throws<PdfExtractionException>(() => prepared.Complete([1, 2, 3]));
        // Structural DER fixture only; this is not a cryptographically valid CMS.
        Assert.NotEmpty(prepared.Complete([0x30, 3, 2, 1, 1]));
        prepared.Dispose();
        prepared.Dispose();
        Assert.Throws<ObjectDisposedException>(() => prepared.Complete([0x30, 0]));
    }

    [Fact]
    public async Task VisibleAppearanceIncludesWatermarkAndFittedText()
    {
        var appearance = new PdfSignatureAppearance
        {
            SignerName = "Test Signer", Text = ["Approved"],
            Watermark = new() { Width = 1, Height = 1, Rgb = [20, 40, 80], Opacity = 0.2f }
        };
        double[] rect = [10, 10, 300, 100];
        var layout = PdfSigning.LayoutAppearance(rect, appearance);
        using var prepared = await PdfSigning.PrepareAsync(Pdf(), new() { Rectangle = rect, Appearance = appearance });
        Assert.NotEmpty(layout.Lines);
        Assert.Equal(layout.Lines, prepared.Layout!.Lines);
        Assert.Equal(layout.FontSize, prepared.Layout.FontSize);
        Assert.Contains("/Subtype /Image", System.Text.Encoding.Latin1.GetString(prepared.PreparedPdf));
        Assert.Throws<PdfExtractionException>(() => PdfSigning.LayoutAppearance([0, 0, 1, 1], appearance));
        appearance.Watermark.Rgb = [1];
        Assert.Throws<PdfExtractionException>(() => PdfSigning.LayoutAppearance(rect, appearance));
    }

    [Fact]
    public async Task ReservedEntriesAndInvalidTargetsAreRejected()
    {
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfSigning.PrepareAsync(Pdf(), new()
            { AdditionalEntries = new() { ["Contents"] = new("Text", "invalid") } }));
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfSigning.PrepareAsync(Pdf(), new() { Existing = true, FieldName = "missing" }));
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfSigning.PrepareAsync(Pdf(), new() { PageIndex = 100 }));
        using var prepared = await PdfSigning.PrepareAsync(Pdf(), new()
            { AdditionalEntries = new() { ["CustomEntry"] = new("Text", "Prueba") } });
        Assert.Contains("/CustomEntry", System.Text.Encoding.Latin1.GetString(prepared.PreparedPdf));
    }

    [Fact]
    public async Task ExistingEmptyFieldCanBeSignedOnlyOnce()
    {
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Annots [4 0 R] >>",
            "<< /Type /Annot /Subtype /Widget /FT /Sig /T (Existing) /Rect [10 10 300 100] /P 3 0 R >>"
        };
        var text = new System.Text.StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(text.Length);
            text.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = text.Length;
        text.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets) text.Append($"{offset:D10} 00000 n \n");
        text.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        var pdf = System.Text.Encoding.ASCII.GetBytes(text.ToString());
        using var prepared = await PdfSigning.PrepareAsync(pdf, new() { Existing = true, FieldName = "Existing" });
        Assert.True(prepared.PreparedPdf.AsSpan().StartsWith(pdf));
        // Structural DER only: this test checks field occupancy, not CMS validity.
        var signed = prepared.Complete([0x30, 3, 2, 1, 1]);
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfSigning.PrepareAsync(signed,
            new() { Existing = true, FieldName = "Existing" }));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public async Task CertificationControlsSubsequentSigning(byte permission, bool allowed)
    {
        using var first = await PdfSigning.PrepareAsync(Pdf(), new() { Certification = permission });
        // Structural DER only: permission parsing is independent of certificate validity.
        var signed = first.Complete([0x30, 3, 2, 1, 1]);
        if (!allowed)
            await Assert.ThrowsAsync<PdfExtractionException>(() => PdfSigning.PrepareAsync(signed, new() { FieldName = "Second" }));
        else
        {
            using var next = await PdfSigning.PrepareAsync(signed, new() { FieldName = "Second" });
            Assert.True(next.PreparedPdf.AsSpan().StartsWith(signed));
        }
    }

    [Fact]
    public async Task FieldLockAndTypedEntriesRoundTripThroughPreparation()
    {
        using var prepared = await PdfSigning.PrepareAsync(Pdf(), new()
        {
            FieldLock = new() { Mode = "All" },
            AdditionalEntries = new()
            {
                ["CustomNull"] = new("Null"), ["CustomBool"] = new("Boolean", true),
                ["CustomInt"] = new("Integer", 42), ["CustomReal"] = new("Real", 1.25),
                ["CustomName"] = new("Name", "Value"), ["CustomBytes"] = new("Bytes", new byte[] { 1, 2 }),
                ["CustomArray"] = new("Array", new[] { new PdfObjectValue("Integer", 7) }),
                ["CustomDict"] = new("Dictionary", new Dictionary<string, PdfObjectValue> { ["Key"] = new("Text", "text") })
            }
        });
        var text = System.Text.Encoding.Latin1.GetString(prepared.PreparedPdf);
        Assert.Contains("/CustomBool true", text);
        Assert.Contains("/CustomInt 42", text);
        Assert.Contains("/CustomName /Value", text);
        Assert.Contains("/Action /All", text);
        var signed = prepared.Complete([0x30, 3, 2, 1, 1]);
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfSigning.PrepareAsync(signed, new() { FieldName = "Second" }));
    }

    [Fact]
    public async Task OversizedCmsAndEncryptedInputAreRejected()
    {
        using var prepared = await PdfSigning.PrepareAsync(Pdf(), new() { PlaceholderBytes = 256 });
        var der = new byte[260];
        der[0] = 0x30; der[1] = 0x82; der[2] = 1; der[3] = 0;
        Assert.Throws<PdfExtractionException>(() => prepared.Complete(der));
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        doc.AddPage(page);
        doc.Encrypt("user", "owner");
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfSigning.PrepareAsync(doc.SaveToBytes(), new()));
    }

    private static byte[] Covered(byte[] pdf, ulong[][] ranges) => ranges.SelectMany(r =>
        pdf.Skip(checked((int)r[0])).Take(checked((int)r[1]))).ToArray();
}
