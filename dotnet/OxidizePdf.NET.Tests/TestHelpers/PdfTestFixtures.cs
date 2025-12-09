using System.Text;

namespace OxidizePdf.NET.Tests.TestHelpers;

/// <summary>
/// Utility class for generating PDF test fixtures programmatically.
/// Creates minimal valid PDFs for testing without external dependencies.
/// </summary>
public static class PdfTestFixtures
{
    /// <summary>
    /// Gets a valid single-page PDF with known text content.
    /// </summary>
    public static byte[] GetValidSinglePagePdf()
    {
        return CreateMinimalPdf("Hello World. This is a test PDF document.");
    }

    /// <summary>
    /// Gets a valid multi-page PDF with numbered pages.
    /// </summary>
    /// <param name="pages">Number of pages to generate</param>
    public static byte[] GetMultiPagePdf(int pages)
    {
        if (pages < 1)
            throw new ArgumentException("Pages must be at least 1", nameof(pages));

        return CreateMultiPagePdf(pages);
    }

    /// <summary>
    /// Gets a valid PDF with no text content.
    /// </summary>
    public static byte[] GetEmptyPdf()
    {
        return CreateMinimalPdf("");
    }

    /// <summary>
    /// Gets corrupted/invalid PDF bytes for error testing.
    /// </summary>
    public static byte[] GetCorruptedPdf()
    {
        return Encoding.ASCII.GetBytes("This is not a valid PDF file at all.");
    }

    /// <summary>
    /// Gets invalid bytes that start with PDF header but are corrupted.
    /// </summary>
    public static byte[] GetPartiallyCorruptedPdf()
    {
        return Encoding.ASCII.GetBytes("%PDF-1.4\n%%EOF\nGarbage data here");
    }

    /// <summary>
    /// Gets a PDF of approximately the specified size in MB.
    /// </summary>
    /// <param name="sizeMb">Target size in megabytes</param>
    public static byte[] GetLargePdf(int sizeMb)
    {
        if (sizeMb < 1)
            throw new ArgumentException("Size must be at least 1 MB", nameof(sizeMb));

        var targetSize = sizeMb * 1024 * 1024;
        var contentBuilder = new StringBuilder();

        // Generate repeated content to reach target size
        var baseContent = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. ";
        while (contentBuilder.Length < targetSize / 2) // PDF overhead roughly doubles content
        {
            contentBuilder.Append(baseContent);
        }

        return CreateMinimalPdf(contentBuilder.ToString());
    }

    /// <summary>
    /// Gets the sample.pdf fixture from the fixtures directory.
    /// </summary>
    public static byte[] GetSamplePdf()
    {
        var path = TestFixtures.GetFixturePath("sample.pdf");
        if (!File.Exists(path))
            throw new FileNotFoundException($"Sample PDF not found at: {path}");

        return File.ReadAllBytes(path);
    }

