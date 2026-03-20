using System.Runtime.InteropServices;

namespace OxidizePdf.NET;

/// <summary>
/// Result of a text measurement operation.
/// </summary>
public readonly struct TextSize
{
    /// <summary>Text width in PDF points.</summary>
    public float Width { get; init; }

    /// <summary>Text height (line height) in PDF points.</summary>
    public float Height { get; init; }
}

/// <summary>
/// Stateless utility for measuring text dimensions using an embedded font.
/// Only works with custom/embedded fonts (TTF/OTF bytes), not with the standard 14 fonts.
/// </summary>
public static class PdfTextMeasurement
{
    /// <summary>
    /// Measures the width and height of a string at the given font size.
    /// </summary>
    /// <param name="fontBytes">Raw TTF/OTF font bytes.</param>
    /// <param name="text">The text to measure.</param>
    /// <param name="fontSize">Font size in points.</param>
    /// <returns>A <see cref="TextSize"/> with Width and Height in PDF points.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="fontBytes"/> or <paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="fontBytes"/> is empty.</exception>
    /// <exception cref="PdfExtractionException">If the font bytes are invalid.</exception>
    public static TextSize Measure(byte[] fontBytes, string text, float fontSize)
    {
        ArgumentNullException.ThrowIfNull(fontBytes);
        ArgumentNullException.ThrowIfNull(text);
        if (fontBytes.Length == 0)
            throw new ArgumentException("Font bytes cannot be empty", nameof(fontBytes));

        var fontPtr = IntPtr.Zero;
        try
        {
            fontPtr = Marshal.AllocHGlobal(fontBytes.Length);
            Marshal.Copy(fontBytes, 0, fontPtr, fontBytes.Length);

            var errorCode = NativeMethods.oxidize_measure_text(
                fontPtr, (nuint)fontBytes.Length, fontSize, text,
                out var width, out var height);

            if (errorCode != (int)NativeMethods.ErrorCode.Success)
            {
                var rustError = NativeMethods.GetLastError();
                var detail = !string.IsNullOrEmpty(rustError)
                    ? rustError
                    : ((NativeMethods.ErrorCode)errorCode).ToString();
                throw new PdfExtractionException($"Failed to measure text: {detail}");
            }

            return new TextSize { Width = width, Height = height };
        }
        finally
        {
            if (fontPtr != IntPtr.Zero)
                Marshal.FreeHGlobal(fontPtr);
        }
    }
}
