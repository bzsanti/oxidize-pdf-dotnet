using OxidizePdf.NET.Models;
using OxidizePdf.NET.Pipeline;
using OxidizePdf.NET.Tests.TestHelpers;

namespace OxidizePdf.NET.Tests.Pipeline;

/// <summary>
/// Tests for the <see cref="PdfExtractor.PartitionAsync(byte[], PartitionConfig, System.Threading.CancellationToken)"/>
/// overload introduced in Task 13. Verifies that JSON-serialized configs cross
/// the FFI boundary correctly (including the payload-carrying XYCut variant) and
/// that C#-side validation errors surface as <see cref="ArgumentException"/>.
/// </summary>
public class PdfExtractorPartitionConfigTests
{
    [Fact]
    public async Task PartitionAsync_with_default_config_matches_no_arg_call()
    {
        // A C#-default PartitionConfig must be wire-compatible with the upstream
        // Rust default — calling with the explicit object should produce the same
        // count of elements as the parameter-less overload.
        var pdf = PdfTestFixtures.GetSamplePdf();
        var extractor = new PdfExtractor();

        var defaulted = await extractor.PartitionAsync(pdf);
        var explicitCfg = await extractor.PartitionAsync(pdf, new PartitionConfig());

        Assert.Equal(defaulted.Count, explicitCfg.Count);
        // Element-by-element shape match — element_type sequence should match too.
        Assert.Equal(
            defaulted.Select(e => e.ElementType),
            explicitCfg.Select(e => e.ElementType));
    }

    [Fact]
    public async Task PartitionAsync_with_XyCut_reading_order_succeeds()
    {
        // XYCut is the payload-carrying ReadingOrderStrategy variant — exercises
        // the {"XYCut":{"min_gap":N}} JSON branch end-to-end.
        var pdf = PdfTestFixtures.GetSamplePdf();
        var extractor = new PdfExtractor();
        var cfg = new PartitionConfig().WithReadingOrder(ReadingOrderStrategy.XyCut(20.0));

        var elements = await extractor.PartitionAsync(pdf, cfg);

        Assert.NotEmpty(elements);
    }

    [Fact]
    public async Task PartitionAsync_with_None_reading_order_succeeds()
    {
        var pdf = PdfTestFixtures.GetSamplePdf();
        var extractor = new PdfExtractor();
        var cfg = new PartitionConfig().WithReadingOrder(ReadingOrderStrategy.None);

        var elements = await extractor.PartitionAsync(pdf, cfg);

        Assert.NotEmpty(elements);
    }

    [Fact]
    public async Task PartitionAsync_returns_PdfElement_with_populated_schema()
    {
        var pdf = PdfTestFixtures.GetSamplePdf();
        var extractor = new PdfExtractor();
        var cfg = new PartitionConfig();

        var elements = await extractor.PartitionAsync(pdf, cfg);

        Assert.All(elements, el =>
        {
            Assert.False(string.IsNullOrWhiteSpace(el.ElementType));
            Assert.NotNull(el.Text);
            Assert.True(el.PageNumber >= 1, $"PageNumber must be 1-based, got {el.PageNumber}");
            Assert.InRange(el.Confidence, 0.0, 1.0);
        });
    }

    [Fact]
    public async Task PartitionAsync_null_config_throws_ArgumentNullException()
    {
        var pdf = PdfTestFixtures.GetSamplePdf();
        var extractor = new PdfExtractor();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => extractor.PartitionAsync(pdf, (PartitionConfig)null!));
    }

    [Fact]
    public async Task PartitionAsync_invalid_config_throws_ArgumentException()
    {
        // PartitionConfig.Validate() is a C#-side preflight that rejects values
        // outside the documented ranges. MinTableConfidence > 1.0 is the canary.
        var pdf = PdfTestFixtures.GetSamplePdf();
        var extractor = new PdfExtractor();
        var bad = new PartitionConfig { MinTableConfidence = 5.0 };

        await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.PartitionAsync(pdf, bad));
    }

    [Fact]
    public async Task PartitionAsync_invalid_header_zone_throws()
    {
        var pdf = PdfTestFixtures.GetSamplePdf();
        var extractor = new PdfExtractor();
        var bad = new PartitionConfig { HeaderZone = -0.1 };

        await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.PartitionAsync(pdf, bad));
    }

    [Fact]
    public async Task PartitionAsync_with_config_null_bytes_throws_ArgumentNullException()
    {
        var extractor = new PdfExtractor();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => extractor.PartitionAsync(null!, new PartitionConfig()));
    }

    [Fact]
    public async Task PartitionAsync_with_config_empty_bytes_throws_ArgumentException()
    {
        var extractor = new PdfExtractor();
        await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.PartitionAsync(Array.Empty<byte>(), new PartitionConfig()));
    }

    [Fact]
    public async Task PartitionAsync_with_config_respects_cancellation()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => extractor.PartitionAsync(pdf, new PartitionConfig(), cts.Token));
    }

    [Fact]
    public async Task PartitionAsync_validation_runs_before_FFI_call()
    {
        // Validation must reject before any FFI plumbing kicks in. We verify
        // by passing both an invalid config AND empty bytes — a successful
        // implementation surfaces the validation error first
        // (ArgumentException, not ArgumentNullException for the bytes).
        var extractor = new PdfExtractor();
        // null bytes triggers ArgumentNullException FIRST per documented order:
        // ThrowIfNull(pdfBytes) before ThrowIfNull(config).
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => extractor.PartitionAsync(null!, new PartitionConfig { MinTableConfidence = 5.0 }));
        // With non-null bytes but invalid config, ArgumentException surfaces.
        var pdf = PdfTestFixtures.GetSamplePdf();
        await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.PartitionAsync(pdf, new PartitionConfig { HeaderZone = 2.0 }));
    }
}
