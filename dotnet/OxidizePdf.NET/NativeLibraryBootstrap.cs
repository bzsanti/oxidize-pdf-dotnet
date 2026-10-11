using System.Runtime.CompilerServices;

namespace OxidizePdf.NET;

// Every P/Invoke entry point in this assembly needs the runtime-specific resolver,
// including APIs declared outside NativeMethods and called before PdfDocument.
internal static class NativeLibraryBootstrap
{
    // Library-wide interop initialization is intentional: relying on the first
    // NativeMethods call leaves independent static API entry points uninitialized.
#pragma warning disable CA2255
    [ModuleInitializer]
    internal static void Initialize() => RuntimeHelpers.RunClassConstructor(typeof(NativeMethods).TypeHandle);
#pragma warning restore CA2255
}
