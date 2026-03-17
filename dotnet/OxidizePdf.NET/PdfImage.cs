using System.Runtime.InteropServices;

namespace OxidizePdf.NET;

/// <summary>
/// Represents an image that can be embedded into a PDF page.
/// Supports JPEG and PNG formats.
/// Implements <see cref="IDisposable"/> to ensure native resources are freed.
/// </summary>
public sealed class PdfImage : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    private PdfImage(IntPtr handle)
    {
        _handle = handle;
    }

    /// <summary>
    /// Exposes the native image handle for use by page operations.
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
    /// Creates an image from JPEG byte data.
    /// </summary>
    /// <param name="data">The raw JPEG file bytes.</param>
    /// <returns>A new <see cref="PdfImage"/> instance.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="data"/> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="data"/> is empty.</exception>
    /// <exception cref="PdfExtractionException">If the data is not valid JPEG.</exception>
    public static PdfImage FromJpegData(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length == 0)
            throw new ArgumentException("Image data cannot be empty", nameof(data));

        IntPtr dataPtr = IntPtr.Zero;
        try
        {
            dataPtr = Marshal.AllocHGlobal(data.Length);
            Marshal.Copy(data, 0, dataPtr, data.Length);

            ThrowIfError(
                NativeMethods.oxidize_image_from_jpeg(dataPtr, (nuint)data.Length, out var handle),
                "Failed to create image from JPEG");

            return new PdfImage(handle);
        }
        finally
        {
            if (dataPtr != IntPtr.Zero)
                Marshal.FreeHGlobal(dataPtr);
        }
    }

    /// <summary>
    /// Creates an image from PNG byte data.
    /// </summary>
    /// <param name="data">The raw PNG file bytes.</param>
    /// <returns>A new <see cref="PdfImage"/> instance.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="data"/> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="data"/> is empty.</exception>
    /// <exception cref="PdfExtractionException">If the data is not valid PNG.</exception>
    public static PdfImage FromPngData(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length == 0)
            throw new ArgumentException("Image data cannot be empty", nameof(data));

        IntPtr dataPtr = IntPtr.Zero;
        try
        {
            dataPtr = Marshal.AllocHGlobal(data.Length);
            Marshal.Copy(data, 0, dataPtr, data.Length);

            ThrowIfError(
                NativeMethods.oxidize_image_from_png(dataPtr, (nuint)data.Length, out var handle),
                "Failed to create image from PNG");

            return new PdfImage(handle);
        }
        finally
        {
            if (dataPtr != IntPtr.Zero)
                Marshal.FreeHGlobal(dataPtr);
        }
    }

    /// <summary>Gets the image width in pixels.</summary>
    /// <exception cref="ObjectDisposedException">If this image has been disposed.</exception>
    public uint Width
    {
        get
        {
            ThrowIfDisposed();
            ThrowIfError(
                NativeMethods.oxidize_image_get_width(_handle, out var width),
                "Failed to get image width");
            return width;
        }
    }

    /// <summary>Gets the image height in pixels.</summary>
    /// <exception cref="ObjectDisposedException">If this image has been disposed.</exception>
    public uint Height
    {
        get
        {
            ThrowIfDisposed();
            ThrowIfError(
                NativeMethods.oxidize_image_get_height(_handle, out var height),
                "Failed to get image height");
            return height;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.oxidize_image_free(_handle);
            _handle = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>Finalizer that ensures native resources are freed if Dispose was not called.</summary>
    ~PdfImage() => Dispose();

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PdfImage));
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
