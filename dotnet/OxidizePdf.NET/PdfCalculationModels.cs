using System.Text.Json.Serialization;

namespace OxidizePdf.NET;

/// <summary>Lower core engine definitions. JavaScript is the restricted core subset, never an external runtime.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "Kind")]
[JsonDerivedType(typeof(Arithmetic), "Arithmetic")]
[JsonDerivedType(typeof(JavaScript), "JavaScript")]
[JsonDerivedType(typeof(Constant), "Constant")]
[JsonDerivedType(typeof(Sum), "Sum")]
[JsonDerivedType(typeof(Average), "Average")]
[JsonDerivedType(typeof(Min), "Min")]
[JsonDerivedType(typeof(Max), "Max")]
[JsonDerivedType(typeof(Product), "Product")]
[JsonDerivedType(typeof(Count), "Count")]
[JsonDerivedType(typeof(If), "If")]
public abstract record PdfCalculation
{
    /// <summary>Core arithmetic expression syntax; at most 4096 UTF-8 bytes.</summary>
    public sealed record Arithmetic(string Expression) : PdfCalculation;
    /// <summary>Core placeholder currently returns Empty; no external execution.</summary>
    public sealed record JavaScript(string Script) : PdfCalculation;
    /// <summary>A constant typed value.</summary>
    public sealed record Constant(PdfFormValue Value) : PdfCalculation;
    /// <summary>Core sum over the named fields.</summary>
    public sealed record Sum(IReadOnlyList<string> Fields) : PdfCalculation;
    /// <summary>Core average over the named fields.</summary>
    public sealed record Average(IReadOnlyList<string> Fields) : PdfCalculation;
    /// <summary>Core min over the named fields.</summary>
    public sealed record Min(IReadOnlyList<string> Fields) : PdfCalculation;
    /// <summary>Core max over the named fields.</summary>
    public sealed record Max(IReadOnlyList<string> Fields) : PdfCalculation;
    /// <summary>Core product over the named fields.</summary>
    public sealed record Product(IReadOnlyList<string> Fields) : PdfCalculation;
    /// <summary>Core count over the named fields.</summary>
    public sealed record Count(IReadOnlyList<string> Fields) : PdfCalculation;
    /// <summary>Conditional core calculation; nested definitions are limited to depth 16.</summary>
    public sealed record If(string ConditionField, PdfCalculation TrueValue, PdfCalculation FalseValue) : PdfCalculation;
}

/// <summary>Supported enhanced form-system calculations.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "Kind")]
[JsonDerivedType(typeof(Simple), "Simple")]
[JsonDerivedType(typeof(Percent), "Percent")]
[JsonDerivedType(typeof(Date), "Date")]
[JsonDerivedType(typeof(Range), "Range")]
[JsonDerivedType(typeof(Number), "Number")]
[JsonDerivedType(typeof(Custom), "Custom")]
public abstract record PdfSystemCalculation
{
    /// <summary>Basic aggregate calculation.</summary>
    public sealed record Simple(PdfSimpleOperation Operation, IReadOnlyList<string> Fields) : PdfSystemCalculation;
    /// <summary>Percentage calculation; a zero base in PercentageOf yields zero.</summary>
    public sealed record Percent(string BaseField, string PercentField, PdfPercentMode Mode) : PdfSystemCalculation;
    /// <summary>Date arithmetic. Core uses ISO dates regardless of Format.</summary>
    public sealed record Date(string StartDateField, string? DaysField = null, string Format = "%Y-%m-%d") : PdfSystemCalculation;
    /// <summary>Clamps to inclusive bounds; reversed bounds fail.</summary>
    public sealed record Range(string Field, double? Min = null, double? Max = null) : PdfSystemCalculation;
    /// <summary>Numeric rounding. Separator and Currency are currently reserved in core.</summary>
    public sealed record Number(string Field, int Decimals = 2, PdfNumberSeparator Separator = PdfNumberSeparator.None, string? Currency = null) : PdfSystemCalculation;
    /// <summary>Restricted two-field-addition subset. Unsupported scripts produce Empty, not execution.</summary>
    public sealed record Custom(string Script, IReadOnlyList<string> Dependencies) : PdfSystemCalculation;
}

