namespace OxidizePdf.NET.Graphics;

/// <summary>
/// Calibrated RGB color space (CalRGB). Mirrors
/// <c>oxidize_pdf::graphics::CalRgbColorSpace</c>.
/// </summary>
public record CalRgbColorSpace
{
    /// <summary>Tristimulus white-point [X, Y, Z]. Y MUST equal 1.0 per PDF spec.</summary>
    public double[] WhitePoint { get; init; } = [0.9505, 1.0, 1.0890];

    /// <summary>Tristimulus black-point [X, Y, Z]. Defaults to [0, 0, 0].</summary>
    public double[] BlackPoint { get; init; } = [0.0, 0.0, 0.0];

    /// <summary>Per-channel gamma exponents (R, G, B). All components must be positive.</summary>
    public (double R, double G, double B) Gamma { get; init; } = (2.2, 2.2, 2.2);

    /// <summary>
    /// 3x3 color-transformation matrix in column-major order [XA YA ZA XB YB ZB XC YC ZC].
    /// Defaults to identity.
    /// </summary>
    public double[] Matrix { get; init; } =
    [
        1.0, 0.0, 0.0,
        0.0, 1.0, 0.0,
        0.0, 0.0, 1.0,
    ];

    /// <summary>sRGB (IEC 61966-2-1) color space, D65 white point, gamma 2.2 approximation.</summary>
    public static CalRgbColorSpace SRgb() => new()
    {
        WhitePoint = [0.9505, 1.0, 1.0890],
        Gamma = (2.2, 2.2, 2.2),
        Matrix =
        [
            0.4124, 0.2126, 0.0193,
            0.3576, 0.7152, 0.1192,
            0.1805, 0.0722, 0.9505,
        ],
    };

    /// <summary>Adobe RGB (1998) color space, D65 white point.</summary>
    public static CalRgbColorSpace AdobeRgb() => new()
    {
        WhitePoint = [0.9505, 1.0, 1.0890],
        Gamma = (2.2, 2.2, 2.2),
        Matrix =
        [
            0.5767, 0.2974, 0.0270,
            0.1856, 0.6273, 0.0707,
            0.1882, 0.0753, 0.9911,
        ],
    };

    /// <summary>Validates this color space. Throws <see cref="ArgumentException"/> on violation.</summary>
    public void Validate()
    {
        if (WhitePoint.Length != 3)
            throw new ArgumentException("WhitePoint must have exactly 3 components.", nameof(WhitePoint));
        if (WhitePoint[1] != 1.0)
            throw new ArgumentException("WhitePoint Y component must equal 1.0 per PDF spec.", nameof(WhitePoint));
        if (WhitePoint.Any(v => v < 0))
            throw new ArgumentException("WhitePoint components must be non-negative.", nameof(WhitePoint));
        if (BlackPoint.Length != 3)
            throw new ArgumentException("BlackPoint must have exactly 3 components.", nameof(BlackPoint));
        if (Gamma.R <= 0 || Gamma.G <= 0 || Gamma.B <= 0 ||
            double.IsNaN(Gamma.R) || double.IsNaN(Gamma.G) || double.IsNaN(Gamma.B))
            throw new ArgumentException("Gamma components must be finite positive numbers.", nameof(Gamma));
        if (Matrix.Length != 9)
            throw new ArgumentException("Matrix must have exactly 9 elements (3x3 column-major).", nameof(Matrix));
    }
}
