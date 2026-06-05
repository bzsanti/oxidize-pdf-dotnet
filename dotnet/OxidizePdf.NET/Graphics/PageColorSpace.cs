namespace OxidizePdf.NET.Graphics;

/// <summary>Discriminator for <see cref="PageColorSpace"/> variants.</summary>
public enum PageColorSpaceKind
{
    /// <summary>A device color space (DeviceRGB, DeviceGray, DeviceCMYK).</summary>
    Device,
    /// <summary>An inline ICCBased color space with N components and an alternate space.</summary>
    IccBased,
    /// <summary>Calibrated gray color space.</summary>
    CalGray,
    /// <summary>Calibrated RGB color space.</summary>
    CalRgb,
    /// <summary>CIE L*a*b* color space.</summary>
    Lab,
}

/// <summary>
/// A page-level color space resource entry. Mirrors the Python bridge's
/// <c>PageColorSpace</c> class.
/// Register with <c>PdfPage.AddColorSpace</c> or <c>PdfPage.AddIccColorSpace</c>.
/// </summary>
public sealed class PageColorSpace
{
    /// <summary>The kind of this color space entry.</summary>
    public PageColorSpaceKind Kind { get; }

    /// <summary>Device color space name (e.g. "DeviceRGB"). Non-null when <see cref="Kind"/> is <see cref="PageColorSpaceKind.Device"/>.</summary>
    public string? DeviceName { get; }

    /// <summary>Number of components for an ICCBased entry. Non-zero when <see cref="Kind"/> is <see cref="PageColorSpaceKind.IccBased"/>.</summary>
    public int IccN { get; }

    /// <summary>Alternate device space name for an ICCBased entry. Non-null when <see cref="Kind"/> is <see cref="PageColorSpaceKind.IccBased"/>.</summary>
    public string? IccAlternate { get; }

    /// <summary>Calibrated gray parameters. Non-null when <see cref="Kind"/> is <see cref="PageColorSpaceKind.CalGray"/>.</summary>
    public CalGrayColorSpace? CalGrayCs { get; }

    /// <summary>Calibrated RGB parameters. Non-null when <see cref="Kind"/> is <see cref="PageColorSpaceKind.CalRgb"/>.</summary>
    public CalRgbColorSpace? CalRgbCs { get; }

    /// <summary>Lab color space parameters. Non-null when <see cref="Kind"/> is <see cref="PageColorSpaceKind.Lab"/>.</summary>
    public LabColorSpace? LabCs { get; }

    /// <summary>A device color space alias (e.g. "DeviceRGB", "DeviceGray", "DeviceCMYK").</summary>
    public static PageColorSpace Device(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return new(PageColorSpaceKind.Device, deviceName: name);
    }

    /// <summary>
    /// Inline ICCBased color space with N components and an alternate device space name.
    /// No binary profile is embedded. Mirrors python's <c>PageColorSpace.icc_based(n, alternate)</c>.
    /// </summary>
    public static PageColorSpace IccBased(int n, string alternate)
    {
        ArgumentNullException.ThrowIfNull(alternate);
        if (n <= 0) throw new ArgumentOutOfRangeException(nameof(n), "Component count must be positive.");
        return new(PageColorSpaceKind.IccBased, iccN: n, iccAlternate: alternate);
    }

    /// <summary>Calibrated gray color space for registration via AddColorSpace.</summary>
    public static PageColorSpace CalGray(CalGrayColorSpace cs)
    {
        ArgumentNullException.ThrowIfNull(cs);
        return new(PageColorSpaceKind.CalGray, calGrayCs: cs);
    }

    /// <summary>Calibrated RGB color space for registration via AddColorSpace.</summary>
    public static PageColorSpace CalRgb(CalRgbColorSpace cs)
    {
        ArgumentNullException.ThrowIfNull(cs);
        return new(PageColorSpaceKind.CalRgb, calRgbCs: cs);
    }

    /// <summary>Lab color space for registration via AddColorSpace.</summary>
    public static PageColorSpace Lab(LabColorSpace cs)
    {
        ArgumentNullException.ThrowIfNull(cs);
        return new(PageColorSpaceKind.Lab, labCs: cs);
    }

    private PageColorSpace(
        PageColorSpaceKind kind,
        string? deviceName = null,
        int iccN = 0,
        string? iccAlternate = null,
        CalGrayColorSpace? calGrayCs = null,
        CalRgbColorSpace? calRgbCs = null,
        LabColorSpace? labCs = null)
    {
        Kind = kind;
        DeviceName = deviceName;
        IccN = iccN;
        IccAlternate = iccAlternate;
        CalGrayCs = calGrayCs;
        CalRgbCs = calRgbCs;
        LabCs = labCs;
    }
}
