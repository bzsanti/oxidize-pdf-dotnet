namespace OxidizePdf.NET.Graphics;

/// <summary>
/// ICC color space type. Mirrors <c>oxidize_pdf::graphics::IccColorSpace</c>.
/// </summary>
public enum IccColorSpace
{
    /// <summary>Grayscale (1 component).</summary>
    Gray = 1,
    /// <summary>Red-Green-Blue (3 components).</summary>
    Rgb = 3,
    /// <summary>Cyan-Magenta-Yellow-Black (4 components).</summary>
    Cmyk = 4,
    /// <summary>Lab (3 components).</summary>
    Lab = 33,  // internal sentinel; component count is 3
}

/// <summary>Extension methods for <see cref="IccColorSpace"/>.</summary>
public static class IccColorSpaceExtensions
{
    /// <summary>Returns the number of color components for this color space.</summary>
    public static int ComponentCount(this IccColorSpace cs) => cs switch
    {
        IccColorSpace.Gray => 1,
        IccColorSpace.Rgb => 3,
        IccColorSpace.Lab => 3,
        IccColorSpace.Cmyk => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(cs), cs, "Unknown ICC color space"),
    };
}
