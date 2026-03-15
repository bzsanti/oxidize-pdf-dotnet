namespace OxidizePdf.NET;

/// <summary>
/// Represents a text flow context for laying out wrapped, aligned text within a page.
/// Provides a fluent API for setting font, alignment, and writing text.
/// Implements <see cref="IDisposable"/> to ensure native resources are freed.
/// </summary>
/// <remarks>
/// Create via <see cref="PdfPage.CreateTextFlow"/> and add to the page via
/// <see cref="PdfPage.AddTextFlow"/> when done.
/// </remarks>
/// <example>
/// <code>
/// using var page = PdfPage.A4();
/// page.SetMargins(50, 50, 50, 50);
/// using var flow = page.CreateTextFlow();
/// flow.SetFont(StandardFont.Helvetica, 12)
///     .SetAlignment(TextAlign.Justified)
///     .WriteWrapped("Lorem ipsum dolor sit amet...");
/// page.AddTextFlow(flow);
/// </code>
/// </example>
public sealed class PdfTextFlow : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    /// <summary>
    /// Exposes the native text flow handle for use by <see cref="PdfPage.AddTextFlow"/>.
    /// </summary>
    internal IntPtr Handle
    {
        get
        {
            ThrowIfDisposed();
            return _handle;
        }
    }

    /// <summary>
    /// Creates a new text flow context from a page.
    /// The text flow inherits the page's dimensions and margins.
    /// </summary>
    /// <param name="page">The page whose dimensions and margins to use.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="page"/> is null.</exception>
    /// <exception cref="PdfExtractionException">If native creation fails.</exception>
    internal PdfTextFlow(PdfPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        _handle = NativeMethods.oxidize_text_flow_create(page.Handle);
        if (_handle == IntPtr.Zero)
            throw new PdfExtractionException("Failed to create text flow context");
    }

    /// <summary>
    /// Sets the font and size for subsequent text in this flow.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="font">One of the 14 standard PDF fonts.</param>
    /// <param name="size">Font size in PDF points.</param>
    /// <exception cref="ObjectDisposedException">If this text flow has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfTextFlow SetFont(StandardFont font, double size)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_text_flow_set_font(
                _handle, (NativeMethods.StandardFont)(int)font, size),
            "Failed to set text flow font");
        return this;
    }

    /// <summary>
    /// Sets the text alignment for this flow.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="alignment">The desired text alignment.</param>
    /// <exception cref="ObjectDisposedException">If this text flow has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfTextFlow SetAlignment(TextAlign alignment)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_text_flow_set_alignment(
                _handle, (NativeMethods.TextAlign)(int)alignment),
            "Failed to set text flow alignment");
        return this;
    }

    /// <summary>
    /// Writes text that will be automatically wrapped within the page margins.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="text">The text to write.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="text"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">If this text flow has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfTextFlow WriteWrapped(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_text_flow_write_wrapped(_handle, text),
            "Failed to write wrapped text");
        return this;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.oxidize_text_flow_free(_handle);
            _handle = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>Finalizer that ensures native resources are freed if Dispose was not called.</summary>
    ~PdfTextFlow() => Dispose();

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PdfTextFlow));
    }

    private static void ThrowIfError(int errorCode, string message)
    {
        if (errorCode == (int)NativeMethods.ErrorCode.Success)
            return;

        var rustError = NativeMethods.GetLastError();
        var detail = !string.IsNullOrEmpty(rustError) ? rustError : ((NativeMethods.ErrorCode)errorCode).ToString();
        throw new PdfExtractionException($"{message}: {detail}");
    }
}
