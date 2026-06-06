using System.Text.RegularExpressions;
using OxidizePdf.NET;
using OxidizePdf.NET.Tests.TestHelpers;
using Xunit;

namespace OxidizePdf.NET.Tests.Graphics;

/// <summary>
/// GFX-022 Draw text from the graphics context. Verifies the
/// <c>BT … Tf … Td (text) Tj ET</c> sequence and the literal text reach the page
/// content stream, distinct from the text-layout <see cref="PdfPage.TextAt"/> path.
/// </summary>
public class PdfPageDrawTextAtIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void DrawTextAt_EmitsTextOperatorsAndLiteralInContentStream()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.DrawTextAt(StandardFont.Helvetica, 14.0, 72.0, 700.0, "Hello GFX");
        doc.AddPage(page);

        var stream = ContentStreamHelper.DecompressAllContentStreams(doc.SaveToBytes());
        Assert.Matches(@"\bBT\b", stream);
        Assert.Matches(@"\bET\b", stream);
        Assert.Matches(@"/Helvetica\s+14\b.*\bTf\b", stream);
        Assert.Matches(@"\bTd\b", stream);
        Assert.Contains("(Hello GFX)", stream);
        Assert.Matches(@"\bTj\b", stream);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void DrawTextAt_PositionCoordinatesAppearInTdOperator()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.DrawTextAt(StandardFont.Courier, 10.0, 100.0, 555.0, "X");
        doc.AddPage(page);

        var stream = ContentStreamHelper.DecompressAllContentStreams(doc.SaveToBytes());
        // The text position is emitted as "x y Td".
        Assert.Matches(@"100(\.0+)?\s+555(\.0+)?\s+Td", stream);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void DrawTextAt_EscapesParenthesesInLiteralString()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        // Parentheses are PDF string delimiters and must be backslash-escaped.
        page.DrawTextAt(StandardFont.Helvetica, 12.0, 50.0, 600.0, "a(b)c");
        doc.AddPage(page);

        var stream = ContentStreamHelper.DecompressAllContentStreams(doc.SaveToBytes());
        Assert.Contains(@"(a\(b\)c)", stream);
    }

    [Fact]
    public void DrawTextAt_NullText_Throws()
    {
        using var page = PdfPage.A4();
        Assert.Throws<ArgumentNullException>(
            () => page.DrawTextAt(StandardFont.Helvetica, 12.0, 0.0, 0.0, null!));
    }
}
