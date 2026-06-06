namespace OxidizePdf.NET.Graphics;

/// <summary>
/// A transparency group attribute (GFX-020, ISO 32000-1 §11.4.5) attached to a
/// <see cref="PdfFormXObject"/>. When present, the form's stream dictionary gains a
/// <c>/Group &lt;&lt; /Type /Group /S /Transparency /CS … /I … /K … &gt;&gt;</c> entry, so the
/// form is composited as a single transparency unit rather than object by object.
/// </summary>
public sealed class PdfTransparencyGroup
{
    /// <summary>Group colour space (e.g. "DeviceRGB", "DeviceGray", "DeviceCMYK").</summary>
    public string ColorSpace { get; }

    /// <summary>
    /// Whether the group is isolated: composited against a fully transparent backdrop
    /// instead of inheriting the parent's backdrop. Emitted as <c>/I true</c> only when true.
    /// </summary>
    public bool Isolated { get; }

    /// <summary>
    /// Whether the group is a knockout group: later objects replace earlier ones
    /// rather than compositing with them. Emitted as <c>/K true</c> only when true.
    /// </summary>
    public bool Knockout { get; }

    /// <summary>Creates a transparency group attribute.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="colorSpace"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="colorSpace"/> is empty.</exception>
    public PdfTransparencyGroup(string colorSpace, bool isolated, bool knockout)
    {
        ArgumentNullException.ThrowIfNull(colorSpace);
        if (colorSpace.Length == 0)
            throw new ArgumentException("Transparency group colour space must not be empty.", nameof(colorSpace));

        ColorSpace = colorSpace;
        Isolated = isolated;
        Knockout = knockout;
    }
}
