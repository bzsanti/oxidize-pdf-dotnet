namespace OxidizePdf.NET.Graphics;

/// <summary>
/// A CIE L*a*b* color value. Mirrors <c>oxidize_pdf::graphics::LabColor</c>.
/// </summary>
public sealed class LabColor
{
    /// <summary>L* component (luminance). Upstream clamps to [0, 100].</summary>
    public double L { get; }

    /// <summary>a* component (green-red axis). Upstream clamps to color-space range.</summary>
    public double A { get; }

    /// <summary>b* component (blue-yellow axis). Upstream clamps to color-space range.</summary>
    public double B { get; }

    /// <summary>The Lab color space parameters.</summary>
    public LabColorSpace ColorSpace { get; }

    /// <exception cref="ArgumentNullException">If <paramref name="colorSpace"/> is null.</exception>
    public LabColor(double l, double a, double b, LabColorSpace colorSpace)
    {
        ArgumentNullException.ThrowIfNull(colorSpace);
        L = l;
        A = a;
        B = b;
        ColorSpace = colorSpace;
    }
}
