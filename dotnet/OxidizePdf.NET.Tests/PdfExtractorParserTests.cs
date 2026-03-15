using OxidizePdf.NET.Tests.TestHelpers;

namespace OxidizePdf.NET.Tests;

/// <summary>
/// Tests for PdfExtractor parser features — IsEncrypted, UnlockWithPassword,
/// GetPdfVersion, and GetPageDimensions.
/// </summary>
public class PdfExtractorParserTests
{
    private readonly PdfExtractor _extractor = new();

    // ── IsEncrypted ──────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public async Task IsEncrypted_WithPlainPdf_ReturnsFalse()
    {
        var pdfBytes = CreateSimplePdf();
        var result = await _extractor.IsEncryptedAsync(pdfBytes);
        Assert.False(result);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task IsEncrypted_WithEncryptedPdf_ReturnsTrue()
    {
        var pdfBytes = CreateEncryptedPdf("user123", "owner456");
        var result = await _extractor.IsEncryptedAsync(pdfBytes);
        Assert.True(result);
    }

    [Fact]
    public async Task IsEncrypted_WithNullBytes_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _extractor.IsEncryptedAsync(null!));
    }

    [Fact]
    public async Task IsEncrypted_WithEmptyBytes_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _extractor.IsEncryptedAsync(Array.Empty<byte>()));
    }

    // ── UnlockWithPassword ───────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UnlockWithPassword_WithCorrectPassword_ReturnsTrue()
    {
        var pdfBytes = CreateEncryptedPdf("user123", "owner456");
        var result = await _extractor.UnlockWithPasswordAsync(pdfBytes, "user123");
        Assert.True(result);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UnlockWithPassword_WithOwnerPassword_ReturnsTrue()
    {
        var pdfBytes = CreateEncryptedPdf("user123", "owner456");
        var result = await _extractor.UnlockWithPasswordAsync(pdfBytes, "owner456");
        Assert.True(result);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UnlockWithPassword_WithWrongPassword_ReturnsFalse()
    {
        var pdfBytes = CreateEncryptedPdf("user123", "owner456");
        var result = await _extractor.UnlockWithPasswordAsync(pdfBytes, "wrongpassword");
        Assert.False(result);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UnlockWithPassword_OnUnencryptedPdf_ReturnsTrue()
    {
        var pdfBytes = CreateSimplePdf();
        var result = await _extractor.UnlockWithPasswordAsync(pdfBytes, "anything");
        Assert.True(result);
    }

    [Fact]
    public async Task UnlockWithPassword_WithNullBytes_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _extractor.UnlockWithPasswordAsync(null!, "password"));
    }

    [Fact]
    public async Task UnlockWithPassword_WithNullPassword_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _extractor.UnlockWithPasswordAsync(new byte[1], null!));
    }

    // ── GetPdfVersion ────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetPdfVersion_WithValidPdf_ReturnsVersionString()
    {
        var pdfBytes = CreateSimplePdf();
        var version = await _extractor.GetPdfVersionAsync(pdfBytes);
        Assert.NotNull(version);
        Assert.NotEmpty(version);
        // Should contain a dot (e.g., "1.4", "1.7", "2.0")
        Assert.Contains(".", version);
    }

    [Fact]
    public async Task GetPdfVersion_WithNullBytes_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _extractor.GetPdfVersionAsync(null!));
    }

    [Fact]
    public async Task GetPdfVersion_WithEmptyBytes_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _extractor.GetPdfVersionAsync(Array.Empty<byte>()));
    }

    // ── GetPageDimensions ────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetPageDimensions_WithA4Pdf_ReturnsCorrectDimensions()
    {
        var pdfBytes = CreateA4Pdf();
        var (width, height) = await _extractor.GetPageDimensionsAsync(pdfBytes, 1);
        // A4 = 595.28 x 841.89 points
        Assert.InRange(width, 595, 596);
        Assert.InRange(height, 841, 842);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetPageDimensions_WithLetterPdf_ReturnsCorrectDimensions()
    {
        var pdfBytes = CreateLetterPdf();
        var (width, height) = await _extractor.GetPageDimensionsAsync(pdfBytes, 1);
        // Letter = 612 x 792 points
        Assert.Equal(612, width);
        Assert.Equal(792, height);
    }

    [Fact]
    public async Task GetPageDimensions_WithZeroPageNumber_ThrowsArgumentOutOfRangeException()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _extractor.GetPageDimensionsAsync(new byte[1], 0));
    }

    [Fact]
    public async Task GetPageDimensions_WithNullBytes_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _extractor.GetPageDimensionsAsync(null!, 1));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static byte[] CreateSimplePdf()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.SetFont(StandardFont.Helvetica, 12)
            .TextAt(50, 750, "Hello World");
        doc.AddPage(page);
        return doc.SaveToBytes();
    }

    private static byte[] CreateA4Pdf()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.SetFont(StandardFont.Helvetica, 12)
            .TextAt(50, 750, "A4 page");
        doc.AddPage(page);
        return doc.SaveToBytes();
    }

    private static byte[] CreateLetterPdf()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.Letter();
        page.SetFont(StandardFont.Helvetica, 12)
            .TextAt(50, 750, "Letter page");
        doc.AddPage(page);
        return doc.SaveToBytes();
    }

    private static byte[] CreateEncryptedPdf(string userPassword, string ownerPassword)
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.SetFont(StandardFont.Helvetica, 12)
            .TextAt(50, 750, "Encrypted content");
        doc.AddPage(page);
        doc.Encrypt(userPassword, ownerPassword);
        return doc.SaveToBytes();
    }
}
