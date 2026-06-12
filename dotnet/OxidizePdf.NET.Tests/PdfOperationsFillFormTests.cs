using OxidizePdf.NET;
using OxidizePdf.NET.Models;

namespace OxidizePdf.NET.Tests;

/// <summary>
/// Behavioral tests for <see cref="PdfOperations.FillFormFieldsAsync"/> (FORM-008,
/// upstream 2.15.0 <c>IncrementalFormFiller</c>). Each test builds a form-template
/// PDF, fills it on the existing bytes, and asserts the recovered values through
/// the read-path (<see cref="PdfExtractor.GetFormFieldsAsync"/>) — not byte-level
/// structure — plus the ISO 32000-1 §7.5.6 invariant that the base bytes are an
/// exact prefix of the output.
/// </summary>
public class PdfOperationsFillFormTests
{
    /// <summary>
    /// Build a serialized form-template PDF with the given empty text fields
    /// (name → rectangle), a stand-in for an arbitrary externally-produced form.
    /// </summary>
    private static byte[] BuildTemplate(params string[] fieldNames)
    {
        using var doc = new PdfDocument();
        doc.EnableForms();
        using var page = PdfPage.A4();
        double y = 700;
        foreach (var name in fieldNames)
        {
            var f = doc.AddTextField(name, 50, y, 300, y + 20);
            page.AddFormWidget(f);
            y -= 40;
        }
        doc.AddPage(page);
        return doc.SaveToBytes();
    }

    [Fact]
    public async Task FillFormFields_SetsValue_RecoverableViaReadBack()
    {
        var template = BuildTemplate("full_name");

        var filled = await PdfOperations.FillFormFieldsAsync(
            template,
            new Dictionary<string, string> { ["full_name"] = "Ada Lovelace" });

        // ISO 32000-1 §7.5.6: the original bytes must be preserved verbatim as a prefix.
        Assert.True(filled.Length > template.Length,
            $"expected appended incremental update (filled {filled.Length} > template {template.Length})");
        Assert.Equal(template, filled[..template.Length]);

        var fields = await new PdfExtractor().GetFormFieldsAsync(filled);
        var field = Assert.Single(fields);
        Assert.Equal("full_name", field.FieldName);
        Assert.Equal("Ada Lovelace", field.Value);
    }

    [Fact]
    public async Task FillFormFields_MultipleFields_AllRecovered()
    {
        var template = BuildTemplate("first", "last", "city");

        var filled = await PdfOperations.FillFormFieldsAsync(
            template,
            new Dictionary<string, string>
            {
                ["first"] = "Grace",
                ["last"] = "Hopper",
                ["city"] = "New York",
            });

        var fields = await new PdfExtractor().GetFormFieldsAsync(filled);
        var byName = fields.ToDictionary(f => f.FieldName, f => f.Value);
        Assert.Equal("Grace", byName["first"]);
        Assert.Equal("Hopper", byName["last"]);
        Assert.Equal("New York", byName["city"]);
    }

    [Fact]
    public async Task FillFormFields_UnknownField_ThrowsPdfExtractionException()
    {
        var template = BuildTemplate("full_name");

        await Assert.ThrowsAsync<PdfExtractionException>(() =>
            PdfOperations.FillFormFieldsAsync(
                template,
                new Dictionary<string, string> { ["does_not_exist"] = "x" }));
    }

    [Fact]
    public async Task FillFormFields_EmptyFieldMap_ThrowsArgumentException()
    {
        var template = BuildTemplate("full_name");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            PdfOperations.FillFormFieldsAsync(template, new Dictionary<string, string>()));
    }

    [Fact]
    public async Task FillFormFields_EmptyPdf_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            PdfOperations.FillFormFieldsAsync(
                Array.Empty<byte>(),
                new Dictionary<string, string> { ["x"] = "y" }));
    }

    [Fact]
    public async Task FillFormFields_NullPdf_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            PdfOperations.FillFormFieldsAsync(
                null!,
                new Dictionary<string, string> { ["x"] = "y" }));
    }

    [Fact]
    public async Task FillFormFields_NullFieldMap_ThrowsArgumentNullException()
    {
        var template = BuildTemplate("full_name");

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            PdfOperations.FillFormFieldsAsync(template, null!));
    }
}
