using OxidizePdf.NET;
using OxidizePdf.NET.KernelMemory;
using OxidizePdf.NET.Pipeline;

namespace OxidizePdf.NET.KernelMemory.Tests;

public class DecodeAsyncTests
{
    private static string FixturePath() =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "sample.pdf");

    [Fact]
    public async Task DecodeAsync_maps_each_rag_chunk_to_one_section()
    {
        var bytes = await File.ReadAllBytesAsync(FixturePath());
        var expected = await new PdfExtractor().RagChunksAsync(bytes, ExtractionProfile.Rag);

        var decoder = new OxidizePdfDecoder();
        var content = await decoder.DecodeAsync(new BinaryData(bytes));

        Assert.Equal("text/plain", content.MimeType);
        Assert.Equal(expected.Count, content.Sections.Count);
        Assert.NotEmpty(content.Sections);

        for (int i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].FullText, content.Sections[i].Content);
            Assert.Equal(expected[i].ChunkIndex, content.Sections[i].Number);
            Assert.True(content.Sections[i].SentencesAreComplete);
            int expectedPage = expected[i].PageNumbers.Count > 0 ? expected[i].PageNumbers[0] : -1;
            Assert.Equal(expectedPage, content.Sections[i].PageNumber);
        }
    }

    [Fact]
    public async Task DecodeAsync_overloads_produce_identical_output()
    {
        var bytes = await File.ReadAllBytesAsync(FixturePath());
        var decoder = new OxidizePdfDecoder();

        var fromFile = await decoder.DecodeAsync(FixturePath());
        var fromBinary = await decoder.DecodeAsync(new BinaryData(bytes));
        using var stream = new MemoryStream(bytes);
        var fromStream = await decoder.DecodeAsync(stream);

        var fileTexts = fromFile.Sections.Select(s => s.Content).ToList();
        Assert.Equal(fileTexts, fromBinary.Sections.Select(s => s.Content).ToList());
        Assert.Equal(fileTexts, fromStream.Sections.Select(s => s.Content).ToList());
    }
}
