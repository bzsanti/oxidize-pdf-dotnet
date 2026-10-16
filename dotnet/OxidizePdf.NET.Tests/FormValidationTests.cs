using System.Text;
using System.Text.Json;

namespace OxidizePdf.NET.Tests;

public class FormValidationTests
{
    private static PdfFieldValidationResult Check(PdfFormValue value, PdfValidationRule rule)
        => PdfFormValidation.Validate(new("field", value, new(new[] { rule })));
    private static PdfFieldValidationResult Format(string value, PdfFormatMask mask)
        => PdfFormValidation.Validate(new("field", new PdfFormValue.Text(value), new(Array.Empty<PdfValidationRule>(), mask)));

    [Fact]
    public void EveryRulePreservesValidInvalidAndTypedDiagnostics()
    {
        (PdfValidationRule Rule, string Valid, string Invalid, PdfValidationErrorType Error)[] cases =
        {
            (new PdfValidationRule.Required(), "ok", "", PdfValidationErrorType.Required),
            (new PdfValidationRule.Range(1, 3), "2", "9", PdfValidationErrorType.Range),
            (new PdfValidationRule.Length(2, 4), "abc", "abcdef", PdfValidationErrorType.Length),
            (new PdfValidationRule.Pattern("^a+$"), "aaa", "b", PdfValidationErrorType.Pattern),
            (new PdfValidationRule.Email(), "a@example.com", "missing", PdfValidationErrorType.Custom),
            (new PdfValidationRule.Url(), "https://example.com/path", "missing", PdfValidationErrorType.Custom),
            (new PdfValidationRule.Phone(PdfPhoneCountry.US), "212-555-1234", "1", PdfValidationErrorType.Custom),
            (new PdfValidationRule.Date(new(2024, 1, 1), new(2024, 12, 31)), "2024-06-01", "2025-01-01", PdfValidationErrorType.Custom),
            (new PdfValidationRule.Time(new(9, 0), new(17, 0)), "12:30", "24:30", PdfValidationErrorType.Custom),
            (new PdfValidationRule.CreditCard(), "4111111111111111", "4111111111111112", PdfValidationErrorType.Custom),
            (new PdfValidationRule.Custom("equals", v => v == new PdfFormValue.Text("yes")), "yes", "no", PdfValidationErrorType.Custom),
        };
        foreach (var item in cases)
        {
            var valid = Check(new PdfFormValue.Text(item.Valid), item.Rule);
            Assert.True(valid.IsValid, JsonSerializer.Serialize(valid));
            var invalid = Check(new PdfFormValue.Text(item.Invalid), item.Rule);
            Assert.False(invalid.IsValid);
            var error = Assert.Single(invalid.Errors);
            Assert.Equal(item.Error, error.ErrorType);
            Assert.Equal("field", error.FieldName);
            Assert.Null(error.Details);
        }
        Assert.Equal(PdfValidationErrorType.Pattern, Assert.Single(Check(new PdfFormValue.Text("a"), new PdfValidationRule.Pattern("[")).Errors).ErrorType);
        Assert.False(Check(new PdfFormValue.Number(0), new PdfValidationRule.Range(1, 3)).IsValid);
        Assert.False(Check(new PdfFormValue.Text("a"), new PdfValidationRule.Length(2, 4)).IsValid);
        Assert.False(Check(new PdfFormValue.Text("2023-12-31"), new PdfValidationRule.Date(new(2024, 1, 1))).IsValid);
        Assert.False(Check(new PdfFormValue.Text("08:59"), new PdfValidationRule.Time(new(9, 0), new(17, 0))).IsValid);
        Assert.False(Check(new PdfFormValue.Text("17:01"), new PdfValidationRule.Time(new(9, 0), new(17, 0))).IsValid);
        Assert.True(Check(new PdfFormValue.Text("17:00"), new PdfValidationRule.Time(new(9, 0), new(17, 0))).IsValid);
    }

