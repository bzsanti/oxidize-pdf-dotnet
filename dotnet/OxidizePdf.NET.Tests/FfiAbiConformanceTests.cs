using System.Reflection;
using System.Runtime.InteropServices;

namespace OxidizePdf.NET.Tests;

/// <summary>
/// Guards the FFI ABI contract: every <c>[DllImport]</c> declared in
/// <see cref="OxidizePdf.NET.NativeMethods"/> must resolve to a symbol actually
/// exported by the native library. Catches signature/name drift (issue #56)
/// — e.g. an upstream bump that renames or removes an export — at test time
/// instead of as a runtime <see cref="DllNotFoundException"/> (or silent stack
/// corruption) on a consumer's machine.
/// </summary>
public class FfiAbiConformanceTests
{
    private static (string rid, string binaryName) GetCurrentPlatformInfo()
    {
        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            _ => throw new PlatformNotSupportedException(
                $"Unsupported process architecture: {RuntimeInformation.ProcessArchitecture}"),
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return ($"win-{arch}", "oxidize_pdf_ffi.dll");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return ($"osx-{arch}", "liboxidize_pdf_ffi.dylib");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var libc = RuntimeInformation.RuntimeIdentifier
                .Contains("musl", StringComparison.OrdinalIgnoreCase) ? "musl-" : "";
            return ($"linux-{libc}{arch}", "liboxidize_pdf_ffi.so");
        }

        throw new PlatformNotSupportedException("Unsupported platform");
    }

    private static string NativeLibraryPath()
    {
        var (rid, binaryName) = GetCurrentPlatformInfo();
        var projectRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "OxidizePdf.NET"));
        return Path.Combine(projectRoot, "runtimes", rid, "native", binaryName);
    }

    [Fact]
    public void Every_DllImport_resolves_to_an_exported_native_symbol()
    {
        // Entry point = explicit EntryPoint, else the method name.
        var entryPoints = typeof(NativeMethods)
            .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(m => (method: m, attr: m.GetCustomAttribute<DllImportAttribute>()))
            .Where(x => x.attr is not null)
            .Select(x => string.IsNullOrEmpty(x.attr!.EntryPoint) ? x.method.Name : x.attr.EntryPoint)
            .Distinct()
            .ToList();

        Assert.NotEmpty(entryPoints);

        var libPath = NativeLibraryPath();
        Assert.True(File.Exists(libPath), $"Native library not found at {libPath}");

        var handle = NativeLibrary.Load(libPath);
        try
        {
            // Mechanism sanity check: a bogus symbol must NOT resolve, proving the
            // assertion below can actually fail on drift.
            Assert.False(
                NativeLibrary.TryGetExport(handle, "oxidize_this_symbol_does_not_exist", out _),
                "TryGetExport resolved a nonexistent symbol — detection is broken.");

            var missing = entryPoints
                .Where(ep => !NativeLibrary.TryGetExport(handle, ep, out _))
                .OrderBy(ep => ep)
                .ToList();

            Assert.True(
                missing.Count == 0,
                $"{missing.Count} DllImport entry point(s) have no matching native export "
                + $"(ABI drift): {string.Join(", ", missing)}");
        }
        finally
        {
            NativeLibrary.Free(handle);
        }
    }
}
