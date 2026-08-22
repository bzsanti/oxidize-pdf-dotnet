namespace OxidizePdf.NET.Models;

/// <summary>Policy for standalone carriage returns decoded during text extraction.</summary>
public enum CarriageReturnHandling : byte
{
    /// <summary>Remove standalone carriage returns (default).</summary>
    Remove = 0,
    /// <summary>Replace each standalone carriage return with a regular space.</summary>
    ReplaceWithSpace = 1,
    /// <summary>Preserve standalone carriage returns and normalize CRLF to LF.</summary>
    NormalizeLineEnding = 2
}
