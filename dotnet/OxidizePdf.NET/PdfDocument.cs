using System.Runtime.InteropServices;

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
    /// Adds a page to the document. The page is cloned internally by the native layer,
    /// so the <see cref="PdfPage"/> can be disposed after this call.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
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
