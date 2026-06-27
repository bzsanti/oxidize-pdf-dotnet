using System.Text.Json;

namespace OxidizePdf.NET;

/// <summary>
/// Represents a table that can be rendered on a PDF page.
/// Uses a builder pattern: create, add rows, then add to a page.
/// The table is consumed when added to a page.
/// Implements <see cref="IDisposable"/> to ensure native resources are freed.
/// </summary>
public sealed class PdfTable : IDisposable
{
    private readonly TableBuilderSafeHandle _safeHandle;

    // Bridge: existing call sites read `_handle` as an IntPtr unchanged.
    private IntPtr _handle => _safeHandle.DangerousGetHandle();
    private bool _consumed;

    /// <summary>
    /// Creates a new table with equal-width columns.
    /// </summary>
    /// <param name="headers">Column header texts.</param>
    /// <param name="totalWidth">Total width of the table in PDF points.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="headers"/> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="headers"/> is empty.</exception>
    /// <exception cref="PdfExtractionException">If native creation fails.</exception>
    public PdfTable(string[] headers, double totalWidth)
    {
        ArgumentNullException.ThrowIfNull(headers);
        if (headers.Length == 0)
            throw new ArgumentException("Headers cannot be empty", nameof(headers));

        var headersJson = JsonSerializer.Serialize(headers);
        ThrowIfError(
            NativeMethods.oxidize_table_builder_create(headersJson, totalWidth, out var handle),
            "Failed to create table builder");
        _safeHandle = new TableBuilderSafeHandle(handle);
    }

    /// <summary>
    /// Sets the table position on the page. Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="x">Left edge in PDF points.</param>
    /// <param name="y">Top edge in PDF points.</param>
    /// <exception cref="ObjectDisposedException">If this table has been disposed.</exception>
    /// <exception cref="InvalidOperationException">If this table has already been added to a page.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfTable SetPosition(double x, double y)
    {
        ThrowIfDisposedOrConsumed();
        ThrowIfError(
            NativeMethods.oxidize_table_builder_set_position(_handle, x, y),
            "Failed to set table position");
        return this;
    }

    /// <summary>
    /// Adds a data row to the table. Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="cells">Cell values for this row.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="cells"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">If this table has been disposed.</exception>
    /// <exception cref="InvalidOperationException">If this table has already been added to a page.</exception>
    /// <exception cref="PdfExtractionException">If the native call fails.</exception>
    public PdfTable AddRow(string[] cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        ThrowIfDisposedOrConsumed();

        var cellsJson = JsonSerializer.Serialize(cells);
        ThrowIfError(
            NativeMethods.oxidize_table_builder_add_row(_handle, cellsJson),
            "Failed to add row to table");
        return this;
    }

    /// <summary>
    /// Exposes the native handle and marks the table as consumed.
    /// Called by <see cref="PdfPage.AddTable"/>.
    /// </summary>
    internal IntPtr ConsumeHandle()
    {
        ThrowIfDisposedOrConsumed();
        _consumed = true;
        var h = _handle;
        // Ownership transferred to native; relinquish so the SafeHandle does not
        // also free it (would reintroduce the double-free of issue #54).
        _safeHandle.SetHandleAsInvalid();
        return h;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // SafeHandle releases the native handle exactly once, atomically,
        // even under concurrent Dispose or finalization (issue #54).
        _safeHandle.Dispose();
    }

    private void ThrowIfDisposedOrConsumed()
    {
        // Check _consumed first: ConsumeHandle invalidates the SafeHandle (so the
        // handle, now owned by native code, isn't freed), which also flips
        // IsClosed. A consumed table must surface InvalidOperationException, not
        // ObjectDisposedException.
        if (_consumed)
            throw new InvalidOperationException("This table has already been added to a page and cannot be modified.");
        if (_safeHandle.IsClosed)
            throw new ObjectDisposedException(nameof(PdfTable));
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
