using System.Text;
using OxidizePdf.NET;
using OxidizePdf.NET.Models;

namespace OxidizePdf.NET.Tests;

/// <summary>
/// Behavioral tests for TXT-014 multi-column text layout. Text is flowed across
/// columns and the emitted content stream is asserted to contain real text-show
/// operators across multiple text blocks.
/// </summary>
public class PdfColumnLayoutTests
{
    private static readonly PdfSaveOptions Uncompressed = new() { CompressStreams = false };

    private const string LongText =
        "Lorem ipsum dolor sit amet consectetur adipiscing elit sed do eiusmod tempor " +
        "incididunt ut labore et dolore magna aliqua ut enim ad minim veniam quis nostrud " +
        "exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat duis aute irure " +
        "dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur";

    [Fact]
    public void RenderColumns_FlowsTextAcrossMultipleColumns()
    {
        byte[] bytes;
        using (var doc = new PdfDocument())
        using (var page = new PdfPage(595, 842))
        {
            page.RenderColumns(new ColumnTextOptions
            {
                Text = LongText,
                ColumnCount = 2,
                TotalWidth = 460,
                ColumnGap = 20,
                StartX = 50,
                StartY = 780,
                ColumnHeight = 700,
                Font = StandardFont.Helvetica,
                FontSize = 10,
                TextAlign = ColumnTextAlign.Left,
            });
            doc.AddPage(page);
            bytes = doc.SaveToBytes(Uncompressed);
        }

        var pdf = Encoding.Latin1.GetString(bytes);
        // Real positioned text: Tj operators, and >= 2 BT blocks (one per column).
        Assert.Contains("Tj", pdf);
        var btCount = CountOccurrences(pdf, "BT");
        Assert.True(btCount >= 2, $"expected >= 2 text blocks across columns, found {btCount}");
        // A word from the source text must appear in the stream.
        Assert.True(pdf.Contains("Lorem") || pdf.Contains("ipsum"), "source text must be emitted");
    }

    [Fact]
    public void RenderColumns_CustomWidths_Renders()
    {
        byte[] bytes;
        using (var doc = new PdfDocument())
        using (var page = new PdfPage(595, 842))
        {
            page.RenderColumns(new ColumnTextOptions
            {
                Text = LongText,
                CustomWidths = new[] { 150.0, 250.0 },
                ColumnGap = 20,
                StartX = 50,
                StartY = 780,
                ColumnHeight = 700,
                FontSize = 9,
            });
            doc.AddPage(page);
            bytes = doc.SaveToBytes(Uncompressed);
        }

        var pdf = Encoding.Latin1.GetString(bytes);
        Assert.Contains("Tj", pdf);
        Assert.True(CountOccurrences(pdf, "BT") >= 2, "custom-width columns must each emit text");
    }

    [Fact]
    public void RenderColumns_NullOptions_Throws()
    {
        using var page = new PdfPage(595, 842);
        Assert.Throws<ArgumentNullException>(() => page.RenderColumns(null!));
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0)
        {
            count++;
            i += needle.Length;
        }
        return count;
    }
}
