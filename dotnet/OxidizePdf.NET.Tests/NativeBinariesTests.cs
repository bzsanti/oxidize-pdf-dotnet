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

    [Fact]
    public void LinuxBinary_ExistsInRuntimes()
    {
        // Arrange
        var runtimePath = Path.Combine(
            GetProjectRoot(),
            "runtimes", "linux-x64", "native", "liboxidize_pdf_ffi.so"
        );

        // Assert
        Assert.True(File.Exists(runtimePath), $"Linux binary not found at: {runtimePath}");
    }
}
