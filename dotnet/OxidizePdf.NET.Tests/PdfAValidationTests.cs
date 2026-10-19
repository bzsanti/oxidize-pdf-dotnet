using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace OxidizePdf.NET.Tests;

public class PdfAValidationTests
{
    // Authored independently of the PDF writer: minimal blank page and XMP.
    // These test the core's acceptance set, not independent ISO certification.
    private static byte[] Pdf(string part = "1", string conformance = "B", string version = "1.4",
        string catalog = "", string resources = "", bool metadata = true, bool compressed = false,
        string? xmlOverride = null, string page = "")
    {
        var xml = Encoding.UTF8.GetBytes(xmlOverride ?? $"""
            <x:xmpmeta xmlns:x="adobe:ns:meta/"><rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
            <rdf:Description rdf:about="" xmlns:pdfaid="http://www.aiim.org/pdfa/ns/id/">
            <pdfaid:part>{part}</pdfaid:part><pdfaid:conformance>{conformance}</pdfaid:conformance>
            </rdf:Description></rdf:RDF></x:xmpmeta>
            """);
        if (compressed)
        {
            using var data = new MemoryStream();
            using (var zlib = new ZLibStream(data, CompressionLevel.Optimal, leaveOpen: true)) zlib.Write(xml);
            xml = data.ToArray();
        }
        byte[][] objects = [
            Encoding.ASCII.GetBytes($"<< /Type /Catalog /Pages 2 0 R {(metadata ? "/Metadata 4 0 R" : "")} {catalog} >>"),
            Encoding.ASCII.GetBytes("<< /Type /Pages /Count 1 /Kids [3 0 R] >>"),
            Encoding.ASCII.GetBytes($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources << {resources} >> {page} >>"),
            [.. Encoding.ASCII.GetBytes($"<< /Type /Metadata /Subtype /XML /Length {xml.Length} {(compressed ? "/Filter /FlateDecode" : "")} >>\nstream\n"), .. xml, .. Encoding.ASCII.GetBytes("\nendstream")]
        ];
        using var pdf = new MemoryStream();
        void Write(string s) => pdf.Write(Encoding.ASCII.GetBytes(s));
        Write($"%PDF-{version}\n");
        var offsets = new List<long> { 0 };
        for (int i = 0; i < objects.Length; i++)
        {
            offsets.Add(pdf.Position); Write($"{i + 1} 0 obj\n"); pdf.Write(objects[i]); Write("\nendobj\n");
        }
        long xref = pdf.Position;
        Write($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (long offset in offsets.Skip(1)) Write(offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
        Write($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return pdf.ToArray();
    }

    [Theory]
    [InlineData(PdfALevel.A1a, "1", "A")]
    [InlineData(PdfALevel.A1b, "1", "B")]
    [InlineData(PdfALevel.A2a, "2", "A")]
    [InlineData(PdfALevel.A2b, "2", "B")]
    [InlineData(PdfALevel.A2u, "2", "U")]
    [InlineData(PdfALevel.A3a, "3", "A")]
    [InlineData(PdfALevel.A3b, "3", "B")]
    [InlineData(PdfALevel.A3u, "3", "U")]
    public async Task EachLevelAcceptsMatchingMetadataAndRejectsMismatchedIdentity(PdfALevel level, string part, string conformance)
    {
        var options = new PdfAValidationOptions { Level = level };
        foreach (bool compressed in new[] { false, true })
        {
            var valid = await PdfAValidation.ValidateAsync(Pdf(part, conformance, compressed: compressed), options);
            Assert.True(valid.IsValid);
            Assert.Equal(level, valid.Level);
            Assert.Empty(valid.Errors); Assert.Empty(valid.Warnings);
            Assert.Equal(0, valid.ErrorCount); Assert.Equal(0, valid.WarningCount);
        }
        // Use a valid identity for another level so parsing succeeds before comparison.
        var otherPart = part == "1" ? "2" : "1";
        var invalid = await PdfAValidation.ValidateAsync(Pdf(otherPart, "B"), options);
        Assert.False(invalid.IsValid);
        Assert.Equal(level, invalid.Level);
        var error = Assert.Single(invalid.Errors);
        Assert.Equal(PdfAValidationErrorKind.XmpInvalidPdfAIdentifier, error.Kind);
        Assert.Equal($"Part mismatch: expected {part}, found {otherPart}", error.Details);
        Assert.Contains(error.Details!, error.Message);
        Assert.Null(error.FontName);
        var wrongConformance = await PdfAValidation.ValidateAsync(Pdf(part, conformance == "A" ? "B" : "A"), options);
        Assert.Equal(PdfAValidationErrorKind.XmpInvalidPdfAIdentifier, Assert.Single(wrongConformance.Errors).Kind);
    }

    [Fact]
    public async Task InvalidIdentityAndMalformedXmlAreRejectedByCoreParser()
    {
        // Core 5.5.0 maps XMP parser failures to XmpMetadataMissing findings.
        foreach (var pdf in new[] { Pdf("9"), Pdf(xmlOverride: "<x:xmpmeta/>") })
        {
            var result = await PdfAValidation.ValidateAsync(pdf);
            Assert.False(result.IsValid);
            Assert.Equal(PdfAValidationErrorKind.XmpMetadataMissing, Assert.Single(result.Errors).Kind);
        }
    }

    [Fact]
    public async Task AllFindingsRetainVariantPayloadsAndCoreCollectSettingBehavior()
    {
        var pdf = Pdf(version: "1.7", metadata: false,
            catalog: "/OpenAction << /S /JavaScript /JS (alert) >> /Names << /EmbeddedFiles << >> >>",
            resources: "/Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >> /ColorSpace << /Cs /DeviceRGB >>");
        var all = await PdfAValidation.ValidateAsync(pdf);
        var requestedFirst = await PdfAValidation.ValidateAsync(pdf, new() { CollectAllErrors = false });
        Assert.False(all.IsValid);
        Assert.Equal(JsonSerializer.Serialize(all), JsonSerializer.Serialize(requestedFirst));
        Assert.Equal(6, all.ErrorCount);
        var version = Assert.Single(all.Errors, e => e.Kind == PdfAValidationErrorKind.IncompatiblePdfVersion);
        Assert.Equal("1.7", version.Actual); Assert.Equal("1.4", version.Required);
        Assert.Contains(all.Errors, e => e.Kind == PdfAValidationErrorKind.XmpMetadataMissing);
        Assert.Contains(all.Errors, e => e.Kind == PdfAValidationErrorKind.EmbeddedFileForbidden);
        Assert.Contains(all.Errors, e => e.Kind == PdfAValidationErrorKind.JavaScriptForbidden && e.Location == "OpenAction");
        Assert.Contains(all.Errors, e => e.Kind == PdfAValidationErrorKind.FontNotEmbedded && e.FontName == "F1");
        Assert.Contains(all.Errors, e => e.Kind == PdfAValidationErrorKind.InvalidColorSpace && e.ColorSpace == "DeviceRGB");
    }

    [Theory]
    [InlineData(PdfALevel.A1a, "1", "A", true, true)]
    [InlineData(PdfALevel.A1b, "1", "B", true, false)]
    [InlineData(PdfALevel.A2a, "2", "A", false, true)]
    [InlineData(PdfALevel.A2b, "2", "B", false, false)]
    [InlineData(PdfALevel.A2u, "2", "U", false, false)]
    [InlineData(PdfALevel.A3a, "3", "A", false, true)]
    [InlineData(PdfALevel.A3b, "3", "B", false, false)]
    [InlineData(PdfALevel.A3u, "3", "U", false, false)]
    public async Task LevelDependentRulesMatchActualCoreCoverage(PdfALevel level, string part, string conformance, bool transparency, bool unicode)
    {
        var result = await PdfAValidation.ValidateAsync(Pdf(part, conformance,
            resources: "/ExtGState << /G << /ca 0.5 >> >> /Font << /F1 << /Subtype /Type3 >> >>"), new() { Level = level });
        Assert.Equal(transparency, result.Errors.Any(e => e.Kind == PdfAValidationErrorKind.TransparencyForbidden));
        Assert.Equal(unicode, result.Errors.Any(e => e.Kind == PdfAValidationErrorKind.FontMissingToUnicode && e.FontName == "F1"));
        Assert.Equal((transparency ? 1 : 0) + (unicode ? 1 : 0), result.ErrorCount);
        var attachment = await PdfAValidation.ValidateAsync(Pdf(part, conformance,
            catalog: "/Names << /EmbeddedFiles << >> >>"), new() { Level = level });
        Assert.Equal(part == "3", attachment.IsValid);
        if (part != "3") Assert.Equal(PdfAValidationErrorKind.EmbeddedFileForbidden, Assert.Single(attachment.Errors).Kind);
    }

    [Fact]
    public async Task MalformedPdfAndUnresolvableReferencesAreOperationFailures()
    {
        foreach (var pdf in new[] { Encoding.ASCII.GetBytes("not a PDF"), Pdf(catalog: "/Pages 99 0 R"), Pdf(catalog: "/OpenAction 99 0 R") })
        {
            var error = await Assert.ThrowsAsync<PdfAException>(() => PdfAValidation.ValidateAsync(pdf));
            Assert.Equal(PdfAErrorKind.ParseError, error.Kind);
            Assert.Null(error.Finding); Assert.NotEmpty(error.Message);
        }
        var noIdentity = await PdfAValidation.ValidateAsync(Pdf(xmlOverride: "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"/>"));
        Assert.Equal(PdfAValidationErrorKind.XmpMissingPdfAIdentifier, Assert.Single(noIdentity.Errors).Kind);
        Assert.True((await PdfAValidation.ValidateAsync(Pdf())).IsValid);
    }

    [Fact]
    public async Task NativeBoundaryRejectsMalformedRequestsAndInvalidLevels()
    {
        string Request(int level) => JsonSerializer.Serialize(new { options = new { Level = level, CollectAllErrors = true }, parser_options = new PdfParserOptions() });
        foreach (var (request, kind) in new[] { ("{}", "InvalidInput"), ("{", "InvalidInput"), (Request(8), "InvalidLevel") })
        {
            var pdf = Pdf();
            int status = NativeMethods.oxidize_pdfa_validate(pdf, (nuint)pdf.Length, request, out var output);
            try
            {
                Assert.Equal(0, status);
                using var json = JsonDocument.Parse(System.Runtime.InteropServices.Marshal.PtrToStringUTF8(output)!);
                Assert.Equal(kind, json.RootElement.GetProperty("error").GetProperty("kind").GetString());
            }
            finally { NativeMethods.oxidize_free_string(output); }
        }
        var invalid = await Assert.ThrowsAsync<PdfAException>(() => PdfAValidation.ValidateAsync(Pdf(), new() { Level = (PdfALevel)255 }));
        Assert.Equal(PdfAErrorKind.InvalidLevel, invalid.Kind);
        await Assert.ThrowsAsync<ArgumentNullException>(() => PdfAValidation.ValidateAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => PdfAValidation.ValidateAsync([]));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfAValidation.ValidateAsync(Pdf(), ct: new CancellationToken(true)));
    }

    [Fact]
    public async Task EncryptionExternalActionsAndMissingMetadataRemainConformanceFindings()
    {
        using var document = new PdfDocument();
        using var page = PdfPage.A4();
        document.AddPage(page); document.Encrypt("user", "owner");
        var bytes = document.SaveToBytes();
        foreach (string? password in new string?[] { null, "wrong" })
        {
            var locked = await Assert.ThrowsAsync<PdfAException>(() => PdfAValidation.ValidateAsync(bytes, new() { Password = password }));
            Assert.Equal(PdfAErrorKind.ParseError, locked.Kind);
            Assert.Null(locked.Finding);
        }
        var encrypted = await PdfAValidation.ValidateAsync(bytes, new() { Password = "user" });
        Assert.False(encrypted.IsValid);
        Assert.Contains(encrypted.Errors, e => e.Kind == PdfAValidationErrorKind.EncryptionForbidden);
        foreach (string action in new[] { "GoToR", "GoToE", "Launch" })
        {
            var result = await PdfAValidation.ValidateAsync(Pdf(catalog: $"/OpenAction << /S /{action} >>"));
            var error = Assert.Single(result.Errors);
            Assert.Equal(PdfAValidationErrorKind.ExternalReferenceForbidden, error.Kind);
            Assert.Equal(action, error.ReferenceType);
        }
        var missing = await PdfAValidation.ValidateAsync(Pdf(metadata: false));
        Assert.Equal(PdfAValidationErrorKind.XmpMetadataMissing, Assert.Single(missing.Errors).Kind);
        var parserError = await Assert.ThrowsAsync<PdfAException>(() => PdfAValidation.ValidateAsync(Pdf(),
            parserOptions: new() { PreferredEncoding = (PdfPreferredEncoding)255 }));
        Assert.Equal(PdfAErrorKind.InvalidInput, parserError.Kind);
    }

    [Fact]
    public async Task InputsAndSettingsAreSnapshottedBeforeScheduling()
    {
        for (int i = 0; i < 16; i++)
        {
            var pdf = Pdf();
            var options = new PdfAValidationOptions();
            var parser = new PdfParserOptions();
            var task = PdfAValidation.ValidateAsync(pdf, options, parser);
            Array.Fill(pdf, (byte)0);
            options.Level = PdfALevel.A3a;
            parser.PreferredEncoding = (PdfPreferredEncoding)255;
            var result = await task;
            Assert.True(result.IsValid); Assert.Equal(PdfALevel.A1b, result.Level);
        }
    }
}
