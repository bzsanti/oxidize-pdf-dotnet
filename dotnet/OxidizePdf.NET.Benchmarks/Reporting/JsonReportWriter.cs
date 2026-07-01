using System.Text.Json;
using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Benchmarks.Reporting;

/// <summary>Writes the raw, auditable record: env + every FileResult + aggregates.</summary>
public static class JsonReportWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static void Write(
        string path, EnvironmentInfo env, IReadOnlyList<FileResult> results, Aggregates aggregates)
    {
        var payload = new
        {
            environment = env,
            results,
            aggregates,
        };
        File.WriteAllText(path, JsonSerializer.Serialize(payload, Options));
    }
}
