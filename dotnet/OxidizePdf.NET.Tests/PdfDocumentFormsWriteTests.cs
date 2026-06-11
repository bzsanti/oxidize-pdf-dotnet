using OxidizePdf.NET;
using OxidizePdf.NET.Models;

namespace OxidizePdf.NET.Tests;

/// <summary>
/// Behavioral round-trip tests for the AcroForm write-path (M2: FORM-007 +
/// PAGE-008). Each test creates a field of a given type, places its widget on a
/// page, serializes, and reads the field back through the read-path
/// (<see cref="PdfExtractor.GetFormFieldsAsync"/>), asserting the recovered
/// name, type, value and options — not byte-level structure.
/// </summary>
public class PdfDocumentFormsWriteTests
{
    private static async Task<List<FormField>> CreateAndReadBack(Action<PdfDocument, PdfPage> build)
    {
        byte[] bytes;
        using (var doc = new PdfDocument())
        {
            doc.EnableForms();
            using var page = PdfPage.A4();
            build(doc, page);
            doc.AddPage(page);
            bytes = doc.SaveToBytes();
        }

        return await new PdfExtractor().GetFormFieldsAsync(bytes);
    }

    [Fact]
    public async Task TextField_CreateAndReadBack_RecoversNameTypeValue()
    {
        var fields = await CreateAndReadBack((doc, page) =>
        {
            var f = doc.AddTextField("username", 50, 700, 300, 720, value: "Alice");
            page.AddFormWidget(f);
        });

        var field = Assert.Single(fields);
        Assert.Equal("username", field.FieldName);
        Assert.Equal("text", field.FieldType);
        Assert.Equal("Alice", field.Value);
    }

    [Fact]
    public async Task TextField_MaxLengthAndFlags_RoundTrip()
    {
        var fields = await CreateAndReadBack((doc, page) =>
        {
            var f = doc.AddTextField("code", 50, 700, 300, 720,
                value: "X", maxLength: 8, readOnly: true, required: true);
            page.AddFormWidget(f);
        });

        var field = Assert.Single(fields);
        Assert.Equal("code", field.FieldName);
        Assert.Equal(8, field.MaxLength);
        Assert.True(field.IsReadOnly);
        Assert.True(field.IsRequired);
    }

    [Fact]
    public async Task CheckBox_Checked_RoundTripsOnValue()
    {
        var fields = await CreateAndReadBack((doc, page) =>
        {
            var f = doc.AddCheckBox("agree", 50, 700, 70, 720, @checked: true, exportValue: "Yes");
            page.AddFormWidget(f);
        });

        var field = Assert.Single(fields);
        Assert.Equal("agree", field.FieldName);
        Assert.Equal("checkbox", field.FieldType);
        Assert.Equal("Yes", field.Value);
    }

    [Fact]
    public async Task RadioGroup_Selected_RoundTripsValueAndType()
    {
        var fields = await CreateAndReadBack((doc, page) =>
        {
            var f = doc.AddRadioGroup("color", 50, 700, 70, 720,
                new[] { ("R", "Red"), ("G", "Green"), ("B", "Blue") }, selected: 1);
            page.AddFormWidget(f);
        });

        var field = Assert.Single(fields);
        Assert.Equal("color", field.FieldName);
        Assert.Equal("radio", field.FieldType);
        Assert.Equal("G", field.Value);
    }

    [Fact]
    public async Task ComboBox_RoundTripsOptionsAndValue()
    {
        var fields = await CreateAndReadBack((doc, page) =>
        {
            var f = doc.AddComboBox("country", 50, 700, 200, 720,
                new[] { ("US", "United States"), ("CA", "Canada") }, value: "US");
            page.AddFormWidget(f);
        });

        var field = Assert.Single(fields);
        Assert.Equal("country", field.FieldName);
        Assert.Equal("dropdown", field.FieldType);
        Assert.Equal("US", field.Value);
        Assert.Equal(2, field.Options.Count);
        Assert.Contains(field.Options, o => o.ExportValue == "US" && o.DisplayText == "United States");
        Assert.Contains(field.Options, o => o.ExportValue == "CA" && o.DisplayText == "Canada");
    }

