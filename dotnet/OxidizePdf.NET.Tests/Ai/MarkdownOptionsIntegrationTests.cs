using OxidizePdf.NET.Ai;
using OxidizePdf.NET.Tests.TestHelpers;

namespace OxidizePdf.NET.Tests.Ai;

/// <summary>
/// End-to-end tests for <see cref="PdfExtractor.ToMarkdownAsync(byte[], MarkdownOptions, System.Threading.CancellationToken)"/>
/// (RAG-012). Verifies that <see cref="MarkdownOptions"/> flags travel
/// through C# → JSON → FFI → upstream exporter and produce visibly
/// different output for every combination.
/// </summary>
public class MarkdownOptionsIntegrationTests
{
    [Fact]
    public async Task ToMarkdownAsync_with_options_includes_yaml_when_metadata_true()
    {
        var pdf = PdfTestFixtures.GetSamplePdf();
        var extractor = new PdfExtractor();
        var opts = new MarkdownOptions { IncludeMetadata = true, IncludePageNumbers = false };

        var md = await extractor.ToMarkdownAsync(pdf, opts);

        Assert.StartsWith("---\n", md);
        Assert.Contains("title:", md);
        Assert.Contains("pages:", md);
        // include_page_numbers=false → no per-page markers.
        Assert.DoesNotContain("**Page ", md);
    }

    [Fact]
    public async Task ToMarkdownAsync_with_options_no_yaml_when_metadata_false()
    {
        var pdf = PdfTestFixtures.GetSamplePdf();
        var extractor = new PdfExtractor();
        var opts = new MarkdownOptions { IncludeMetadata = false, IncludePageNumbers = false };

        var md = await extractor.ToMarkdownAsync(pdf, opts);

        Assert.False(md.StartsWith("---\n"), "no YAML when metadata=false");
        Assert.DoesNotContain("title:", md);
        Assert.DoesNotContain("**Page ", md);
        Assert.False(string.IsNullOrEmpty(md));
    }

    [Fact]
    public async Task ToMarkdownAsync_with_options_includes_page_markers_when_flag_true()
    {
        var pdf = PdfTestFixtures.GetSamplePdf();
        var extractor = new PdfExtractor();
        var opts = new MarkdownOptions { IncludeMetadata = false, IncludePageNumbers = true };

        var md = await extractor.ToMarkdownAsync(pdf, opts);

        Assert.False(md.StartsWith("---\n"), "no YAML when metadata=false");
        Assert.Contains("**Page 1**", md);
    }

    [Fact]
    public async Task ToMarkdownAsync_with_options_both_true_emits_yaml_and_page_markers()
    {
        var pdf = PdfTestFixtures.GetSamplePdf();
        var extractor = new PdfExtractor();
        var opts = new MarkdownOptions { IncludeMetadata = true, IncludePageNumbers = true };

        var md = await extractor.ToMarkdownAsync(pdf, opts);

        Assert.StartsWith("---\n", md);
        Assert.Contains("title:", md);
        Assert.Contains("**Page 1**", md);
    }

    [Fact]
    public async Task ToMarkdownAsync_four_flag_combinations_produce_four_distinct_outputs()
    {
        // Cross-product check: every pair of distinct flag combinations must
        // produce different markdown. Guards against silent option-discarding.
        var pdf = PdfTestFixtures.GetSamplePdf();
        var extractor = new PdfExtractor();
        var combos = new[]
        {
            new MarkdownOptions { IncludeMetadata = false, IncludePageNumbers = false },
            new MarkdownOptions { IncludeMetadata = true,  IncludePageNumbers = false },
            new MarkdownOptions { IncludeMetadata = false, IncludePageNumbers = true  },
            new MarkdownOptions { IncludeMetadata = true,  IncludePageNumbers = true  },
        };
        var outputs = new string[combos.Length];
        for (int i = 0; i < combos.Length; i++)
            outputs[i] = await extractor.ToMarkdownAsync(pdf, combos[i]);

        for (int i = 0; i < outputs.Length; i++)
        {
            for (int j = i + 1; j < outputs.Length; j++)
            {
                Assert.NotEqual(outputs[i], outputs[j]);
            }
        }
    }

    [Fact]
    public async Task ToMarkdownAsync_with_options_differs_from_no_arg_call_when_metadata_disabled()
    {
        // The no-arg ToMarkdownAsync calls oxidize_to_markdown which always
        // emits YAML + page markers (export_with_metadata_and_pages). Calling
        // the explicit options overload with both flags off MUST produce a
        // different string.
        var pdf = PdfTestFixtures.GetSamplePdf();
        var extractor = new PdfExtractor();

        var defaulted = await extractor.ToMarkdownAsync(pdf);
        var noMeta = await extractor.ToMarkdownAsync(
            pdf,
            new MarkdownOptions { IncludeMetadata = false, IncludePageNumbers = false });

        Assert.NotEqual(defaulted, noMeta);
    }

    [Fact]
    public async Task ToMarkdownAsync_with_options_null_options_throws_ArgumentNullException()
    {
        var pdf = PdfTestFixtures.GetSamplePdf();
        var extractor = new PdfExtractor();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => extractor.ToMarkdownAsync(pdf, (MarkdownOptions)null!));
    }

    [Fact]
    public async Task ToMarkdownAsync_with_options_null_bytes_throws_ArgumentNullException()
    {
        var extractor = new PdfExtractor();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => extractor.ToMarkdownAsync(null!, new MarkdownOptions()));
    }

    [Fact]
    public async Task ToMarkdownAsync_with_options_empty_bytes_throws_ArgumentException()
    {
        var extractor = new PdfExtractor();

        await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.ToMarkdownAsync(Array.Empty<byte>(), new MarkdownOptions()));
    }

    [Fact]
    public async Task ToMarkdownAsync_with_options_respects_cancellation()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => extractor.ToMarkdownAsync(pdf, new MarkdownOptions(), cts.Token));
    }
}
