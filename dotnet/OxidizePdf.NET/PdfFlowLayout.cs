namespace OxidizePdf.NET;

/// <summary>
/// Automatic flow layout engine with page break support.
/// Manages a vertical cursor and a list of elements. When an element
/// would overflow the current page's bottom margin, a new page is created automatically.
/// Implements <see cref="IDisposable"/> to ensure native resources are freed.
/// </summary>
/// <example>
/// <code>
/// using var layout = PdfFlowLayout.A4();
/// layout.AddText("Hello World", StandardFont.Helvetica, 12)
///       .AddSpacer(20)
///       .AddText("Second paragraph", StandardFont.Helvetica, 12);
///
/// using var doc = new PdfDocument();
/// layout.BuildInto(doc);
/// byte[] bytes = doc.SaveToBytes();
/// </code>
/// </example>
public sealed class PdfFlowLayout : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    private PdfFlowLayout(IntPtr handle)
    {
        _handle = handle;
    }

    /// <summary>
    /// Creates a FlowLayout with A4 page size (595x842 pts) and 72pt margins.
    /// </summary>
    public static PdfFlowLayout A4()
    {
        ThrowIfError(
            NativeMethods.oxidize_flow_layout_create_a4(out var handle),
            "Failed to create A4 flow layout");
        return new PdfFlowLayout(handle);
    }

    /// <summary>
    /// Creates a FlowLayout with custom page dimensions and margins.
    /// </summary>
    public static PdfFlowLayout Create(
        double width, double height,
        double marginLeft, double marginRight,
        double marginTop, double marginBottom)
    {
        ThrowIfError(
            NativeMethods.oxidize_flow_layout_create(
                width, height, marginLeft, marginRight, marginTop, marginBottom, out var handle),
            "Failed to create flow layout");
        return new PdfFlowLayout(handle);
    }

    // ── Properties ───────────────────────────────────────────────────────

    /// <summary>
    /// Available width for content (page width minus left and right margins).
    /// </summary>
    public double ContentWidth
    {
        get
        {
            ThrowIfDisposed();
            ThrowIfError(
                NativeMethods.oxidize_flow_layout_content_width(_handle, out var width),
                "Failed to get content width");
            return width;
        }
    }

    /// <summary>
    /// Available height for content (page height minus top and bottom margins).
    /// </summary>
    public double UsableHeight
    {
        get
        {
            ThrowIfDisposed();
            ThrowIfError(
                NativeMethods.oxidize_flow_layout_usable_height(_handle, out var height),
                "Failed to get usable height");
            return height;
        }
    }

    // ── Builder methods ──────────────────────────────────────────────────

    /// <summary>
    /// Adds a text block with default line height (1.2). Returns <c>this</c> for fluent chaining.
    /// </summary>
    public PdfFlowLayout AddText(string text, StandardFont font, double fontSize)
    {
        ArgumentNullException.ThrowIfNull(text);
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_flow_layout_add_text(_handle, text, (NativeMethods.StandardFont)font, fontSize),
            "Failed to add text to flow layout");
        return this;
    }

    /// <summary>
    /// Adds a text block with custom line height. Returns <c>this</c> for fluent chaining.
    /// </summary>
    public PdfFlowLayout AddTextWithLineHeight(string text, StandardFont font, double fontSize, double lineHeight)
    {
        ArgumentNullException.ThrowIfNull(text);
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_flow_layout_add_text_with_line_height(
                _handle, text, (NativeMethods.StandardFont)font, fontSize, lineHeight),
            "Failed to add text with line height to flow layout");
        return this;
    }

    /// <summary>
    /// Adds vertical spacing in points. Returns <c>this</c> for fluent chaining.
    /// </summary>
    public PdfFlowLayout AddSpacer(double points)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_flow_layout_add_spacer(_handle, points),
            "Failed to add spacer to flow layout");
        return this;
    }

    /// <summary>
    /// Adds a simple table to the layout. Returns <c>this</c> for fluent chaining.
    /// </summary>
    public PdfFlowLayout AddTable(PdfSimpleTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_flow_layout_add_table(_handle, table.ToJson()),
            "Failed to add table to flow layout");
        return this;
    }

    /// <summary>
    /// Adds a single line of mixed-style text. Returns <c>this</c> for fluent chaining.
    /// </summary>
    public PdfFlowLayout AddRichText(PdfRichText richText)
    {
        ArgumentNullException.ThrowIfNull(richText);
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_flow_layout_add_rich_text(_handle, richText.ToJson()),
            "Failed to add rich text to flow layout");
        return this;
    }

    /// <summary>
    /// Adds an image scaled to fit within max dimensions, left-aligned.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    public PdfFlowLayout AddImage(string name, PdfImage image, double maxWidth, double maxHeight)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(image);
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_flow_layout_add_image(_handle, name, image.Handle, maxWidth, maxHeight),
            "Failed to add image to flow layout");
        return this;
    }

    /// <summary>
    /// Adds an image scaled to fit within max dimensions, centered horizontally.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    public PdfFlowLayout AddImageCentered(string name, PdfImage image, double maxWidth, double maxHeight)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(image);
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_flow_layout_add_image_centered(_handle, name, image.Handle, maxWidth, maxHeight),
            "Failed to add centered image to flow layout");
        return this;
    }

    /// <summary>
    /// Builds all elements into the document, creating pages as needed.
    /// The layout is NOT consumed — it can be reused.
    /// </summary>
    public void BuildInto(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_flow_layout_build_into(_handle, document.Handle),
            "Failed to build layout into document");
    }

    // ── IDisposable ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.oxidize_flow_layout_free(_handle);
            _handle = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>Finalizer that ensures native resources are freed if Dispose was not called.</summary>
    ~PdfFlowLayout() => Dispose();

    // ── Helpers ──────────────────────────────────────────────────────────

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PdfFlowLayout));
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
