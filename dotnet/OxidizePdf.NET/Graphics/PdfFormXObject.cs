namespace OxidizePdf.NET.Graphics;

/// <summary>
/// A Form XObject (GFX-018): a self-contained, reusable content stream that can be
/// painted one or more times on a page (ISO 32000-1 §8.10.2). Register it on a page
/// with <see cref="PdfPage.AddFormXObject"/>, then paint it with
/// <see cref="PdfPage.InvokeXObject"/> (which emits the <c>/name Do</c> operator).
/// </summary>
public sealed class PdfFormXObject
{
    /// <summary>X coordinate of the form bounding box lower-left corner.</summary>
    public double X { get; }

    /// <summary>Y coordinate of the form bounding box lower-left corner.</summary>
    public double Y { get; }

    /// <summary>Width of the form bounding box.</summary>
    public double Width { get; }

    /// <summary>Height of the form bounding box.</summary>
    public double Height { get; }

    /// <summary>Raw content-stream operators that draw the form.</summary>
    public byte[] Content { get; }

    /// <summary>Optional 6-element form matrix [a b c d e f]; null means identity.</summary>
    public double[]? Matrix { get; }

    /// <summary>Creates a Form XObject definition.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="matrix"/> is not exactly 6 elements.</exception>
    public PdfFormXObject(
        double x,
        double y,
        double width,
        double height,
        byte[] content,
        double[]? matrix = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (matrix is not null && matrix.Length != 6)
            throw new ArgumentException("Matrix must have exactly 6 elements.", nameof(matrix));

        X = x;
        Y = y;
        Width = width;
        Height = height;
        Content = content;
        Matrix = matrix;
    }
}
