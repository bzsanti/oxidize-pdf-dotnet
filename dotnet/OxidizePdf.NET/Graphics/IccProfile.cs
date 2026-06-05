namespace OxidizePdf.NET.Graphics;

/// <summary>
/// An ICC color profile for embedding in a PDF.
/// Mirrors <c>oxidize_pdf::graphics::IccProfile</c>.
/// Pass to <c>PdfPage.AddIccColorSpace</c> to register
/// an embedded-profile color space on a page.
/// </summary>
public sealed class IccProfile
{
    /// <summary>Resource name for the color space (PDF resource dict key).</summary>
    public string Name { get; }

    /// <summary>Raw ICC profile binary data.</summary>
    public byte[] Data { get; }

    /// <summary>Color space type declared by the profile.</summary>
    public IccColorSpace ColorSpace { get; }

    /// <exception cref="ArgumentNullException">If <paramref name="name"/> or <paramref name="data"/> is null.</exception>
    public IccProfile(string name, byte[] data, IccColorSpace colorSpace)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(data);
        Name = name;
        Data = data;
        ColorSpace = colorSpace;
    }

    /// <summary>Validates the profile. Throws <see cref="ArgumentException"/> if data is empty or the color space enum value is unknown.</summary>
    public void Validate()
    {
        if (Data.Length == 0)
            throw new ArgumentException("ICC profile data must not be empty.", "data");
        if (!Enum.IsDefined(typeof(IccColorSpace), ColorSpace))
            throw new ArgumentException($"Unknown ICC color space: {ColorSpace}.", "ColorSpace");
    }
}
