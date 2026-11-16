namespace OxidizePdf.NET.Tests;

public class Core561Tests
{
    [Fact]
    public async Task StrictExtractionResolvesIndirectLength()
    {
        var pages = await PdfTextExtraction.ExtractAsync(IndirectLengthFixture(), parserOptions: PdfParserOptions.Strict());
        Assert.Equal("Release 561", Assert.Single(pages).Text.Text.Trim());
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("/Bad")]
    [InlineData("5 0 R")]
    [InlineData("2")]
    [InlineData("9223372036854775807")]
    public async Task StrictExtractionRejectsInvalidIndirectLength(string value)
    {
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfTextExtraction.ExtractAsync(
            IndirectLengthFixture(value), parserOptions: PdfParserOptions.Strict()));
    }

    private static byte[] IndirectLengthFixture(string? lengthValue = null)
    {
        const string content = "BT /F1 12 Tf 30 40 Td (Release 561) Tj ET\n";
        string[] objects = [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 6 0 R >> >> /Contents 4 0 R >>",
            "<< /Length 5 0 R >>\nstream\n" + content + "endstream",
            lengthValue ?? content.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        ];
        var result = new System.Text.StringBuilder("%PDF-1.7\n");
        var offsets = new List<int>();
        for (int i = 0; i < objects.Length; i++)
        {
            offsets.Add(result.Length);
            result.Append(System.FormattableString.Invariant($"{i + 1} 0 obj\n{objects[i]}\nendobj\n"));
        }
        int xref = result.Length;
        result.Append("xref\n0 7\n0000000000 65535 f \n");
        foreach (int offset in offsets)
            result.Append(offset.ToString("D10", System.Globalization.CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        result.Append(System.FormattableString.Invariant($"trailer\n<< /Size 7 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n"));
        return System.Text.Encoding.ASCII.GetBytes(result.ToString());
    }
}
