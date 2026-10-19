using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using S = OxidizePdf.NET.PdfCalculationStep;
using C = OxidizePdf.NET.PdfCalculation;
using J = OxidizePdf.NET.PdfSystemCalculation;
using V = OxidizePdf.NET.PdfFormValue;

namespace OxidizePdf.NET.Tests;

public class CalculationTests
{
    private static double Number(PdfCalculationStepResult result) => Assert.IsType<V.Number>(result.Value).Value;

    [Fact]
    public void SystemPropagatesTypedValuesAndOrderedEventsThroughTwoHops()
    {
        var before = DateTimeOffset.UtcNow;
        var r = PdfCalculations.RunSystem([
            new S.Set("a", new V.Number(2)),
            new S.DefineSystem("b", new J.Simple(PdfSimpleOperation.Sum, ["a"])),
            new S.DefineSystem("c", new J.Simple(PdfSimpleOperation.Sum, ["b"])),
            new S.ClearEvents(), new S.Set("a", new V.Number(7)),
            new S.Get("b"), new S.Get("c"), new S.Events(), new S.Summary(), new S.Settings(), new S.ExportOrder()]);
        Assert.Equal(7, Number(r[5])); Assert.Equal(7, Number(r[6]));
        var events = r[7].Events!;
        Assert.Equal(new[] { "a", "b", "c" }, events.Select(e => e.Field));
        Assert.Equal(new[] { PdfCalculationEventKind.ValueChanged, PdfCalculationEventKind.CalculationTriggered, PdfCalculationEventKind.CalculationTriggered }, events.Select(e => e.Kind));
        Assert.Equal(new V.Number(2), events[0].OldValue); Assert.Equal(new V.Number(7), events[2].NewValue);
        Assert.All(events, e => Assert.InRange(e.Timestamp, before, DateTimeOffset.UtcNow));
        Assert.Equal(new PdfCalculationSystemSummary(3, 2, 0, 3), r[8].SystemSummary);
        Assert.Equal(new PdfCalculationSettings(), r[9].Settings);
        Assert.Equal(new[] { "b", "c" }, r[10].ExportedOrder!.Order());
    }

    [Fact]
    public void LowerEngineFunctionsExpressionsAndRemovalPreserveCoreResults()
    {
        var definitions = new C[] { new C.Sum(["a", "b", "missing"]), new C.Average(["a", "b"]),
            new C.Min(["a", "b"]), new C.Max(["a", "b"]), new C.Product(["a", "b"]), new C.Count(["a", "b", "empty", "missing"]),
            new C.Arithmetic("a * (b + 1)"), new C.If("yes", new C.Constant(new V.Text("sí")), new C.Constant(new V.Empty())), new C.JavaScript("throw new Error('never executed')") };
        var steps = new List<S> { new S.Set("a", new V.Number(2)), new S.Set("b", new V.Number(4)), new S.Set("empty", new V.Empty()), new S.Set("yes", new V.Boolean(true)) };
        for (int i = 0; i < definitions.Length; i++) { steps.Add(new S.Define("r" + i, definitions[i])); steps.Add(new S.Get("r" + i)); }
        steps.Add(new S.Summary()); steps.Add(new S.Calculate("r0")); steps.Add(new S.Recalculate());
        steps.Add(new S.Remove("r0")); steps.Add(new S.Get("r0"));
        var r = PdfCalculations.RunEngine(steps);
        var expected = new[] { 6d, 3, 2, 4, 8, 2, 10 };
        for (int i = 0; i < expected.Length; i++) Assert.Equal(expected[i], Number(r[5 + i * 2]));
        Assert.Equal(new V.Text("sí"), r[19].Value); Assert.IsType<V.Empty>(r[21].Value);
        Assert.Equal(9, r[22].EngineSummary!.CalculatedFields);
        Assert.Null(r[^1].Value);
        Assert.Null(PdfCalculations.RunEngine([new S.Get("a")])[0].Value); // calls do not retain state
    }