    [Fact]
    public void EveryMaskAndLayoutUsesCoreFormatting()
    {
        Assert.Equal("$1,234.50!", Format("1234.5", new PdfFormatMask.Number(2, true, true, "$", "!")).FormattedValue);
        Assert.False(Format("-1", new PdfFormatMask.Number(AllowNegative: false)).IsValid);
        var dates = new[] { "03/15/2024", "15/03/2024", "2024-03-15", "15.03.2024", "03-15-2024" };
        foreach (var format in Enum.GetValues<PdfDateFormat>())
            Assert.Equal(dates[(int)format], Format("2024-03-15", new PdfFormatMask.Date(format)).FormattedValue);
        var times = new[] { "13:45", "13:45:30", "01:45 PM", "13:45:30" };
        foreach (var format in Enum.GetValues<PdfTimeFormat>())
            Assert.Equal(times[(int)format], Format("13:45:30", new PdfFormatMask.Time(format)).FormattedValue);
        Assert.Equal("01:45:30 PM", Format("13:45:30", new PdfFormatMask.Time(PdfTimeFormat.HMSAM, false)).FormattedValue);
        Assert.Equal("(212) 555-1234", Format("2125551234", new PdfFormatMask.Phone()).FormattedValue);
        Assert.Equal("+44 2012 345678", Format("442012345678", new PdfFormatMask.Phone(PdfPhoneCountry.UK)).FormattedValue);
        foreach (var country in new[] { PdfPhoneCountry.EU, PdfPhoneCountry.Japan, PdfPhoneCountry.Custom })
            Assert.Equal("123456789", Format("123-456-789", new PdfFormatMask.Phone(country)).FormattedValue);
        Assert.Equal("123-45-6789", Format("123456789", new PdfFormatMask.Ssn()).FormattedValue);
        Assert.Equal("12345", Format("123456789", new PdfFormatMask.ZipCode()).FormattedValue);
        Assert.Equal("12345-6789", Format("123456789", new PdfFormatMask.ZipCode(true)).FormattedValue);
        Assert.Equal("4111 1111 1111 1111", Format("4111111111111111", new PdfFormatMask.CreditCard()).FormattedValue);
        Assert.Equal("é-日本", Format("é日本", new PdfFormatMask.Custom("#-##")).FormattedValue);
        Assert.Equal("a-b", Format("ab", new PdfFormatMask.Custom("🙂-🙂", "🙂")).FormattedValue);
    }