    [Fact]
    public async Task ListBox_RoundTripsOptionsAndType()
    {
        var fields = await CreateAndReadBack((doc, page) =>
        {
            var f = doc.AddListBox("sizes", 50, 650, 200, 720,
                new[] { ("S", "Small"), ("M", "Medium"), ("L", "Large") },
                selectedIndices: new[] { 1 });
            page.AddFormWidget(f);
        });

        var field = Assert.Single(fields);
        Assert.Equal("sizes", field.FieldName);
        Assert.Equal("listbox", field.FieldType);
        Assert.Equal(3, field.Options.Count);
    }

    [Fact]
    public async Task PushButton_RoundTripsType()
    {
        var fields = await CreateAndReadBack((doc, page) =>
        {
            var f = doc.AddPushButton("submit", 50, 700, 150, 720, caption: "Submit");
            page.AddFormWidget(f);
        });

        var field = Assert.Single(fields);
        Assert.Equal("submit", field.FieldName);
        Assert.Equal("pushbutton", field.FieldType);
    }

    [Fact]
    public async Task FillField_UpdatesValue_ObservableViaReadBack()
    {
        byte[] bytes;
        using (var doc = new PdfDocument())
        {
            doc.EnableForms();
            var f = doc.AddTextField("email", 50, 700, 300, 720);
            using var page = PdfPage.A4();
            page.AddFormWidget(f);
            doc.AddPage(page);
            doc.FillField("email", "user@example.com");
            bytes = doc.SaveToBytes();
        }

        var fields = await new PdfExtractor().GetFormFieldsAsync(bytes);
        var field = Assert.Single(fields);
        Assert.Equal("email", field.FieldName);
        Assert.Equal("user@example.com", field.Value);
    }

    [Fact]
    public async Task MultipleFields_AllRecovered()
    {
        var fields = await CreateAndReadBack((doc, page) =>
        {
            page.AddFormWidget(doc.AddTextField("fullname", 50, 700, 300, 720, value: "Bob"));
            page.AddFormWidget(doc.AddCheckBox("subscribe", 50, 660, 70, 680, @checked: true, exportValue: "On"));
            page.AddFormWidget(doc.AddPushButton("go", 50, 620, 150, 640));
        });

        Assert.Equal(3, fields.Count);
        Assert.Contains(fields, f => f.FieldName == "fullname" && f.FieldType == "text");
        Assert.Contains(fields, f => f.FieldName == "subscribe" && f.FieldType == "checkbox");
        Assert.Contains(fields, f => f.FieldName == "go" && f.FieldType == "pushbutton");
    }

    [Fact]
    public async Task AcroForm_IsPresent_WhenFieldCreated()
    {
        byte[] bytes;
        using (var doc = new PdfDocument())
        {
            doc.EnableForms();
            var f = doc.AddTextField("x", 50, 700, 300, 720);
            using var page = PdfPage.A4();
            page.AddFormWidget(f);
            doc.AddPage(page);
            bytes = doc.SaveToBytes();
        }

        Assert.True(await new PdfExtractor().HasFormFieldsAsync(bytes));
    }

    // ── Argument validation ──────────────────────────────────────────────────

    [Fact]
    public void AddTextField_NullName_Throws()
    {
        using var doc = new PdfDocument();
        Assert.ThrowsAny<ArgumentException>(() => doc.AddTextField(null!, 0, 0, 1, 1));
    }

    [Fact]
    public void AddRadioGroup_EmptyOptions_Throws()
    {
        using var doc = new PdfDocument();
        Assert.Throws<ArgumentException>(
            () => doc.AddRadioGroup("r", 0, 0, 1, 1, Array.Empty<(string, string)>()));
    }

    [Fact]
    public void FillField_UnknownName_Throws()
    {
        using var doc = new PdfDocument();
        doc.EnableForms();
        doc.AddTextField("present", 0, 0, 1, 1);
        Assert.Throws<PdfExtractionException>(() => doc.FillField("absent", "x"));
    }
}
