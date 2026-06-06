namespace OxidizePdf.NET.Graphics;

/// <summary>
/// Paint type for a tiling pattern (ISO 32000-1 §8.7.3.1, PatternType 1).
/// </summary>
public enum PaintType
{
    /// <summary>The pattern specifies its own colours (coloured tiling pattern).</summary>
    Colored = 1,

    /// <summary>The pattern is uncoloured; colour is supplied when the pattern is selected.</summary>
    Uncolored = 2,
}

/// <summary>
/// Tiling type controls how the pattern cell is spaced (ISO 32000-1 §8.7.3.1).
/// </summary>
public enum TilingType
{
    /// <summary>Constant spacing; pattern cells are spaced by integer multiples of the step.</summary>
    ConstantSpacing = 1,

    /// <summary>No distortion; cells are not distorted but spacing may vary slightly.</summary>
    NoDistortion = 2,

    /// <summary>Constant spacing with faster tiling.</summary>
    ConstantSpacingFaster = 3,
}

/// <summary>
/// A tiling pattern (GFX-016): a small graphics cell replicated to fill a region.
/// Register it on a page with <see cref="PdfPage.AddTilingPattern"/>, then select it
/// as the fill or stroke colour with <see cref="PdfPage.SetFillPattern"/> /
/// <see cref="PdfPage.SetStrokePattern"/>.
/// </summary>
public sealed class PdfTilingPattern
{
    /// <summary>Resource name the pattern is registered under (e.g. "P1").</summary>
    public string Name { get; }

    /// <summary>Paint type (coloured/uncoloured).</summary>
    public PaintType PaintType { get; }

    /// <summary>Tiling type.</summary>
    public TilingType TilingType { get; }

    /// <summary>X coordinate of the pattern cell bounding box lower-left corner.</summary>
    public double BBoxX { get; }

    /// <summary>Y coordinate of the pattern cell bounding box lower-left corner.</summary>
    public double BBoxY { get; }

    /// <summary>Width of the pattern cell bounding box.</summary>
    public double BBoxWidth { get; }

    /// <summary>Height of the pattern cell bounding box.</summary>
    public double BBoxHeight { get; }

    /// <summary>Horizontal spacing between pattern cells. Must be positive.</summary>
    public double XStep { get; }

    /// <summary>Vertical spacing between pattern cells. Must be positive.</summary>
    public double YStep { get; }

    /// <summary>Raw content-stream operators that draw a single pattern cell.</summary>
    public byte[] ContentStream { get; }

    /// <summary>Optional 6-element pattern matrix [a b c d e f]; null means identity.</summary>
    public double[]? Matrix { get; }

    /// <summary>Creates a tiling pattern definition.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="contentStream"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty, steps are non-positive, or <paramref name="matrix"/> is not 6 elements.</exception>
    public PdfTilingPattern(
        string name,
        PaintType paintType,
        TilingType tilingType,
        double bboxX,
        double bboxY,
        double bboxWidth,
        double bboxHeight,
        double xStep,
        double yStep,
        byte[] contentStream,
        double[]? matrix = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(contentStream);
        if (name.Length == 0)
            throw new ArgumentException("Pattern name must not be empty.", nameof(name));
        if (xStep <= 0.0)
            throw new ArgumentException("XStep must be positive.", nameof(xStep));
        if (yStep <= 0.0)
            throw new ArgumentException("YStep must be positive.", nameof(yStep));
        if (matrix is not null && matrix.Length != 6)
            throw new ArgumentException("Matrix must have exactly 6 elements.", nameof(matrix));

        Name = name;
        PaintType = paintType;
        TilingType = tilingType;
        BBoxX = bboxX;
        BBoxY = bboxY;
        BBoxWidth = bboxWidth;
        BBoxHeight = bboxHeight;
        XStep = xStep;
        YStep = yStep;
        ContentStream = contentStream;
        Matrix = matrix;
    }
}
