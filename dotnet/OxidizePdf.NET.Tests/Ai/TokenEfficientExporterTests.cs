using OxidizePdf.NET;
using OxidizePdf.NET.Ai;

namespace OxidizePdf.NET.Tests.Ai;

/// <summary>
/// Tests for <see cref="TokenEfficientExporter"/> (2.13.0): the token-efficient
/// TOON-style chunk serializer and its inverse parser.
/// </summary>
public class TokenEfficientExporterTests
{
    private static DocumentChunk SampleChunk(int index, string content, List<int> pages)
    {
        return new DocumentChunk
        {
            Id = $"chunk_{index}",
            Content = content,
            Tokens = content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
            PageNumbers = pages,
            ChunkIndex = index,
            Metadata = new ChunkMetadata
            {
                Position = new ChunkPosition
                {
                    StartChar = index * 100,
                    EndChar = index * 100 + content.Length,
                    FirstPage = pages.First(),
                    LastPage = pages.Last(),
                },
                Confidence = 1.0f,
                SentenceBoundaryRespected = true,
            },
        };
    }

    [Fact]
    public void Export_emits_format_magic_header()
    {
        var payload = TokenEfficientExporter.Export(new[] { SampleChunk(0, "Hello world.", new() { 1 }) });
        Assert.StartsWith("#oxct/1", payload);
    }

    [Fact]
    public void Export_then_parse_round_trips_scalar_fields()
    {
        var original = new List<DocumentChunk>
        {
            SampleChunk(0, "First chunk content spanning page one.", new() { 1 }),
            SampleChunk(1, "Second chunk that crosses a page boundary.", new() { 1, 2 }),
            SampleChunk(2, "Third and final chunk on the last page.", new() { 2 }),
        };

        var payload = TokenEfficientExporter.Export(original);
        var restored = TokenEfficientExporter.Parse(payload);

        Assert.Equal(original.Count, restored.Count);
        for (int i = 0; i < original.Count; i++)
        {
            Assert.Equal(original[i].Id, restored[i].Id);
            Assert.Equal(original[i].Content, restored[i].Content);
            Assert.Equal(original[i].Tokens, restored[i].Tokens);
            Assert.Equal(original[i].ChunkIndex, restored[i].ChunkIndex);
            Assert.Equal(original[i].PageNumbers, restored[i].PageNumbers);
            Assert.Equal(original[i].Metadata.Position.FirstPage, restored[i].Metadata.Position.FirstPage);
            Assert.Equal(original[i].Metadata.Position.LastPage, restored[i].Metadata.Position.LastPage);
        }
    }

    [Fact]
    public void Export_null_throws_argument_null()
    {
        Assert.Throws<ArgumentNullException>(() => TokenEfficientExporter.Export(null!));
    }

    [Fact]
    public void Parse_null_throws_argument_null()
    {
        Assert.Throws<ArgumentNullException>(() => TokenEfficientExporter.Parse(null!));
    }

    [Fact]
    public void Parse_malformed_payload_throws_extraction_exception()
    {
        Assert.Throws<PdfExtractionException>(() => TokenEfficientExporter.Parse("#wrong/9\nbad\nrow"));
    }
}