/// <summary>Core field formatting; only Number and Percent are effective in 5.5.0.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "Kind")]
[JsonDerivedType(typeof(Number), "Number")]
[JsonDerivedType(typeof(Percent), "Percent")]
[JsonDerivedType(typeof(Date), "Date")]
[JsonDerivedType(typeof(Time), "Time")]
[JsonDerivedType(typeof(Special), "Special")]
[JsonDerivedType(typeof(Custom), "Custom")]
public abstract record PdfCalculationFormat
{
    /// <summary>Numeric display formatting; separator, negative style and currency are reserved.</summary>
    public sealed record Number(int Decimals = 2, PdfNumberSeparator Separator = PdfNumberSeparator.None, PdfNegativeStyle NegativeStyle = PdfNegativeStyle.MinusBlack, string? Currency = null) : PdfCalculationFormat;
    /// <summary>Percentage display formatting.</summary>
    public sealed record Percent(int Decimals = 2) : PdfCalculationFormat;
    /// <summary>Reserved date format.</summary>
    public sealed record Date(string Format) : PdfCalculationFormat;
    /// <summary>Reserved time format.</summary>
    public sealed record Time(string Format) : PdfCalculationFormat;
    /// <summary>Reserved special format.</summary>
    public sealed record Special(PdfSpecialFormat FormatType) : PdfCalculationFormat;
    /// <summary>Reserved custom format.</summary>
    public sealed record Custom(string FormatString) : PdfCalculationFormat;
}

/// <summary>Ordered operations. Select the matching engine; unsupported operations return InvalidOperation.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "Kind")]
[JsonDerivedType(typeof(Set), "Set")]
[JsonDerivedType(typeof(Get), "Get")]
[JsonDerivedType(typeof(Define), "Define")]
[JsonDerivedType(typeof(DefineSystem), "DefineSystem")]
[JsonDerivedType(typeof(SetFormat), "SetFormat")]
[JsonDerivedType(typeof(Remove), "Remove")]
[JsonDerivedType(typeof(Calculate), "Calculate")]
[JsonDerivedType(typeof(Recalculate), "Recalculate")]
[JsonDerivedType(typeof(Summary), "Summary")]
[JsonDerivedType(typeof(Events), "Events")]
[JsonDerivedType(typeof(ClearEvents), "ClearEvents")]
[JsonDerivedType(typeof(ExportOrder), "ExportOrder")]
[JsonDerivedType(typeof(Settings), "Settings")]
public abstract record PdfCalculationStep
{
    /// <summary>Set a value and invoke core automatic recalculation.</summary>
    public sealed record Set(string Field, PdfFormValue Value) : PdfCalculationStep;
    /// <summary>Read a typed value; absent fields return null, distinct from Empty.</summary>
    public sealed record Get(string Field) : PdfCalculationStep;
    /// <summary>Lower engine only: register or replace a definition.</summary>
    public sealed record Define(string Field, PdfCalculation Definition) : PdfCalculationStep;
    /// <summary>Enhanced system only: register or replace a definition.</summary>
    public sealed record DefineSystem(string Field, PdfSystemCalculation Definition) : PdfCalculationStep;
    /// <summary>Enhanced system only: set a field format.</summary>
    public sealed record SetFormat(string Field, PdfCalculationFormat Format) : PdfCalculationStep;
    /// <summary>Lower engine only: remove a calculation and its value.</summary>
    public sealed record Remove(string Field) : PdfCalculationStep;
    /// <summary>Lower engine only: calculate one field.</summary>
    public sealed record Calculate(string Field) : PdfCalculationStep;
    /// <summary>Lower engine only: recalculate all definitions.</summary>
    public sealed record Recalculate : PdfCalculationStep;
    /// <summary>Read the selected engine summary.</summary>
    public sealed record Summary : PdfCalculationStep;
    /// <summary>Enhanced system only: read recent events in chronological order, up to 65536.</summary>
    public sealed record Events(int Count = 65536) : PdfCalculationStep;
    /// <summary>Enhanced system only: clear event history.</summary>
    public sealed record ClearEvents : PdfCalculationStep;
    /// <summary>Enhanced system only: export the CO field names; core does not guarantee dependency order here.</summary>
    public sealed record ExportOrder : PdfCalculationStep;
    /// <summary>Enhanced system only: read effective settings.</summary>
    public sealed record Settings : PdfCalculationStep;
}

/// <summary>Core calculation SimpleOperation values.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PdfSimpleOperation>))]
public enum PdfSimpleOperation
{
    /// <summary>Sum.</summary>
    Sum,
    /// <summary>Product.</summary>
    Product,
    /// <summary>Average.</summary>
    Average,
    /// <summary>Minimum.</summary>
    Minimum,
    /// <summary>Maximum.</summary>
    Maximum,
}

/// <summary>Core calculation PercentMode values.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PdfPercentMode>))]
public enum PdfPercentMode
{
    /// <summary>PercentOf.</summary>
    PercentOf,
    /// <summary>PercentageOf.</summary>
    PercentageOf,
    /// <summary>AddPercent.</summary>
    AddPercent,
    /// <summary>SubtractPercent.</summary>
    SubtractPercent,
}

