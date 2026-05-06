namespace OxidizePdf.NET.Pipeline;

/// <summary>
/// Pre-configured extraction profiles for different document types.
/// Mirrors <c>oxidize_pdf::pipeline::ExtractionProfile</c> — discriminant order MUST match.
/// </summary>
public enum ExtractionProfile : byte
{
    /// <summary>General-purpose extraction for most documents.</summary>
    Standard = 0,
    /// <summary>Optimised for academic papers: handles two-column layouts, footnotes, and citations.</summary>
    Academic = 1,
    /// <summary>Optimised for fillable and static forms.</summary>
    Form = 2,
    /// <summary>Optimised for government documents: dense tables, mixed layouts, and headers.</summary>
    Government = 3,
    /// <summary>Optimised for densely typeset documents with small fonts and narrow margins.</summary>
    Dense = 4,
    /// <summary>Optimised for slide decks and presentations.</summary>
    Presentation = 5,
    /// <summary>Produces chunked, embedding-ready output for RAG pipelines.</summary>
    Rag = 6,
}
