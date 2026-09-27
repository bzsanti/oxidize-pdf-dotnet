using System.Text.Json;
using OxidizePdf.NET.Models;

namespace OxidizePdf.NET.Tests;

public class TaggedEditingTests
{
    private static byte[] Pdf(bool tagged = true, bool encrypt = false)
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        var mcid = page.BeginMarkedContent("P");
        page.DrawTextAt(StandardFont.Helvetica, 12, 50, 700, "First");
        page.EndMarkedContent();
        page.BeginMarkedContent("P");
        page.DrawTextAt(StandardFont.Helvetica, 12, 50, 650, "Second");
        page.EndMarkedContent();
        doc.AddPage(page);
        if (tagged)
        {
            var tree = new PdfStructureTree();
            var root = tree.AddRoot("Document");
            tree.AddChild(root, "P", lang: "en-US", mcids: [(0, mcid)]);
            doc.SetStructureTree(tree);
        }
        if (encrypt) doc.Encrypt("user", "owner");
        return doc.SaveToBytes();
    }

    [Fact]
    public async Task InspectionExposesStructureDictionariesAndMissingStructureFinding()
    {
        var report = await PdfTaggedOperations.InspectAsync(Pdf());
        Assert.True(report.Tagged);
        Assert.NotNull(report.StructureTreeRoot);
        Assert.Equal(2, report.Elements.Length);
        var paragraph = Assert.Single(report.Elements, e => e.StructureType == "P");
        Assert.Equal("en-US", paragraph.Language);
        Assert.Contains("S", paragraph.DictionaryKeys);
        Assert.Equal("Name", paragraph.Dictionary["S"].GetProperty("type").GetString());
        Assert.Contains(report.ContentMcids, c => c.Mcids.Contains(0) && c.Mcids.Contains(1));
        var untagged = await PdfTaggedOperations.InspectAsync(Pdf(tagged: false));
        Assert.False(untagged.Tagged);
        Assert.Contains(untagged.Findings, f => f.Code == "MissingStructureTree");
    }

    [Fact]
    public async Task PlanMatchesEditAndAttributesCanBeSetAndRemoved()
    {
        var pdf = Pdf();
        var before = await PdfTaggedOperations.InspectAsync(pdf);
        var paragraph = before.Elements.Single(e => e.StructureType == "P");
        PdfTaggedMutation[] changes = [new PdfTaggedMutation.SetElementAttribute(paragraph.Object, "Alt", new("Text", "Descripción"))];
        var plan = await PdfTaggedOperations.PlanAsync(pdf, changes);
        var edited = await PdfTaggedOperations.EditAsync(pdf, changes);
        Assert.True(edited.PdfBytes.AsSpan().StartsWith(pdf));
        Assert.Equal(JsonSerializer.Serialize(plan), JsonSerializer.Serialize(edited.Plan));
        Assert.Equal("Descripción", edited.ValidationAfter.Elements.Single(e => e.Object == paragraph.Object).AlternateText);
        var removed = await PdfTaggedOperations.EditAsync(edited.PdfBytes,
            [new PdfTaggedMutation.SetElementAttribute(paragraph.Object, "Alt", null)]);
        Assert.Null(removed.ValidationAfter.Elements.Single(e => e.Object == paragraph.Object).AlternateText);
    }

    [Fact]
    public async Task CreateAndReparentPreserveIdentitiesAndRejectCycles()
    {
        var pdf = Pdf();
        var before = await PdfTaggedOperations.InspectAsync(pdf);
        var parent = before.Elements.Single(e => e.StructureType == "Document").Object;
        var child = before.Elements.Single(e => e.StructureType == "P").Object;
        var created = await PdfTaggedOperations.EditAsync(pdf,
            [new PdfTaggedMutation.CreateElement(parent, "Sect", new() { ["T"] = new("Text", "Section") })]);
        var section = created.ValidationAfter.Elements.Single(e => e.StructureType == "Sect");
        Assert.Equal("Section", section.Title);
        var moved = await PdfTaggedOperations.EditAsync(created.PdfBytes,
            [new PdfTaggedMutation.ReparentElement(child, section.Object)]);
        Assert.Equal(section.Object, moved.ValidationAfter.Elements.Single(e => e.Object == child).Parent);
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfTaggedOperations.EditAsync(moved.PdfBytes,
            [new PdfTaggedMutation.ReparentElement(section.Object, child)]));
    }

    [Fact]
    public async Task AssociateExistingMcidUpdatesParentTree()
    {
        var pdf = Pdf();
        var before = await PdfTaggedOperations.InspectAsync(pdf);
        var child = before.Elements.Single(e => e.StructureType == "P").Object;
        var page = Assert.Single(before.ContentMcids).Context;
        var edited = await PdfTaggedOperations.EditAsync(pdf, [new PdfTaggedMutation.AssociateMcid(child, page, 1)]);
        Assert.Equal(2u, edited.ValidationAfter.Elements.Single(e => e.Object == child).MarkedContentCount);
        Assert.Contains(edited.Plan.ChangedObjects, o => o.Kind == "ParentTree");
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfTaggedOperations.EditAsync(pdf,
            [new PdfTaggedMutation.AssociateMcid(child, page, 999)]));
    }

    [Fact]
    public async Task ParentTreeEntryCanBeRemovedAndRestored()
    {
        var pdf = Pdf();
        var before = await PdfTaggedOperations.InspectAsync(pdf);
        var entry = Assert.Single(before.ParentTree);
        var removed = await PdfTaggedOperations.EditAsync(pdf, [new PdfTaggedMutation.SetParentTreeEntry(entry.Key, null)]);
        Assert.DoesNotContain(removed.ValidationAfter.ParentTree, e => e.Key == entry.Key);
        var value = new PdfObjectValue(entry.Value.GetProperty("type").GetString()!, entry.Value.GetProperty("value"));
        var restored = await PdfTaggedOperations.EditAsync(removed.PdfBytes, [new PdfTaggedMutation.SetParentTreeEntry(entry.Key, value)]);
        Assert.Equal(entry.Value.GetRawText(), Assert.Single(restored.ValidationAfter.ParentTree).Value.GetRawText());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task CertificationRejectsStructuralEdits(byte permission)
    {
        using var prepared = await PdfSigning.PrepareAsync(Pdf(), new() { Certification = permission });
        // Synthetic DER: tests policy parsing, not cryptographic signature validity.
        var pdf = prepared.Complete([0x30, 3, 2, 1, 1]);
        var inspected = await PdfTaggedOperations.InspectAsync(pdf);
        PdfTaggedMutation[] changes = [new PdfTaggedMutation.SetElementAttribute(inspected.Elements[0].Object,
            "T", new("Text", "changed"))];
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfTaggedOperations.PlanAsync(pdf, changes));
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfTaggedOperations.EditAsync(pdf, changes));
    }

    [Fact]
    public async Task InvalidStructuralKeysLimitsAndEncryptionAreRejected()
    {
        var pdf = Pdf();
        var before = await PdfTaggedOperations.InspectAsync(pdf);
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfTaggedOperations.EditAsync(pdf,
            [new PdfTaggedMutation.SetElementAttribute(before.Elements[0].Object, "K", new("Null"))]));
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfTaggedOperations.InspectAsync(pdf, new() { MaxObjects = 1 }));
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfTaggedOperations.InspectAsync(Pdf(encrypt: true)));
    }
}
