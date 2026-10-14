using System.IO.Compression;
using System.Text;

namespace OxidizePdf.NET.Tests;

public class NamedImageTests
{
    private static readonly byte[] Left = [255, 0, 0, 0, 255, 0];
    private static readonly byte[] Right = [0, 0, 255];
    private const string Resources = "<< /XObject << /Left 4 0 R /Right 5 0 R /Form 6 0 R /Broken 99 0 R /NotStream 7 0 R /Alias 4 0 R /A#20B 4 0 R >> >>";
    private static byte[] B(string text) => Encoding.ASCII.GetBytes(text);
    private static byte[] Compress(byte[] bytes)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, true)) zlib.Write(bytes);
        return output.ToArray();
    }
    private static byte[] Stream(byte[] bytes, string dictionary)
        => B($"<< /Length {bytes.Length} {dictionary} >>\nstream\n").Concat(bytes).Concat(B("\nendstream")).ToArray();
    private static byte[] Image(byte[] bytes, string extra = "", long width = 2, long height = 1)
        => Stream(bytes, $"/Type /XObject /Subtype /Image /Width {width} /Height {height} /BitsPerComponent 8 /ColorSpace /DeviceRGB {extra}");
    private static byte[] Pdf(byte[]? left = null, byte[]? right = null, bool inherited = false,
        string resources = Resources, string? pageOverride = null,
        string? formResources = "<< /XObject << /Inside 5 0 R >> >>", byte[][]? extra = null)
    {
        var objects = new List<byte[]>
        {
            B("<< /Type /Catalog /Pages 2 0 R >>"),
            B("<< /Type /Pages /Kids [3 0 R] /Count 1 " + (inherited ? "/Resources " + resources : "") + " >>"),
            B("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] " +
                (pageOverride is not null ? "/Resources " + pageOverride : inherited ? "" : "/Resources " + resources) + " >>"),
            left ?? Image(Left),
            right ?? Image(Compress(Right), "/Filter /FlateDecode", 1),
            // Unsupported Form contents must not be decoded while following resource names.
            Stream([1, 2, 3], "/Type /XObject /Subtype /Form /BBox [0 0 10 10] /Filter /Unsupported " +
                (formResources is null ? "" : "/Resources " + formResources))
        };
        objects.AddRange(extra ?? [B("42")]);
        using var pdf = new MemoryStream();
        void Write(string value) => pdf.Write(B(value));
        Write("%PDF-1.4\n");
        var offsets = new List<long>();
        for (int i = 0; i < objects.Count; i++)
        {
            offsets.Add(pdf.Position);
            Write($"{i + 1} 0 obj\n"); pdf.Write(objects[i]); Write("\nendobj\n");
        }
        var xref = pdf.Position;
        Write($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets) Write($"{offset:D10} 00000 n \n");
        Write($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return pdf.ToArray();
    }
    private static async Task<PdfImageException> Fails(byte[] pdf, string name, PdfImageErrorKind kind,
        string[]? path = null, PdfNamedImageOptions? options = null)
    {
        var error = await Assert.ThrowsAsync<PdfImageException>(() => PdfImages.ExtractAsync(pdf, 0, name, path, options));
        Assert.Equal(kind, error.Kind);
        Assert.NotEmpty(error.Message);
        return error;
    }

    [Fact]
    public async Task SelectsDistinctNamesAndAliasesWithoutExtractingUnrelatedResources()
    {
        var pdf = Pdf();
        var options = new PdfNamedImageOptions { Limits = new() { MaxImages = 1 } };
        var left = await PdfImages.ExtractAsync(pdf, 0, "Left", options: options);
        var right = await PdfImages.ExtractAsync(pdf, 0, "Right", options: options);
        Assert.Equal(Left, left.Data);
        Assert.Equal(Left, left.EncodedData);
        Assert.Equal(Right, right.Data);
        Assert.Equal(Compress(Right), right.EncodedData);
        Assert.Equal(new PdfIndirectObjectId(4, 0), left.ObjectId);
        Assert.Equal(new PdfIndirectObjectId(5, 0), right.ObjectId);
        Assert.Equal(new PdfIndirectObjectId(3, 0), left.PageObjectId);
        Assert.Equal(new PdfIndirectObjectId(3, 0), left.ResourceOwnerId);
        Assert.Null(left.ResourceDictionaryId);
        Assert.Equal(0u, left.PageIndex);
        Assert.Equal("Left", left.ResourceName);
        Assert.Empty(left.FormPath);
        Assert.Empty(left.FormObjectIds);
        Assert.Equal(2u, left.Width);
        Assert.Equal(1u, left.Height);
        Assert.Equal(8u, left.BitsPerComponent);
        Assert.False(left.ImageMask);
        Assert.Equal(PdfImagePayloadFormat.PackedSamples, left.EncodedFormat);
        Assert.Equal(PdfImagePayloadFormat.FilterEncoded, right.EncodedFormat);
        Assert.Equal(PdfImagePayloadFormat.PackedSamples, right.OutputFormat);
        Assert.Equal("FlateDecode", right.Dictionary["Filter"].Value!.Value.GetString());
        Assert.Empty(right.Diagnostics);
        var alias = await PdfImages.ExtractAsync(pdf, 0, "Alias");
        Assert.Equal(left.ObjectId, alias.ObjectId);
        Assert.Equal("Alias", alias.ResourceName);
        Assert.Equal(Left, (await PdfImages.ExtractAsync(Pdf(right: Image([0], "/Filter /Unsupported", long.MaxValue)), 0, "Left")).Data);
        Assert.Equal(Left, (await PdfImages.ExtractAsync(pdf, 0, "A B")).Data);
    }

    [Fact]
    public async Task InheritedIndirectResourcesAndEmptyOverridesPreserveOwnership()
    {
        var pdf = Pdf(inherited: true, resources: "8 0 R", extra: [B("42"), B("<< /XObject 9 0 R >>"), B("<< /Left 4 0 R >>")]);
        var result = await PdfImages.ExtractAsync(pdf, 0, "Left");
        Assert.Equal(Left, result.Data);
        Assert.Equal(new PdfIndirectObjectId(2, 0), result.ResourceOwnerId);
        Assert.Equal(new PdfIndirectObjectId(8, 0), result.ResourceDictionaryId);
        await Fails(Pdf(inherited: true, pageOverride: "<< >>"), "Left", PdfImageErrorKind.MissingResource);
        await Fails(Pdf(inherited: true, pageOverride: "null"), "Left", PdfImageErrorKind.InvalidData);
        await Fails(pdf, "Left", PdfImageErrorKind.LimitExceeded, options: new() { MaxAncestorDepth = 0 });
    }

    [Theory]
    [InlineData("left", PdfImageErrorKind.MissingResource)]
    [InlineData("Missing", PdfImageErrorKind.MissingResource)]
    [InlineData("Form", PdfImageErrorKind.NotImage)]
    [InlineData("NotStream", PdfImageErrorKind.NotImage)]
    // Core resolves a dangling object reference to PDF null, which is not an image.
    [InlineData("Broken", PdfImageErrorKind.NotImage)]
    public async Task MissingNonImageAndDanglingResourcesHaveTypedErrors(string name, PdfImageErrorKind kind)
        => await Fails(Pdf(), name, kind);

    [Fact]
    public async Task FormsRequireExplicitPathsAndUseNearestDeclaredResources()
    {
        var pdf = Pdf();
        await Fails(pdf, "Inside", PdfImageErrorKind.MissingResource);
        await Fails(pdf, "Form/Inside", PdfImageErrorKind.MissingResource);
        var result = await PdfImages.ExtractAsync(pdf, 0, "Inside", ["Form"]);
        Assert.Equal(Right, result.Data);
        Assert.Equal(new PdfIndirectObjectId(5, 0), result.ObjectId);
        Assert.Equal(new PdfIndirectObjectId(6, 0), result.ResourceOwnerId);
        Assert.Equal(new[] { "Form" }, result.FormPath);
        Assert.Equal(new PdfIndirectObjectId?[] { new(6, 0) }, result.FormObjectIds);
        var fallback = await PdfImages.ExtractAsync(Pdf(formResources: null), 0, "Left", ["Form"]);
        Assert.Equal(Left, fallback.Data);
        Assert.Equal(new PdfIndirectObjectId(3, 0), fallback.ResourceOwnerId);
        await Fails(Pdf(formResources: "<< >>"), "Left", PdfImageErrorKind.MissingResource, ["Form"]);
        await Fails(Pdf(formResources: "null"), "Left", PdfImageErrorKind.InvalidData, ["Form"]);
        await Fails(pdf, "Left", PdfImageErrorKind.NotForm, ["Left"]);
        await Fails(Pdf(formResources: null), "Left", PdfImageErrorKind.InvalidData, ["Form", "Form"]);
        var nested = Pdf(formResources: "<< /XObject << /Nested 7 0 R >> >>", extra:
            [Stream([], "/Subtype /Form /BBox [0 0 10 10] /Resources << /XObject << /Deep 5 0 R >> >>")]);
        var deep = await PdfImages.ExtractAsync(nested, 0, "Deep", ["Form", "Nested"]);
        Assert.Equal(Right, deep.Data);
        Assert.Equal(new PdfIndirectObjectId?[] { new(6, 0), new(7, 0) }, deep.FormObjectIds);
        Assert.Equal(new PdfIndirectObjectId(7, 0), deep.ResourceOwnerId);
    }

    [Fact]
    public async Task ChainedFiltersResolvePredictorsAndRetainColorDecodeAndMaskMetadata()
    {
        var predictor = new byte[] { 0 }.Concat(Left).ToArray();
        var encoded = B(Convert.ToHexString(Compress(predictor)) + ">");
        var image = Image(encoded, "/Filter [/ASCIIHexDecode /FlateDecode] /DecodeParms [null 7 0 R] /Decode [1 0 1 0 1 0] /SMask 99 0 R");
        var pdf = Pdf(left: image, extra: [B("<< /Predictor 12 /Colors 3 /Columns 2 /BitsPerComponent 8 >>")]);
        var result = await PdfImages.ExtractAsync(pdf, 0, "Left");
        Assert.Equal(encoded, result.EncodedData);
        Assert.Equal(Left, result.Data);
        var filters = result.Dictionary["Filter"].Value!.Value;
        Assert.Equal("ASCIIHexDecode", filters[0].GetProperty("value").GetString());
        Assert.Equal("FlateDecode", filters[1].GetProperty("value").GetString());
        var parms = result.Dictionary["DecodeParms"].Value!.Value;
        Assert.Equal("Null", parms[0].GetProperty("type").GetString());
        Assert.Equal(7, parms[1].GetProperty("value")[0].GetInt32());
        Assert.Equal("DeviceRGB", result.Dictionary["ColorSpace"].Value!.Value.GetString());
        Assert.Equal(6, result.Dictionary["Decode"].Value!.Value.GetArrayLength());
        Assert.Equal("Reference", result.Dictionary["SMask"].Type);
        Assert.Equal(99, result.Dictionary["SMask"].Value!.Value[0].GetInt32());
    }

    [Theory]
    [InlineData("/JPXDecode")]
    [InlineData("/CCITTFaxDecode")]
    [InlineData("/JBIG2Decode")]
    [InlineData("[/ASCIIHexDecode /DCTDecode]")]
    public async Task UnsupportedFiltersFailInsteadOfSkippingTheNamedImage(string filter)
        => await Fails(Pdf(left: Image([1, 2, 3], "/Filter " + filter)), "Left", PdfImageErrorKind.UnsupportedFilter);

    [Fact]
    public async Task JpegPassthroughAndStencilSamplesHaveExplicitFormats()
    {
        // Deliberately only markers: passthrough must not claim JPEG integrity or decoded samples.
        byte[] jpeg = [0xff, 0xd8, 0xff, 0xd9];
        var image = await PdfImages.ExtractAsync(Pdf(left: Image(jpeg, "/Filter /DCTDecode")), 0, "Left");
        Assert.Equal(jpeg, image.EncodedData);
        Assert.Equal(jpeg, image.Data);
        Assert.Equal(PdfImagePayloadFormat.Jpeg, image.EncodedFormat);
        Assert.Equal(PdfImagePayloadFormat.Jpeg, image.OutputFormat);
        Assert.Empty(image.Diagnostics);
        await Fails(Pdf(left: Image(jpeg, "/Filter /DCTDecode")), "Left", PdfImageErrorKind.LimitExceeded, options: new() { MaxDecodedBytes = 3 });
        var mask = Stream([0b10100000], "/Subtype /Image /Width 3 /Height 1 /ImageMask true /Decode [1 0]");
        var stencil = await PdfImages.ExtractAsync(Pdf(left: mask), 0, "Left");
        Assert.True(stencil.ImageMask);
        Assert.Equal(1u, stencil.BitsPerComponent);
        Assert.Equal(new byte[] { 0b10100000 }, stencil.Data);
        Assert.Equal(PdfImagePayloadFormat.PackedSamples, stencil.OutputFormat);
    }

    [Fact]
    public async Task ImageCountPixelAndByteLimitsAreEnforcedBeforeDecode()
    {
        var options = new PdfNamedImageOptions { Limits = new() { MaxImages = 1, MaxDecodedPixelsPerImage = 2, MaxEncodedBytesPerImage = 6, MaxTotalEncodedBytes = 6 }, MaxDecodedBytes = 6 };
        Assert.Equal(Left, (await PdfImages.ExtractAsync(Pdf(), 0, "Left", options: options)).Data);
        options.Limits.MaxDecodedPixelsPerImage = 1;
        await Fails(Pdf(), "Left", PdfImageErrorKind.LimitExceeded, options: options);
        // A malformed filter would fail if the pixel guard were bypassed.
        await Fails(Pdf(left: Image([1, 2, 3], "/Filter /FlateDecode")), "Left", PdfImageErrorKind.LimitExceeded, options: options);
        options.Limits.MaxDecodedPixelsPerImage = 2;
        options.Limits.MaxEncodedBytesPerImage = 5;
        await Fails(Pdf(), "Left", PdfImageErrorKind.LimitExceeded, options: options);
        options.Limits.MaxEncodedBytesPerImage = 6;
        options.Limits.MaxTotalEncodedBytes = 5;
        await Fails(Pdf(), "Left", PdfImageErrorKind.LimitExceeded, options: options);
        options.Limits.MaxTotalEncodedBytes = 6;
        options.MaxDecodedBytes = 5;
        var strict = await Fails(Pdf(), "Left", PdfImageErrorKind.DecodeError, options: options);
        Assert.Equal(PdfStreamErrorKind.DecodeError, strict.StreamErrorKind);
        options.Limits.MaxImages = 0;
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => PdfImages.ExtractAsync(Pdf(), 0, "Left", options: options));
        await Fails(Pdf(left: Image([], width: uint.MaxValue, height: uint.MaxValue)), "Left", PdfImageErrorKind.LimitExceeded);
        await Fails(Pdf(left: Image([], width: -1)), "Left", PdfImageErrorKind.InvalidData);
    }

    [Fact]
    public async Task RecoveryDiagnosticsAndExpansionLimitsReuseStreamSemantics()
    {
        var source = Pdf(left: Image(Compress(Left)[..^4], "/Filter /FlateDecode"));
        var strict = await Fails(source, "Left", PdfImageErrorKind.DecodeError);
        Assert.Equal(PdfStreamErrorKind.DecodeError, strict.StreamErrorKind);
        var recovered = await PdfImages.ExtractAsync(source, 0, "Left", options: new() { AllowRecovery = true });
        Assert.Equal(Left, recovered.Data);
        var diagnostic = Assert.Single(recovered.Diagnostics);
        Assert.Equal(PdfFlateRecoveryKind.Unverified, diagnostic.Kind);
        Assert.Equal(0ul, diagnostic.FilterIndex);
        Assert.NotEmpty(diagnostic.Error);
        var corrupt = await Fails(Pdf(left: Image([0, 1, 2, 3], "/Filter /FlateDecode")), "Left", PdfImageErrorKind.DecodeError, options: new() { AllowRecovery = true });
        Assert.Equal(PdfStreamErrorKind.InvalidFlate, corrupt.StreamErrorKind);
        var expansion = await Fails(Pdf(left: Image(Compress(new byte[4096]), "/Filter /FlateDecode")), "Left", PdfImageErrorKind.LimitExceeded, options: new() { AllowRecovery = true, MaxDecodedBytes = 4095 });
        Assert.Equal(PdfStreamErrorKind.ResourceLimit, expansion.StreamErrorKind);
        var emptyFilter = Pdf(left: Image(Left, "/Filter []"));
        await Fails(emptyFilter, "Left", PdfImageErrorKind.DecodeError);
        Assert.Equal(Left, (await PdfImages.ExtractAsync(emptyFilter, 0, "Left", options: new() { AllowRecovery = true })).Data);
    }

    [Fact]
    public async Task LookupAndMetadataLimitsHaveExactBoundaries()
    {
        var pdf = Pdf();
        Assert.Equal(Left, (await PdfImages.ExtractAsync(pdf, 0, "Left", options: new() { MaxNameBytes = 4, MaxReferenceDepth = 1, MaxMetadataNodes = 7, MaxMetadataBytes = 75, MaxMetadataDepth = 0 })).Data);
        await Fails(pdf, "Left", PdfImageErrorKind.LimitExceeded, options: new() { MaxNameBytes = 3 });
        await Fails(pdf, "Left", PdfImageErrorKind.LimitExceeded, options: new() { MaxReferenceDepth = 0 });
        var metadataLimit = await Fails(pdf, "Left", PdfImageErrorKind.LimitExceeded, options: new() { MaxMetadataNodes = 6 });
        Assert.Null(metadataLimit.StreamErrorKind);
        await Fails(pdf, "Left", PdfImageErrorKind.LimitExceeded, options: new() { MaxMetadataBytes = 74 });
        Assert.Equal(Right, (await PdfImages.ExtractAsync(pdf, 0, "Inside", ["Form"], new() { MaxFormDepth = 1, MaxNameBytes = 10 })).Data);
        await Fails(pdf, "Inside", PdfImageErrorKind.LimitExceeded, ["Form"], new() { MaxFormDepth = 0 });
        await Fails(pdf, "Inside", PdfImageErrorKind.LimitExceeded, ["Form"], new() { MaxNameBytes = 9 });
        await Fails(Pdf(left: Image(Left, "/Decode [0 1]")), "Left", PdfImageErrorKind.LimitExceeded, options: new() { MaxMetadataDepth = 0 });
    }

    [Fact]
    public async Task ExistingBatchApiStillReturnsEncodedPngFiles()
    {
        var samples = Enumerable.Repeat((byte)127, 300).ToArray();
        var pdf = Pdf(left: Image(samples, width: 10, height: 10),
            right: Image(Compress(samples), "/Filter /FlateDecode", 10, 10),
            resources: "<< /XObject << /Left 4 0 R /Right 5 0 R >> >>");
        var selected = await PdfImages.ExtractAsync(pdf, 0, "Right", options: new() { Limits = new() { MaxImages = 1 } });
        Assert.Equal(samples, selected.Data);
        var batch = await PdfOperations.ExtractImagesAsync(pdf);
        Assert.Equal(2, batch.Count);
        foreach (var image in batch)
        {
            Assert.Equal("png", image.Format);
            Assert.Equal(10u, image.Width);
            Assert.Equal(10u, image.Height);
            Assert.True(image.ImageData.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }));
        }
        await Assert.ThrowsAsync<PdfExtractionException>(() => PdfOperations.ExtractImagesAsync(pdf, new ImageExtractionOptions { MaxImages = 1 }));
    }

    [Fact]
    public async Task InputsAndNestedSettingsAreSnapshottedAndInvalidInputsFail()
    {
        var original = Pdf();
        var input = (byte[])original.Clone();
        var path = new List<string> { "Form" };
        var options = new PdfNamedImageOptions();
        var parser = new PdfParserOptions();
        var task = PdfImages.ExtractAsync(input, 0, "Inside", path, options, parser);
        Array.Fill(input, (byte)0);
        path[0] = "Missing";
        options.Limits.MaxImages = 0;
        options.MaxDecodedBytes = 0;
        parser.PreferredEncoding = (PdfPreferredEncoding)255;
        Assert.Equal(Right, (await task).Data);
        await Assert.ThrowsAsync<ArgumentNullException>(() => PdfImages.ExtractAsync(null!, 0, "Left"));
        await Assert.ThrowsAsync<ArgumentException>(() => PdfImages.ExtractAsync([], 0, "Left"));
        await Assert.ThrowsAsync<ArgumentNullException>(() => PdfImages.ExtractAsync(original, 0, null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => PdfImages.ExtractAsync(original, 0, "Left", [null!]));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfImages.ExtractAsync(original, 0, "Left", ct: new CancellationToken(true)));
        await Fails([1, 2, 3], "Left", PdfImageErrorKind.InvalidData);
        var missingPage = await Assert.ThrowsAsync<PdfImageException>(() => PdfImages.ExtractAsync(original, uint.MaxValue, "Left"));
        Assert.Equal(PdfImageErrorKind.InvalidData, missingPage.Kind);
        await Fails(original, "Left", PdfImageErrorKind.InvalidData, options: new() { MaxAncestorDepth = 257 });
        await Fails(original, "Left", PdfImageErrorKind.InvalidData, options: new() { MaxFormDepth = 65 });
        await Fails(original, "Left", PdfImageErrorKind.InvalidData, options: new() { MaxReferenceDepth = 129 });
        await Fails(original, "Left", PdfImageErrorKind.InvalidData, options: new() { MaxMetadataDepth = 17 });
    }
}
