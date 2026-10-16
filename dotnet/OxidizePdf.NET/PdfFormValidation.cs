using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OxidizePdf.NET;

/// <summary>Explicit, synchronous form validation. Does not change FillField,
/// modify PDF bytes, resolve external resources or execute PDF JavaScript.</summary>
public static class PdfFormValidation
{
    /// <summary>Validates one field with the core engine and returns owned diagnostics.</summary>
    public static PdfFieldValidationResult Validate(PdfFieldValidation field, PdfValidationSettings? settings = null)
        => ValidateMany(new[] { field }, settings)[0];

    /// <summary>Validates up to 256 independent requests in input order. Duplicate
    /// names stay independent. Rules and values are snapshotted before custom predicates.
    /// At most 128 rules per field, 1,024 rules total, and 1 MiB serialized input.</summary>
    /// <remarks>Custom predicates must be pure; exceptions propagate and no partial
    /// results are returned. Core diagnostics preserve required/mask/rule order.</remarks>
    public static IReadOnlyList<PdfFieldValidationResult> ValidateMany(
        IReadOnlyList<PdfFieldValidation> fields, PdfValidationSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(fields);
        if (fields.Count > 256) throw new ArgumentOutOfRangeException(nameof(fields));
        var snapshot = fields.Select(field =>
        {
            ArgumentNullException.ThrowIfNull(field);
            ArgumentNullException.ThrowIfNull(field.FieldName);
            ArgumentNullException.ThrowIfNull(field.Value);
            if (field.Validator is not { } validator) return field;
            ArgumentNullException.ThrowIfNull(validator.Rules);
            if (validator.Rules.Count > 128) throw new ArgumentOutOfRangeException(nameof(fields));
            var rules = validator.Rules.ToArray();
            foreach (var rule in rules)
            {
                ArgumentNullException.ThrowIfNull(rule);
                if (rule is PdfValidationRule.Custom custom) ArgumentNullException.ThrowIfNull(custom.Predicate);
            }
            return field with { Validator = validator with { Rules = rules } };
        }).ToArray();
        if (snapshot.Sum(field => field.Validator?.Rules.Count ?? 0) > 1024)
            throw new ArgumentOutOfRangeException(nameof(fields));
        var request = JsonSerializer.SerializeToNode(new { Fields = snapshot, Settings = settings ?? new PdfValidationSettings() })!;
        if (Encoding.UTF8.GetByteCount(request.ToJsonString()) > 1_048_576)
            throw new ArgumentOutOfRangeException(nameof(fields), "Request exceeds 1 MiB JSON.");
        for (int i = 0; i < snapshot.Length; i++)
        {
            var field = snapshot[i];
            if (field.Validator is not { } validator) continue;
            for (int j = 0; j < validator.Rules.Count; j++)
            {
                if (validator.Rules[j] is PdfValidationRule.Custom custom)
                    request["Fields"]![i]!["Validator"]!["Rules"]![j]!["Passed"] = custom.Predicate(field.Value);
            }
        }
        byte[] bytes = Encoding.UTF8.GetBytes(request.ToJsonString());
        if (bytes.Length > 1_048_576) throw new ArgumentOutOfRangeException(nameof(fields), "Request exceeds 1 MiB JSON.");
        IntPtr output = IntPtr.Zero;
        try
        {
            int code = NativeMethods.oxidize_form_validate(bytes, (nuint)bytes.Length, out output);
            if (code != 0) throw new PdfFormValidationException(code, NativeMethods.GetLastError());
            return JsonSerializer.Deserialize<PdfFieldValidationResult[]>(Marshal.PtrToStringUTF8(output)!)!;
        }
        finally
        {
            if (output != IntPtr.Zero) NativeMethods.oxidize_free_string(output);
        }
    }
}

/// <summary>A failed validation operation, distinct from field diagnostics.
/// ErrorCode uses existing native codes: 9 invalid request, 10 caught panic.</summary>
public sealed class PdfFormValidationException : Exception
{
    /// <summary>Native error code.</summary>
    public int ErrorCode { get; }
    internal PdfFormValidationException(int errorCode, string? detail)
        : base($"Form validation failed ({errorCode}): {detail}") => ErrorCode = errorCode;
}
