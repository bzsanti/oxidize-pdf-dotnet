using System.Reflection;

namespace OxidizePdf.NET.Tests;

/// <summary>
/// Utility class for accessing test fixture files with dynamic path resolution
/// </summary>
public static class TestFixtures
{
    /// <summary>
    /// Gets the project root directory by navigating from the assembly location
    /// </summary>
    private static string GetProjectRoot()
    {
        var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (assemblyPath == null)
        {
            throw new InvalidOperationException("Cannot determine assembly location");
        }

        // Navigate from bin/Debug/net9.0 to project root (3 levels up)
        var projectRoot = Path.GetFullPath(Path.Combine(assemblyPath, "..", "..", ".."));
        return projectRoot;
    }

    /// <summary>
    /// Gets the full path to a test fixture file
    /// </summary>
    /// <param name="filename">Name of the fixture file (e.g., "sample.pdf")</param>
    /// <returns>Full path to the fixture file</returns>
    public static string GetFixturePath(string filename)
    {
        var fixturesDir = Path.Combine(GetProjectRoot(), "fixtures");
        return Path.Combine(fixturesDir, filename);
    }

    /// <summary>
    /// Gets the fixtures directory path
    /// </summary>
    public static string GetFixturesDirectory()
    {
        return Path.Combine(GetProjectRoot(), "fixtures");
    }

    /// <summary>
    /// Checks if a fixture file exists
    /// </summary>
    public static bool FixtureExists(string filename)
    {
        return File.Exists(GetFixturePath(filename));
    }
}
