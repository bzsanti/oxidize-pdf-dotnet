using System.IO.Compression;
using System.Text;

namespace OxidizePdf.NET.Tests.TestHelpers;

/// <summary>
/// Helpers for inspecting raw PDF bytes in integration tests.
/// Decompresses FlateDecode content streams so tests can search for
/// PDF operators without relying only on resource dictionary text.
/// </summary>
public static class ContentStreamHelper
{
    /// <summary>
    /// Returns the PDF as a Latin-1 string for plain-text resource-dict searches.
    /// Color-space resource entries live in the page resource dict, which is not
    /// compressed, so this is sufficient for /ColorSpace dict assertions.
    /// </summary>
    public static string ToLatin1(byte[] pdfBytes) =>
        Encoding.Latin1.GetString(pdfBytes);

    /// <summary>
    /// Locates the first content stream in <paramref name="pdfBytes"/> and
    /// returns its decompressed text.
    ///
    /// Strategy: find the byte sequence "stream\r\n" or "stream\n", advance past it,
    /// read until "endstream", strip the two-byte zlib header (0x78 0x9C or similar),
    /// and decompress with <see cref="DeflateStream"/>.
    ///
    /// Returns null if no compressed stream is found (uncompressed PDFs are handled
    /// by returning the raw stream bytes as Latin-1 text instead).
    /// </summary>
    public static string? DecompressFirstContentStream(byte[] pdfBytes) =>
        DecompressStreamAt(pdfBytes, 0);

    /// <summary>
    /// Finds the Nth content stream (0-based) and returns its decompressed text.
    /// Useful when a PDF contains auxiliary streams (e.g. embedded ICC profiles) before
    /// the page content stream — the caller can skip those by specifying a higher index.
    /// Returns null if fewer than <paramref name="streamIndex"/> + 1 streams are present.
    /// </summary>
    public static string? DecompressContentStreamAt(byte[] pdfBytes, int streamIndex) =>
        DecompressStreamAt(pdfBytes, streamIndex);

    private static string? DecompressStreamAt(byte[] pdfBytes, int streamIndex)
    {
        var streamMarker = Encoding.ASCII.GetBytes("stream");
        var endMarker    = Encoding.ASCII.GetBytes("endstream");

        int searchFrom = 0;
        for (int i = 0; i <= streamIndex; i++)
        {
            int streamStart = IndexOf(pdfBytes, streamMarker, searchFrom);
            if (streamStart < 0) return null;

            int dataStart = streamStart + streamMarker.Length;
            if (dataStart < pdfBytes.Length && pdfBytes[dataStart] == '\r') dataStart++;
            if (dataStart < pdfBytes.Length && pdfBytes[dataStart] == '\n') dataStart++;

            int streamEnd = IndexOf(pdfBytes, endMarker, dataStart);
            if (streamEnd < 0) return null;

            int dataEnd = streamEnd;
            while (dataEnd > dataStart && (pdfBytes[dataEnd - 1] == '\r' || pdfBytes[dataEnd - 1] == '\n'))
                dataEnd--;

            if (i == streamIndex)
            {
                var streamBytes = pdfBytes[dataStart..dataEnd];
                if (streamBytes.Length >= 2 && streamBytes[0] == 0x78)
                {
                    using var compressed = new MemoryStream(streamBytes, 2, streamBytes.Length - 2);
                    using var deflate    = new DeflateStream(compressed, CompressionMode.Decompress);
                    using var output     = new MemoryStream();
                    deflate.CopyTo(output);
                    return Encoding.Latin1.GetString(output.ToArray());
                }
                return Encoding.Latin1.GetString(streamBytes);
            }

            // Advance past this stream's endstream marker so next search starts after it.
            searchFrom = streamEnd + endMarker.Length;
        }
        return null;
    }

    private static int IndexOf(byte[] haystack, byte[] needle, int startIndex)
    {
        for (int i = startIndex; i <= haystack.Length - needle.Length; i++)
        {
            if (haystack.AsSpan(i, needle.Length).SequenceEqual(needle))
                return i;
        }
        return -1;
    }
}