    [Fact]
    public void LowerEngineDependencyOrderMissingInputsAndInfinityAreObservable()
    {
        var r = PdfCalculations.RunEngine([new S.Set("a", new V.Number(2)),
            new S.Define("b", new C.Arithmetic("a + absent")), new S.Define("c", new C.Arithmetic("b * 3")),
            new S.Set("a", new V.Number(7)), new S.Get("c"), new S.Summary(),
            new S.Define("infinite", new C.Arithmetic("1 / 0")), new S.Get("infinite")]);
        Assert.Equal(21, Number(r[4]));
        Assert.Equal(new[] { "b", "c" }, r[5].EngineSummary!.CalculationOrder);
        Assert.True(double.IsPositiveInfinity(Number(r[7])));
    }

    [Theory]
    [InlineData(PdfSimpleOperation.Sum, 6)]
    [InlineData(PdfSimpleOperation.Product, 8)]
    [InlineData(PdfSimpleOperation.Average, 3)]
    [InlineData(PdfSimpleOperation.Minimum, 2)]
    [InlineData(PdfSimpleOperation.Maximum, 4)]
    public void SystemAggregates(PdfSimpleOperation operation, double expected)
        => Assert.Equal(expected, Number(PdfCalculations.RunSystem([new S.Set("a", new V.Number(2)), new S.Set("b", new V.Number(4)),
            new S.DefineSystem("r", new J.Simple(operation, ["a", "b"])), new S.Get("r")])[3]));

    [Theory]
    [InlineData(PdfPercentMode.PercentOf, 20)]
    [InlineData(PdfPercentMode.PercentageOf, 5)]
    [InlineData(PdfPercentMode.AddPercent, 220)]
    [InlineData(PdfPercentMode.SubtractPercent, 180)]
    public void SystemPercentages(PdfPercentMode mode, double expected)
        => Assert.Equal(expected, Number(PdfCalculations.RunSystem([new S.Set("base", new V.Number(200)), new S.Set("percent", new V.Number(10)),
            new S.DefineSystem("r", new J.Percent("base", "percent", mode)), new S.Get("r")])[3]), 8);

    [Fact]
    public void SystemDatesRangesRoundingAndRestrictedScriptsUseCoreSemantics()
    {
        var r = PdfCalculations.RunSystem([new S.Set("date", new V.Text("2024-02-28")), new S.Set("days", new V.Number(2)),
            new S.DefineSystem("dateResult", new J.Date("date", "days", "ignored")), new S.Get("dateResult"),
            new S.Set("amount", new V.Number(1.234)), new S.DefineSystem("bounded", new J.Range("amount", 0, 2)), new S.Get("bounded"),
            new S.DefineSystem("rounded", new J.Number("amount", 2)), new S.Get("rounded"),
            new S.DefineSystem("added", new J.Custom("amount + days", ["amount", "days"])), new S.Get("added"),
            new S.DefineSystem("unsupported", new J.Custom("alert('not executed')", [])), new S.Get("unsupported"),
            new S.DefineSystem("missing", new J.Percent("absent", "absent", PdfPercentMode.PercentageOf)), new S.Get("missing")]);
        Assert.Equal(new V.Text("2024-03-01"), r[3].Value); Assert.Equal(1.234, Number(r[6]));
        Assert.Equal(1.23, Number(r[8])); Assert.Equal(3.234, Number(r[10]));
        Assert.IsType<V.Empty>(r[12].Value); Assert.Equal(0, Number(r[14]));
    }

