using System.Text;
using OxidizePdf.NET.Models;

namespace OxidizePdf.NET.Tests;

public class Core560Tests
{
    private static byte[] Pdf(string name) => File.ReadAllBytes(
        Path.Combine(AppContext.BaseDirectory, "fixtures", "core560", name + ".pdf"));

    [Theory]
    [InlineData("visual", "12-34-56\nAccount No.\nPayments to be made to:", "Account No. 12-34-56\nPayments to be made to:")]
    [InlineData("fallback", "����� �����", "Hello world")]
    [InlineData("annotation", "Body", "Body\nNote")]
    public async Task OptInsReachBothExtractionRoutes(string fixture, string ordinary, string enabled)
    {
        var pdf = Pdf(fixture);
        var options = new PdfTextExtractionOptions
        {
            VisualLineOrder = fixture == "visual",
            IncompleteToUnicodeEncodingFallback = fixture == "fallback",
            IncludeAnnotationAppearances = fixture == "annotation"
        };
        var legacy = new ExtractionOptions
        {
            VisualLineOrder = options.VisualLineOrder,
            IncompleteToUnicodeEncodingFallback = options.IncompleteToUnicodeEncodingFallback,
            IncludeAnnotationAppearances = options.IncludeAnnotationAppearances
        };
        var extractor = new PdfExtractor();
        Assert.Equal(ordinary, (await extractor.ExtractTextAsync(pdf, new ExtractionOptions())).Trim());
        Assert.Equal(enabled, (await extractor.ExtractTextAsync(pdf, legacy)).Trim());
        Assert.Equal(ordinary, Assert.Single(await PdfTextExtraction.ExtractAsync(pdf)).Text.Text);
        Assert.Equal(enabled, Assert.Single(await PdfTextExtraction.ExtractAsync(pdf, options)).Text.Text);
        var recovered = Assert.Single(await PdfTextExtraction.RecoverAsync(pdf, 4096, options));
        Assert.Equal(enabled, recovered.Text.Text);
        Assert.Empty(recovered.Diagnostics);
    }

    [Fact]
    public async Task TjDefaultAndExplicitOldThreshold()
    {
        var pdf = Pdf("tj");
        var extractor = new PdfExtractor();
        Assert.Equal("Alpha Beta", (await extractor.ExtractTextAsync(pdf, new ExtractionOptions())).Trim());
        Assert.Equal("AlphaBeta", (await extractor.ExtractTextAsync(pdf, new ExtractionOptions { TjSpaceThreshold = 0.2 })).Trim());
        Assert.Equal("Alpha Beta", Assert.Single(await PdfTextExtraction.ExtractAsync(pdf)).Text.Text);
        Assert.Equal("AlphaBeta", Assert.Single(await PdfTextExtraction.ExtractAsync(pdf,
            new() { TjSpaceThreshold = 0.2 })).Text.Text);
    }

    [Fact]
    public async Task AnnotationRecoveryPreservesIdentityAndBudgets()
    {
        var pdf = Pdf("annotation-damaged");
        var options = new PdfTextExtractionOptions { IncludeAnnotationAppearances = true };
        Assert.Equal("Body", Assert.Single(await PdfTextExtraction.ExtractAsync(pdf)).Text.Text);
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfTextExtraction.ExtractAsync(pdf, options));
        var recovered = Assert.Single(await PdfTextExtraction.RecoverAsync(pdf, 4096, options));
        Assert.Equal("Body\nNote", recovered.Text.Text);
        var diagnostic = Assert.Single(recovered.Diagnostics);
        Assert.Equal(PdfRecoveryLocation.AnnotationAppearance, diagnostic.Location);
        Assert.Equal(new uint[] { 7, 0 }, diagnostic.ObjectId);
        Assert.Null(diagnostic.ContentsIndex);
        Assert.Null(diagnostic.FormName);
        Assert.Equal(PdfRecoveryAction.Recovered, diagnostic.Action);
        Assert.Equal(PdfFlateRecoveryKind.Unverified, diagnostic.Kind);
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfTextExtraction.RecoverAsync(pdf, 2, options));
        options.MaxExtractedBytes = 6;
        var bounded = Assert.Single(await PdfTextExtraction.RecoverAsync(pdf, 4096, options));
        Assert.True(bounded.Text.Truncated);
        Assert.True(Encoding.UTF8.GetByteCount(bounded.Text.Text) <= 6);
    }
}
