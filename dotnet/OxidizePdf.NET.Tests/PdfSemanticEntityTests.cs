using System.Text.Json;
using OxidizePdf.NET;

namespace OxidizePdf.NET.Tests;

/// <summary>
/// Behavioral tests for DOC-021 semantic entities. Entities are marked on a
/// document and the exported JSON / JSON-LD markup is parsed and asserted.
/// (Per upstream, entities are an in-memory + export feature, not embedded in
/// the saved PDF.)
/// </summary>
public class PdfSemanticEntityTests
{
    [Fact]
    public void MarkEntity_PlainJsonExport_CarriesContentAndType()
    {
        using var doc = new PdfDocument();
        doc.MarkEntity("inv_num_1", "invoiceNumber", 100, 700, 150, 20, page: 1)
           .SetEntityContent("inv_num_1", "INV-2024-001")
           .SetEntityConfidence("inv_num_1", 0.97f)
           .AddEntityMetadata("inv_num_1", "currency", "USD");

        var json = doc.ExportSemanticEntitiesJson();

        using var parsed = JsonDocument.Parse(json);
        var entity = parsed.RootElement[0];
        Assert.Equal("inv_num_1", entity.GetProperty("id").GetString());
        Assert.Equal("INV-2024-001", entity.GetProperty("content").GetString());
        // Entity type serializes (camelCase) somewhere in the payload.
        Assert.Contains("invoiceNumber", json);
    }

    [Fact]
    public void MarkEntity_JsonLdExport_HasSchemaOrgContextAndId()
    {
        using var doc = new PdfDocument();
        doc.MarkEntity("inv_num_1", "invoiceNumber", 100, 700, 150, 20, page: 1)
           .SetEntityContent("inv_num_1", "INV-2024-001");

        var jsonLd = doc.ExportSemanticEntitiesJsonLd();

        using var parsed = JsonDocument.Parse(jsonLd);
        Assert.Equal("https://schema.org", parsed.RootElement.GetProperty("@context").GetString());
        Assert.True(parsed.RootElement.TryGetProperty("hasPart", out var parts));
        Assert.True(parts.GetArrayLength() >= 1);
        Assert.Contains("inv_num_1", jsonLd);
    }

    [Fact]
    public void RelateEntities_RecordedAndUnknownIdThrows()
    {
        using var doc = new PdfDocument();
        doc.MarkEntity("a", "text", 0, 0, 10, 10, page: 1)
           .MarkEntity("b", "text", 0, 20, 10, 10, page: 1)
           .RelateEntities("a", "b", "contains");

        // The relationship is recorded on entity "a".
        var json = doc.ExportSemanticEntitiesJson();
        Assert.Contains("\"b\"", json);

        Assert.Throws<PdfExtractionException>(() => doc.RelateEntities("a", "missing", "contains"));
    }

    [Fact]
    public void SetEntityContent_UnknownEntity_Throws()
    {
        using var doc = new PdfDocument();
        Assert.Throws<PdfExtractionException>(() => doc.SetEntityContent("nope", "x"));
    }

    [Fact]
    public void MarkEntity_NullId_Throws()
    {
        using var doc = new PdfDocument();
        Assert.Throws<ArgumentNullException>(() => doc.MarkEntity(null!, "text", 0, 0, 1, 1, 1));
    }
}
