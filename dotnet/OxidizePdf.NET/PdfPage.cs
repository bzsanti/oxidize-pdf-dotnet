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

    // ── Rotation ──────────────────────────────────────────────────────────────

    /// <summary>Gets the page rotation in degrees (0, 90, 180, or 270).</summary>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public int Rotation
    {
        get
        {
            ThrowIfDisposed();
            ThrowIfError(
                NativeMethods.oxidize_page_get_rotation(_handle, out var degrees),
                "Failed to get page rotation");
            return degrees;
        }
    }

    /// <summary>
    /// Sets the page rotation in degrees (0, 90, 180, or 270).
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="degrees">Rotation angle in degrees.</param>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage SetRotation(int degrees)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_set_rotation(_handle, degrees),
            "Failed to set page rotation");
        return this;
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

    /// <summary>
    /// Sets a custom (embedded) font by name and size. The font must have been
    /// previously registered via <see cref="PdfDocument.AddFont"/>.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="fontName">The name used when registering the font.</param>
    /// <param name="size">Font size in PDF points.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="fontName"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage SetCustomFont(string fontName, double size)
    {
        ArgumentNullException.ThrowIfNull(fontName);
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_set_custom_font(_handle, fontName, size),
            "Failed to set custom font");
        return this;
    }

    // ── Text operations (advanced) ─────────────────────────────────────────────

    /// <summary>
    /// Sets horizontal scaling for text. A value of 100 is normal width; 50 compresses to half width.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="scale">Scaling percentage (100 = normal).</param>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage SetHorizontalScaling(double scale)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_set_horizontal_scaling(_handle, scale),
            "Failed to set horizontal scaling");
        return this;
    }

    /// <summary>
    /// Sets text rise (vertical offset from the baseline) in PDF points.
    /// Positive values move text up; negative values move it down.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="rise">Vertical offset in PDF points.</param>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage SetTextRise(double rise)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_set_text_rise(_handle, rise),
            "Failed to set text rise");
        return this;
    }

    /// <summary>
    /// Sets the text rendering mode (fill, stroke, invisible, clip, or combinations).
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="mode">The rendering mode to apply.</param>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage SetTextRenderingMode(TextRenderingMode mode)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_set_rendering_mode(_handle, (NativeMethods.TextRenderingMode)(int)mode),
            "Failed to set text rendering mode");
        return this;
    }

    // ── Text flow operations ─────────────────────────────────────────────────

    /// <summary>
    /// Creates a new text flow context initialized with this page's dimensions and margins.
    /// Use the returned <see cref="PdfTextFlow"/> to set font, alignment, and write wrapped text,
    /// then call <see cref="AddTextFlow"/> to render it on the page.
    /// </summary>
    /// <returns>A new <see cref="PdfTextFlow"/> instance that must be disposed after use.</returns>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If native creation fails.</exception>
    public PdfTextFlow CreateTextFlow()
    {
        ThrowIfDisposed();
        return new PdfTextFlow(this);
    }

    /// <summary>
    /// Adds a text flow's rendered operations to this page.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="textFlow">The text flow to add.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="textFlow"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage AddTextFlow(PdfTextFlow textFlow)
    {
        ArgumentNullException.ThrowIfNull(textFlow);
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_add_text_flow(_handle, textFlow.Handle),
            "Failed to add text flow to page");
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

    // ── Line style (advanced) ────────────────────────────────────────────────

    /// <summary>
    /// Sets the line cap style for stroke endpoints. Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="cap">The cap style to apply.</param>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage SetLineCap(LineCap cap)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_set_line_cap(_handle, (NativeMethods.LineCap)(int)cap),
            "Failed to set line cap");
        return this;
    }

    /// <summary>
    /// Sets the line join style where path segments meet. Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="join">The join style to apply.</param>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage SetLineJoin(LineJoin join)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_set_line_join(_handle, (NativeMethods.LineJoin)(int)join),
            "Failed to set line join");
        return this;
    }

    /// <summary>
    /// Sets the miter limit for line joins. Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="limit">The miter limit value (minimum 1.0).</param>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage SetMiterLimit(double limit)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_set_miter_limit(_handle, limit),
            "Failed to set miter limit");
        return this;
    }

    /// <summary>
    /// Sets a dash pattern for stroke operations. Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="dashLength">Length of each dash in PDF points.</param>
    /// <param name="gapLength">Length of each gap in PDF points.</param>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage SetDashPattern(double dashLength, double gapLength)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_set_dash_pattern(_handle, dashLength, gapLength),
            "Failed to set dash pattern");
        return this;
    }

    /// <summary>
    /// Resets the stroke to a solid line (removes any dash pattern).
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage SetLineSolid()
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_set_line_solid(_handle),
            "Failed to set line solid");
        return this;
    }

    // ── Graphics state ───────────────────────────────────────────────────────

    /// <summary>
    /// Saves the current graphics state onto an internal stack.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage SaveGraphicsState()
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_save_state(_handle),
            "Failed to save graphics state");
        return this;
    }

    /// <summary>
    /// Restores the most recently saved graphics state.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage RestoreGraphicsState()
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_restore_state(_handle),
            "Failed to restore graphics state");
        return this;
    }

    // ── Clipping ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets a rectangular clipping region. All subsequent drawing is confined to this rectangle.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="x">Left edge in PDF points.</param>
    /// <param name="y">Bottom edge in PDF points.</param>
    /// <param name="width">Width in PDF points.</param>
    /// <param name="height">Height in PDF points.</param>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage ClipRect(double x, double y, double width, double height)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_clip_rect(_handle, x, y, width, height),
            "Failed to set clipping rect");
        return this;
    }

    /// <summary>
    /// Sets a circular clipping region. All subsequent drawing is confined to this circle.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="cx">Centre X in PDF points.</param>
    /// <param name="cy">Centre Y in PDF points.</param>
    /// <param name="radius">Radius in PDF points.</param>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage ClipCircle(double cx, double cy, double radius)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_clip_circle(_handle, cx, cy, radius),
            "Failed to set clipping circle");
        return this;
    }

    /// <summary>
    /// Clears all clipping regions. Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage ClearClipping()
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_clear_clipping(_handle),
            "Failed to clear clipping");
        return this;
    }

    // ── Blend mode ───────────────────────────────────────────────────────────

    /// <summary>
    /// Sets the blend mode for compositing overlapping elements.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="mode">The blend mode to apply.</param>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage SetBlendMode(BlendMode mode)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_set_blend_mode(_handle, (NativeMethods.BlendMode)(int)mode),
            "Failed to set blend mode");
        return this;
    }

    // ── Table operations ──────────────────────────────────────────────────

    /// <summary>
    /// Builds and renders a table on this page. The table is consumed by this call.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="table">The table to render. It cannot be used after this call.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="table"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage AddTable(PdfTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        ThrowIfDisposed();
        var builderHandle = table.ConsumeHandle();
        ThrowIfError(
            NativeMethods.oxidize_page_add_table(_handle, builderHandle),
            "Failed to add table to page");
        return this;
    }

    // ── Header/Footer operations ─────────────────────────────────────────

    /// <summary>
    /// Sets a header on this page with the specified content, font, and size.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="content">The header text content.</param>
    /// <param name="font">The font to use.</param>
    /// <param name="size">Font size in PDF points.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="content"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage SetHeader(string content, StandardFont font, double size)
    {
        ArgumentNullException.ThrowIfNull(content);
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_set_header(_handle, content, (NativeMethods.StandardFont)(int)font, size),
            "Failed to set header");
        return this;
    }

    /// <summary>
    /// Sets a footer on this page with the specified content, font, and size.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="content">The footer text content.</param>
    /// <param name="font">The font to use.</param>
    /// <param name="size">Font size in PDF points.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="content"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage SetFooter(string content, StandardFont font, double size)
    {
        ArgumentNullException.ThrowIfNull(content);
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_set_footer(_handle, content, (NativeMethods.StandardFont)(int)font, size),
            "Failed to set footer");
        return this;
    }

    // ── List operations ──────────────────────────────────────────────────

    /// <summary>
    /// Adds an ordered (numbered) list to this page.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="items">The list item texts.</param>
    /// <param name="x">Left edge in PDF points.</param>
    /// <param name="y">Top edge in PDF points.</param>
    /// <param name="style">The numbering style.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="items"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage AddOrderedList(string[] items, double x, double y, OrderedListStyle style = OrderedListStyle.Decimal)
    {
        ArgumentNullException.ThrowIfNull(items);
        ThrowIfDisposed();
        var itemsJson = System.Text.Json.JsonSerializer.Serialize(items);
        ThrowIfError(
            NativeMethods.oxidize_page_add_ordered_list(_handle, itemsJson, x, y, (NativeMethods.OrderedListStyle)(int)style),
            "Failed to add ordered list");
        return this;
    }

    /// <summary>
    /// Adds an unordered (bulleted) list to this page.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="items">The list item texts.</param>
    /// <param name="x">Left edge in PDF points.</param>
    /// <param name="y">Top edge in PDF points.</param>
    /// <param name="style">The bullet style.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="items"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage AddUnorderedList(string[] items, double x, double y, BulletStyle style = BulletStyle.Disc)
    {
        ArgumentNullException.ThrowIfNull(items);
        ThrowIfDisposed();
        var itemsJson = System.Text.Json.JsonSerializer.Serialize(items);
        ThrowIfError(
            NativeMethods.oxidize_page_add_unordered_list(_handle, itemsJson, x, y, (NativeMethods.BulletStyle)(int)style),
            "Failed to add unordered list");
        return this;
    }

    // ── Image operations ────────────────────────────────────────────────────

    /// <summary>
    /// Registers an image on this page by name. The image is cloned internally.
    /// Use <see cref="DrawImage"/> to render it at a specific position.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="name">A unique name to identify the image on this page.</param>
    /// <param name="image">The image to register.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="name"/> or <paramref name="image"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage AddImage(string name, PdfImage image)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(image);
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_add_image(_handle, name, image.Handle),
            "Failed to add image to page");
        return this;
    }

    /// <summary>
    /// Draws a previously registered image at the specified position and dimensions.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="name">The name used when registering the image via <see cref="AddImage"/>.</param>
    /// <param name="x">Left edge in PDF points.</param>
    /// <param name="y">Bottom edge in PDF points.</param>
    /// <param name="width">Display width in PDF points.</param>
    /// <param name="height">Display height in PDF points.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="name"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">If this page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails (e.g. image name not found).</exception>
    public PdfPage DrawImage(string name, double x, double y, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(name);
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_page_draw_image(_handle, name, x, y, width, height),
            "Failed to draw image");
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
