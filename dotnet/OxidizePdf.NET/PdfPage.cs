using System.Runtime.InteropServices;

namespace OxidizePdf.NET;

/// <summary>
/// Represents a single PDF page. Provides fluent methods for text and graphics operations.
/// Implements <see cref="IDisposable"/> to ensure native resources are freed.
/// </summary>
/// <remarks>
/// Pages are typically added to a <see cref="PdfDocument"/> via
/// <see cref="PdfDocument.AddPage"/>. The native page is cloned when added, so the
/// <see cref="PdfPage"/> can be disposed after adding it to a document.
/// </remarks>
public sealed class PdfPage : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    /// <summary>
    /// Exposes the native page handle for use by <see cref="PdfDocument.AddPage"/>.
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
    /// Creates a new page with explicit dimensions in PDF points (1 point = 1/72 inch).
    /// </summary>
    /// <param name="width">Page width in PDF points.</param>
    /// <param name="height">Page height in PDF points.</param>
    /// <exception cref="PdfExtractionException">If native page creation fails.</exception>
    public PdfPage(double width, double height)
    {
        _handle = NativeMethods.oxidize_page_create(width, height);
        if (_handle == IntPtr.Zero)
            throw new PdfExtractionException("Failed to create page");
    }

    private PdfPage(IntPtr handle)
    {
        _handle = handle;
        if (_handle == IntPtr.Zero)
            throw new PdfExtractionException("Failed to create page from preset");
    }

    // ── Static factory methods for page presets ───────────────────────────────

    /// <summary>Creates an A4 page (595 x 842 points).</summary>
    public static PdfPage A4() =>
        new(NativeMethods.oxidize_page_create_preset(NativeMethods.PagePreset.A4));

    /// <summary>Creates an A4 Landscape page (842 x 595 points).</summary>
    public static PdfPage A4Landscape() =>
        new(NativeMethods.oxidize_page_create_preset(NativeMethods.PagePreset.A4Landscape));

    /// <summary>Creates a US Letter page (612 x 792 points).</summary>
    public static PdfPage Letter() =>
        new(NativeMethods.oxidize_page_create_preset(NativeMethods.PagePreset.Letter));

    /// <summary>Creates a US Letter Landscape page (792 x 612 points).</summary>
    public static PdfPage LetterLandscape() =>
        new(NativeMethods.oxidize_page_create_preset(NativeMethods.PagePreset.LetterLandscape));

    /// <summary>Creates a US Legal page (612 x 1008 points).</summary>
    public static PdfPage Legal() =>
        new(NativeMethods.oxidize_page_create_preset(NativeMethods.PagePreset.Legal));

    /// <summary>Creates a US Legal Landscape page (1008 x 612 points).</summary>
    public static PdfPage LegalLandscape() =>
        new(NativeMethods.oxidize_page_create_preset(NativeMethods.PagePreset.LegalLandscape));

    // ── Properties ────────────────────────────────────────────────────────────

    /// <summary>Gets the page width in PDF points.</summary>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public double Width
    {
        get
        {
            ThrowIfDisposed();
            ThrowIfError(
                NativeMethods.oxidize_page_get_width(_handle, out var value),
                "Failed to get page width");
            return value;
        }
    }

    /// <summary>Gets the page height in PDF points.</summary>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public double Height
    {
        get
        {
            ThrowIfDisposed();
            ThrowIfError(
                NativeMethods.oxidize_page_get_height(_handle, out var value),
                "Failed to get page height");
            return value;
        }
    }

    // ── Margins ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets page margins in PDF points. Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="top">Top margin in PDF points.</param>
    /// <param name="right">Right margin in PDF points.</param>
    /// <param name="bottom">Bottom margin in PDF points.</param>
    /// <param name="left">Left margin in PDF points.</param>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage SetMargins(double top, double right, double bottom, double left)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_set_margins(_handle, top, right, bottom, left),
            "Failed to set page margins");
        return this;
    }

    // ── Text operations ───────────────────────────────────────────────────────

    /// <summary>
    /// Sets the current font and size for text operations. Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="font">One of the 14 standard PDF fonts.</param>
    /// <param name="size">Font size in PDF points.</param>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage SetFont(StandardFont font, double size)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_set_font(_handle, (NativeMethods.StandardFont)(int)font, size),
            "Failed to set font");
        return this;
    }

    /// <summary>
    /// Sets the text fill color using RGB components (each in range 0.0–1.0).
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage SetTextColor(double r, double g, double b)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_set_text_color_rgb(_handle, r, g, b),
            "Failed to set text color");
        return this;
    }

    /// <summary>
    /// Sets the text fill color using a gray value (0.0 = black, 1.0 = white).
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage SetTextColorGray(double value)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_set_text_color_gray(_handle, value),
            "Failed to set text color (gray)");
        return this;
    }

    /// <summary>
    /// Sets the text fill color using CMYK components (each in range 0.0–1.0).
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage SetTextColorCmyk(double c, double m, double y, double k)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_set_text_color_cmyk(_handle, c, m, y, k),
            "Failed to set text color (CMYK)");
        return this;
    }

    /// <summary>
    /// Sets character spacing for subsequent text operations. Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage SetCharacterSpacing(double spacing)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_set_character_spacing(_handle, spacing),
            "Failed to set character spacing");
        return this;
    }

    /// <summary>
    /// Sets word spacing for subsequent text operations. Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage SetWordSpacing(double spacing)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_set_word_spacing(_handle, spacing),
            "Failed to set word spacing");
        return this;
    }

    /// <summary>
    /// Sets text leading (line height) for subsequent text operations.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage SetLeading(double leading)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_set_leading(_handle, leading),
            "Failed to set leading");
        return this;
    }

    /// <summary>
    /// Writes text at the given position (in PDF points). Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="x">X coordinate in PDF points from the left edge.</param>
    /// <param name="y">Y coordinate in PDF points from the bottom edge.</param>
    /// <param name="text">The text to render.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="text"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage TextAt(double x, double y, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_text_at(_handle, x, y, text),
            "Failed to write text");
        return this;
    }

    // ── Graphics operations ───────────────────────────────────────────────────

    /// <summary>
    /// Sets the fill color using RGB components (each in range 0.0–1.0).
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage SetFillColor(double r, double g, double b)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_set_fill_color_rgb(_handle, r, g, b),
            "Failed to set fill color");
        return this;
    }

    /// <summary>
    /// Sets the fill color using a gray value (0.0 = black, 1.0 = white).
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage SetFillColorGray(double value)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_set_fill_color_gray(_handle, value),
            "Failed to set fill color (gray)");
        return this;
    }

    /// <summary>
    /// Sets the fill color using CMYK components (each in range 0.0–1.0).
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage SetFillColorCmyk(double c, double m, double y, double k)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_set_fill_color_cmyk(_handle, c, m, y, k),
            "Failed to set fill color (CMYK)");
        return this;
    }

    /// <summary>
    /// Sets the stroke color using RGB components (each in range 0.0–1.0).
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage SetStrokeColor(double r, double g, double b)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_set_stroke_color_rgb(_handle, r, g, b),
            "Failed to set stroke color");
        return this;
    }

    /// <summary>
    /// Sets the stroke color using a gray value (0.0 = black, 1.0 = white).
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage SetStrokeColorGray(double value)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_set_stroke_color_gray(_handle, value),
            "Failed to set stroke color (gray)");
        return this;
    }

    /// <summary>
    /// Sets the stroke color using CMYK components (each in range 0.0–1.0).
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage SetStrokeColorCmyk(double c, double m, double y, double k)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_set_stroke_color_cmyk(_handle, c, m, y, k),
            "Failed to set stroke color (CMYK)");
        return this;
    }

    /// <summary>
    /// Sets the stroke line width. Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage SetLineWidth(double width)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_set_line_width(_handle, width),
            "Failed to set line width");
        return this;
    }

    /// <summary>
    /// Sets fill opacity (0.0 = transparent, 1.0 = opaque). Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage SetFillOpacity(double opacity)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_set_fill_opacity(_handle, opacity),
            "Failed to set fill opacity");
        return this;
    }

    /// <summary>
    /// Sets stroke opacity (0.0 = transparent, 1.0 = opaque). Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage SetStrokeOpacity(double opacity)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_set_stroke_opacity(_handle, opacity),
            "Failed to set stroke opacity");
        return this;
    }

    /// <summary>
    /// Adds a rectangle to the current path (does not paint automatically).
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="x">Left edge in PDF points.</param>
    /// <param name="y">Bottom edge in PDF points.</param>
    /// <param name="width">Rectangle width in PDF points.</param>
    /// <param name="height">Rectangle height in PDF points.</param>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage DrawRect(double x, double y, double width, double height)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_rect(_handle, x, y, width, height),
            "Failed to draw rect");
        return this;
    }

    /// <summary>
    /// Adds a circle to the current path (does not paint automatically).
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="cx">Centre X in PDF points.</param>
    /// <param name="cy">Centre Y in PDF points.</param>
    /// <param name="radius">Radius in PDF points.</param>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage DrawCircle(double cx, double cy, double radius)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_circle(_handle, cx, cy, radius),
            "Failed to draw circle");
        return this;
    }

    /// <summary>
    /// Moves the current drawing point to (x, y) without drawing a line.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage MoveTo(double x, double y)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_move_to(_handle, x, y),
            "Failed to move to point");
        return this;
    }

    /// <summary>
    /// Draws a straight line from the current point to (x, y).
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage LineTo(double x, double y)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_line_to(_handle, x, y),
            "Failed to draw line");
        return this;
    }

    /// <summary>
    /// Draws a cubic Bezier curve from the current point using two control points and an endpoint.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="x1">First control point X.</param>
    /// <param name="y1">First control point Y.</param>
    /// <param name="x2">Second control point X.</param>
    /// <param name="y2">Second control point Y.</param>
    /// <param name="x3">Endpoint X.</param>
    /// <param name="y3">Endpoint Y.</param>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage CurveTo(double x1, double y1, double x2, double y2, double x3, double y3)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_curve_to(_handle, x1, y1, x2, y2, x3, y3),
            "Failed to draw curve");
        return this;
    }

    /// <summary>
    /// Closes the current path by drawing a line back to its starting point.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage ClosePath()
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_close_path(_handle),
            "Failed to close path");
        return this;
    }

    /// <summary>
    /// Fills the current path using the current fill color.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage Fill()
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_fill(_handle),
            "Failed to fill path");
        return this;
    }

    /// <summary>
    /// Strokes the current path using the current stroke color.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage Stroke()
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_stroke(_handle),
            "Failed to stroke path");
        return this;
    }

    /// <summary>
    /// Fills and then strokes the current path.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage FillAndStroke()
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_fill_and_stroke(_handle),
            "Failed to fill and stroke path");
        return this;
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.oxidize_page_free(_handle);
            _handle = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>Finalizer that ensures native resources are freed if Dispose was not called.</summary>
    ~PdfPage() => Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PdfPage));
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
