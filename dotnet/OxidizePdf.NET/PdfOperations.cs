using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

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

    /// <summary>
    /// Reorders pages in a PDF according to the specified new order.
    /// </summary>
    /// <param name="pdfBytes">The source PDF as a byte array.</param>
    /// <param name="newOrder">Array of 0-based page indices defining the new page order.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The reordered PDF as a byte array.</returns>
    public static Task<byte[]> ReorderPagesAsync(byte[] pdfBytes, int[] newOrder, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(pdfBytes);
        ArgumentNullException.ThrowIfNull(newOrder);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        if (newOrder.Length == 0)
            throw new ArgumentException("Page order cannot be empty", nameof(newOrder));

        for (var i = 0; i < newOrder.Length; i++)
        {
            if (newOrder[i] < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(newOrder),
                    $"Page index at position {i} is negative ({newOrder[i]}). Indices are 0-based.");
        }

        ct.ThrowIfCancellationRequested();
        return Task.Run(() => ReorderPages(pdfBytes, newOrder), ct);
    }

    /// <summary>
    /// Swaps two pages in a PDF by their 0-based indices.
    /// </summary>
    /// <param name="pdfBytes">The source PDF as a byte array.</param>
    /// <param name="pageA">0-based index of the first page.</param>
    /// <param name="pageB">0-based index of the second page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The PDF with swapped pages as a byte array.</returns>
    public static Task<byte[]> SwapPagesAsync(byte[] pdfBytes, int pageA, int pageB, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ArgumentOutOfRangeException.ThrowIfNegative(pageA);
        ArgumentOutOfRangeException.ThrowIfNegative(pageB);

        ct.ThrowIfCancellationRequested();
        return Task.Run(() => SwapPages(pdfBytes, pageA, pageB), ct);
    }

    /// <summary>
    /// Moves a page from one position to another in a PDF.
    /// </summary>
    /// <param name="pdfBytes">The source PDF as a byte array.</param>
    /// <param name="fromIndex">0-based index of the page to move.</param>
    /// <param name="toIndex">0-based destination index.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The PDF with the moved page as a byte array.</returns>
    public static Task<byte[]> MovePageAsync(byte[] pdfBytes, int fromIndex, int toIndex, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ArgumentOutOfRangeException.ThrowIfNegative(fromIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(toIndex);

        ct.ThrowIfCancellationRequested();
        return Task.Run(() => MovePage(pdfBytes, fromIndex, toIndex), ct);
    }

    /// <summary>
    /// Reverses the order of all pages in a PDF.
    /// </summary>
    /// <param name="pdfBytes">The source PDF as a byte array.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The PDF with reversed page order as a byte array.</returns>
    public static Task<byte[]> ReversePagesAsync(byte[] pdfBytes, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));

        ct.ThrowIfCancellationRequested();
        return Task.Run(() => ReversePages(pdfBytes), ct);
    }

    /// <summary>
    /// Extracts all images from a PDF document.
    /// </summary>
    /// <param name="pdfBytes">The source PDF as a byte array.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of <see cref="ExtractedImageInfo"/> objects, one per extracted image.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="pdfBytes"/> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="pdfBytes"/> is empty.</exception>
    /// <exception cref="OperationCanceledException">If the operation is cancelled.</exception>
    /// <exception cref="PdfExtractionException">If the native extraction operation fails.</exception>
    public static Task<List<ExtractedImageInfo>> ExtractImagesAsync(byte[] pdfBytes, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));

        ct.ThrowIfCancellationRequested();
        return Task.Run(() => ExtractImages(pdfBytes), ct);
    }

    /// <summary>
    /// Overlays one PDF on top of another using default options.
    /// </summary>
    /// <param name="basePdf">The base PDF as a byte array.</param>
    /// <param name="overlayPdf">The overlay PDF as a byte array.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The combined PDF as a byte array.</returns>
    public static Task<byte[]> OverlayAsync(byte[] basePdf, byte[] overlayPdf, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(basePdf);
        ArgumentNullException.ThrowIfNull(overlayPdf);
        if (basePdf.Length == 0)
            throw new ArgumentException("Base PDF bytes cannot be empty", nameof(basePdf));
        if (overlayPdf.Length == 0)
            throw new ArgumentException("Overlay PDF bytes cannot be empty", nameof(overlayPdf));

        ct.ThrowIfCancellationRequested();
        return Task.Run(() => Overlay(basePdf, overlayPdf), ct);
    }

    /// <summary>
    /// Splits a PDF into multiple PDFs according to the specified options.
    /// </summary>
    /// <param name="pdfBytes">The source PDF as a byte array.</param>
    /// <param name="options">Split options controlling how the document is divided.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of byte arrays, one per resulting chunk.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="pdfBytes"/> or <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="pdfBytes"/> is empty.</exception>
    /// <exception cref="OperationCanceledException">If the operation is cancelled.</exception>
    /// <exception cref="PdfExtractionException">If the native split operation fails.</exception>
    public static Task<List<byte[]>> SplitAsync(byte[] pdfBytes, PdfSplitOptions options, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(pdfBytes);
        ArgumentNullException.ThrowIfNull(options);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));

        ct.ThrowIfCancellationRequested();
        return Task.Run(() => SplitWithOptions(pdfBytes, options), ct);
    }

    /// <summary>
    /// Merges multiple PDFs into a single PDF, with optional per-input page range selection.
    /// </summary>
    /// <param name="inputs">Collection of <see cref="PdfMergeInput"/> entries to merge in order.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The merged PDF as a byte array.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="inputs"/> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="inputs"/> is empty.</exception>
    /// <exception cref="OperationCanceledException">If the operation is cancelled.</exception>
    /// <exception cref="PdfExtractionException">If the native merge operation fails.</exception>
    public static Task<byte[]> MergeAsync(IReadOnlyList<PdfMergeInput> inputs, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(inputs);
        if (inputs.Count == 0)
            throw new ArgumentException("At least one PDF is required for merge", nameof(inputs));

        ct.ThrowIfCancellationRequested();
        return Task.Run(() => MergeWithRanges(inputs), ct);
    }

    /// <summary>
    /// Rotates specific pages of a PDF by the specified number of degrees.
    /// </summary>
    /// <param name="pdfBytes">The source PDF as a byte array.</param>
    /// <param name="degrees">Rotation angle. Must be 0, 90, 180, or 270.</param>
    /// <param name="pages">Pages to rotate. Pass <see cref="PdfPageRange.All"/> to rotate all pages.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The rotated PDF as a byte array.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="pdfBytes"/> or <paramref name="pages"/> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="pdfBytes"/> is empty.</exception>
    /// <exception cref="OperationCanceledException">If the operation is cancelled.</exception>
    /// <exception cref="PdfExtractionException">If the native rotate operation fails or degrees is invalid.</exception>
    public static Task<byte[]> RotatePagesAsync(byte[] pdfBytes, int degrees, PdfPageRange pages, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(pdfBytes);
        ArgumentNullException.ThrowIfNull(pages);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));

        ct.ThrowIfCancellationRequested();
        return Task.Run(() => RotatePages(pdfBytes, degrees, pages), ct);
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

    private static byte[] ReorderPages(byte[] pdfBytes, int[] newOrder)
    {
        IntPtr pdfPtr = IntPtr.Zero;
        IntPtr outPtr = IntPtr.Zero;
        nuint outLen = 0;

        try
        {
            pdfPtr = Marshal.AllocHGlobal(pdfBytes.Length);
            Marshal.Copy(pdfBytes, 0, pdfPtr, pdfBytes.Length);

            var orderJson = JsonSerializer.Serialize(newOrder);

            var result = NativeMethods.oxidize_reorder_pages_bytes(
                pdfPtr, (nuint)pdfBytes.Length, orderJson, out outPtr, out outLen);

            ThrowIfError(result, "Failed to reorder pages");

            var length = (int)outLen;
            var output = new byte[length];
            Marshal.Copy(outPtr, output, 0, length);
            return output;
        }
        finally
        {
            if (pdfPtr != IntPtr.Zero) Marshal.FreeHGlobal(pdfPtr);
            if (outPtr != IntPtr.Zero) NativeMethods.oxidize_free_bytes(outPtr, outLen);
        }
    }

    private static byte[] SwapPages(byte[] pdfBytes, int pageA, int pageB)
    {
        IntPtr pdfPtr = IntPtr.Zero;
        IntPtr outPtr = IntPtr.Zero;
        nuint outLen = 0;

        try
        {
            pdfPtr = Marshal.AllocHGlobal(pdfBytes.Length);
            Marshal.Copy(pdfBytes, 0, pdfPtr, pdfBytes.Length);

            var result = NativeMethods.oxidize_swap_pages_bytes(
                pdfPtr, (nuint)pdfBytes.Length, (nuint)pageA, (nuint)pageB, out outPtr, out outLen);

            ThrowIfError(result, "Failed to swap pages");

            var length = (int)outLen;
            var output = new byte[length];
            Marshal.Copy(outPtr, output, 0, length);
            return output;
        }
        finally
        {
            if (pdfPtr != IntPtr.Zero) Marshal.FreeHGlobal(pdfPtr);
            if (outPtr != IntPtr.Zero) NativeMethods.oxidize_free_bytes(outPtr, outLen);
        }
    }

    private static byte[] MovePage(byte[] pdfBytes, int fromIndex, int toIndex)
    {
        IntPtr pdfPtr = IntPtr.Zero;
        IntPtr outPtr = IntPtr.Zero;
        nuint outLen = 0;

        try
        {
            pdfPtr = Marshal.AllocHGlobal(pdfBytes.Length);
            Marshal.Copy(pdfBytes, 0, pdfPtr, pdfBytes.Length);

            var result = NativeMethods.oxidize_move_page_bytes(
                pdfPtr, (nuint)pdfBytes.Length, (nuint)fromIndex, (nuint)toIndex, out outPtr, out outLen);

            ThrowIfError(result, "Failed to move page");

            var length = (int)outLen;
            var output = new byte[length];
            Marshal.Copy(outPtr, output, 0, length);
            return output;
        }
        finally
        {
            if (pdfPtr != IntPtr.Zero) Marshal.FreeHGlobal(pdfPtr);
            if (outPtr != IntPtr.Zero) NativeMethods.oxidize_free_bytes(outPtr, outLen);
        }
    }

    private static byte[] ReversePages(byte[] pdfBytes)
    {
        IntPtr pdfPtr = IntPtr.Zero;
        IntPtr outPtr = IntPtr.Zero;
        nuint outLen = 0;

        try
        {
            pdfPtr = Marshal.AllocHGlobal(pdfBytes.Length);
            Marshal.Copy(pdfBytes, 0, pdfPtr, pdfBytes.Length);

            var result = NativeMethods.oxidize_reverse_pages_bytes(
                pdfPtr, (nuint)pdfBytes.Length, out outPtr, out outLen);

            ThrowIfError(result, "Failed to reverse pages");

            var length = (int)outLen;
            var output = new byte[length];
            Marshal.Copy(outPtr, output, 0, length);
            return output;
        }
        finally
        {
            if (pdfPtr != IntPtr.Zero) Marshal.FreeHGlobal(pdfPtr);
            if (outPtr != IntPtr.Zero) NativeMethods.oxidize_free_bytes(outPtr, outLen);
        }
    }

    private static byte[] Overlay(byte[] basePdf, byte[] overlayPdf)
    {
        IntPtr basePtr = IntPtr.Zero;
        IntPtr overlayPtr = IntPtr.Zero;
        IntPtr outPtr = IntPtr.Zero;
        nuint outLen = 0;

        try
        {
            basePtr = Marshal.AllocHGlobal(basePdf.Length);
            Marshal.Copy(basePdf, 0, basePtr, basePdf.Length);

            overlayPtr = Marshal.AllocHGlobal(overlayPdf.Length);
            Marshal.Copy(overlayPdf, 0, overlayPtr, overlayPdf.Length);

            var result = NativeMethods.oxidize_overlay_pdf_bytes(
                basePtr, (nuint)basePdf.Length,
                overlayPtr, (nuint)overlayPdf.Length,
                out outPtr, out outLen);

            ThrowIfError(result, "Failed to overlay PDFs");

            var length = (int)outLen;
            var output = new byte[length];
            Marshal.Copy(outPtr, output, 0, length);
            return output;
        }
        finally
        {
            if (basePtr != IntPtr.Zero) Marshal.FreeHGlobal(basePtr);
            if (overlayPtr != IntPtr.Zero) Marshal.FreeHGlobal(overlayPtr);
            if (outPtr != IntPtr.Zero) NativeMethods.oxidize_free_bytes(outPtr, outLen);
        }
    }

    private static List<ExtractedImageInfo> ExtractImages(byte[] pdfBytes)
    {
        IntPtr pdfPtr = IntPtr.Zero;
        IntPtr jsonPtr = IntPtr.Zero;

        try
        {
            pdfPtr = Marshal.AllocHGlobal(pdfBytes.Length);
            Marshal.Copy(pdfBytes, 0, pdfPtr, pdfBytes.Length);

            var result = NativeMethods.oxidize_extract_images_bytes(
                pdfPtr,
                (nuint)pdfBytes.Length,
                out jsonPtr);

            ThrowIfError(result, "Failed to extract images from PDF");

            var json = Marshal.PtrToStringUTF8(jsonPtr) ?? "[]";
            var dtos = JsonSerializer.Deserialize<List<ExtractedImageDto>>(json) ?? [];

            var images = new List<ExtractedImageInfo>(dtos.Count);
            foreach (var dto in dtos)
            {
                images.Add(new ExtractedImageInfo
                {
                    PageNumber = dto.PageNumber,
                    ImageIndex = dto.ImageIndex,
                    Width = dto.Width,
                    Height = dto.Height,
                    Format = dto.Format,
                    ImageData = Convert.FromBase64String(dto.Data),
                });
            }

            return images;
        }
        finally
        {
            if (pdfPtr != IntPtr.Zero) Marshal.FreeHGlobal(pdfPtr);
            if (jsonPtr != IntPtr.Zero) NativeMethods.oxidize_free_string(jsonPtr);
        }
    }

    private static List<byte[]> SplitWithOptions(byte[] pdfBytes, PdfSplitOptions options)
    {
        IntPtr pdfPtr = IntPtr.Zero;
        IntPtr jsonPtr = IntPtr.Zero;

        try
        {
            pdfPtr = Marshal.AllocHGlobal(pdfBytes.Length);
            Marshal.Copy(pdfBytes, 0, pdfPtr, pdfBytes.Length);

            var optionsJson = options.ToJson();

            var result = NativeMethods.oxidize_split_pdf_bytes_with_options(
                pdfPtr,
                (nuint)pdfBytes.Length,
                optionsJson,
                out jsonPtr);

            ThrowIfError(result, "Failed to split PDF with options");

            var json = Marshal.PtrToStringUTF8(jsonPtr) ?? "[]";
            var base64Chunks = JsonSerializer.Deserialize<List<string>>(json)
                ?? new List<string>();

            var chunks = new List<byte[]>(base64Chunks.Count);
            foreach (var encoded in base64Chunks)
                chunks.Add(Convert.FromBase64String(encoded));

            return chunks;
        }
        finally
        {
            if (pdfPtr != IntPtr.Zero)
                Marshal.FreeHGlobal(pdfPtr);
            if (jsonPtr != IntPtr.Zero)
                NativeMethods.oxidize_free_string(jsonPtr);
        }
    }

    private static byte[] MergeWithRanges(IReadOnlyList<PdfMergeInput> inputs)
    {
        IntPtr outPtr = IntPtr.Zero;
        nuint outLen = 0;

        try
        {
            var inputObjects = inputs.Select(i => i.ToJsonObject()).ToArray();
            var inputsJson = JsonSerializer.Serialize(inputObjects);

            var result = NativeMethods.oxidize_merge_pdfs_with_ranges(inputsJson, out outPtr, out outLen);
            ThrowIfError(result, "Failed to merge PDFs with page ranges");

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

    private static byte[] RotatePages(byte[] pdfBytes, int degrees, PdfPageRange pages)
    {
        IntPtr pdfPtr = IntPtr.Zero;
        IntPtr outPtr = IntPtr.Zero;
        nuint outLen = 0;

        try
        {
            pdfPtr = Marshal.AllocHGlobal(pdfBytes.Length);
            Marshal.Copy(pdfBytes, 0, pdfPtr, pdfBytes.Length);

            string? pagesJson = pages is PdfPageRange.All
                ? null
                : JsonSerializer.Serialize(pages.ToJsonObject());

            var result = NativeMethods.oxidize_rotate_pages_bytes(
                pdfPtr,
                (nuint)pdfBytes.Length,
                degrees,
                pagesJson,
                out outPtr,
                out outLen);

            ThrowIfError(result, $"Failed to rotate selected pages by {degrees} degrees");

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

    // ── Private DTOs ──────────────────────────────────────────────────────────

    private sealed class ExtractedImageDto
    {
        [JsonPropertyName("page_number")]
        public int PageNumber { get; init; }

        [JsonPropertyName("image_index")]
        public int ImageIndex { get; init; }

        [JsonPropertyName("width")]
        public uint Width { get; init; }

        [JsonPropertyName("height")]
        public uint Height { get; init; }

        [JsonPropertyName("format")]
        public string Format { get; init; } = string.Empty;

        [JsonPropertyName("data")]
        public string Data { get; init; } = string.Empty;
    }

}
