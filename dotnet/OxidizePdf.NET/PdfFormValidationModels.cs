using System.Text.Json.Serialization;

namespace OxidizePdf.NET;

/// <summary>Typed core form value. No PDF JavaScript is executed.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "Kind")]
[JsonDerivedType(typeof(Number), "Number")]
[JsonDerivedType(typeof(Text), "Text")]
[JsonDerivedType(typeof(Boolean), "Boolean")]
[JsonDerivedType(typeof(Empty), "Empty")]
public abstract record PdfFormValue
{
    /// <summary>A finite numeric value.</summary>
    public sealed record Number(double Value) : PdfFormValue;
    /// <summary>A text value.</summary>
    public sealed record Text(string Value) : PdfFormValue;
    /// <summary>A boolean value.</summary>
    public sealed record Boolean(bool Value) : PdfFormValue;
    /// <summary>An empty value.</summary>
    public sealed record Empty : PdfFormValue;
}

/// <summary>Ordered rules supported by the core validator.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "Kind")]
[JsonDerivedType(typeof(Required), "Required")]
[JsonDerivedType(typeof(Range), "Range")]
[JsonDerivedType(typeof(Length), "Length")]
[JsonDerivedType(typeof(Pattern), "Pattern")]
[JsonDerivedType(typeof(Email), "Email")]
[JsonDerivedType(typeof(Url), "Url")]
[JsonDerivedType(typeof(Phone), "Phone")]
[JsonDerivedType(typeof(Date), "Date")]
[JsonDerivedType(typeof(Time), "Time")]
[JsonDerivedType(typeof(CreditCard), "CreditCard")]
[JsonDerivedType(typeof(Custom), "Custom")]
public abstract record PdfValidationRule
{
    /// <summary>Reject Empty and empty text; whitespace is not empty in core.</summary>
    public sealed record Required : PdfValidationRule;
    /// <summary>Inclusive numeric bounds.</summary>
    public sealed record Range(double? Min = null, double? Max = null) : PdfValidationRule;
    /// <summary>Core 5.4.2 counts UTF-8 bytes, despite its character wording.</summary>
    public sealed record Length(int? Min = null, int? Max = null) : PdfValidationRule;
    /// <summary>Rust regex syntax. Invalid regex produces a Pattern diagnostic.</summary>
    public sealed record Pattern(string Expression) : PdfValidationRule;
    /// <summary>Core email syntax check.</summary>
    public sealed record Email : PdfValidationRule;
    /// <summary>Core URL prefix check, not URI safety validation.</summary>
    public sealed record Url : PdfValidationRule;
    /// <summary>Phone syntax for a core country format.</summary>
    public sealed record Phone(PdfPhoneCountry Country) : PdfValidationRule;
    /// <summary>ISO date input and inclusive bounds.</summary>
    public sealed record Date(DateOnly? Min = null, DateOnly? Max = null) : PdfValidationRule;
    /// <summary>24-hour time input and inclusive bounds.</summary>
    public sealed record Time(TimeOnly? Min = null, TimeOnly? Max = null) : PdfValidationRule;
    /// <summary>Core digit-length and Luhn checks.</summary>
    public sealed record CreditCard : PdfValidationRule;
    /// <summary>A pure managed predicate, called synchronously once per rule on
    /// the snapshotted value. Exceptions propagate before native validation.</summary>
    public sealed record Custom(string Name, [property: JsonIgnore] Func<PdfFormValue, bool> Predicate) : PdfValidationRule;
}

/// <summary>Core phone formats; Custom permits digits and common phone punctuation.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PdfPhoneCountry
{
    /// <summary>United States.</summary>
    US,
    /// <summary>United Kingdom.</summary>
    UK,
    /// <summary>European format.</summary>
    EU,
    /// <summary>Japanese format.</summary>
    Japan,
    /// <summary>General phone punctuation.</summary>
    Custom,
}
/// <summary>Core date output layouts.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PdfDateFormat
{
    /// <summary>Month/day/year.</summary>
    MDY,
    /// <summary>Day/month/year.</summary>
    DMY,
    /// <summary>Year-month-day.</summary>
    YMD,
    /// <summary>Day.month.year.</summary>
    DotDMY,
    /// <summary>Month-day-year.</summary>
    DashMDY,
}
/// <summary>Core time output layouts; HMSAM still honors Use24Hour in 5.4.2.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PdfTimeFormat
{
    /// <summary>Hours and minutes.</summary>
    HM,
    /// <summary>Hours, minutes and seconds.</summary>
    HMS,
    /// <summary>Hours and minutes with AM/PM.</summary>
    HMAM,
    /// <summary>Hours, minutes and seconds with optional AM/PM.</summary>
    HMSAM,
}

