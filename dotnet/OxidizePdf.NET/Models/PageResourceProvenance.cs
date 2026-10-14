using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Models;

/// <summary>Where the effective resource dictionary was declared, independent of whether it is indirect.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PageResourceOrigin>))]
public enum PageResourceOrigin
{
    /// <summary>No Resources entry was found in the page or inspected ancestors.</summary>
    None,
    /// <summary>The page itself declares Resources, including an empty dictionary.</summary>
    Direct,
    /// <summary>The nearest ancestor declaring Resources supplies the entire dictionary.</summary>
    Inherited
}

/// <summary>Ownership and resolved names for an existing page's effective resource dictionary.</summary>
public sealed class PageResourceProvenance
{
    /// <summary>One-based page number.</summary>
    [JsonPropertyName("page_number")] public int PageNumber { get; init; }
    /// <summary>The page object's identity in the input PDF.</summary>
    [JsonPropertyName("page_reference")] public PdfIndirectObjectId PageReference { get; init; }
    /// <summary>Dictionary ownership; an indirect dictionary on the page is still Direct.</summary>
    [JsonPropertyName("origin")] public PageResourceOrigin Origin { get; init; }
    /// <summary>Page or ancestor object declaring Resources; null when absent.</summary>
    [JsonPropertyName("source_reference")] public PdfIndirectObjectId? SourceReference { get; init; }
    /// <summary>Reference used by the Resources entry; null for an inline dictionary or absence.</summary>
    [JsonPropertyName("resource_dictionary_reference")] public PdfIndirectObjectId? ResourceDictionaryReference { get; init; }
    /// <summary>Number of Parent links to the declaring node: zero for Direct, null when absent.</summary>
    [JsonPropertyName("ancestor_depth")] public int? AncestorDepth { get; init; }
    /// <summary>Sorted keys/font names and XObject presence, resolving indirect category dictionaries.</summary>
    [JsonPropertyName("resolved_resources")] public PageResources ResolvedResources { get; init; } = new();
    /// <summary>Named entries sorted by category then name. Resource contents are not loaded or validated.</summary>
    [JsonPropertyName("entries")] public PageResourceEntry[] Entries { get; init; } = [];
}

/// <summary>A name in a standard resource category; shares its containing resource dictionary's provenance.</summary>
public sealed class PageResourceEntry
{
    /// <summary>Font, XObject, ExtGState, ColorSpace, Pattern, Shading or Properties, without a leading slash.</summary>
    [JsonPropertyName("category")] public string Category { get; init; } = string.Empty;
    /// <summary>Resource name without a leading slash.</summary>
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    /// <summary>Indirect identity as declared; null for a direct value. The referenced object is not loaded.</summary>
    [JsonPropertyName("reference")] public PdfIndirectObjectId? Reference { get; init; }
}

/// <summary>Limits for provenance inspection after core page parsing. Exceeding a limit fails without partial results.</summary>
public sealed class PageResourceReadOptions
{
    /// <summary>Maximum Parent links followed to find Resources, 0..256; default 256. Zero allows direct resources only.</summary>
    public int MaxAncestorDepth { get; set; } = 256;
    /// <summary>Maximum combined top-level keys and standard named resource entries; default 100,000. Zero permits empty/absent resources.</summary>
    public int MaxEntries { get; set; } = 100_000;
    /// <summary>Maximum aggregate UTF-8 bytes of reported names, counting repeated category/font names; default 1 MiB. Excludes JSON syntax/escaping.</summary>
    public int MaxNameBytes { get; set; } = 1024 * 1024;

    internal void Validate()
    {
        if (MaxAncestorDepth < 0 || MaxAncestorDepth > 256) throw new ArgumentOutOfRangeException(nameof(MaxAncestorDepth));
        if (MaxEntries < 0) throw new ArgumentOutOfRangeException(nameof(MaxEntries));
        if (MaxNameBytes < 0) throw new ArgumentOutOfRangeException(nameof(MaxNameBytes));
    }
}
