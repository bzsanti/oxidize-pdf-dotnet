using System.Text.Json;

namespace OxidizePdf.NET;

/// <summary>
/// Represents a single item in a PDF document outline (bookmark).
/// Items may be nested to form a hierarchy.
/// </summary>
public sealed class PdfOutlineItem
{
    /// <summary>
    /// Creates a new outline item.
    /// </summary>
    /// <param name="title">The visible bookmark title.</param>
    /// <param name="pageIndex">Zero-based page index this item navigates to.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="title"/> is null.</exception>
    public PdfOutlineItem(string title, int pageIndex)
    {
        ArgumentNullException.ThrowIfNull(title);
        Title = title;
        PageIndex = pageIndex;
    }

    /// <summary>The visible bookmark title.</summary>
    public string Title { get; }

    /// <summary>Zero-based page index this item navigates to.</summary>
    public int PageIndex { get; }

    /// <summary>Whether the bookmark title is rendered in bold.</summary>
    public bool IsBold { get; init; }

    /// <summary>Whether the bookmark title is rendered in italic.</summary>
    public bool IsItalic { get; init; }

    /// <summary>Whether this item is expanded by default. Defaults to <c>true</c>.</summary>
    public bool IsOpen { get; init; } = true;

    /// <summary>Child items nested under this bookmark.</summary>
    public List<PdfOutlineItem> Children { get; init; } = [];

    internal object ToJson() => new
    {
        title = Title,
        page = PageIndex,
        bold = IsBold,
        italic = IsItalic,
        open = IsOpen,
        children = Children.Select(c => c.ToJson()).ToArray(),
    };
}

/// <summary>
/// Represents the complete outline (bookmarks/table of contents) for a PDF document.
/// </summary>
public sealed class PdfOutline
{
    private readonly List<PdfOutlineItem> _items = [];

    /// <summary>
    /// Adds a top-level item to the outline.
    /// </summary>
    /// <param name="item">The outline item to add.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="item"/> is null.</exception>
    public void AddItem(PdfOutlineItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _items.Add(item);
    }

    internal string ToJson() =>
        JsonSerializer.Serialize(new { items = _items.Select(i => i.ToJson()).ToArray() });
}
