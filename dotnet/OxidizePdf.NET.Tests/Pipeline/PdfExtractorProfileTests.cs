using OxidizePdf.NET.Models;
using OxidizePdf.NET.Pipeline;
using OxidizePdf.NET.Tests.TestHelpers;

namespace OxidizePdf.NET.Tests.Pipeline;

/// <summary>
/// Tests for the <see cref="PdfExtractor.PartitionAsync(byte[], ExtractionProfile, System.Threading.CancellationToken)"/>
/// overload introduced in Task 12 (RAG-004 / RAG-005). Verifies that the byte
/// discriminant crosses the FFI boundary correctly and that error paths surface
/// as <see cref="PdfExtractionException"/>.
/// </summary>
public class PdfExtractorProfileTests
{
    [Fact]
    public async Task PartitionAsync_with_Standard_profile_returns_elements()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var elements = await extractor.PartitionAsync(pdf, ExtractionProfile.Standard);

        Assert.NotNull(elements);
        Assert.NotEmpty(elements);
        Assert.All(elements, el => Assert.False(string.IsNullOrEmpty(el.ElementType)));
    }

    [Fact]
    public async Task PartitionAsync_with_Rag_profile_returns_elements()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var elements = await extractor.PartitionAsync(pdf, ExtractionProfile.Rag);

        Assert.NotEmpty(elements);
    }

    [Theory]
    [InlineData(ExtractionProfile.Standard)]
    [InlineData(ExtractionProfile.Academic)]
    [InlineData(ExtractionProfile.Form)]
    [InlineData(ExtractionProfile.Government)]
    [InlineData(ExtractionProfile.Dense)]
    [InlineData(ExtractionProfile.Presentation)]
    [InlineData(ExtractionProfile.Rag)]
    public async Task PartitionAsync_accepts_every_declared_profile(ExtractionProfile profile)
    {
        // Exhaustive: every C# enum value must round-trip through the FFI.
        // Guards against a silent reorder upstream that would shift discriminants.
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var elements = await extractor.PartitionAsync(pdf, profile);

        Assert.NotEmpty(elements);
    }

    [Fact]
    public async Task PartitionAsync_with_invalid_discriminant_throws_PdfExtractionException()
    {
        // Defensive: the enum surface restricts callers, but a deliberate cast
        // to an out-of-range value must surface the FFI's InvalidArgument as a
        // managed exception, not an opaque non-zero return code.
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        await Assert.ThrowsAsync<PdfExtractionException>(
            () => extractor.PartitionAsync(pdf, (ExtractionProfile)99));
    }

    [Fact]
    public async Task PartitionAsync_with_profile_null_bytes_throws_ArgumentNullException()
    {
        var extractor = new PdfExtractor();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => extractor.PartitionAsync(null!, ExtractionProfile.Standard));
    }

    [Fact]
    public async Task PartitionAsync_with_profile_empty_bytes_throws_ArgumentException()
    {
        var extractor = new PdfExtractor();
        await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.PartitionAsync(Array.Empty<byte>(), ExtractionProfile.Standard));
    }

    [Fact]
    public async Task PartitionAsync_with_profile_respects_cancellation()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => extractor.PartitionAsync(pdf, ExtractionProfile.Standard, cts.Token));
    }

    [Fact]
    public async Task PartitionAsync_with_profile_emits_1_based_page_numbers()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var elements = await extractor.PartitionAsync(pdf, ExtractionProfile.Rag);

        Assert.All(elements, el => Assert.True(
            el.PageNumber >= 1,
            $"PageNumber must be 1-based, got {el.PageNumber} for element type {el.ElementType}"));
    }

    [Fact]
    public async Task PartitionAsync_with_profile_returns_PdfElement_with_populated_schema()
    {
        // Schema validation: every property the FFI promises must be populated.
        // Confidence is between 0 and 1; bounding box width/height >= 0.
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var elements = await extractor.PartitionAsync(pdf, ExtractionProfile.Standard);

        Assert.All(elements, el =>
        {
            Assert.False(string.IsNullOrWhiteSpace(el.ElementType));
            Assert.NotNull(el.Text);
            Assert.InRange(el.Confidence, 0.0, 1.0);
            Assert.True(el.Width >= 0.0, $"width must be non-negative, got {el.Width}");
            Assert.True(el.Height >= 0.0, $"height must be non-negative, got {el.Height}");
        });
    }
}
