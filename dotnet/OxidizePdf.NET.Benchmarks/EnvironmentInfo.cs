using System.Runtime.InteropServices;

namespace OxidizePdf.NET.Benchmarks;

/// <summary>One library's identity in the environment block.</summary>
public sealed record AdapterInfo(string Name, string License, string Version);

/// <summary>Reproducibility context written into results.json.</summary>
public sealed record EnvironmentInfo(
    string Machine,
    string Os,
    string DotnetVersion,
    string CorpusPath,
    int FileCount,
    IReadOnlyList<AdapterInfo> Adapters)
{
    public static EnvironmentInfo Capture(
        string corpusPath, int fileCount, IReadOnlyList<IPdfExtractorAdapter> adapters)
    {
        return new EnvironmentInfo(
            Machine: Environment.MachineName,
            Os: RuntimeInformation.OSDescription,
            DotnetVersion: RuntimeInformation.FrameworkDescription,
            CorpusPath: corpusPath,
            FileCount: fileCount,
            Adapters: adapters.Select(a => new AdapterInfo(a.Name, a.License, a.Version)).ToList());
    }
}