    [Fact]
    public void DiagnosticsKeepRequiredMaskAndRuleOrderWithoutDrops()
    {
        var rules = new PdfValidationRule[] { new PdfValidationRule.Required(), new PdfValidationRule.Range(1, 2),
            new PdfValidationRule.Length(3), new PdfValidationRule.Pattern("^x"), new PdfValidationRule.Custom("custom", _ => false) };
        var result = PdfFormValidation.Validate(new("名", new PdfFormValue.Empty(),
            new(rules, new PdfFormatMask.Ssn(), "rule-message"), new("required-message")));
        Assert.Equal(new[] { PdfValidationErrorType.Required, PdfValidationErrorType.Format, PdfValidationErrorType.Required,
            PdfValidationErrorType.Range, PdfValidationErrorType.Length, PdfValidationErrorType.Pattern, PdfValidationErrorType.Custom }, result.Errors.Select(e => e.ErrorType));
        Assert.Equal("required-message", result.Errors[0].Message);
        Assert.All(result.Errors.Skip(2), e => Assert.Equal("rule-message", e.Message));
        Assert.All(result.Errors, e => Assert.Equal("名", e.FieldName));
        Assert.Empty(result.Warnings);
        Assert.Null(result.FormattedValue);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void CoreLimitsDefaultsAndAllValueKindsAreExplicit()
    {
        Assert.True(Check(new PdfFormValue.Boolean(false), new PdfValidationRule.Required()).IsValid);
        Assert.True(Check(new PdfFormValue.Number(2), new PdfValidationRule.Range(2, 2)).IsValid);
        Assert.False(Check(new PdfFormValue.Empty(), new PdfValidationRule.Range()).IsValid);
        Assert.True(Check(new PdfFormValue.Text(" "), new PdfValidationRule.Required()).IsValid);
        Assert.False(Check(new PdfFormValue.Text("é"), new PdfValidationRule.Length(Max: 1)).IsValid);
        Assert.True(Check(new PdfFormValue.Text("é"), new PdfValidationRule.Length(Max: 2)).IsValid);
        foreach (var condition in new PdfRequirementCondition[] {
            new PdfRequirementCondition.IfFieldEquals("other", new PdfFormValue.Boolean(true)),
            new PdfRequirementCondition.IfFieldNotEmpty("other"), new PdfRequirementCondition.IfGroupHasValue("group") })
            Assert.True(PdfFormValidation.Validate(new("x", new PdfFormValue.Empty(), Required: new("missing", "group", condition))).IsValid);
        var request = new PdfFieldValidation("x", new PdfFormValue.Empty(), Required: new("missing", Condition: new PdfRequirementCondition.Always()));
        Assert.False(PdfFormValidation.Validate(request).IsValid);
        var disabled = new PdfValidationSettings { ValidateOnChange = false, ShowFormatHints = false, AutoFormat = false,
            AllowPartial = true, RealTimeValidation = false, HighlightErrors = false, ShowErrorMessages = false };
        Assert.Equal(JsonSerializer.Serialize(PdfFormValidation.Validate(request)), JsonSerializer.Serialize(PdfFormValidation.Validate(request, disabled)));
        Assert.Equal("2024-99-99", Format("20249999", new PdfFormatMask.Date()).FormattedValue); // mask alone is not date validation
    }

    [Fact]
    public void SnapshotsCustomPredicatesBatchOrderAndExceptionsAreOwned()
    {
        var rules = new List<PdfValidationRule>();
        var fields = new List<PdfFieldValidation>();
        int calls = 0;
        rules.Add(new PdfValidationRule.Custom("change caller lists", value => {
            calls++; rules.Clear(); fields.Clear(); return value == new PdfFormValue.Text("é");
        }));
        rules.Add(new PdfValidationRule.Length(Max: 1));
        fields.Add(new("same", new PdfFormValue.Text("é"), new(rules)));
        fields.Add(new("same", new PdfFormValue.Number(2), new(new[] { new PdfValidationRule.Range(1, 3) })));
        var results = PdfFormValidation.ValidateMany(fields);
        Assert.Equal(1, calls);
        Assert.Equal(2, results.Count);
        Assert.Equal(PdfValidationErrorType.Length, Assert.Single(results[0].Errors).ErrorType);
        Assert.True(results[1].IsValid);
        Assert.Throws<InvalidOperationException>(() => Check(new PdfFormValue.Empty(), new PdfValidationRule.Custom("throws", _ => throw new InvalidOperationException("predicate"))));
        Assert.True(Check(new PdfFormValue.Text("ok"), new PdfValidationRule.Required()).IsValid);
    }

    [Fact]
    public void InvalidRequestsAndBudgetsFailWithoutPartialOutput()
    {
        Assert.Empty(PdfFormValidation.ValidateMany(Array.Empty<PdfFieldValidation>()));
        var field = new PdfFieldValidation("x", new PdfFormValue.Text("a"));
        Assert.Equal(256, PdfFormValidation.ValidateMany(Enumerable.Repeat(field, 256).ToArray()).Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfFormValidation.ValidateMany(Enumerable.Repeat(field, 257).ToArray()));
        Assert.Throws<ArgumentNullException>(() => PdfFormValidation.Validate(null!));
        var rules = Enumerable.Repeat<PdfValidationRule>(new PdfValidationRule.Required(), 128).ToArray();
        var manyRules = field with { Validator = new(rules) };
        Assert.Equal(8, PdfFormValidation.ValidateMany(Enumerable.Repeat(manyRules, 8).ToArray()).Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfFormValidation.ValidateMany(Enumerable.Repeat(manyRules, 9).ToArray()));
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfFormValidation.Validate(field with { Validator = new(rules.Append(new PdfValidationRule.Required()).ToArray()) }));
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfFormValidation.Validate(field with { Value = new PdfFormValue.Text(new string('a', 1_048_576)) }));
        Assert.Throws<PdfFormValidationException>(() => PdfFormValidation.Validate(field with { Value = new PdfFormValue.Text(new string('a', 65_537)) }));
        Assert.Throws<PdfFormValidationException>(() => Format("1", new PdfFormatMask.Number(33)));
        Assert.Throws<PdfFormValidationException>(() => Format("1", new PdfFormatMask.Custom("#", "ab")));
        Assert.Throws<PdfFormValidationException>(() => Format("1", new PdfFormatMask.Date((PdfDateFormat)99)));
        Assert.Throws<PdfFormValidationException>(() => Check(new PdfFormValue.Text("a"), new PdfValidationRule.Length(-1)));
        Assert.Throws<PdfFormValidationException>(() => PdfFormValidation.Validate(new(new string('x', 1025), new PdfFormValue.Empty())));
        var invalid = new PdfFieldValidation("bad", new PdfFormValue.Text("a"), new(Array.Empty<PdfValidationRule>(), new PdfFormatMask.Number(33)));
        Assert.Throws<PdfFormValidationException>(() => PdfFormValidation.ValidateMany(new[] { field, invalid }));
        var bytes = Encoding.UTF8.GetBytes("{}");
        Assert.Equal(9, NativeMethods.oxidize_form_validate(bytes, (nuint)bytes.Length, out var output));
        Assert.Equal(IntPtr.Zero, output);
        Assert.Equal(9, NativeMethods.oxidize_form_validate(bytes, nuint.MaxValue, out output));
        Assert.Equal(IntPtr.Zero, output);
    }
}
