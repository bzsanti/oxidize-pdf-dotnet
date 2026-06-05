namespace OxidizePdf.NET.Graphics;

/// <summary>
/// CIE L*a*b* color space. Mirrors <c>oxidize_pdf::graphics::LabColorSpace</c>.
/// </summary>
public record LabColorSpace
{
    /// <summary>Tristimulus white-point [X, Y, Z]. Y MUST equal 1.0 per PDF spec.</summary>
    public double[] WhitePoint { get; init; } = [0.9642, 1.0, 0.8251];

    /// <summary>Tristimulus black-point [X, Y, Z]. Defaults to [0, 0, 0].</summary>
    public double[] BlackPoint { get; init; } = [0.0, 0.0, 0.0];

    /// <summary>
    /// Range [aMin, aMax, bMin, bMax] for the a* and b* components.
    /// NOTE: upstream with_range takes FOUR separate f64 args (a_min, a_max, b_min, b_max) —
    /// not an array. This property is expanded to four scalars at the FFI boundary.
    /// Standard CIE range: [-128, 127, -128, 127].
    /// </summary>
    public double[] Range { get; init; } = [-128.0, 127.0, -128.0, 127.0];

    /// <summary>D50 standard illuminant (ICC profile connection space).</summary>
    public static LabColorSpace D50() => new() { WhitePoint = [0.9642, 1.0, 0.8251] };

    /// <summary>D65 standard illuminant (sRGB / most monitors).</summary>
    public static LabColorSpace D65() => new() { WhitePoint = [0.9505, 1.0, 1.0890] };

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
        if (Range.Length != 4)
            throw new ArgumentException("Range must have exactly 4 elements [aMin, aMax, bMin, bMax].", nameof(Range));
        if (Range[0] >= Range[1])
            throw new ArgumentException("Range[0] (aMin) must be less than Range[1] (aMax).", nameof(Range));
        if (Range[2] >= Range[3])
            throw new ArgumentException("Range[2] (bMin) must be less than Range[3] (bMax).", nameof(Range));
    }
}
