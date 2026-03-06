using System.Runtime.InteropServices;

namespace OxidizePdf.NET;

/// <summary>
/// P/Invoke declarations for oxidize-pdf FFI library
/// </summary>
internal static class NativeMethods
{
    private const string LibraryName = "oxidize_pdf_ffi";

    /// <summary>
    /// Error codes returned by native functions
    /// </summary>
    internal enum ErrorCode
    {
        Success = 0,
        NullPointer = 1,
        InvalidUtf8 = 2,
        PdfParseError = 3,
        AllocationError = 4,
        SerializationError = 5,
    }

    /// <summary>
    /// Chunk options for text extraction
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct ChunkOptionsNative
    {
        public nuint MaxChunkSize;
        public nuint Overlap;
        [MarshalAs(UnmanagedType.I1)]
        public bool PreserveSentenceBoundaries;
        [MarshalAs(UnmanagedType.I1)]
        public bool IncludeMetadata;
    }

    /// <summary>
    /// Free a C string allocated by Rust
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void oxidize_free_string(IntPtr ptr);

    /// <summary>
    /// Extract plain text from PDF bytes
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_extract_text(
        IntPtr pdfBytes,
        nuint pdfLen,
        out IntPtr outText
    );

    /// <summary>
    /// Extract text chunks optimized for RAG/LLM pipelines
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_extract_chunks(
        IntPtr pdfBytes,
        nuint pdfLen,
        ref ChunkOptionsNative options,
        out IntPtr outJson
    );

    /// <summary>
    /// Get version string
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_version(out IntPtr outVersion);

    /// <summary>
    /// Get the last error message from native library
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_get_last_error(out IntPtr outError);

    /// <summary>
    /// Get the number of pages in a PDF
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_get_page_count(
        IntPtr pdfBytes,
        nuint pdfLen,
        out nuint outCount
    );

    /// <summary>
    /// Extract plain text from a specific page of a PDF
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_extract_text_from_page(
        IntPtr pdfBytes,
        nuint pdfLen,
        nuint pageNumber,
        out IntPtr outText
    );

    /// <summary>
    /// Extract text chunks from a specific page of a PDF
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_extract_chunks_from_page(
        IntPtr pdfBytes,
        nuint pdfLen,
        nuint pageNumber,
        ref ChunkOptionsNative options,
        out IntPtr outJson
    );

    /// <summary>
    /// Gets the last error message from the native library and clears it
    /// </summary>
    /// <returns>The error message, or null if no error was set</returns>
    internal static string? GetLastError()
    {
        IntPtr errorPtr = IntPtr.Zero;
        try
        {
            var result = oxidize_get_last_error(out errorPtr);
            if (result != (int)ErrorCode.Success || errorPtr == IntPtr.Zero)
                return null;

            return Marshal.PtrToStringUTF8(errorPtr);
        }
        finally
        {
            if (errorPtr != IntPtr.Zero)
                oxidize_free_string(errorPtr);
        }
    }

    /// <summary>
    /// Load native library for current platform
    /// </summary>
    static NativeMethods()
    {
        NativeLibrary.SetDllImportResolver(typeof(NativeMethods).Assembly, DllImportResolver);
    }

    private static IntPtr DllImportResolver(string libraryName, System.Reflection.Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != LibraryName)
            return IntPtr.Zero;

        // Determine platform-specific library name
        string fileName;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            fileName = $"{libraryName}.dll";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            fileName = $"lib{libraryName}.so";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            fileName = $"lib{libraryName}.dylib";
        else
            throw new PlatformNotSupportedException($"Unsupported platform: {RuntimeInformation.OSDescription}");

        // Determine runtime identifier
        string rid;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            rid = "win-x64";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            rid = "linux-x64";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            rid = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
        else
            throw new PlatformNotSupportedException($"Unsupported platform: {RuntimeInformation.OSDescription}");

        // Try to load from runtimes folder
        var baseDirectory = AppContext.BaseDirectory;
        var libraryPath = Path.Combine(baseDirectory, "runtimes", rid, "native", fileName);

        if (File.Exists(libraryPath))
        {
            if (NativeLibrary.TryLoad(libraryPath, out var handle))
                return handle;
        }

        // Try to load from current directory (development scenario)
        libraryPath = Path.Combine(baseDirectory, fileName);
        if (File.Exists(libraryPath))
        {
            if (NativeLibrary.TryLoad(libraryPath, out var handle))
                return handle;
        }

        throw new DllNotFoundException(
            $"Unable to load native library '{fileName}' for runtime '{rid}'. " +
            $"Searched paths: {baseDirectory}");
    }
}