/// <summary>Formatting only: use validation rules to validate the original value.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "Kind")]
[JsonDerivedType(typeof(Number), "Number")]
[JsonDerivedType(typeof(Date), "Date")]
[JsonDerivedType(typeof(Time), "Time")]
[JsonDerivedType(typeof(Phone), "Phone")]
[JsonDerivedType(typeof(Ssn), "Ssn")]
[JsonDerivedType(typeof(ZipCode), "ZipCode")]
[JsonDerivedType(typeof(CreditCard), "CreditCard")]
[JsonDerivedType(typeof(Custom), "Custom")]
public abstract record PdfFormatMask
{
    /// <summary>Number formatting with at most 32 decimal places.</summary>
    public sealed record Number(int Decimals = 2, bool ThousandsSeparator = false,
        bool AllowNegative = true, string? Prefix = null, string? Suffix = null) : PdfFormatMask;
    /// <summary>Core date formatting.</summary>
    public sealed record Date(PdfDateFormat Format = PdfDateFormat.YMD) : PdfFormatMask;
    /// <summary>Core time formatting.</summary>
    public sealed record Time(PdfTimeFormat Format = PdfTimeFormat.HM, bool Use24Hour = true) : PdfFormatMask;
    /// <summary>Core phone formatting.</summary>
    public sealed record Phone(PdfPhoneCountry Country = PdfPhoneCountry.US) : PdfFormatMask;
    /// <summary>US social security number formatting.</summary>
    public sealed record Ssn : PdfFormatMask;
    /// <summary>US postal code formatting.</summary>
    public sealed record ZipCode(bool PlusFour = false) : PdfFormatMask;
    /// <summary>Groups digits into blocks of four.</summary>
    public sealed record CreditCard : PdfFormatMask;
    /// <summary>Placeholder is exactly one Unicode scalar, not necessarily one UTF-16 code unit.</summary>
    public sealed record Custom(string Pattern, string Placeholder = "#") : PdfFormatMask;
}

/// <summary>Core requirement conditions. Only Always is effective in 5.4.2;
/// the other variants currently return false, with no access to ambient values.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "Kind")]
[JsonDerivedType(typeof(Always), "Always")]
[JsonDerivedType(typeof(IfFieldEquals), "IfFieldEquals")]
[JsonDerivedType(typeof(IfFieldNotEmpty), "IfFieldNotEmpty")]
[JsonDerivedType(typeof(IfGroupHasValue), "IfGroupHasValue")]
public abstract record PdfRequirementCondition
{
    /// <summary>Unconditionally required.</summary>
    public sealed record Always : PdfRequirementCondition;
    /// <summary>Currently ineffective in core 5.4.2.</summary>
    public sealed record IfFieldEquals(string Field, PdfFormValue Value) : PdfRequirementCondition;
    /// <summary>Currently ineffective in core 5.4.2.</summary>
    public sealed record IfFieldNotEmpty(string Field) : PdfRequirementCondition;
    /// <summary>Currently ineffective in core 5.4.2.</summary>
    public sealed record IfGroupHasValue(string Group) : PdfRequirementCondition;
}

/// <summary>Required-field metadata. Null condition means always; Group is stored but unused.</summary>
public sealed record PdfRequiredField(string ErrorMessage, string? Group = null, PdfRequirementCondition? Condition = null);
/// <summary>Rules run in list order after the optional format mask; ErrorMessage overrides rule messages only.</summary>
public sealed record PdfFieldValidator(IReadOnlyList<PdfValidationRule> Rules, PdfFormatMask? FormatMask = null, string? ErrorMessage = null);
/// <summary>An independent field-validation request. No document handle or file is modified.</summary>
public sealed record PdfFieldValidation(string FieldName, PdfFormValue Value,
    PdfFieldValidator? Validator = null, PdfRequiredField? Required = null);

/// <summary>Core settings and defaults. All seven settings are currently stored but unused by core 5.4.2.</summary>
public sealed record PdfValidationSettings
{
    /// <summary>Core ValidateOnChange setting; stored but currently unused.</summary>
    public bool ValidateOnChange { get; init; } = true;
    /// <summary>Core ShowFormatHints setting; stored but currently unused.</summary>
    public bool ShowFormatHints { get; init; } = true;
    /// <summary>Core AutoFormat setting; stored but currently unused.</summary>
    public bool AutoFormat { get; init; } = true;
    /// <summary>Core AllowPartial setting; stored but currently unused.</summary>
    public bool AllowPartial { get; init; }
    /// <summary>Core RealTimeValidation setting; stored but currently unused.</summary>
    public bool RealTimeValidation { get; init; } = true;
    /// <summary>Core HighlightErrors setting; stored but currently unused.</summary>
    public bool HighlightErrors { get; init; } = true;
    /// <summary>Core ShowErrorMessages setting; stored but currently unused.</summary>
    public bool ShowErrorMessages { get; init; } = true;
}

/// <summary>Core diagnostic kinds. Mask failures use Format; email/URL/phone/date/time/card rules use Custom in 5.4.2.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PdfValidationErrorType
{
    /// <summary>Required validation failure.</summary>
    Required,
    /// <summary>Format validation failure.</summary>
    Format,
    /// <summary>Range validation failure.</summary>
    Range,
    /// <summary>Length validation failure.</summary>
    Length,
    /// <summary>Pattern validation failure.</summary>
    Pattern,
    /// <summary>Custom validation failure.</summary>
    Custom,
}
/// <summary>Owned core diagnostic. Details is currently null.</summary>
public sealed record PdfValidationError(string FieldName, PdfValidationErrorType ErrorType, string Message, string? Details);
/// <summary>Owned core validation result. Warnings currently has no producer.</summary>
public sealed record PdfFieldValidationResult(string FieldName, bool IsValid,
    IReadOnlyList<PdfValidationError> Errors, IReadOnlyList<string> Warnings, string? FormattedValue);
