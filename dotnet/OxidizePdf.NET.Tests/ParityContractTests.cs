using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace OxidizePdf.NET.Tests;

public class ParityContractTests
{
    private static string Contract => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "parity-contract.json"));
    private static string Cargo => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "parity-native-Cargo.toml"));

    private static List<string> Validate(string contract, string cargo)
    {
        using var document = JsonDocument.Parse(contract);
        var root = document.RootElement;
        var errors = new List<string>();
        var assembly = typeof(PdfTextExtraction).Assembly;
        var pin = Regex.Match(cargo, "oxidize-pdf\\s*=\\s*\\{\\s*version\\s*=\\s*\"=([^\"]+)\"");
        if (!pin.Success || pin.Groups[1].Value != root.GetProperty("core").GetString()) errors.Add("Core pin changed without a parity audit");
        foreach (var entry in root.GetProperty("callables").EnumerateObject())
        {
            var parts = entry.Name.Split('.');
            var type = assembly.GetType("OxidizePdf.NET." + parts[0]);
            var expected = entry.Value.EnumerateArray().Select(v => v.GetString()).ToArray();
            var matches = type?.GetMethods().Where(m => m.Name == parts[1]).Any(m => m.GetParameters().Select(p => p.Name).SequenceEqual(expected)) ?? false;
            if (!matches) errors.Add("Missing callable/options: " + entry.Name);
        }
        foreach (var entry in root.GetProperty("properties").EnumerateObject())
        {
            var type = assembly.GetType("OxidizePdf.NET." + entry.Name);
            foreach (var property in entry.Value.EnumerateArray())
                if (type?.GetProperty(property.GetString()!) is null) errors.Add("Missing result property: " + entry.Name + "." + property.GetString());
        }
        foreach (var entry in root.GetProperty("defaults").EnumerateObject())
        {
            var type = assembly.GetType("OxidizePdf.NET." + entry.Name);
            if (type is null) { errors.Add("Missing options: " + entry.Name); continue; }
            var instance = Activator.CreateInstance(type);
            foreach (var property in entry.Value.EnumerateObject())
            {
                var member = type.GetProperty(property.Name);
                if (member is null || !JsonElement.DeepEquals(JsonSerializer.SerializeToElement(member.GetValue(instance)), property.Value))
                    errors.Add("Changed option/default: " + entry.Name + "." + property.Name);
            }
        }
        foreach (var entry in root.GetProperty("behavior_tests").EnumerateArray())
        {
            var parts = entry.GetString()!.Split('.');
            var type = typeof(ParityContractTests).Assembly.GetType("OxidizePdf.NET.Tests." + parts[0]);
            var method = type?.GetMethod(parts[1]);
            if (method is null || !method.GetCustomAttributes(typeof(FactAttribute), true).Any()) errors.Add("Missing behavior test: " + entry.GetString());
        }
        return errors;
    }

    [Fact]
    public void AuditedOptionsResultsDefaultsAndBehaviorTestsRemainAvailable()
        => Assert.Empty(Validate(Contract, Cargo));

    [Theory]
    [InlineData("option")]
    [InlineData("result")]
    [InlineData("default")]
    [InlineData("test")]
    [InlineData("core")]
    public void GateRejectsContractRegressions(string mutation)
    {
        var root = JsonNode.Parse(Contract)!;
        string cargo = Cargo;
        switch (mutation)
        {
            case "option": root["callables"]!["PdfTextExtraction.RecoverAsync"]![1] = "missingBudget"; break;
            case "result": root["properties"]!["PdfExtractedPage"]![2] = "MissingDiagnostics"; break;
            case "default": root["defaults"]!["PdfTextExtractionOptions"]!["NormalizeNonBreakingSpaces"] = true; break;
            case "test": root["behavior_tests"]![0] = "SignatureSlotTests.MissingTest"; break;
            case "core": cargo = cargo.Replace("=" + root["core"]!.GetValue<string>(), "=0.0.0"); break;
        }
        Assert.NotEmpty(Validate(root.ToJsonString(), cargo));
    }
}
