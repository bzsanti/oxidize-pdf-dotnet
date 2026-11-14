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
    internal static extern int oxidize_generation_save([In] byte[] input, nuint length, GenerationCallback callback, out IntPtr output);
}
