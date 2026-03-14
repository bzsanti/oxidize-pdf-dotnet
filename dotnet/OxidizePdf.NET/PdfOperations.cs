using System.Runtime.InteropServices;
using System.Text.Json;

namespace OxidizePdf.NET;

/// <summary>
/// Stateless PDF manipulation operations: split, merge, rotate, and page extraction.
/// All methods work with byte arrays, requiring no file system access.
/// </summary>
public static class PdfOperations
{
    /// <summary>
    /// Splits a PDF into individual single-page PDFs.
    /// </summary>
    /// <param name="pdfBytes">The source PDF as a byte array.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of byte arrays, one per page of the source PDF.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="pdfBytes"/> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="pdfBytes"/> is empty.</exception>
    /// <exception cref="OperationCanceledException">If the operation is cancelled.</exception>
    /// <exception cref="PdfExtractionException">If the native split operation fails.</exception>
    public static Task<List<byte[]>> SplitAsync(byte[] pdfBytes, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));

        ct.ThrowIfCancellationRequested();
        return Task.Run(() => Split(pdfBytes), ct);
    }

    /// <summary>
    /// Merges multiple PDFs into a single PDF.
    /// </summary>
    /// <param name="pdfs">Collection of PDF byte arrays to merge in order.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The merged PDF as a byte array.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="pdfs"/> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="pdfs"/> is empty or contains a null or empty entry.</exception>
    /// <exception cref="OperationCanceledException">If the operation is cancelled.</exception>
    /// <exception cref="PdfExtractionException">If the native merge operation fails.</exception>
    public static Task<byte[]> MergeAsync(IReadOnlyList<byte[]> pdfs, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(pdfs);
        if (pdfs.Count == 0)
            throw new ArgumentException("At least one PDF is required for merge", nameof(pdfs));

        for (var i = 0; i < pdfs.Count; i++)
        {
            if (pdfs[i] == null)
                throw new ArgumentException($"PDF at index {i} is null", nameof(pdfs));
            if (pdfs[i].Length == 0)
                throw new ArgumentException($"PDF at index {i} is empty", nameof(pdfs));
        }

        ct.ThrowIfCancellationRequested();
        return Task.Run(() => Merge(pdfs), ct);
    }

    /// <summary>
    /// Rotates all pages of a PDF by the specified number of degrees.
    /// </summary>
    /// <param name="pdfBytes">The source PDF as a byte array.</param>
    /// <param name="degrees">Rotation angle. Must be 0, 90, 180, or 270.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The rotated PDF as a byte array.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="pdfBytes"/> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="pdfBytes"/> is empty.</exception>
    /// <exception cref="OperationCanceledException">If the operation is cancelled.</exception>
    /// <exception cref="PdfExtractionException">If the native rotate operation fails or degrees is invalid.</exception>
    public static Task<byte[]> RotateAsync(byte[] pdfBytes, int degrees, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));

        ct.ThrowIfCancellationRequested();
        return Task.Run(() => Rotate(pdfBytes, degrees), ct);
    }

    /// <summary>
    /// Extracts specific pages from a PDF into a new PDF.
    /// </summary>
    /// <param name="pdfBytes">The source PDF as a byte array.</param>
    /// <param name="pageIndices">0-based page indices to extract.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A new PDF containing only the specified pages.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="pdfBytes"/> or <paramref name="pageIndices"/> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="pdfBytes"/> is empty, or <paramref name="pageIndices"/> is empty,
    /// or any index is negative.</exception>
    /// <exception cref="OperationCanceledException">If the operation is cancelled.</exception>
    /// <exception cref="PdfExtractionException">If the native extract operation fails.</exception>
    public static Task<byte[]> ExtractPagesAsync(byte[] pdfBytes, int[] pageIndices, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(pdfBytes);
        ArgumentNullException.ThrowIfNull(pageIndices);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        if (pageIndices.Length == 0)
            throw new ArgumentException("At least one page index is required", nameof(pageIndices));

        for (var i = 0; i < pageIndices.Length; i++)
        {
            if (pageIndices[i] < 0)
                throw new ArgumentException(
                    $"Page index at position {i} is negative ({pageIndices[i]}). Indices are 0-based.",
                    nameof(pageIndices));
        }

        ct.ThrowIfCancellationRequested();
        return Task.Run(() => ExtractPages(pdfBytes, pageIndices), ct);
    }

    // ── Private synchronous implementations ──────────────────────────────────

    private static List<byte[]> Split(byte[] pdfBytes)
    {
        IntPtr pdfPtr = IntPtr.Zero;
        IntPtr jsonPtr = IntPtr.Zero;

        try
        {
            pdfPtr = Marshal.AllocHGlobal(pdfBytes.Length);
            Marshal.Copy(pdfBytes, 0, pdfPtr, pdfBytes.Length);

            var result = NativeMethods.oxidize_split_pdf_bytes(
                pdfPtr,
                (nuint)pdfBytes.Length,
                out jsonPtr);

            ThrowIfError(result, "Failed to split PDF");

            var json = Marshal.PtrToStringUTF8(jsonPtr) ?? "[]";
            var base64Pages = JsonSerializer.Deserialize<List<string>>(json)
                ?? new List<string>();

            var pages = new List<byte[]>(base64Pages.Count);
            foreach (var encoded in base64Pages)
                pages.Add(Convert.FromBase64String(encoded));

            return pages;
        }
        finally
        {
            if (pdfPtr != IntPtr.Zero)
                Marshal.FreeHGlobal(pdfPtr);
            if (jsonPtr != IntPtr.Zero)
                NativeMethods.oxidize_free_string(jsonPtr);
        }
    }

    private static byte[] Merge(IReadOnlyList<byte[]> pdfs)
    {
        IntPtr outPtr = IntPtr.Zero;
        nuint outLen = 0;

        try
        {
            var base64List = new List<string>(pdfs.Count);
            foreach (var pdf in pdfs)
                base64List.Add(Convert.ToBase64String(pdf));

            var pdfsJson = JsonSerializer.Serialize(base64List);

            var result = NativeMethods.oxidize_merge_pdfs_bytes(pdfsJson, out outPtr, out outLen);
            ThrowIfError(result, "Failed to merge PDFs");

            var length = (int)outLen;
            var output = new byte[length];
            Marshal.Copy(outPtr, output, 0, length);
            return output;
        }
        finally
        {
            if (outPtr != IntPtr.Zero)
                NativeMethods.oxidize_free_bytes(outPtr, outLen);
        }
    }

    private static byte[] Rotate(byte[] pdfBytes, int degrees)
    {
        IntPtr pdfPtr = IntPtr.Zero;
        IntPtr outPtr = IntPtr.Zero;
        nuint outLen = 0;

        try
        {
            pdfPtr = Marshal.AllocHGlobal(pdfBytes.Length);
            Marshal.Copy(pdfBytes, 0, pdfPtr, pdfBytes.Length);

            var result = NativeMethods.oxidize_rotate_pdf_bytes(
                pdfPtr,
                (nuint)pdfBytes.Length,
                degrees,
                out outPtr,
                out outLen);

            ThrowIfError(result, $"Failed to rotate PDF by {degrees} degrees");

            var length = (int)outLen;
            var output = new byte[length];
            Marshal.Copy(outPtr, output, 0, length);
            return output;
        }
        finally
        {
            if (pdfPtr != IntPtr.Zero)
                Marshal.FreeHGlobal(pdfPtr);
            if (outPtr != IntPtr.Zero)
                NativeMethods.oxidize_free_bytes(outPtr, outLen);
        }
    }

    private static byte[] ExtractPages(byte[] pdfBytes, int[] pageIndices)
    {
        IntPtr pdfPtr = IntPtr.Zero;
        IntPtr outPtr = IntPtr.Zero;
        nuint outLen = 0;

        try
        {
            pdfPtr = Marshal.AllocHGlobal(pdfBytes.Length);
            Marshal.Copy(pdfBytes, 0, pdfPtr, pdfBytes.Length);

            var pagesJson = JsonSerializer.Serialize(pageIndices);

            var result = NativeMethods.oxidize_extract_pages_bytes(
                pdfPtr,
                (nuint)pdfBytes.Length,
                pagesJson,
                out outPtr,
                out outLen);

            ThrowIfError(result, "Failed to extract pages from PDF");

            var length = (int)outLen;
            var output = new byte[length];
            Marshal.Copy(outPtr, output, 0, length);
            return output;
        }
        finally
        {
            if (pdfPtr != IntPtr.Zero)
                Marshal.FreeHGlobal(pdfPtr);
            if (outPtr != IntPtr.Zero)
                NativeMethods.oxidize_free_bytes(outPtr, outLen);
        }
    }

    // ── Error helper ──────────────────────────────────────────────────────────

    private static void ThrowIfError(int errorCode, string message)
    {
        if (errorCode == (int)NativeMethods.ErrorCode.Success)
            return;

        var rustError = NativeMethods.GetLastError();
        var detail = !string.IsNullOrEmpty(rustError) ? rustError : ((NativeMethods.ErrorCode)errorCode).ToString();
        throw new PdfExtractionException($"{message}: {detail}");
    }
}