/// <summary>Core calculation NumberSeparator values.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PdfNumberSeparator>))]
public enum PdfNumberSeparator
{
    /// <summary>CommaPeriod.</summary>
    CommaPeriod,
    /// <summary>PeriodComma.</summary>
    PeriodComma,
    /// <summary>SpacePeriod.</summary>
    SpacePeriod,
    /// <summary>ApostrophePeriod.</summary>
    ApostrophePeriod,
    /// <summary>None.</summary>
    None,
}

/// <summary>Core calculation NegativeStyle values.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PdfNegativeStyle>))]
public enum PdfNegativeStyle
{
    /// <summary>MinusBlack.</summary>
    MinusBlack,
    /// <summary>RedParentheses.</summary>
    RedParentheses,
    /// <summary>BlackParentheses.</summary>
    BlackParentheses,
    /// <summary>MinusRed.</summary>
    MinusRed,
}

/// <summary>Core calculation SpecialFormat values.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PdfSpecialFormat>))]
public enum PdfSpecialFormat
{
    /// <summary>ZipCode.</summary>
    ZipCode,
    /// <summary>ZipCodePlus4.</summary>
    ZipCodePlus4,
    /// <summary>PhoneNumber.</summary>
    PhoneNumber,
    /// <summary>SSN.</summary>
    SSN,
}

/// <summary>Core calculation CalculationEventKind values.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PdfCalculationEventKind>))]
public enum PdfCalculationEventKind
{
    /// <summary>ValueChanged.</summary>
    ValueChanged,
    /// <summary>CalculationTriggered.</summary>
    CalculationTriggered,
    /// <summary>ValidationFailed.</summary>
    ValidationFailed,
    /// <summary>FormatApplied.</summary>
    FormatApplied,
    /// <summary>DependencyUpdated.</summary>
    DependencyUpdated,
}

/// <summary>Core calculation CalculationErrorKind values.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PdfCalculationErrorKind>))]
public enum PdfCalculationErrorKind
{
    /// <summary>InvalidInput.</summary>
    InvalidInput,
    /// <summary>InvalidStructure.</summary>
    InvalidStructure,
    /// <summary>InvalidOperation.</summary>
    InvalidOperation,
    /// <summary>InvalidFormat.</summary>
    InvalidFormat,
    /// <summary>CoreFailure.</summary>
    CoreFailure,
    /// <summary>NativeFailure.</summary>
    NativeFailure,
}

/// <summary>Enhanced-system settings. DecimalPrecision is stored but unused by core.</summary>
public sealed record PdfCalculationSettings
{
    /// <summary>Automatically propagate dependent values.</summary>
    public bool AutoRecalculate { get; init; } = true;
    /// <summary>Maximum dependent levels, 0..256. Zero rejects dependent updates.</summary>
    public int MaxDepth { get; init; } = 100;
    /// <summary>Record value/calculation/format events.</summary>
    public bool LogEvents { get; init; } = true;
    /// <summary>Reserved core setting, 0..32.</summary>
    public int DecimalPrecision { get; init; } = 2;
}
/// <summary>Lower-engine counts and dependency order.</summary>
public sealed record PdfCalculationSummary(int TotalFields, int CalculatedFields, int Dependencies, IReadOnlyList<string> CalculationOrder);
/// <summary>Enhanced-system counts.</summary>
public sealed record PdfCalculationSystemSummary(int TotalFields, int Calculations, int FormattedFields, int EventsLogged);
/// <summary>Core event with original UTC time and typed before/after values.</summary>
public sealed record PdfCalculationEvent(DateTimeOffset Timestamp, string Field, PdfCalculationEventKind Kind, PdfFormValue? OldValue, PdfFormValue? NewValue);
/// <summary>Owned result of one step. Only the requested result member is populated.
/// A Get of an absent field has null Value; Empty is a non-null PdfFormValue.Empty.</summary>
public sealed record PdfCalculationStepResult
{
    /// <summary>Get result. Lower-engine infinity/NaN values are preserved.</summary>
    public PdfFormValue? Value { get; init; }
    /// <summary>Lower-engine Summary result.</summary>
    public PdfCalculationSummary? EngineSummary { get; init; }
    /// <summary>Enhanced-system Summary result.</summary>
    public PdfCalculationSystemSummary? SystemSummary { get; init; }
    /// <summary>Events result, oldest first.</summary>
    public IReadOnlyList<PdfCalculationEvent>? Events { get; init; }
    /// <summary>Settings result.</summary>
    public PdfCalculationSettings? Settings { get; init; }
    /// <summary>ExportOrder result; do not infer a topological order.</summary>
    public IReadOnlyList<string>? ExportedOrder { get; init; }
}
