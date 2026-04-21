using System.Text.Json.Serialization;

namespace OxidizePdf.NET;

/// <summary>
/// Describes where to land inside the document: target page + fit mode.
/// Page indices are 0-based.
/// </summary>
public sealed class PdfDestination
{
    /// <summary>0-based index of the target page.</summary>
    [JsonPropertyName("page")]
    public int PageIndex { get; init; }

    /// <summary>How the viewport should be positioned when opening the destination.</summary>
    [JsonPropertyName("fit")]
    public PdfDestinationFit FitMode { get; init; }

    /// <summary>Left coordinate in PDF user-space units (used by Xyz and FitV).</summary>
    [JsonPropertyName("left")]
    public double? Left { get; init; }

    /// <summary>Top coordinate in PDF user-space units (used by Xyz and FitH).</summary>
    [JsonPropertyName("top")]
    public double? Top { get; init; }

    /// <summary>Zoom factor (used by Xyz; null means inherit current zoom).</summary>
    [JsonPropertyName("zoom")]
    public double? Zoom { get; init; }

    /// <summary>Fit whole page in window.</summary>
    public static PdfDestination Fit(int pageIndex = 0) =>
        new() { PageIndex = pageIndex, FitMode = PdfDestinationFit.Fit };

    /// <summary>Position at specific coordinates with optional zoom.</summary>
    public static PdfDestination Xyz(int pageIndex, double? left = null, double? top = null, double? zoom = null) =>
        new() { PageIndex = pageIndex, FitMode = PdfDestinationFit.Xyz, Left = left, Top = top, Zoom = zoom };

    /// <summary>Fit page width at optional top coordinate.</summary>
    public static PdfDestination FitH(int pageIndex, double? top = null) =>
        new() { PageIndex = pageIndex, FitMode = PdfDestinationFit.FitH, Top = top };

    /// <summary>Fit page height at optional left coordinate.</summary>
    public static PdfDestination FitV(int pageIndex, double? left = null) =>
        new() { PageIndex = pageIndex, FitMode = PdfDestinationFit.FitV, Left = left };
}
