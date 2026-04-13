using System.Text.Json;

namespace OxidizePdf.NET;

/// <summary>
/// A single line of mixed-style text composed of multiple <see cref="PdfTextSpan"/>s.
/// Each span can have a different font, size, and color.
/// </summary>
public sealed class PdfRichText
{
    private readonly PdfTextSpan[] _spans;

    /// <summary>
    /// Creates a new rich text from one or more styled spans.
    /// </summary>
    /// <param name="spans">The text spans to compose.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="spans"/> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="spans"/> is empty.</exception>
    public PdfRichText(params PdfTextSpan[] spans)
    {
        ArgumentNullException.ThrowIfNull(spans);
        if (spans.Length == 0)
            throw new ArgumentException("At least one span is required", nameof(spans));
        _spans = spans;
    }

    /// <summary>The text spans composing this rich text.</summary>
    public IReadOnlyList<PdfTextSpan> Spans => _spans;

    /// <summary>
    /// Serializes this rich text to the JSON format expected by the native FFI layer.
    /// </summary>
    internal string ToJson()
    {
        var array = _spans.Select(s => new
        {
            text = s.Text,
            font = (int)s.Font,
            font_size = s.FontSize,
            r = s.R,
            g = s.G,
            b = s.B,
        });
        return JsonSerializer.Serialize(array);
    }
}