    /// <summary>
    /// Creates a minimal valid PDF with the specified text content.
    /// This generates a PDF 1.4 compliant document.
    /// </summary>
    private static byte[] CreateMinimalPdf(string textContent)
    {
        var escapedContent = EscapePdfString(textContent);

        // Calculate stream length for the content stream
        var streamContent = $"BT /F1 12 Tf 50 750 Td ({escapedContent}) Tj ET";
        var streamLength = Encoding.ASCII.GetByteCount(streamContent);

        var pdf = new StringBuilder();

        // Header
        pdf.AppendLine("%PDF-1.4");
        pdf.AppendLine("%\xe2\xe3\xcf\xd3"); // Binary marker

        // Object 1: Catalog
        pdf.AppendLine("1 0 obj");
        pdf.AppendLine("<< /Type /Catalog /Pages 2 0 R >>");
        pdf.AppendLine("endobj");

        // Object 2: Pages
        pdf.AppendLine("2 0 obj");
        pdf.AppendLine("<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        pdf.AppendLine("endobj");

        // Object 3: Page
        pdf.AppendLine("3 0 obj");
        pdf.AppendLine("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>");
        pdf.AppendLine("endobj");

        // Object 4: Content Stream
        pdf.AppendLine("4 0 obj");
        pdf.AppendLine($"<< /Length {streamLength} >>");
        pdf.AppendLine("stream");
        pdf.Append(streamContent);
        pdf.AppendLine();
        pdf.AppendLine("endstream");
        pdf.AppendLine("endobj");

        // Object 5: Font
        pdf.AppendLine("5 0 obj");
        pdf.AppendLine("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        pdf.AppendLine("endobj");

        // Cross-reference table
        var xrefPos = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.AppendLine("xref");
        pdf.AppendLine("0 6");
        pdf.AppendLine("0000000000 65535 f ");
        pdf.AppendLine("0000000015 00000 n ");
        pdf.AppendLine("0000000068 00000 n ");
        pdf.AppendLine("0000000125 00000 n ");
        pdf.AppendLine("0000000266 00000 n ");
        pdf.AppendLine($"0000000{350 + streamLength:D3} 00000 n ");

        // Trailer
        pdf.AppendLine("trailer");
        pdf.AppendLine("<< /Size 6 /Root 1 0 R >>");
        pdf.AppendLine("startxref");
        pdf.AppendLine(xrefPos.ToString());
        pdf.AppendLine("%%EOF");

        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    /// <summary>
    /// Creates a multi-page PDF with numbered page content.
    /// </summary>
    private static byte[] CreateMultiPagePdf(int pageCount)
    {
        var pdf = new StringBuilder();
        var objectNumber = 1;
        var pageObjectIds = new List<int>();
        var contentObjectIds = new List<int>();

        // Header
        pdf.AppendLine("%PDF-1.4");
        pdf.AppendLine("%\xe2\xe3\xcf\xd3");

        // Object 1: Catalog
        pdf.AppendLine($"{objectNumber} 0 obj");
        pdf.AppendLine("<< /Type /Catalog /Pages 2 0 R >>");
        pdf.AppendLine("endobj");
        objectNumber++;

        // Reserve object 2 for Pages (we'll need to know all page IDs first)
        var pagesObjectNumber = objectNumber;
        objectNumber++;

        // Object 3: Font
        var fontObjectNumber = objectNumber;
        pdf.AppendLine($"{fontObjectNumber} 0 obj");
        pdf.AppendLine("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        pdf.AppendLine("endobj");
        objectNumber++;

        // Create pages and their content streams
        for (int i = 0; i < pageCount; i++)
        {
            var pageObjNum = objectNumber;
            var contentObjNum = objectNumber + 1;
            pageObjectIds.Add(pageObjNum);
            contentObjectIds.Add(contentObjNum);

            var pageText = $"Page {i + 1} of {pageCount}. This is test content for page number {i + 1}.";
            var escapedText = EscapePdfString(pageText);
            var streamContent = $"BT /F1 12 Tf 50 750 Td ({escapedText}) Tj ET";
            var streamLength = Encoding.ASCII.GetByteCount(streamContent);

            // Page object
            pdf.AppendLine($"{pageObjNum} 0 obj");
            pdf.AppendLine($"<< /Type /Page /Parent {pagesObjectNumber} 0 R /MediaBox [0 0 612 792] /Contents {contentObjNum} 0 R /Resources << /Font << /F1 {fontObjectNumber} 0 R >> >> >>");
            pdf.AppendLine("endobj");

            // Content stream
            pdf.AppendLine($"{contentObjNum} 0 obj");
            pdf.AppendLine($"<< /Length {streamLength} >>");
            pdf.AppendLine("stream");
            pdf.Append(streamContent);
            pdf.AppendLine();
            pdf.AppendLine("endstream");
            pdf.AppendLine("endobj");

            objectNumber += 2;
        }

        // Now insert the Pages object (we need to rebuild the PDF with correct ordering)
        var pdfContent = pdf.ToString();
        var kidsArray = string.Join(" ", pageObjectIds.Select(id => $"{id} 0 R"));

        // Rebuild with Pages object in correct position
        pdf.Clear();
        pdf.AppendLine("%PDF-1.4");
        pdf.AppendLine("%\xe2\xe3\xcf\xd3");

        pdf.AppendLine("1 0 obj");
        pdf.AppendLine("<< /Type /Catalog /Pages 2 0 R >>");
        pdf.AppendLine("endobj");

        pdf.AppendLine("2 0 obj");
        pdf.AppendLine($"<< /Type /Pages /Kids [{kidsArray}] /Count {pageCount} >>");
        pdf.AppendLine("endobj");

        pdf.AppendLine($"3 0 obj");
        pdf.AppendLine("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        pdf.AppendLine("endobj");

        objectNumber = 4;
        for (int i = 0; i < pageCount; i++)
        {
            var pageObjNum = objectNumber;
            var contentObjNum = objectNumber + 1;

            var pageText = $"Page {i + 1} of {pageCount}. This is test content for page number {i + 1}.";
            var escapedText = EscapePdfString(pageText);
            var streamContent = $"BT /F1 12 Tf 50 750 Td ({escapedText}) Tj ET";
            var streamLength = Encoding.ASCII.GetByteCount(streamContent);

            pdf.AppendLine($"{pageObjNum} 0 obj");
            pdf.AppendLine($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents {contentObjNum} 0 R /Resources << /Font << /F1 3 0 R >> >> >>");
            pdf.AppendLine("endobj");

            pdf.AppendLine($"{contentObjNum} 0 obj");
            pdf.AppendLine($"<< /Length {streamLength} >>");
            pdf.AppendLine("stream");
            pdf.Append(streamContent);
            pdf.AppendLine();
            pdf.AppendLine("endstream");
            pdf.AppendLine("endobj");

            objectNumber += 2;
        }

        // Cross-reference table (simplified)
        var xrefPos = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.AppendLine("xref");
        pdf.AppendLine($"0 {objectNumber}");
        pdf.AppendLine("0000000000 65535 f ");

        // Simplified xref entries (not precise but functional for testing)
        for (int i = 1; i < objectNumber; i++)
        {
            pdf.AppendLine($"0000000{i * 100:D3} 00000 n ");
        }

        pdf.AppendLine("trailer");
        pdf.AppendLine($"<< /Size {objectNumber} /Root 1 0 R >>");
        pdf.AppendLine("startxref");
        pdf.AppendLine(xrefPos.ToString());
        pdf.AppendLine("%%EOF");

        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    /// <summary>
    /// Escapes special characters for PDF string literals.
    /// </summary>
    private static string EscapePdfString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        return input
            .Replace("\\", "\\\\")
            .Replace("(", "\\(")
            .Replace(")", "\\)")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
    }
}
