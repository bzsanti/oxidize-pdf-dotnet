namespace OxidizePdf.NET.Tests;

/// <summary>
/// Tests for PdfDocument class — document creation, metadata, and lifecycle.
/// </summary>
public class PdfDocumentTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void Constructor_CreatesInstance()
    {
        using var doc = new PdfDocument();
        Assert.NotNull(doc);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void PageCount_InitiallyZero()
    {
        using var doc = new PdfDocument();
        Assert.Equal(0, doc.PageCount);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetTitle_ReturnsSameInstance_ForFluentChaining()
    {
        using var doc = new PdfDocument();
        var result = doc.SetTitle("Test");
        Assert.Same(doc, result);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetMetadata_FluentChaining_Works()
    {
        using var doc = new PdfDocument();
        var result = doc
            .SetTitle("Title")
            .SetAuthor("Author")
            .SetSubject("Subject")
            .SetKeywords("key1, key2")
            .SetCreator("Creator");
        Assert.Same(doc, result);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void AddPage_IncrementsPageCount()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        doc.AddPage(page);
        Assert.Equal(1, doc.PageCount);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void AddPage_MultiplePagesIncrementCount()
    {
        using var doc = new PdfDocument();
        using var page1 = PdfPage.A4();
        using var page2 = PdfPage.Letter();
        doc.AddPage(page1);
        doc.AddPage(page2);
        Assert.Equal(2, doc.PageCount);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void AddPage_WithNull_ThrowsArgumentNullException()
    {
        using var doc = new PdfDocument();
        Assert.Throws<ArgumentNullException>(() => doc.AddPage(null!));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SaveToBytes_ReturnsNonEmptyArray()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        doc.AddPage(page);
        var bytes = doc.SaveToBytes();
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SaveToBytes_StartsWithPdfHeader()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        doc.AddPage(page);
        var bytes = doc.SaveToBytes();
        // PDF files start with %PDF-
        Assert.True(bytes.Length >= 5);
        Assert.Equal((byte)'%', bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'D', bytes[2]);
        Assert.Equal((byte)'F', bytes[3]);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Encrypt_ReturnsSameInstance()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        doc.AddPage(page);
        var result = doc.Encrypt("user", "owner");
        Assert.Same(doc, result);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Encrypt_WithPermissions_ReturnsSameInstance()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        doc.AddPage(page);
        var result = doc.Encrypt("user", "owner", PdfPermissions.Print | PdfPermissions.Copy);
        Assert.Same(doc, result);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Dispose_ThenAccess_ThrowsObjectDisposedException()
    {
        var doc = new PdfDocument();
        doc.Dispose();
        Assert.Throws<ObjectDisposedException>(() => doc.PageCount);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void DoubleDispose_DoesNotThrow()
    {
        var doc = new PdfDocument();
        doc.Dispose();
        doc.Dispose(); // Should not throw
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void FullWorkflow_CreateDocumentWithContentAndSave()
    {
        using var doc = new PdfDocument();
        doc.SetTitle("Integration Test Document")
           .SetAuthor("Test Runner");

        using var page = PdfPage.A4();
        page.SetFont(StandardFont.Helvetica, 12)
            .TextAt(72, 700, "Hello from .NET integration test!");
        doc.AddPage(page);

        var bytes = doc.SaveToBytes();
        Assert.True(bytes.Length > 100, "Generated PDF should be non-trivial size");
    }

    // ── Custom font tests ────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public void AddFont_WithValidTtfBytes_Succeeds()
    {
        var fontBytes = GetTestFontBytes();
        if (fontBytes == null)
            return; // Skip if no font available on system

        using var doc = new PdfDocument();
        var result = doc.AddFont("TestFont", fontBytes);
        Assert.Same(doc, result);
    }

    [Fact]
    public void AddFont_WithNullName_ThrowsArgumentNullException()
    {
        using var doc = new PdfDocument();
        Assert.Throws<ArgumentNullException>(() => doc.AddFont(null!, new byte[10]));
    }

    [Fact]
    public void AddFont_WithNullBytes_ThrowsArgumentNullException()
    {
        using var doc = new PdfDocument();
        Assert.Throws<ArgumentNullException>(() => doc.AddFont("TestFont", null!));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetCustomFont_OnPage_CreatesValidPdf()
    {
        var fontBytes = GetTestFontBytes();
        if (fontBytes == null)
            return; // Skip if no font available on system

        using var doc = new PdfDocument();
        doc.AddFont("TestFont", fontBytes);

        using var page = PdfPage.A4();
        page.SetCustomFont("TestFont", 14)
            .TextAt(50, 750, "Hello with custom font!");
        doc.AddPage(page);

        var bytes = doc.SaveToBytes();
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void SetCustomFont_WithNullName_ThrowsArgumentNullException()
    {
        using var page = PdfPage.A4();
        Assert.Throws<ArgumentNullException>(() => page.SetCustomFont(null!, 12));
    }

    /// <summary>
    /// Tries to load a TTF font from the system for testing.
    /// Returns null if no font is available (test will be skipped).
    /// </summary>
    private static byte[]? GetTestFontBytes()
    {
        var fontPaths = new[]
        {
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
            "/usr/share/fonts/truetype/freefont/FreeSans.ttf",
            "/Library/Fonts/Arial.ttf",
            @"C:\Windows\Fonts\arial.ttf",
        };

        foreach (var path in fontPaths)
        {
            if (File.Exists(path))
                return File.ReadAllBytes(path);
        }

        return null;
    }
}
