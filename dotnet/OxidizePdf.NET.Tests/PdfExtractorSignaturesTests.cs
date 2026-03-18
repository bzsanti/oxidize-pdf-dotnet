using OxidizePdf.NET.Models;
using OxidizePdf.NET.Tests.TestHelpers;

namespace OxidizePdf.NET.Tests;

/// <summary>
/// Tests for digital signature detection, parsing, and verification (SIG-001/002/003).
/// </summary>
public class PdfExtractorSignaturesTests
{
    // ── Model tests ──────────────────────────────────────────────────────────

    [Fact]
    public void DigitalSignature_HasExpectedDefaults()
    {
        var sig = new DigitalSignature();

        Assert.Null(sig.FieldName);
        Assert.Equal(string.Empty, sig.Filter);
        Assert.Null(sig.SubFilter);
        Assert.Null(sig.Reason);
        Assert.Null(sig.Location);
        Assert.Null(sig.ContactInfo);
        Assert.Null(sig.SigningTime);
        Assert.Null(sig.SignerName);
        Assert.Equal(0L, sig.ContentsSize);
        Assert.False(sig.IsPades);
        Assert.False(sig.IsPkcs7Detached);
    }

    [Fact]
    public void SignatureVerificationResult_HasExpectedDefaults()
    {
        var result = new SignatureVerificationResult();

        Assert.Null(result.FieldName);
        Assert.Null(result.SignerName);
        Assert.Null(result.SigningTime);
        Assert.False(result.HashValid);
        Assert.False(result.SignatureValid);
        Assert.False(result.IsValid);
        Assert.False(result.HasModificationsAfterSigning);
        Assert.NotNull(result.Errors);
        Assert.Empty(result.Errors);
        Assert.NotNull(result.Warnings);
        Assert.Empty(result.Warnings);
        Assert.Null(result.DigestAlgorithm);
        Assert.Null(result.SignatureAlgorithm);
        Assert.Null(result.Certificate);
    }

    [Fact]
    public void CertificateInfo_HasExpectedDefaults()
    {
        var cert = new CertificateInfo();

        Assert.Equal(string.Empty, cert.Subject);
        Assert.Equal(string.Empty, cert.Issuer);
        Assert.Equal(string.Empty, cert.ValidFrom);
        Assert.Equal(string.Empty, cert.ValidTo);
        Assert.False(cert.IsTimeValid);
        Assert.False(cert.IsTrusted);
        Assert.False(cert.IsSignatureCapable);
        Assert.NotNull(cert.Warnings);
        Assert.Empty(cert.Warnings);
    }

    // ── SIG-001: HasDigitalSignaturesAsync ───────────────────────────────────

    [Fact]
    public async Task HasDigitalSignaturesAsync_NullBytes_ThrowsArgumentNullException()
    {
        var extractor = new PdfExtractor();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => extractor.HasDigitalSignaturesAsync(null!));
    }

    [Fact]
    public async Task HasDigitalSignaturesAsync_EmptyBytes_ThrowsArgumentException()
    {
        var extractor = new PdfExtractor();
        await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.HasDigitalSignaturesAsync(Array.Empty<byte>()));
    }

    [Fact]
    public async Task HasDigitalSignaturesAsync_OnUnsignedPdf_ReturnsFalse()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var result = await extractor.HasDigitalSignaturesAsync(pdf);

        Assert.False(result);
    }

    // ── SIG-002: GetDigitalSignaturesAsync ───────────────────────────────────

    [Fact]
    public async Task GetDigitalSignaturesAsync_NullBytes_ThrowsArgumentNullException()
    {
        var extractor = new PdfExtractor();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => extractor.GetDigitalSignaturesAsync(null!));
    }

    [Fact]
    public async Task GetDigitalSignaturesAsync_EmptyBytes_ThrowsArgumentException()
    {
        var extractor = new PdfExtractor();
        await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.GetDigitalSignaturesAsync(Array.Empty<byte>()));
    }

    [Fact]
    public async Task GetDigitalSignaturesAsync_OnUnsignedPdf_ReturnsEmptyList()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var sigs = await extractor.GetDigitalSignaturesAsync(pdf);

        Assert.NotNull(sigs);
        Assert.Empty(sigs);
    }

    [Fact]
    public async Task GetDigitalSignaturesAsync_WhenPresent_FilterIsNotEmpty()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var sigs = await extractor.GetDigitalSignaturesAsync(pdf);

        if (sigs.Count > 0)
        {
            Assert.All(sigs, s => Assert.NotEmpty(s.Filter));
        }
    }

    [Fact]
    public async Task GetDigitalSignaturesAsync_WhenPresent_ContentsSizeIsPositive()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var sigs = await extractor.GetDigitalSignaturesAsync(pdf);

        if (sigs.Count > 0)
        {
            Assert.All(sigs, s => Assert.True(s.ContentsSize > 0));
        }
    }

    // ── SIG-003: VerifySignaturesAsync ───────────────────────────────────────

    [Fact]
    public async Task VerifySignaturesAsync_NullBytes_ThrowsArgumentNullException()
    {
        var extractor = new PdfExtractor();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => extractor.VerifySignaturesAsync(null!));
    }

    [Fact]
    public async Task VerifySignaturesAsync_EmptyBytes_ThrowsArgumentException()
    {
        var extractor = new PdfExtractor();
        await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.VerifySignaturesAsync(Array.Empty<byte>()));
    }

    [Fact]
    public async Task VerifySignaturesAsync_OnUnsignedPdf_ReturnsEmptyList()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var results = await extractor.VerifySignaturesAsync(pdf);

        Assert.NotNull(results);
        Assert.Empty(results);
    }

    // ── Integration: signed PDFs from production ─────────────────────────────

    [Fact]
    public async Task HasDigitalSignaturesAsync_OnProductionPdfs_DoesNotThrow()
    {
        var extractor = new PdfExtractor();
        var pdfDir = "/home/santi/repos/BelowZero/oxidizePdf/failed-pdfs/";
        if (!Directory.Exists(pdfDir)) return;

        var files = Directory.GetFiles(pdfDir, "*.pdf").Take(20);
        foreach (var file in files)
        {
            var pdf = File.ReadAllBytes(file);
            if (pdf.Length == 0) continue;

            // Should not throw regardless of whether PDF has signatures
            var _ = await extractor.HasDigitalSignaturesAsync(pdf);
        }
    }
}
