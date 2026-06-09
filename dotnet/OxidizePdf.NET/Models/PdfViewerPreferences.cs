using System.Text.Json.Serialization;

namespace OxidizePdf.NET;

/// <summary>
/// Viewer-preferences dictionary — hints that tell a PDF reader how to present
/// the document when it opens. All properties are optional; unset properties
/// are omitted from the serialized PDF.
/// </summary>
public sealed class PdfViewerPreferences
{
    /// <summary>Hide the viewer's toolbar.</summary>
    [JsonPropertyName("hide_toolbar")]
    public bool? HideToolbar { get; init; }

    /// <summary>Hide the viewer's menu bar.</summary>
    [JsonPropertyName("hide_menubar")]
    public bool? HideMenubar { get; init; }

    /// <summary>Hide other UI elements (scroll bars, navigation controls).</summary>
    [JsonPropertyName("hide_window_ui")]
    public bool? HideWindowUi { get; init; }

    /// <summary>Resize the viewer window to match the first page.</summary>
    [JsonPropertyName("fit_window")]
    public bool? FitWindow { get; init; }

    /// <summary>Position the viewer window in the center of the screen.</summary>
    [JsonPropertyName("center_window")]
    public bool? CenterWindow { get; init; }

    /// <summary>Display the document title in the window title bar instead of the filename.</summary>
    [JsonPropertyName("display_doc_title")]
    public bool? DisplayDocTitle { get; init; }

    /// <summary>Page layout to use when the document opens.</summary>
    [JsonPropertyName("page_layout")]
    public PdfPageLayout? PageLayout { get; init; }

    /// <summary>Initial page mode — which side panel is visible on open.</summary>
    [JsonPropertyName("page_mode")]
    public PdfPageMode? PageMode { get; init; }

    /// <summary>Print-scaling behaviour hint.</summary>
    [JsonPropertyName("print_scaling")]
    public PdfPrintScaling? PrintScaling { get; init; }

    /// <summary>Duplex printing preference.</summary>
    [JsonPropertyName("duplex")]
    public PdfDuplex? Duplex { get; init; }

    /// <summary>Default number of copies to print.</summary>
    [JsonPropertyName("num_copies")]
    public uint? NumCopies { get; init; }

    /// <summary>Let the viewer pick the paper tray based on the PDF page size.</summary>
    [JsonPropertyName("pick_tray_by_pdf_size")]
    public bool? PickTrayByPdfSize { get; init; }
}