    [Fact]
    public void SettingsFormatsAndClearEventsAreForwarded()
    {
        var settings = new PdfCalculationSettings { AutoRecalculate = false, LogEvents = false, DecimalPrecision = 7, MaxDepth = 4 };
        var r = PdfCalculations.RunSystem([new S.Set("a", new V.Number(2)), new S.DefineSystem("b", new J.Simple(PdfSimpleOperation.Sum, ["a"])),
            new S.Set("a", new V.Number(7)), new S.Get("b"), new S.Settings(), new S.Events()], settings);
        Assert.Equal(2, Number(r[3])); Assert.Equal(settings, r[4].Settings); Assert.Empty(r[5].Events!);
        var formats = new PdfCalculationFormat[] { new PdfCalculationFormat.Number(2), new PdfCalculationFormat.Percent(1),
            new PdfCalculationFormat.Date("x"), new PdfCalculationFormat.Time("x"), new PdfCalculationFormat.Custom("x"), new PdfCalculationFormat.Special(PdfSpecialFormat.SSN) };
        foreach (var format in formats)
        {
            var formatted = PdfCalculations.RunSystem([new S.Set("a", new V.Number(1.234)), new S.SetFormat("r", format),
                new S.DefineSystem("r", new J.Simple(PdfSimpleOperation.Sum, ["a"])), new S.Get("r"), new S.Summary(), new S.ClearEvents(), new S.Events()]);
            Assert.Equal(1, formatted[4].SystemSummary!.FormattedFields);
            Assert.Empty(formatted[6].Events!);
            if (format is PdfCalculationFormat.Number) Assert.Equal(new V.Number(1.23), formatted[3].Value);
            else if (format is PdfCalculationFormat.Percent) Assert.Equal(new V.Text("123.4%"), formatted[3].Value);
            else Assert.Equal(new V.Number(1.234), formatted[3].Value);
        }
    }

    [Fact]
    public void CyclesSyntaxRangeAndDepthFailuresHaveTypedStepIndices()
    {
        var cycle = Assert.Throws<PdfCalculationException>(() => PdfCalculations.RunSystem([
            new S.DefineSystem("a", new J.Simple(PdfSimpleOperation.Sum, ["b"])), new S.DefineSystem("b", new J.Simple(PdfSimpleOperation.Sum, ["a"]))]));
        Assert.Equal(PdfCalculationErrorKind.InvalidStructure, cycle.Kind); Assert.Equal(1, cycle.StepIndex);
        var syntax = Assert.Throws<PdfCalculationException>(() => PdfCalculations.RunEngine([new S.Define("bad", new C.Arithmetic("@"))]));
        Assert.Equal(PdfCalculationErrorKind.InvalidFormat, syntax.Kind); Assert.Equal(0, syntax.StepIndex);
        var range = Assert.Throws<PdfCalculationException>(() => PdfCalculations.RunSystem([new S.Set("a", new V.Number(10)), new S.DefineSystem("r", new J.Range("a", 2, 0))]));
        Assert.Equal(PdfCalculationErrorKind.InvalidOperation, range.Kind); Assert.Equal(1, range.StepIndex);
        var depth = Assert.Throws<PdfCalculationException>(() => PdfCalculations.RunSystem([
            new S.Set("a", new V.Number(1)), new S.DefineSystem("b", new J.Simple(PdfSimpleOperation.Sum, ["a"])), new S.Set("a", new V.Number(2))], new() { MaxDepth = 0 }));
        Assert.Equal(PdfCalculationErrorKind.InvalidOperation, depth.Kind);
        Assert.Null(PdfCalculations.RunSystem([new S.Get("a")])[0].Value);
    }

    [Fact]
    public void InvalidRequestsAreBoundedAndNativeOutputIsCleared()
    {
        Assert.Throws<ArgumentNullException>(() => PdfCalculations.RunSystem(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfCalculations.RunEngine(Enumerable.Repeat<S>(new S.Summary(), 257).ToArray()));
        Assert.Equal(PdfCalculationErrorKind.InvalidInput, Assert.Throws<PdfCalculationException>(() => PdfCalculations.RunSystem([], new() { MaxDepth = -1 })).Kind);
        Assert.Equal(PdfCalculationErrorKind.InvalidInput, Assert.Throws<PdfCalculationException>(() => PdfCalculations.RunSystem([new S.DefineSystem("r", new J.Number("a", 33))])).Kind);
        Assert.Empty(PdfCalculations.RunEngine([]));
        foreach (var input in new[] { "{}", "{", "{\"System\":false,\"Steps\":[{\"Kind\":\"Unknown\"}]}" })
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            int code = NativeMethods.oxidize_calculations(bytes, (nuint)bytes.Length, out var output);
            try { Assert.Equal(9, code); Assert.Equal(IntPtr.Zero, output); }
            finally { if (output != IntPtr.Zero) NativeMethods.oxidize_free_string(output); }
        }
    }
}
