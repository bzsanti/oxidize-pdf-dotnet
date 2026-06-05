namespace OxidizePdf.NET.Graphics;

/// <summary>
/// A calibrated color value: either CalGray or CalRGB.
/// Mirrors <c>oxidize_pdf::graphics::CalibratedColor</c>.
/// Use <see cref="CalGray"/> or <see cref="CalRgb"/> to construct.
/// </summary>
public sealed class CalibratedColor
{
    /// <summary>True if this is a CalGray color; false if CalRGB.</summary>
    public bool IsCalGray { get; }

    /// <summary>Gray component value. Only valid when <see cref="IsCalGray"/> is true.</summary>
    public double GrayValue { get; }

    /// <summary>The CalGray color space. Only valid when <see cref="IsCalGray"/> is true.</summary>
    public CalGrayColorSpace? GrayColorSpace { get; }

    /// <summary>RGB component values [R, G, B]. Only valid when <see cref="IsCalGray"/> is false.</summary>
    public double[]? RgbValues { get; }

    /// <summary>The CalRGB color space. Only valid when <see cref="IsCalGray"/> is false.</summary>
    public CalRgbColorSpace? RgbColorSpace { get; }

    private CalibratedColor(double gray, CalGrayColorSpace cs)
    {
        IsCalGray = true;
        GrayValue = gray;
        GrayColorSpace = cs;
    }

    private CalibratedColor(double[] rgb, CalRgbColorSpace cs)
    {
        IsCalGray = false;
        RgbValues = rgb;
        RgbColorSpace = cs;
    }

    /// <summary>Constructs a calibrated gray color.</summary>
    /// <exception cref="ArgumentNullException">If <paramref name="colorSpace"/> is null.</exception>
    public static CalibratedColor CalGray(double value, CalGrayColorSpace colorSpace)
    {
        ArgumentNullException.ThrowIfNull(colorSpace);
        return new CalibratedColor(value, colorSpace);
    }

    /// <summary>Constructs a calibrated RGB color.</summary>
    /// <exception cref="ArgumentNullException">If <paramref name="colorSpace"/> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="rgb"/> does not have exactly 3 components.</exception>
    public static CalibratedColor CalRgb(double[] rgb, CalRgbColorSpace colorSpace)
    {
        ArgumentNullException.ThrowIfNull(colorSpace);
        ArgumentNullException.ThrowIfNull(rgb);
        if (rgb.Length != 3)
            throw new ArgumentException("CalRGB color requires exactly 3 components [R, G, B].", nameof(rgb));
        return new CalibratedColor(rgb, colorSpace);
    }
}
