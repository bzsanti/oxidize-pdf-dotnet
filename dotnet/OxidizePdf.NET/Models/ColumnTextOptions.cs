namespace OxidizePdf.NET.Models;

/// <summary>
/// TXT-014 — Options for flowing a block of text across multiple columns on a
/// page via <see cref="PdfPage.RenderColumns"/>. The text is emitted as real
/// positioned text (one column block after another) into the content stream.
/// </summary>
public sealed class ColumnTextOptions
{
    /// <summary>The text to flow across the columns. Required.</summary>
    public string Text { get; init; } = "";

    /// <summary>Number of equal-width columns. Ignored when <see cref="CustomWidths"/> is set.</summary>
    public int ColumnCount { get; init; } = 2;

    /// <summary>Total width spanned by all columns and gutters, in PDF points.</summary>
    public double TotalWidth { get; init; }

    /// <summary>Gutter between columns, in PDF points.</summary>
    public double ColumnGap { get; init; } = 15.0;

    /// <summary>Optional explicit per-column widths (overrides <see cref="ColumnCount"/>/<see cref="TotalWidth"/>).</summary>
    public IReadOnlyList<double>? CustomWidths { get; init; }

    /// <summary>X of the left edge of the first column, in PDF points.</summary>
    public double StartX { get; init; }

    /// <summary>Y of the top of the columns, in PDF points.</summary>
    public double StartY { get; init; }

    /// <summary>Height available for each column, in PDF points.</summary>
    public double ColumnHeight { get; init; }

    /// <summary>Standard font (defaults to Helvetica).</summary>
    public StandardFont? Font { get; init; }

    /// <summary>Font size in points.</summary>
    public double? FontSize { get; init; }

    /// <summary>Line-height multiplier.</summary>
    public double? LineHeight { get; init; }

    /// <summary>Text alignment within columns: Left, Right, Center, or Justified.</summary>
    public ColumnTextAlign? TextAlign { get; init; }

    /// <summary>Whether to balance content evenly across columns.</summary>
    public bool? BalanceColumns { get; init; }

    /// <summary>Whether to draw separator lines between columns.</summary>
    public bool? ShowSeparators { get; init; }

    /// <summary>Text color as (R, G, B) in 0.0..1.0.</summary>
    public (double R, double G, double B)? Color { get; init; }
}

/// <summary>Text alignment for column layout (TXT-014).</summary>
public enum ColumnTextAlign
{
    /// <summary>Left-aligned.</summary>
    Left,
    /// <summary>Right-aligned.</summary>
    Right,
    /// <summary>Centered.</summary>
    Center,
    /// <summary>Justified.</summary>
    Justified,
}
