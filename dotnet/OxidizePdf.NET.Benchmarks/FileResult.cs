namespace OxidizePdf.NET.Benchmarks;

/// <summary>Outcome of one extraction attempt.</summary>
public enum ExtractStatus
{
    /// <summary>Succeeded with non-empty text.</summary>
    Ok,
    /// <summary>Succeeded but produced zero characters (silent-failure mode).</summary>
    Empty,
    /// <summary>Threw an exception.</summary>
    Error,
    /// <summary>Exceeded the per-file timeout.</summary>
    Timeout,
}

/// <summary>One (adapter, pdf) measurement.</summary>
public sealed record FileResult(
    string Adapter,
    string File,
    int PageCount,
    long ElapsedMs,
    ExtractStatus Status,
    int TextLength,
    string? ErrorType);
