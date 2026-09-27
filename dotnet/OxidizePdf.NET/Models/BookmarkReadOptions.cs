namespace OxidizePdf.NET.Models;

/// <summary>Resource limits for outline and named-destination reading.</summary>
public sealed class BookmarkReadOptions
{
    /// <summary>Maximum visited outline items. Default: 100,000.</summary>
    public int MaxItems { get; set; } = 100_000;
    /// <summary>Maximum outline and name-tree nesting depth, from 1 to 256.</summary>
    public int MaxDepth { get; set; } = 256;
    /// <summary>Maximum named destinations collected. Default: 100,000.</summary>
    public int MaxNamedDestinations { get; set; } = 100_000;
    /// <summary>Maximum name-tree nodes visited, including empty nodes. Default: 100,000.</summary>
    public int MaxNameTreeNodes { get; set; } = 100_000;

    internal void Validate()
    {
        if (MaxItems < 1) throw new ArgumentOutOfRangeException(nameof(MaxItems));
        if (MaxDepth < 1 || MaxDepth > 256) throw new ArgumentOutOfRangeException(nameof(MaxDepth));
        if (MaxNamedDestinations < 1) throw new ArgumentOutOfRangeException(nameof(MaxNamedDestinations));
        if (MaxNameTreeNodes < 1) throw new ArgumentOutOfRangeException(nameof(MaxNameTreeNodes));
    }
}
