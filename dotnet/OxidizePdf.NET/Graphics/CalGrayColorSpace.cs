namespace OxidizePdf.NET.Graphics;

/// <summary>
/// Calibrated gray color space (CalGray). Mirrors
/// <c>oxidize_pdf::graphics::CalGrayColorSpace</c>.
/// </summary>
public record CalGrayColorSpace
{
    /// <summary>
    /// Tristimulus white-point [X, Y, Z]. Y MUST equal 1.0 per PDF spec.
    /// Defaults to D65: [0.9505, 1.0000, 1.0890].
    /// </summary>
    public double[] WhitePoint { get; init; } = [0.9505, 1.0, 1.0890];

    /// <summary>Tristimulus black-point [X, Y, Z]. Defaults to [0, 0, 0].</summary>
    public double[] BlackPoint { get; init; } = [0.0, 0.0, 0.0];

    /// <summary>Gamma exponent. Must be positive. Defaults to 1.0 (linear).</summary>
    public double Gamma { get; init; } = 1.0;

    /// <summary>D50 standard illuminant (ICC profile connection space).</summary>
    public static CalGrayColorSpace D50() => new() { WhitePoint = [0.9642, 1.0, 0.8251] };

    /// <summary>D65 standard illuminant (sRGB / most monitors).</summary>
    public static CalGrayColorSpace D65() => new() { WhitePoint = [0.9505, 1.0, 1.0890] };

    /// <summary>Validates against PDF spec constraints. Throws <see cref="ArgumentException"/> on violation.</summary>
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
        if (Gamma <= 0 || double.IsNaN(Gamma) || double.IsInfinity(Gamma))
            throw new ArgumentException("Gamma must be a finite positive number.", nameof(Gamma));
    }
}
