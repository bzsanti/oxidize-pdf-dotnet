namespace OxidizePdf.NET.Models;

/// <summary>
/// Result of a text extraction that carries out-of-band information beyond the
/// text itself.
/// </summary>
/// <param name="Text">The extracted plain text (a valid UTF-8 prefix when truncated).</param>
/// <param name="Truncated">
/// True when extraction stopped early because a page hit the per-page byte
/// budget (<see cref="ExtractionOptions.MaxExtractedBytes"/>); the returned
/// <paramref name="Text"/> is then a prefix of the full page text
/// (oxidize-pdf 4.0.0, upstream issue #382).
/// </param>
public sealed record TextExtractionResult(string Text, bool Truncated);
