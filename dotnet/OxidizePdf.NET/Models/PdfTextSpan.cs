namespace OxidizePdf.NET;

/// <summary>
/// A styled text segment with its own font, size, and RGB color.
/// Used to compose <see cref="PdfRichText"/> for mixed-style text lines.
/// </summary>
public sealed class PdfTextSpan
{
    /// <summary>The text content of this span.</summary>
    public string Text { get; }

    /// <summary>The font for this span.</summary>
    public StandardFont Font { get; }

    /// <summary>The font size in points.</summary>
    public double FontSize { get; }

    /// <summary>Red component (0.0–1.0).</summary>
    public double R { get; }

    /// <summary>Green component (0.0–1.0).</summary>
    public double G { get; }

    /// <summary>Blue component (0.0–1.0).</summary>
    public double B { get; }

    /// <summary>
    /// Creates a new text span with the specified styling.
    /// </summary>
    /// <param name="text">The text content.</param>
    /// <param name="font">The font to use.</param>
    /// <param name="fontSize">Font size in points.</param>
    /// <param name="r">Red component (0.0–1.0).</param>
    /// <param name="g">Green component (0.0–1.0).</param>
    /// <param name="b">Blue component (0.0–1.0).</param>
    public PdfTextSpan(string text, StandardFont font, double fontSize, double r, double g, double b)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
        Font = font;
        FontSize = fontSize;
        R = r;
        G = g;
        B = b;
    }
}
