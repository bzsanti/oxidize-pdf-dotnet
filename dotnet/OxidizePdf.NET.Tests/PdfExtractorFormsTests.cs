using OxidizePdf.NET.Models;
using OxidizePdf.NET.Tests.TestHelpers;

namespace OxidizePdf.NET.Tests;

/// <summary>
/// Tests for interactive form field reading (FORM-001 to FORM-006).
/// </summary>
public class PdfExtractorFormsTests
{
    private static readonly HashSet<string> ValidFieldTypes = new()
    {
        "text", "checkbox", "radio", "dropdown", "listbox", "pushbutton", "signature", "unknown"
    };

    // ── Model tests ──────────────────────────────────────────────────────────

    [Fact]
    public void FormField_HasExpectedDefaults()
    {
        var field = new FormField();

        Assert.Equal(string.Empty, field.FieldName);
        Assert.Equal("unknown", field.FieldType);
        Assert.Equal(0, field.PageNumber);
        Assert.Null(field.Value);
        Assert.Null(field.DefaultValue);
        Assert.False(field.IsReadOnly);
        Assert.False(field.IsRequired);
        Assert.False(field.IsMultiline);
        Assert.Null(field.MaxLength);
        Assert.NotNull(field.Options);
        Assert.Empty(field.Options);
        Assert.Null(field.Rect);
    }

    [Fact]
    public void FormFieldOption_HasExpectedDefaults()
    {
        var opt = new FormFieldOption();

        Assert.Equal(string.Empty, opt.ExportValue);
        Assert.Equal(string.Empty, opt.DisplayText);
    }

    // ── HasFormFieldsAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task HasFormFieldsAsync_NullBytes_ThrowsArgumentNullException()
    {
        var extractor = new PdfExtractor();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => extractor.HasFormFieldsAsync(null!));
    }

    [Fact]
    public async Task HasFormFieldsAsync_EmptyBytes_ThrowsArgumentException()
    {
        var extractor = new PdfExtractor();
        await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.HasFormFieldsAsync(Array.Empty<byte>()));
    }

    [Fact]
    public async Task HasFormFieldsAsync_OnPdfWithoutForms_ReturnsFalse()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var result = await extractor.HasFormFieldsAsync(pdf);

        Assert.False(result);
    }

    // ── GetFormFieldsAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetFormFieldsAsync_NullBytes_ThrowsArgumentNullException()
    {
        var extractor = new PdfExtractor();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => extractor.GetFormFieldsAsync(null!));
    }

    [Fact]
    public async Task GetFormFieldsAsync_EmptyBytes_ThrowsArgumentException()
    {
        var extractor = new PdfExtractor();
        await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.GetFormFieldsAsync(Array.Empty<byte>()));
    }

    [Fact]
    public async Task GetFormFieldsAsync_OnPdfWithoutForms_ReturnsEmptyList()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var fields = await extractor.GetFormFieldsAsync(pdf);

        Assert.NotNull(fields);
        Assert.Empty(fields);
    }

    // ── Contract tests (sample PDF has no forms — validates empty-list behavior) ──

    [Fact]
    public async Task GetFormFieldsAsync_OnEmptyForms_FieldTypeContractHolds()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var fields = await extractor.GetFormFieldsAsync(pdf);

        // Vacuously true on empty list — validates contract, not behavior.
        // A fixture with real form fields is needed for non-empty coverage.
        Assert.All(fields, f => Assert.Contains(f.FieldType, ValidFieldTypes));
    }

    [Fact]
    public async Task GetFormFieldsAsync_OnEmptyForms_PageNumberContractHolds()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var fields = await extractor.GetFormFieldsAsync(pdf);

        Assert.All(fields, f =>
            Assert.True(f.PageNumber >= 1, $"Page number should be >= 1, got {f.PageNumber}"));
    }

    [Fact]
    public async Task GetFormFieldsAsync_OnEmptyForms_RectContractHolds()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var fields = await extractor.GetFormFieldsAsync(pdf);

        Assert.All(fields.Where(f => f.Rect != null), f =>
            Assert.Equal(4, f.Rect!.Length));
    }

    [Fact]
    public async Task GetFormFieldsAsync_OnEmptyForms_MaxLengthContractHolds()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var fields = await extractor.GetFormFieldsAsync(pdf);

        Assert.All(fields.Where(f => f.MaxLength != null), f =>
            Assert.True(f.MaxLength > 0, $"MaxLength should be positive, got {f.MaxLength}"));
    }
}
