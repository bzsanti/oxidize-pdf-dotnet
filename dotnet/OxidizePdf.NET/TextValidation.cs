using System.Runtime.InteropServices;
using System.Text.Json;
using OxidizePdf.NET.Models;

namespace OxidizePdf.NET;

/// <summary>
/// TXT-016 — Validation and key-information extraction over already-extracted
/// text (e.g. the output of <see cref="PdfExtractor.ExtractTextAsync(byte[], System.Threading.CancellationToken)"/>).
/// </summary>
/// <remarks>
/// This is a <em>text-content</em> validator: it classifies dates, monetary
/// amounts, contract numbers and party names within a string. It is NOT a
/// PDF-structure integrity checker — feed it text, not raw PDF bytes.
/// </remarks>
public static class TextValidation
{
    /// <summary>
    /// Validates contract-style text, returning the dates, monetary amounts,
    /// contract numbers and party names found within it.
    /// </summary>
    /// <param name="text">The text to validate (already extracted).</param>
    /// <exception cref="ArgumentNullException">If <paramref name="text"/> is null.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public static TextValidationResult ValidateContract(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return DeserializeResult(
            NativeMethods.oxidize_text_validate_contract(text, out var outJson),
            outJson,
            "Failed to validate text");
    }

    /// <summary>
    /// Searches <paramref name="text"/> for <paramref name="target"/>, returning
    /// the classified matches found.
    /// </summary>
    /// <exception cref="ArgumentNullException">If either argument is null.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public static TextValidationResult Search(string text, string target)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(target);
        return DeserializeResult(
            NativeMethods.oxidize_text_search_target(text, target, out var outJson),
            outJson,
            "Failed to search text");
    }

    /// <summary>
    /// Extracts key information from <paramref name="text"/>, grouped by category
    /// (e.g. "dates", "monetary_amounts", "organizations").
    /// </summary>
    /// <exception cref="ArgumentNullException">If <paramref name="text"/> is null.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public static IReadOnlyDictionary<string, List<string>> ExtractKeyInfo(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var json = ReadJsonResult(
            NativeMethods.oxidize_text_extract_key_info(text, out var outJson),
            outJson,
            "Failed to extract key info");
        return JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json)
               ?? new Dictionary<string, List<string>>();
    }

    private static TextValidationResult DeserializeResult(int code, IntPtr outJson, string failMessage)
    {
        var json = ReadJsonResult(code, outJson, failMessage);
        return JsonSerializer.Deserialize<TextValidationResult>(json)
               ?? new TextValidationResult();
    }

    private static string ReadJsonResult(int code, IntPtr outJson, string failMessage)
    {
        if (code != (int)NativeMethods.ErrorCode.Success)
        {
            var rustError = NativeMethods.GetLastError();
            var detail = !string.IsNullOrEmpty(rustError)
                ? rustError
                : ((NativeMethods.ErrorCode)code).ToString();
            throw new PdfExtractionException($"{failMessage}: {detail}");
        }
        try
        {
            return Marshal.PtrToStringUTF8(outJson) ?? "";
        }
        finally
        {
            if (outJson != IntPtr.Zero)
                NativeMethods.oxidize_free_string(outJson);
        }
    }
}
