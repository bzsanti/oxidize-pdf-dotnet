using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OxidizePdf.NET;

/// <summary>Explicit core calculations, independent of PDF documents. Calls execute
/// ordered steps in a fresh engine and return owned snapshots. No native state is retained,
/// no PDF JavaScript executes automatically, and no external script runtime is invoked.</summary>
public static class PdfCalculations
{
    private static readonly JsonSerializerOptions Json = new()
    {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        MaxDepth = 64,
    };

    /// <summary>Run lower-engine arithmetic, functions, constants and limited script definitions.
    /// Missing numeric references use zero. Division by zero may return infinity; core
    /// automatic updates can suppress calculation errors. Use Calculate/Recalculate for
    /// explicit errors. This method does not change those core semantics.</summary>
    public static IReadOnlyList<PdfCalculationStepResult> RunEngine(IReadOnlyList<PdfCalculationStep> steps)
        => Run(steps, false, null);

    /// <summary>Run the enhanced system with typed values/events and transitive recalculation.
    /// Failed updates return a typed error. Each call starts with empty state.</summary>
    /// <remarks>At most 256 steps, 256 entries per nested array, 1 MiB input JSON,
    /// 64 KiB per string, depth-16 conditional definitions, decimals 0..32 and
    /// MaxDepth 0..256. Output is limited to 4 MiB per step and 8 MiB total.
    /// Calls are synchronous; results are returned only when the entire sequence succeeds.</remarks>
    public static IReadOnlyList<PdfCalculationStepResult> RunSystem(
        IReadOnlyList<PdfCalculationStep> steps, PdfCalculationSettings? settings = null)
        => Run(steps, true, settings ?? new());

    private static IReadOnlyList<PdfCalculationStepResult> Run(
        IReadOnlyList<PdfCalculationStep> steps, bool system, PdfCalculationSettings? settings)
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (steps.Count > 256) throw new ArgumentOutOfRangeException(nameof(steps));
        foreach (var step in steps) ArgumentNullException.ThrowIfNull(step);
        // Serialize before entering native code; later caller mutations cannot change execution.
        var input = JsonSerializer.SerializeToUtf8Bytes(new { System = system, Settings = settings, Steps = steps });
        if (input.Length > 1_048_576) throw new ArgumentOutOfRangeException(nameof(steps), "Request exceeds 1 MiB.");
        IntPtr output = IntPtr.Zero;
        try
        {
            int code = NativeMethods.oxidize_calculations(input, (nuint)input.Length, out output);
            if (code != 0) throw new PdfCalculationException(
                code == 9 ? PdfCalculationErrorKind.InvalidInput : PdfCalculationErrorKind.NativeFailure,
                null, NativeMethods.GetLastError() ?? $"Native failure {code}");
            var response = JsonSerializer.Deserialize<Response>(Marshal.PtrToStringUTF8(output)!, Json)!;
            if (response.Error is { } error) throw new PdfCalculationException(error.Kind, error.StepIndex, error.Message);
            return response.Results ?? throw new InvalidOperationException("Missing calculation results.");
        }
        finally
        {
            if (output != IntPtr.Zero) NativeMethods.oxidize_free_string(output);
        }
    }
    private sealed record Error(PdfCalculationErrorKind Kind, int StepIndex, string Message);
    private sealed record Response(PdfCalculationStepResult[]? Results, Error? Error);
}

/// <summary>Typed calculation operation failure. No partial sequence result is returned.</summary>
public sealed class PdfCalculationException : Exception
{
    /// <summary>Core error variant or binding input/native failure.</summary>
    public PdfCalculationErrorKind Kind { get; }
    /// <summary>Zero-based failing step; null for a request/boundary failure.</summary>
    public int? StepIndex { get; }
    internal PdfCalculationException(PdfCalculationErrorKind kind, int? stepIndex, string message)
        : base(message) { Kind = kind; StepIndex = stepIndex; }
}
