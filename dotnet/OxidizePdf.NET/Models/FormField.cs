using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Models;

/// <summary>
/// A form field (AcroForm widget) read from an existing PDF document.
/// </summary>
public class FormField
{
    /// <summary>Field name (from /T entry).</summary>
    [JsonPropertyName("field_name")]
    public string FieldName { get; set; } = string.Empty;

    /// <summary>Field type: "text", "checkbox", "radio", "dropdown", "listbox", "pushbutton", or "unknown".</summary>
    [JsonPropertyName("field_type")]
    public string FieldType { get; set; } = "unknown";

    /// <summary>1-based page number where the field appears.</summary>
    [JsonPropertyName("page_number")]
    public int PageNumber { get; set; }

    /// <summary>Current value of the field (/V entry).</summary>
    [JsonPropertyName("value")]
    public string? Value { get; set; }

    /// <summary>Default value of the field (/DV entry).</summary>
    [JsonPropertyName("default_value")]
    public string? DefaultValue { get; set; }

    /// <summary>Whether the field is read-only (bit 0 of /Ff).</summary>
    [JsonPropertyName("is_read_only")]
    public bool IsReadOnly { get; set; }

    /// <summary>Whether the field is required (bit 1 of /Ff).</summary>
    [JsonPropertyName("is_required")]
    public bool IsRequired { get; set; }

    /// <summary>Whether a text field allows multiline input (bit 12 of /Ff).</summary>
    [JsonPropertyName("is_multiline")]
    public bool IsMultiline { get; set; }

    /// <summary>Maximum character length for text fields (/MaxLen entry).</summary>
    [JsonPropertyName("max_length")]
    public int? MaxLength { get; set; }

    /// <summary>Available options for choice fields (dropdown/listbox).</summary>
    [JsonPropertyName("options")]
    public List<FormFieldOption> Options { get; set; } = new();

    /// <summary>Widget rectangle [x1, y1, x2, y2] in PDF coordinates.</summary>
    [JsonPropertyName("rect")]
    public double[]? Rect { get; set; }
}
