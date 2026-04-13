namespace OxidizePdf.NET;

/// <summary>
/// High-level builder for creating multi-page PDF documents with automatic layout.
/// Uses an owned-chaining API so you can build a complete document in a single expression.
/// Implements <see cref="IDisposable"/> to ensure native resources are freed.
/// </summary>
/// <example>
/// <code>
/// using var builder = PdfDocumentBuilder.A4();
/// builder.AddText("Invoice #001", StandardFont.HelveticaBold, 18)
///        .AddSpacer(10)
///        .AddText("Date: 2026-04-13", StandardFont.Helvetica, 12);
///
/// using var doc = builder.Build();
/// byte[] bytes = doc.SaveToBytes();
/// </code>
/// </example>
public sealed class PdfDocumentBuilder : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;
    private bool _built;

    private PdfDocumentBuilder(IntPtr handle)
    {
        _handle = handle;
    }

    /// <summary>
    /// Creates a DocumentBuilder with A4 page size (595x842 pts) and 72pt margins.
    /// </summary>
    public static PdfDocumentBuilder A4()
    {
        ThrowIfError(
            NativeMethods.oxidize_document_builder_create_a4(out var handle),
            "Failed to create A4 document builder");
        return new PdfDocumentBuilder(handle);
    }

    /// <summary>
    /// Creates a DocumentBuilder with custom page dimensions and margins.
    /// </summary>
    public static PdfDocumentBuilder Create(
        double width, double height,
        double marginLeft, double marginRight,
        double marginTop, double marginBottom)
    {
        ThrowIfError(
            NativeMethods.oxidize_document_builder_create(
                width, height, marginLeft, marginRight, marginTop, marginBottom, out var handle),
            "Failed to create document builder");
        return new PdfDocumentBuilder(handle);
    }

    // ── Builder methods ──────────────────────────────────────────────────

    /// <summary>
    /// Adds a text block with default line height (1.2). Returns <c>this</c> for fluent chaining.
    /// </summary>
    public PdfDocumentBuilder AddText(string text, StandardFont font, double fontSize)
    {
        ArgumentNullException.ThrowIfNull(text);
        ThrowIfDisposedOrBuilt();
        ThrowIfError(
            NativeMethods.oxidize_document_builder_add_text(
                _handle, text, (NativeMethods.StandardFont)font, fontSize),
            "Failed to add text to document builder");
        return this;
    }

    /// <summary>
    /// Adds a text block with custom line height. Returns <c>this</c> for fluent chaining.
    /// </summary>
    public PdfDocumentBuilder AddTextWithLineHeight(string text, StandardFont font, double fontSize, double lineHeight)
    {
        ArgumentNullException.ThrowIfNull(text);
        ThrowIfDisposedOrBuilt();
        ThrowIfError(
            NativeMethods.oxidize_document_builder_add_text_with_line_height(
                _handle, text, (NativeMethods.StandardFont)font, fontSize, lineHeight),
            "Failed to add text with line height to document builder");
        return this;
    }

    /// <summary>
    /// Adds vertical spacing in points. Returns <c>this</c> for fluent chaining.
    /// </summary>
    public PdfDocumentBuilder AddSpacer(double points)
    {
        ThrowIfDisposedOrBuilt();
        ThrowIfError(
            NativeMethods.oxidize_document_builder_add_spacer(_handle, points),
            "Failed to add spacer to document builder");
        return this;
    }

    /// <summary>
    /// Adds a simple table to the builder. Returns <c>this</c> for fluent chaining.
    /// </summary>
    public PdfDocumentBuilder AddTable(PdfSimpleTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        ThrowIfDisposedOrBuilt();
        ThrowIfError(
            NativeMethods.oxidize_document_builder_add_table(_handle, table.ToJson()),
            "Failed to add table to document builder");
        return this;
    }

    /// <summary>
    /// Adds a single line of mixed-style text. Returns <c>this</c> for fluent chaining.
    /// </summary>
    public PdfDocumentBuilder AddRichText(PdfRichText richText)
    {
        ArgumentNullException.ThrowIfNull(richText);
        ThrowIfDisposedOrBuilt();
        ThrowIfError(
            NativeMethods.oxidize_document_builder_add_rich_text(_handle, richText.ToJson()),
            "Failed to add rich text to document builder");
        return this;
    }

    /// <summary>
    /// Adds an image scaled to fit within max dimensions, left-aligned.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    public PdfDocumentBuilder AddImage(string name, PdfImage image, double maxWidth, double maxHeight)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(image);
        ThrowIfDisposedOrBuilt();
        ThrowIfError(
            NativeMethods.oxidize_document_builder_add_image(_handle, name, image.Handle, maxWidth, maxHeight),
            "Failed to add image to document builder");
        return this;
    }

    /// <summary>
    /// Adds an image scaled to fit within max dimensions, centered horizontally.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    public PdfDocumentBuilder AddImageCentered(string name, PdfImage image, double maxWidth, double maxHeight)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(image);
        ThrowIfDisposedOrBuilt();
        ThrowIfError(
            NativeMethods.oxidize_document_builder_add_image_centered(
                _handle, name, image.Handle, maxWidth, maxHeight),
            "Failed to add centered image to document builder");
        return this;
    }

    /// <summary>
    /// Builds the document, creating pages as needed for all added elements.
    /// The builder is consumed by this call — subsequent add/build calls will throw.
    /// </summary>
    /// <returns>A new <see cref="PdfDocument"/> with the laid-out content.</returns>
    public PdfDocument Build()
    {
        ThrowIfDisposedOrBuilt();
        _built = true;

        ThrowIfError(
            NativeMethods.oxidize_document_builder_build(_handle, out var docHandle),
            "Failed to build document");
        return PdfDocument.FromNativeHandle(docHandle);
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
            NativeMethods.oxidize_document_builder_free(_handle);
            _handle = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>Finalizer that ensures native resources are freed if Dispose was not called.</summary>
    ~PdfDocumentBuilder() => Dispose();

    // ── Helpers ──────────────────────────────────────────────────────────

    private void ThrowIfDisposedOrBuilt()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PdfDocumentBuilder));
        if (_built)
            throw new InvalidOperationException("This builder has already been built and cannot be modified.");
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
