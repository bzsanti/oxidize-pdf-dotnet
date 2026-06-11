using System.Text.Json;
using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Models;

/// <summary>
/// DOC-019 — Builder for a Tagged-PDF logical structure tree. Add a single root
/// element, then add children referencing their parent by the index returned
/// from a previous <see cref="AddRoot"/> / <see cref="AddChild"/> call. Attach
/// the result to a document with <see cref="PdfDocument.SetStructureTree"/>;
/// the writer then emits <c>/StructTreeRoot</c>, <c>/MarkInfo</c> and the
/// structure-element dictionaries (ISO 32000-1 §14.7-14.8).
/// </summary>
/// <remarks>
/// Structure types use PDF standard names (e.g. "Document", "H1", "P",
/// "Figure", "Table"). Unknown names become custom structure types and should
/// be role-mapped to a standard type via <see cref="MapRole"/>. Link an element
/// to tagged page content by passing the MCID returned from
/// <see cref="PdfPage.BeginMarkedContent"/>.
/// </remarks>
public sealed class PdfStructureTree
{
    internal sealed class McidRef
    {
        [JsonPropertyName("page")] public int Page { get; set; }
        [JsonPropertyName("mcid")] public int Mcid { get; set; }
    }

    internal sealed class Element
    {
        [JsonPropertyName("type")] public string Type { get; set; } = "";
        [JsonPropertyName("parent")] public int? Parent { get; set; }
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("lang")] public string? Lang { get; set; }
        [JsonPropertyName("alt_text")] public string? AltText { get; set; }
        [JsonPropertyName("actual_text")] public string? ActualText { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("mcids")] public List<McidRef> Mcids { get; set; } = new();
    }

    private sealed class TreeDto
    {
        [JsonPropertyName("elements")] public List<Element> Elements { get; set; } = new();
        [JsonPropertyName("role_map")] public Dictionary<string, string> RoleMap { get; set; } = new();
    }

    private readonly TreeDto _dto = new();

    /// <summary>
    /// Adds the single root element (typically "Document"). Must be called once,
    /// before any <see cref="AddChild"/>. Returns the element index (0).
    /// </summary>
    /// <exception cref="InvalidOperationException">If a root has already been added.</exception>
    public int AddRoot(
        string type,
        string? id = null,
        string? lang = null,
        string? altText = null,
        string? actualText = null,
        string? title = null,
        IEnumerable<(int page, int mcid)>? mcids = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        if (_dto.Elements.Count != 0)
            throw new InvalidOperationException("A root element has already been added.");
        return Add(type, null, id, lang, altText, actualText, title, mcids);
    }

    /// <summary>
    /// Adds a child element under <paramref name="parentIndex"/> (an index
    /// previously returned by this builder). Returns the new element index.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">If the parent index is invalid.</exception>
    public int AddChild(
        int parentIndex,
        string type,
        string? id = null,
        string? lang = null,
        string? altText = null,
        string? actualText = null,
        string? title = null,
        IEnumerable<(int page, int mcid)>? mcids = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        if (parentIndex < 0 || parentIndex >= _dto.Elements.Count)
            throw new ArgumentOutOfRangeException(nameof(parentIndex), parentIndex,
                "Parent index must refer to an already-added element.");
        return Add(type, parentIndex, id, lang, altText, actualText, title, mcids);
    }

    /// <summary>
    /// Maps a custom structure type name to a standard PDF structure type, so
    /// non-standard tags remain accessible (emitted in the tree's <c>/RoleMap</c>).
    /// </summary>
    public PdfStructureTree MapRole(string customType, string standardType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customType);
        ArgumentException.ThrowIfNullOrWhiteSpace(standardType);
        _dto.RoleMap[customType] = standardType;
        return this;
    }

    private int Add(
        string type, int? parent, string? id, string? lang,
        string? altText, string? actualText, string? title,
        IEnumerable<(int page, int mcid)>? mcids)
    {
        var element = new Element
        {
            Type = type,
            Parent = parent,
            Id = id,
            Lang = lang,
            AltText = altText,
            ActualText = actualText,
            Title = title,
        };
        if (mcids is not null)
        {
            foreach (var (page, mcid) in mcids)
                element.Mcids.Add(new McidRef { Page = page, Mcid = mcid });
        }
        _dto.Elements.Add(element);
        return _dto.Elements.Count - 1;
    }

    internal bool IsEmpty => _dto.Elements.Count == 0;

    internal string ToJson() => JsonSerializer.Serialize(_dto);
}
