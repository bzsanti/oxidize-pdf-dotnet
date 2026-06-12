using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using OxidizePdf.NET.Models;

namespace OxidizePdf.NET;

/// <summary>
/// Represents a PDF document being built programmatically.
/// Provides a fluent API for adding pages and setting metadata.
/// Implements <see cref="IDisposable"/> to ensure native resources are freed.
/// </summary>
/// <example>
/// <code>
/// using var doc = new PdfDocument();
/// using var page = PdfPage.A4();
/// page.SetFont(StandardFont.Helvetica, 12)
///     .TextAt(50, 750, "Hello, World!");
/// doc.SetTitle("My Document")
///    .AddPage(page);
/// byte[] pdfBytes = doc.SaveToBytes();
/// </code>
/// </example>
public sealed class PdfDocument : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    /// <summary>
    /// Creates a new empty PDF document.
    /// </summary>
    /// <exception cref="PdfExtractionException">If native document creation fails.</exception>
    public PdfDocument()
    {
        _handle = NativeMethods.oxidize_document_create();
        if (_handle == IntPtr.Zero)
            throw new PdfExtractionException("Failed to create document");
    }

    private PdfDocument(IntPtr handle)
    {
        _handle = handle;
    }

    /// <summary>
    /// Wraps an existing native document handle. Used by <see cref="PdfDocumentBuilder"/>.
    /// </summary>
    internal static PdfDocument FromNativeHandle(IntPtr handle) => new(handle);

    /// <summary>
    /// Exposes the native document handle for internal use by layout builders.
    /// </summary>
    internal IntPtr Handle
    {
        get
        {
            ThrowIfDisposed();
            return _handle;
        }
    }

    // ── Metadata ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets the document title. Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="title">The document title.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="title"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">If this document has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfDocument SetTitle(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_document_set_title(_handle, title),
            "Failed to set document title");
        return this;
    }

    /// <summary>
    /// Sets the document author. Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="author">The document author.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="author"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">If this document has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfDocument SetAuthor(string author)
    {
        ArgumentNullException.ThrowIfNull(author);
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_document_set_author(_handle, author),
            "Failed to set document author");
        return this;
    }

    /// <summary>
    /// Sets the document subject. Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="subject">The document subject.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="subject"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">If this document has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfDocument SetSubject(string subject)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_document_set_subject(_handle, subject),
            "Failed to set document subject");
        return this;
    }

    /// <summary>
    /// Sets the document keywords. Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="keywords">The document keywords.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="keywords"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">If this document has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfDocument SetKeywords(string keywords)
    {
        ArgumentNullException.ThrowIfNull(keywords);
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_document_set_keywords(_handle, keywords),
            "Failed to set document keywords");
        return this;
    }

    /// <summary>
    /// Sets the document creator application name. Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="creator">The creator application name.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="creator"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">If this document has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfDocument SetCreator(string creator)
    {
        ArgumentNullException.ThrowIfNull(creator);
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_document_set_creator(_handle, creator),
            "Failed to set document creator");
        return this;
    }

    /// <summary>
    /// Sets the document producer application name. Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="producer">The producer application name.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="producer"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">If this document has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfDocument SetProducer(string producer)
    {
        ArgumentNullException.ThrowIfNull(producer);
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_document_set_producer(_handle, producer),
            "Failed to set document producer");
        return this;
    }

    /// <summary>
    /// Sets the document creation date. Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="date">The creation date.</param>
    /// <exception cref="ObjectDisposedException">If this document has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfDocument SetCreationDate(DateTimeOffset date)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_document_set_creation_date(_handle, date.ToUnixTimeSeconds()),
            "Failed to set creation date");
        return this;
    }

    /// <summary>
    /// Sets the document modification date. Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="date">The modification date.</param>
    /// <exception cref="ObjectDisposedException">If this document has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfDocument SetModificationDate(DateTimeOffset date)
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_document_set_modification_date(_handle, date.ToUnixTimeSeconds()),
            "Failed to set modification date");
        return this;
    }

    // ── Fonts ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers a custom font from byte data (e.g., TTF/OTF) for use in pages.
    /// After registration, use <see cref="PdfPage.SetCustomFont"/> with the same name.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="name">A unique name to identify the font.</param>
    /// <param name="fontData">The raw font file bytes (TTF/OTF).</param>
    /// <exception cref="ArgumentNullException">If <paramref name="name"/> or <paramref name="fontData"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">If this document has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfDocument AddFont(string name, byte[] fontData)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(fontData);
        ThrowIfDisposed();

        unsafe
        {
            fixed (byte* ptr = fontData)
            {
                ThrowIfError(
                    NativeMethods.oxidize_document_add_font_from_bytes(
                        _handle, name, (IntPtr)ptr, (nuint)fontData.Length),
                    "Failed to add font");
            }
        }

        return this;
    }

    /// <summary>
    /// Registers a custom font from a file path (TTF/OTF) for use in pages.
    /// After registration, use <see cref="PdfPage.SetCustomFont"/> with the same name.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="name">A unique name to identify the font.</param>
    /// <param name="path">Absolute or relative path to the TTF/OTF font file.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="name"/> or <paramref name="path"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">If this document has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the file cannot be read or is not a valid font.</exception>
    public PdfDocument AddFontFromFile(string name, string path)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(path);
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_document_add_font_from_file(_handle, name, path),
            "Failed to add font from file");
        return this;
    }

    // ── Pages ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new A4 page bound to this document's font metrics store.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this factory <strong>instead of</strong> <see cref="PdfPage.A4"/> when the
    /// page will draw text in a custom font registered via
    /// <see cref="AddFont(string, byte[])"/> or <see cref="AddFontFromFile"/>. Pages
    /// produced by this factory share the document's per-instance metrics store, so
    /// measurement-driven flows (text wrapping in <see cref="PdfTextFlow"/>, table
    /// layout, header/footer width) resolve custom-font widths against the real font
    /// metrics rather than the default 500/em fallback that pages constructed
    /// standalone receive in oxidize-pdf 2.8.0+.
    /// </para>
    /// <para>
    /// Custom fonts can be added to the document either before or after this call —
    /// the underlying store is shared via <c>Arc</c>, so subsequent registrations
    /// are visible to the page automatically.
    /// </para>
    /// </remarks>
    /// <returns>A new <see cref="PdfPage"/> bound to this document's metrics store.</returns>
    /// <exception cref="ObjectDisposedException">If this document has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage NewPageA4()
    {
        ThrowIfDisposed();
        var ptr = NativeMethods.oxidize_document_new_page_a4(_handle);
        if (ptr == IntPtr.Zero)
        {
            var rustError = NativeMethods.GetLastError();
            throw new PdfExtractionException(
                $"Failed to create document-bound A4 page: {rustError ?? "(no detail)"}");
        }
        return new PdfPage(ptr);
    }

    /// <summary>
    /// Creates a new US Letter page bound to this document's font metrics store.
    /// See <see cref="NewPageA4"/> for the rationale.
    /// </summary>
    /// <returns>A new <see cref="PdfPage"/> bound to this document's metrics store.</returns>
    /// <exception cref="ObjectDisposedException">If this document has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage NewPageLetter()
    {
        ThrowIfDisposed();
        var ptr = NativeMethods.oxidize_document_new_page_letter(_handle);
        if (ptr == IntPtr.Zero)
        {
            var rustError = NativeMethods.GetLastError();
            throw new PdfExtractionException(
                $"Failed to create document-bound Letter page: {rustError ?? "(no detail)"}");
        }
        return new PdfPage(ptr);
    }

    /// <summary>
    /// Creates a new page with explicit dimensions bound to this document's font
    /// metrics store. See <see cref="NewPageA4"/> for the rationale.
    /// </summary>
    /// <param name="width">Page width in PDF points (must be finite and positive).</param>
    /// <param name="height">Page height in PDF points (must be finite and positive).</param>
    /// <returns>A new <see cref="PdfPage"/> bound to this document's metrics store.</returns>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="width"/> or <paramref name="height"/> is non-finite or non-positive.</exception>
    /// <exception cref="ObjectDisposedException">If this document has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfPage NewPage(double width, double height)
    {
        if (!double.IsFinite(width) || width <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(width),
                "Width must be finite and positive.");
        if (!double.IsFinite(height) || height <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(height),
                "Height must be finite and positive.");
        ThrowIfDisposed();
        var ptr = NativeMethods.oxidize_document_new_page(_handle, width, height);
        if (ptr == IntPtr.Zero)
        {
            var rustError = NativeMethods.GetLastError();
            throw new PdfExtractionException(
                $"Failed to create document-bound page ({width}×{height}): {rustError ?? "(no detail)"}");
        }
        return new PdfPage(ptr);
    }

    /// <summary>
    /// Adds a page to the document. The page is cloned internally by the native layer,
    /// so the <see cref="PdfPage"/> can be disposed after this call.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <remarks>
    /// For pages that draw custom-font text in measurement-heavy flows, prefer the
    /// document-bound factories (<see cref="NewPageA4"/>, <see cref="NewPageLetter"/>,
    /// <see cref="NewPage"/>) over <see cref="PdfPage.A4"/> / <see cref="PdfPage.Letter"/>
    /// / <see cref="PdfPage(double, double)"/>. Pages from those factories carry the
    /// document's font metrics store from creation, so drawing operations executed
    /// before this call already see the real font widths.
    /// </remarks>
    /// <param name="page">The page to add.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="page"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">If this document or the page has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfDocument AddPage(PdfPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_document_add_page(_handle, page.Handle),
            "Failed to add page to document");
        return this;
    }

    // ── DOC-019: Tagged PDF structure tree ─────────────────────────────────────

    /// <summary>
    /// DOC-019: Attaches a Tagged-PDF logical structure tree to the document.
    /// On the next save the writer emits <c>/StructTreeRoot</c>,
    /// <c>/MarkInfo &lt;&lt;/Marked true&gt;&gt;</c> and the structure-element
    /// dictionaries, producing a Tagged PDF (the basis for PDF/UA). Link
    /// structure elements to page content via the MCIDs returned from
    /// <see cref="PdfPage.BeginMarkedContent"/>. Returns <c>this</c>.
    /// </summary>
    /// <param name="tree">The structure tree built with <see cref="PdfStructureTree"/>.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="tree"/> is null.</exception>
    /// <exception cref="ArgumentException">If the tree has no elements.</exception>
    public PdfDocument SetStructureTree(PdfStructureTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        if (tree.IsEmpty)
            throw new ArgumentException("Structure tree must have at least a root element.", nameof(tree));
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_document_set_struct_tree_json(_handle, tree.ToJson()),
            "Failed to set structure tree");
        return this;
    }

    // ── DOC-021: Semantic entities (AI-ready markup) ───────────────────────────
    //
    // NOTE: semantic entities are an in-memory annotation + export feature. They
    // are NOT embedded in the saved PDF (use SetStructureTree for in-PDF tagged
    // structure). Their value is producing AI-ready JSON / JSON-LD markup.

    /// <summary>
    /// DOC-021: Marks a region of the document as a typed semantic entity.
    /// </summary>
    /// <param name="id">Caller-chosen unique entity identifier.</param>
    /// <param name="entityType">camelCase type name (e.g. "heading", "invoiceNumber"); unknown names become custom types.</param>
    /// <param name="x">Bounding-box X in page coordinates.</param>
    /// <param name="y">Bounding-box Y in page coordinates.</param>
    /// <param name="width">Bounding-box width.</param>
    /// <param name="height">Bounding-box height.</param>
    /// <param name="page">One-based page number the entity lives on.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="id"/> or <paramref name="entityType"/> is null.</exception>
    public PdfDocument MarkEntity(string id, string entityType, double x, double y, double width, double height, int page)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_document_mark_entity(_handle, id, entityType, x, y, width, height, (uint)page),
            "Failed to mark semantic entity");
        return this;
    }

    /// <summary>DOC-021: Sets the content text of a previously marked entity.</summary>
    /// <exception cref="ArgumentNullException">If an argument is null.</exception>
    /// <exception cref="PdfExtractionException">If no entity has the given id.</exception>
    public PdfDocument SetEntityContent(string id, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(content);
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_document_set_entity_content(_handle, id, content),
            "Failed to set entity content");
        return this;
    }

    /// <summary>DOC-021: Adds a metadata key/value pair to a marked entity.</summary>
    /// <exception cref="ArgumentNullException">If an argument is null.</exception>
    /// <exception cref="PdfExtractionException">If no entity has the given id.</exception>
    public PdfDocument AddEntityMetadata(string id, string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_document_add_entity_metadata(_handle, id, key, value),
            "Failed to add entity metadata");
        return this;
    }

    /// <summary>DOC-021: Sets the confidence (0..1) of a marked entity.</summary>
    /// <exception cref="ArgumentNullException">If <paramref name="id"/> is null.</exception>
    /// <exception cref="PdfExtractionException">If no entity has the given id.</exception>
    public PdfDocument SetEntityConfidence(string id, float confidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_document_set_entity_confidence(_handle, id, confidence),
            "Failed to set entity confidence");
        return this;
    }

    /// <summary>DOC-021: Records a relationship between two marked entities.</summary>
    /// <param name="fromId">Id of the source entity.</param>
    /// <param name="toId">Id of the target entity.</param>
    /// <param name="relation">camelCase relation (e.g. "contains", "isPartOf", "references"); unknown names become custom.</param>
    /// <exception cref="ArgumentNullException">If an argument is null.</exception>
    /// <exception cref="PdfExtractionException">If either id is unknown.</exception>
    public PdfDocument RelateEntities(string fromId, string toId, string relation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fromId);
        ArgumentException.ThrowIfNullOrWhiteSpace(toId);
        ArgumentException.ThrowIfNullOrWhiteSpace(relation);
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_document_relate_entities(_handle, fromId, toId, relation),
            "Failed to relate entities");
        return this;
    }

    /// <summary>
    /// DOC-021: Exports all marked semantic entities as a plain JSON array,
    /// preserving every field including each entity's content and relationships.
    /// </summary>
    public string ExportSemanticEntitiesJson()
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_document_export_semantic_entities_json(_handle, out var outJson),
            "Failed to export semantic entities");
        return ReadAndFreeString(outJson);
    }

    /// <summary>
    /// DOC-021: Exports all marked semantic entities as Schema.org JSON-LD.
    /// Carries entity type, id, bounds, metadata and confidence — but not the
    /// per-entity content text (use <see cref="ExportSemanticEntitiesJson"/> for full fidelity).
    /// </summary>
    public string ExportSemanticEntitiesJsonLd()
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_document_export_semantic_entities_json_ld(_handle, out var outJson),
            "Failed to export semantic entities as JSON-LD");
        return ReadAndFreeString(outJson);
    }

    private static string ReadAndFreeString(IntPtr ptr)
    {
        try
        {
            return Marshal.PtrToStringUTF8(ptr) ?? "";
        }
        finally
        {
            if (ptr != IntPtr.Zero)
                NativeMethods.oxidize_free_string(ptr);
        }
    }

    /// <summary>
    /// Gets the number of pages currently in the document.
    /// </summary>
    /// <exception cref="ObjectDisposedException">If this document has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public int PageCount
    {
        get
        {
            ThrowIfDisposed();
            ThrowIfError(
                NativeMethods.oxidize_document_page_count(_handle, out var count),
                "Failed to get page count");
            return (int)count;
        }
    }

    // ── Serialization ─────────────────────────────────────────────────────────

    /// <summary>
    /// Serializes the document to a PDF byte array.
    /// </summary>
    /// <returns>The complete PDF file as a managed byte array.</returns>
    /// <exception cref="ObjectDisposedException">If this document has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If serialization fails.</exception>
    public byte[] SaveToBytes()
    {
        ThrowIfDisposed();

        ThrowIfError(
            NativeMethods.oxidize_document_save_to_bytes(_handle, out var nativePtr, out var nativeLen),
            "Failed to save document to bytes");

        try
        {
            var length = (int)nativeLen;
            var result = new byte[length];
            Marshal.Copy(nativePtr, result, 0, length);
            return result;
        }
        finally
        {
            if (nativePtr != IntPtr.Zero)
                NativeMethods.oxidize_free_bytes(nativePtr, nativeLen);
        }
    }

    /// <summary>
    /// Serializes the document to PDF bytes using explicit writer options
    /// (PDF version, xref/object streams, stream compression).
    /// </summary>
    /// <param name="options">Writer configuration. See <see cref="PdfSaveOptions"/>.</param>
    /// <returns>The serialized PDF bytes.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="options"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">If this document has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If serialization fails.</exception>
    public byte[] SaveToBytes(PdfSaveOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ThrowIfDisposed();

        ThrowIfError(
            NativeMethods.oxidize_document_save_to_bytes_with_config(
                _handle,
                options.UseXrefStreams ? 1 : 0,
                options.UseObjectStreams ? 1 : 0,
                options.PdfVersion,
                options.CompressStreams ? 1 : 0,
                out var nativePtr,
                out var nativeLen),
            "Failed to save document to bytes with config");

        try
        {
            var length = checked((int)nativeLen);
            var result = new byte[length];
            Marshal.Copy(nativePtr, result, 0, length);
            return result;
        }
        finally
        {
            if (nativePtr != IntPtr.Zero)
                NativeMethods.oxidize_free_bytes(nativePtr, nativeLen);
        }
    }

    /// <summary>
    /// Sets the action triggered when the document is opened (navigate to a
    /// destination, or open a URI). Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="action">The open action to embed.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="action"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">If this document has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfDocument SetOpenAction(PdfOpenAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ThrowIfDisposed();
        string json = System.Text.Json.JsonSerializer.Serialize(action);
        ThrowIfError(
            NativeMethods.oxidize_document_set_open_action_json(_handle, json),
            "Failed to set document open action");
        return this;
    }

    /// <summary>
    /// Applies viewer preferences (toolbar/menu visibility, window behaviour,
    /// page layout, print scaling, duplex mode, etc.). Unset properties are
    /// omitted from the PDF. Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="preferences">Viewer preferences to apply.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="preferences"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">If this document has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfDocument SetViewerPreferences(PdfViewerPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        ThrowIfDisposed();
        string json = System.Text.Json.JsonSerializer.Serialize(preferences);
        ThrowIfError(
            NativeMethods.oxidize_document_set_viewer_preferences_json(_handle, json),
            "Failed to set document viewer preferences");
        return this;
    }

    /// <summary>
    /// Registers a named destination — a symbolic label that outlines or link
    /// annotations can reference instead of a hard-coded page. Re-adding an
    /// existing name overwrites it. Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="name">Destination name (non-empty, non-whitespace).</param>
    /// <param name="destination">Target location inside the document.</param>
    /// <exception cref="ArgumentException">If <paramref name="name"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException">If <paramref name="destination"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">If this document has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfDocument AddNamedDestination(string name, PdfDestination destination)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(destination);
        ThrowIfDisposed();
        string json = System.Text.Json.JsonSerializer.Serialize(new NamedDestinationPayload(name, destination));
        ThrowIfError(
            NativeMethods.oxidize_document_add_named_destination_json(_handle, json),
            "Failed to add named destination");
        return this;
    }

    private sealed record NamedDestinationPayload(
        [property: System.Text.Json.Serialization.JsonPropertyName("name")] string Name,
        [property: System.Text.Json.Serialization.JsonPropertyName("destination")] PdfDestination Destination);

    /// <summary>
    /// Applies a custom page-numbering scheme (page labels). Returns <c>this</c>
    /// for fluent chaining.
    /// </summary>
    /// <param name="labels">The page-label ranges; must contain at least one range.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="labels"/> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="labels"/> has no ranges.</exception>
    /// <exception cref="ObjectDisposedException">If this document has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfDocument SetPageLabels(PdfPageLabels labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        if (labels.Ranges.Count == 0)
            throw new ArgumentException("Page labels require at least one range", nameof(labels));
        ThrowIfDisposed();
        string json = System.Text.Json.JsonSerializer.Serialize(labels);
        ThrowIfError(
            NativeMethods.oxidize_document_set_page_labels_json(_handle, json),
            "Failed to set page labels");
        return this;
    }

    // ── Interactive forms (AcroForm write-path) ─────────────────────────────────

    private static readonly JsonSerializerOptions FormJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// Enables interactive forms (creates the AcroForm + form manager if absent).
    /// Idempotent; the <c>Add*</c> field factories call this implicitly, so an
    /// explicit call is only needed to signal intent. Returns <c>this</c>.
    /// </summary>
    /// <exception cref="ObjectDisposedException">If this document has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfDocument EnableForms()
    {
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_document_enable_forms(_handle),
            "Failed to enable forms");
        return this;
    }

    /// <summary>
    /// Creates an AcroForm text field. Place its visual on a page with
    /// <see cref="PdfPage.AddFormWidget(FormFieldRef)"/> before adding the page.
    /// </summary>
    /// <param name="name">Unique field name (the AcroForm <c>/T</c> entry).</param>
    /// <param name="x1">Lower-left X of the widget rectangle, in PDF points.</param>
    /// <param name="y1">Lower-left Y of the widget rectangle, in PDF points.</param>
    /// <param name="x2">Upper-right X of the widget rectangle, in PDF points.</param>
    /// <param name="y2">Upper-right Y of the widget rectangle, in PDF points.</param>
    /// <param name="value">Initial value (<c>/V</c>), or null.</param>
    /// <param name="defaultValue">Default value (<c>/DV</c>), or null.</param>
    /// <param name="maxLength">Maximum character length, or null for unlimited.</param>
    /// <param name="multiline">Whether the field accepts multiple lines.</param>
    /// <param name="password">Whether the field masks its input.</param>
    /// <param name="readOnly">Whether the field is read-only.</param>
    /// <param name="required">Whether the field is required.</param>
    /// <returns>A reference used to attach widgets and identify the field.</returns>
    /// <exception cref="ArgumentException">If <paramref name="name"/> is null/empty/whitespace.</exception>
    /// <exception cref="ObjectDisposedException">If this document has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public FormFieldRef AddTextField(
        string name,
        double x1,
        double y1,
        double x2,
        double y2,
        string? value = null,
        string? defaultValue = null,
        int? maxLength = null,
        bool multiline = false,
        bool password = false,
        bool readOnly = false,
        bool required = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var payload = new FormFieldPayload("text", name, new[] { x1, y1, x2, y2 })
        {
            Value = value,
            DefaultValue = defaultValue,
            MaxLength = maxLength,
            Multiline = multiline,
            Password = password,
            ReadOnly = readOnly,
            Required = required,
        };
        return AddFormFieldCore(payload, x1, y1, x2, y2);
    }

    /// <summary>
    /// Creates an AcroForm checkbox field.
    /// </summary>
    /// <param name="name">Unique field name.</param>
    /// <param name="x1">Lower-left X of the widget rectangle.</param>
    /// <param name="y1">Lower-left Y of the widget rectangle.</param>
    /// <param name="x2">Upper-right X of the widget rectangle.</param>
    /// <param name="y2">Upper-right Y of the widget rectangle.</param>
    /// <param name="checked">Whether the box starts checked.</param>
    /// <param name="exportValue">The "on" export value (default "Yes").</param>
    /// <param name="readOnly">Whether the field is read-only.</param>
    /// <param name="required">Whether the field is required.</param>
    /// <returns>A reference used to attach widgets and identify the field.</returns>
    /// <exception cref="ArgumentException">If <paramref name="name"/> is null/empty/whitespace.</exception>
    /// <exception cref="ObjectDisposedException">If this document has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public FormFieldRef AddCheckBox(
        string name,
        double x1,
        double y1,
        double x2,
        double y2,
        bool @checked = false,
        string exportValue = "Yes",
        bool readOnly = false,
        bool required = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var payload = new FormFieldPayload("checkbox", name, new[] { x1, y1, x2, y2 })
        {
            Checked = @checked,
            ExportValue = exportValue,
            ReadOnly = readOnly,
            Required = required,
        };
        return AddFormFieldCore(payload, x1, y1, x2, y2);
    }

    /// <summary>
    /// Creates an AcroForm radio-button group. Each option is an
    /// (export value, label) pair; <paramref name="selected"/> is the 0-based
    /// index of the initially selected option, or null for none.
    /// </summary>
    /// <param name="name">Unique field name.</param>
    /// <param name="x1">Lower-left X of the primary widget rectangle.</param>
    /// <param name="y1">Lower-left Y of the primary widget rectangle.</param>
    /// <param name="x2">Upper-right X of the primary widget rectangle.</param>
    /// <param name="y2">Upper-right Y of the primary widget rectangle.</param>
    /// <param name="options">The (export, label) options; must be non-empty.</param>
    /// <param name="selected">0-based index of the selected option, or null.</param>
    /// <param name="readOnly">Whether the field is read-only.</param>
    /// <param name="required">Whether the field is required.</param>
    /// <returns>A reference used to attach widgets and identify the field.</returns>
    /// <exception cref="ArgumentException">If <paramref name="name"/> is invalid or <paramref name="options"/> is empty.</exception>
    /// <exception cref="ArgumentNullException">If <paramref name="options"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">If this document has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public FormFieldRef AddRadioGroup(
        string name,
        double x1,
        double y1,
        double x2,
        double y2,
        IEnumerable<(string Export, string Label)> options,
        int? selected = null,
        bool readOnly = false,
        bool required = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(options);
        var opts = ToOptionPayloads(options);
        if (opts.Count == 0)
            throw new ArgumentException("Radio group requires at least one option", nameof(options));
        var payload = new FormFieldPayload("radio", name, new[] { x1, y1, x2, y2 })
        {
            Options = opts,
            Selected = selected,
            ReadOnly = readOnly,
            Required = required,
        };
        return AddFormFieldCore(payload, x1, y1, x2, y2);
    }

    /// <summary>
    /// Creates an AcroForm combo box (dropdown). Each option is an
    /// (export value, display text) pair.
    /// </summary>
    /// <param name="name">Unique field name.</param>
    /// <param name="x1">Lower-left X of the widget rectangle.</param>
    /// <param name="y1">Lower-left Y of the widget rectangle.</param>
    /// <param name="x2">Upper-right X of the widget rectangle.</param>
    /// <param name="y2">Upper-right Y of the widget rectangle.</param>
    /// <param name="options">The (export, display) options; must be non-empty.</param>
    /// <param name="value">Initial value (<c>/V</c>), or null.</param>
    /// <param name="editable">Whether the user may type a custom value.</param>
    /// <param name="readOnly">Whether the field is read-only.</param>
    /// <param name="required">Whether the field is required.</param>
    /// <returns>A reference used to attach widgets and identify the field.</returns>
    /// <exception cref="ArgumentException">If <paramref name="name"/> is invalid or <paramref name="options"/> is empty.</exception>
    /// <exception cref="ArgumentNullException">If <paramref name="options"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">If this document has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public FormFieldRef AddComboBox(
        string name,
        double x1,
        double y1,
        double x2,
        double y2,
        IEnumerable<(string Export, string Display)> options,
        string? value = null,
        bool editable = false,
        bool readOnly = false,
        bool required = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(options);
        var opts = ToOptionPayloads(options);
        if (opts.Count == 0)
            throw new ArgumentException("Combo box requires at least one option", nameof(options));
        var payload = new FormFieldPayload("combobox", name, new[] { x1, y1, x2, y2 })
        {
            Options = opts,
            Value = value,
            Editable = editable,
            ReadOnly = readOnly,
            Required = required,
        };
        return AddFormFieldCore(payload, x1, y1, x2, y2);
    }

    /// <summary>
    /// Creates an AcroForm list box. Each option is an
    /// (export value, display text) pair.
    /// </summary>
    /// <param name="name">Unique field name.</param>
    /// <param name="x1">Lower-left X of the widget rectangle.</param>
    /// <param name="y1">Lower-left Y of the widget rectangle.</param>
    /// <param name="x2">Upper-right X of the widget rectangle.</param>
    /// <param name="y2">Upper-right Y of the widget rectangle.</param>
    /// <param name="options">The (export, display) options; must be non-empty.</param>
    /// <param name="selectedIndices">0-based indices of selected options, or null.</param>
    /// <param name="multiSelect">Whether multiple options may be selected.</param>
    /// <param name="readOnly">Whether the field is read-only.</param>
    /// <param name="required">Whether the field is required.</param>
    /// <returns>A reference used to attach widgets and identify the field.</returns>
    /// <exception cref="ArgumentException">If <paramref name="name"/> is invalid or <paramref name="options"/> is empty.</exception>
    /// <exception cref="ArgumentNullException">If <paramref name="options"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">If this document has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public FormFieldRef AddListBox(
        string name,
        double x1,
        double y1,
        double x2,
        double y2,
        IEnumerable<(string Export, string Display)> options,
        IEnumerable<int>? selectedIndices = null,
        bool multiSelect = false,
        bool readOnly = false,
        bool required = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(options);
        var opts = ToOptionPayloads(options);
        if (opts.Count == 0)
            throw new ArgumentException("List box requires at least one option", nameof(options));
        var payload = new FormFieldPayload("listbox", name, new[] { x1, y1, x2, y2 })
        {
            Options = opts,
            SelectedIndices = selectedIndices?.ToList() ?? new List<int>(),
            MultiSelect = multiSelect,
            ReadOnly = readOnly,
            Required = required,
        };
        return AddFormFieldCore(payload, x1, y1, x2, y2);
    }

    /// <summary>
    /// Creates an AcroForm push button (an action trigger, not a value holder).
    /// </summary>
    /// <param name="name">Unique field name.</param>
    /// <param name="x1">Lower-left X of the widget rectangle.</param>
    /// <param name="y1">Lower-left Y of the widget rectangle.</param>
    /// <param name="x2">Upper-right X of the widget rectangle.</param>
    /// <param name="y2">Upper-right Y of the widget rectangle.</param>
    /// <param name="caption">The button caption, or null.</param>
    /// <returns>A reference used to attach widgets and identify the field.</returns>
    /// <exception cref="ArgumentException">If <paramref name="name"/> is null/empty/whitespace.</exception>
    /// <exception cref="ObjectDisposedException">If this document has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public FormFieldRef AddPushButton(
        string name,
        double x1,
        double y1,
        double x2,
        double y2,
        string? caption = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var payload = new FormFieldPayload("pushbutton", name, new[] { x1, y1, x2, y2 })
        {
            Caption = caption,
        };
        return AddFormFieldCore(payload, x1, y1, x2, y2);
    }

    /// <summary>
    /// Sets the value of a form field created on this document, updating its
    /// <c>/V</c> entry and regenerating the widget appearance. Only works for
    /// fields registered in this document's form manager (i.e. created via the
    /// <c>Add*</c> factories); there is no path to fill fields of a parsed PDF.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="name">The field name to fill.</param>
    /// <param name="value">The textual value to set.</param>
    /// <exception cref="ArgumentException">If <paramref name="name"/> is null/empty/whitespace.</exception>
    /// <exception cref="ArgumentNullException">If <paramref name="value"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">If this document has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the field does not exist or the native call fails.</exception>
    public PdfDocument FillField(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_document_fill_field(_handle, name, value),
            $"Failed to fill form field '{name}'");
        return this;
    }

    private FormFieldRef AddFormFieldCore(FormFieldPayload payload, double x1, double y1, double x2, double y2)
    {
        ThrowIfDisposed();
        string json = JsonSerializer.Serialize(payload, FormJsonOptions);
        ThrowIfError(
            NativeMethods.oxidize_document_add_form_field_json(_handle, json, out uint objNum),
            $"Failed to create form field '{payload.Name}'");
        return new FormFieldRef(objNum, x1, y1, x2, y2);
    }

    private static List<FormOptionPayload> ToOptionPayloads(IEnumerable<(string, string)> options) =>
        options.Select(o => new FormOptionPayload(o.Item1, o.Item2)).ToList();

    private sealed record FormOptionPayload(
        [property: JsonPropertyName("export")] string Export,
        [property: JsonPropertyName("label")] string Label);

    // Mirrors the Rust `CreateFieldJson` DTO. Property names map to snake_case
    // via FormJsonOptions; bool fields are always emitted (the Rust DTO derives
    // them with serde `default` and cannot deserialize null into bool).
    private sealed class FormFieldPayload
    {
        public FormFieldPayload(string kind, string name, double[] rect)
        {
            Kind = kind;
            Name = name;
            Rect = rect;
        }

        public string Kind { get; }
        public string Name { get; }
        public double[] Rect { get; }

        public string? Value { get; init; }
        public string? DefaultValue { get; init; }
        public int? MaxLength { get; init; }
        public bool Multiline { get; init; }
        public bool Password { get; init; }

        public bool Checked { get; init; }
        public string? ExportValue { get; init; }

        public List<FormOptionPayload> Options { get; init; } = new();
        public int? Selected { get; init; }
        public List<int> SelectedIndices { get; init; } = new();
        public bool MultiSelect { get; init; }
        public bool Editable { get; init; }

        public string? Caption { get; init; }

        public bool ReadOnly { get; init; }
        public bool Required { get; init; }
        public bool NoExport { get; init; }
        public int? Quadding { get; init; }
    }

    /// <summary>
    /// Saves the document to a file at the specified path.
    /// </summary>
    /// <param name="path">The file path to write the PDF to.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="path"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">If this document has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public void SaveToFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_document_save_to_file(_handle, path),
            "Failed to save document to file");
    }

    // ── Security ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Encrypts the document with user and owner passwords using default permissions.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="userPassword">Password required to open and read the document.</param>
    /// <param name="ownerPassword">Password that grants full control over the document.</param>
    /// <exception cref="ArgumentNullException">If either password is null.</exception>
    /// <exception cref="ObjectDisposedException">If this document has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfDocument Encrypt(string userPassword, string ownerPassword)
    {
        ArgumentNullException.ThrowIfNull(userPassword);
        ArgumentNullException.ThrowIfNull(ownerPassword);
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_document_encrypt(_handle, userPassword, ownerPassword),
            "Failed to encrypt document");
        return this;
    }

    /// <summary>
    /// Encrypts the document with user and owner passwords and specific permissions.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="userPassword">Password required to open and read the document.</param>
    /// <param name="ownerPassword">Password that grants full control over the document.</param>
    /// <param name="permissions">Combination of <see cref="PdfPermissions"/> flags.</param>
    /// <exception cref="ArgumentNullException">If either password is null.</exception>
    /// <exception cref="ObjectDisposedException">If this document has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfDocument Encrypt(string userPassword, string ownerPassword, PdfPermissions permissions)
    {
        ArgumentNullException.ThrowIfNull(userPassword);
        ArgumentNullException.ThrowIfNull(ownerPassword);
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_document_encrypt_with_permissions(
                _handle, userPassword, ownerPassword, (uint)permissions),
            "Failed to encrypt document with permissions");
        return this;
    }

    /// <summary>
    /// Encrypts the document with AES-128 and user and owner passwords using default permissions.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="userPassword">Password required to open and read the document.</param>
    /// <param name="ownerPassword">Password that grants full control over the document.</param>
    /// <exception cref="ArgumentNullException">If either password is null.</exception>
    /// <exception cref="ObjectDisposedException">If this document has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfDocument EncryptAes128(string userPassword, string ownerPassword)
    {
        ArgumentNullException.ThrowIfNull(userPassword);
        ArgumentNullException.ThrowIfNull(ownerPassword);
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_document_encrypt_aes128(_handle, userPassword, ownerPassword),
            "Failed to encrypt document with AES-128");
        return this;
    }

    /// <summary>
    /// Encrypts the document with AES-128 and user and owner passwords and specific permissions.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="userPassword">Password required to open and read the document.</param>
    /// <param name="ownerPassword">Password that grants full control over the document.</param>
    /// <param name="permissions">Combination of <see cref="PdfPermissions"/> flags.</param>
    /// <exception cref="ArgumentNullException">If either password is null.</exception>
    /// <exception cref="ObjectDisposedException">If this document has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfDocument EncryptAes128(string userPassword, string ownerPassword, PdfPermissions permissions)
    {
        ArgumentNullException.ThrowIfNull(userPassword);
        ArgumentNullException.ThrowIfNull(ownerPassword);
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_document_encrypt_aes128_with_permissions(
                _handle, userPassword, ownerPassword, (uint)permissions),
            "Failed to encrypt document with AES-128 and permissions");
        return this;
    }

    /// <summary>
    /// Encrypts the document with AES-256 and user and owner passwords using default permissions.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="userPassword">Password required to open and read the document.</param>
    /// <param name="ownerPassword">Password that grants full control over the document.</param>
    /// <exception cref="ArgumentNullException">If either password is null.</exception>
    /// <exception cref="ObjectDisposedException">If this document has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfDocument EncryptAes256(string userPassword, string ownerPassword)
    {
        ArgumentNullException.ThrowIfNull(userPassword);
        ArgumentNullException.ThrowIfNull(ownerPassword);
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_document_encrypt_aes256(_handle, userPassword, ownerPassword),
            "Failed to encrypt document with AES-256");
        return this;
    }

    /// <summary>
    /// Encrypts the document with AES-256 and user and owner passwords and specific permissions.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="userPassword">Password required to open and read the document.</param>
    /// <param name="ownerPassword">Password that grants full control over the document.</param>
    /// <param name="permissions">Combination of <see cref="PdfPermissions"/> flags.</param>
    /// <exception cref="ArgumentNullException">If either password is null.</exception>
    /// <exception cref="ObjectDisposedException">If this document has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfDocument EncryptAes256(string userPassword, string ownerPassword, PdfPermissions permissions)
    {
        ArgumentNullException.ThrowIfNull(userPassword);
        ArgumentNullException.ThrowIfNull(ownerPassword);
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_document_encrypt_aes256_with_permissions(
                _handle, userPassword, ownerPassword, (uint)permissions),
            "Failed to encrypt document with AES-256 and permissions");
        return this;
    }

    // ── Outline ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets the document outline (bookmarks / table of contents). Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="outline">The outline tree to apply.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="outline"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">If this document has been disposed.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfDocument SetOutline(PdfOutline outline)
    {
        ArgumentNullException.ThrowIfNull(outline);
        ThrowIfDisposed();
        ThrowIfError(
            NativeMethods.oxidize_document_set_outline(_handle, outline.ToJson()),
            "Failed to set document outline");
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
            NativeMethods.oxidize_document_free(_handle);
            _handle = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>Finalizer that ensures native resources are freed if Dispose was not called.</summary>
    ~PdfDocument() => Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PdfDocument));
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
