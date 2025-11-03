namespace OxidizePdf.NET.Tests;

/// <summary>
/// Tests for validating package icon format and properties
/// </summary>
public class PackageIconValidationTests
{
    private static string GetIconPath()
    {
        var projectRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "OxidizePdf.NET"
        ));
        return Path.Combine(projectRoot, "icon.png");
    }

    [Fact]
    public void PackageIcon_IsValidPng()
    {
        // Arrange
        var iconPath = GetIconPath();

        // Act
        using var stream = File.OpenRead(iconPath);
        var header = new byte[8];
        var bytesRead = stream.Read(header, 0, 8);

        // Assert - Verify PNG magic bytes
        var pngSignature = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        Assert.Equal(8, bytesRead);
        Assert.Equal(pngSignature, header);
    }

    [Fact]
    public void PackageIcon_HasReasonableSize()
    {
        // Arrange
        var iconPath = GetIconPath();
        var fileInfo = new FileInfo(iconPath);

        // Assert - Should be between 100 bytes and 50KB
        Assert.True(fileInfo.Length > 100, "Icon file is too small (possibly corrupt)");
        Assert.True(fileInfo.Length < 50 * 1024, "Icon file is too large (should be optimized)");
    }
}
