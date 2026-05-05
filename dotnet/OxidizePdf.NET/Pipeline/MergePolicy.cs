namespace OxidizePdf.NET.Pipeline;

/// <summary>
/// Policy for merging adjacent elements in hybrid chunking.
/// Mirrors <c>oxidize_pdf::pipeline::MergePolicy</c>.
/// </summary>
public enum MergePolicy : byte
{
    /// <summary>Only merge Paragraph+Paragraph and ListItem+ListItem.</summary>
    SameTypeOnly = 0,
    /// <summary>Merge any adjacent non-structural elements. Default.</summary>
    AnyInlineContent = 1,
}
