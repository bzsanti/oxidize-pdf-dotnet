using System.Runtime.InteropServices;

namespace OxidizePdf.NET;

internal static partial class NativeMethods
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate IntPtr GenerationCallback(uint ordinal);
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint oxidize_generation_capabilities();
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr oxidize_generation_clone_page(PageSafeHandle page);
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_generation_create([In] byte[] input, nuint length, GenerationCallback callback, out IntPtr output);
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_generation_save(GenerationSafeHandle handle, [In] byte[] path, nuint length, out IntPtr output);
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void oxidize_generation_free(IntPtr handle);
}
