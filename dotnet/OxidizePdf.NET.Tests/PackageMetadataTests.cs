namespace OxidizePdf.NET.Tests;

/// <summary>
/// Tests for package metadata (icon, README, etc.)
/// </summary>
public class PackageMetadataTests
{
    [Fact]
    public void PackageIcon_FileExists()
    {
        // Arrange
        var projectRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "OxidizePdf.NET"
        ));
        var iconPath = Path.Combine(projectRoot, "icon.png");

        // Assert
        Assert.True(File.Exists(iconPath), $"Icon file not found at: {iconPath}");
    }
}
