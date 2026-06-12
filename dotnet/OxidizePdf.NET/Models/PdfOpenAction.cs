using System.Text.Json.Serialization;

namespace OxidizePdf.NET;

/// <summary>
/// Action triggered when the document is opened.
/// </summary>
public sealed class PdfOpenAction
{
    /// <summary>Discriminator for the action type: "goto" or "uri".</summary>
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = "";

    /// <summary>Target destination for GoTo actions; null for URI actions.</summary>
    [JsonPropertyName("destination")]
    public PdfDestination? Destination { get; init; }

    /// <summary>URI string for URI actions; null for GoTo actions.</summary>
    [JsonPropertyName("uri")]
    public string? UriTarget { get; init; }

    /// <summary>Navigate to a destination inside this document.</summary>
    public static PdfOpenAction GoTo(int pageIndex, PdfDestination? destination = null) =>
        new() { Kind = "goto", Destination = destination ?? PdfDestination.Fit(pageIndex) };

    /// <summary>Open a URI (external URL) on document open.</summary>
    public static PdfOpenAction Uri(string uri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);
        return new PdfOpenAction { Kind = "uri", UriTarget = uri };
    }
}
