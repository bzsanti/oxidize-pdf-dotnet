using System.Runtime.InteropServices;

namespace OxidizePdf.NET.Tests;

/// <summary>
/// Tests for native binary presence and packaging
/// </summary>
public class NativeBinariesTests
{
    private static string GetProjectRoot()
    {
        var baseDir = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "OxidizePdf.NET"));
    }

    private static (string rid, string binaryName) GetCurrentPlatformInfo()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return ("win-x64", "oxidize_pdf_ffi.dll");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var rid = RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                ? "osx-arm64"
                : "osx-x64";
            return (rid, "liboxidize_pdf_ffi.dylib");
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return ("linux-x64", "liboxidize_pdf_ffi.so");

        throw new PlatformNotSupportedException("Unsupported platform");
    }

    [Fact]
    public void NativeBinary_ExistsForCurrentPlatform()
    {
        // Arrange
        var (rid, binaryName) = GetCurrentPlatformInfo();
        var runtimePath = Path.Combine(
            GetProjectRoot(),
            "runtimes", rid, "native", binaryName
        );

        // Assert
        Assert.True(File.Exists(runtimePath),
            $"Native binary for {rid} not found at: {runtimePath}");
    }

    [Fact]
    public void NativeBinary_HasNonZeroSize()
    {
        // Arrange
        var (rid, binaryName) = GetCurrentPlatformInfo();
        var runtimePath = Path.Combine(
            GetProjectRoot(),
            "runtimes", rid, "native", binaryName
        );

        // Skip if binary doesn't exist (covered by other test)
        if (!File.Exists(runtimePath))
        {
            Assert.Fail($"Binary not found at {runtimePath} - cannot verify size");
            return;
        }

        // Act
        var fileInfo = new FileInfo(runtimePath);

        // Assert
        Assert.True(fileInfo.Length > 0, $"Native binary at {runtimePath} has zero size");
    }
}
