using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Models;

/// <summary>
/// An option value for a choice form field (dropdown or listbox).
/// </summary>
public class FormFieldOption
{
    /// <summary>The export value sent when the option is selected.</summary>
    [JsonPropertyName("export_value")]
    public string ExportValue { get; set; } = string.Empty;

    /// <summary>The display text shown to the user.</summary>
    [JsonPropertyName("display_text")]
    public string DisplayText { get; set; } = string.Empty;
}
