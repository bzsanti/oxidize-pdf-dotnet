using System.Text.Json;
using OxidizePdf.NET.Models;

namespace OxidizePdf.NET.Tests;

/// <summary>
/// Tests for DocumentChunk model serialization and structure.
/// </summary>
public class DocumentChunkModelTests
{
    [Fact]
    public void DocumentChunk_DeserializesFromRustJson_Correctly()
    {
        // Arrange - JSON format from Rust FFI
        var json = """
            {
                "index": 0,
                "page_number": 1,
                "text": "Sample text",
                "confidence": 0.95,
                "x": 10.5,
                "y": 20.5,
                "width": 100.0,
                "height": 50.0
            }
            """;

        // Act
        var chunk = JsonSerializer.Deserialize<DocumentChunk>(json);

        // Assert
        Assert.NotNull(chunk);
        Assert.Equal(0, chunk.Index);
        Assert.Equal(1, chunk.PageNumber);
        Assert.Equal("Sample text", chunk.Text);
        Assert.Equal(0.95, chunk.Confidence);
        Assert.NotNull(chunk.BoundingBox);
        Assert.Equal(10.5, chunk.BoundingBox.X);
        Assert.Equal(20.5, chunk.BoundingBox.Y);
        Assert.Equal(100.0, chunk.BoundingBox.Width);
        Assert.Equal(50.0, chunk.BoundingBox.Height);
    }

    [Fact]
    public void DocumentChunk_BoundingBox_IsNotNull_ByDefault()
    {
        // Arrange & Act
        var chunk = new DocumentChunk();

        // Assert
        Assert.NotNull(chunk.BoundingBox);
    }

    [Fact]
    public void BoundingBox_DefaultValues_AreZero()
    {
        // Arrange & Act
        var box = new BoundingBox();

        // Assert
        Assert.Equal(0.0, box.X);
        Assert.Equal(0.0, box.Y);
        Assert.Equal(0.0, box.Width);
        Assert.Equal(0.0, box.Height);
    }

    [Fact]
    public void DocumentChunk_DoesNotHave_DuplicateBoundingBoxProperties()
    {
        // This test verifies that DocumentChunk doesn't have X, Y, Width, Height
        // properties directly - they should only be in BoundingBox
        var chunkType = typeof(DocumentChunk);

        // These properties should NOT exist on DocumentChunk directly
        var directXProperty = chunkType.GetProperty("X",
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.DeclaredOnly);
        var directYProperty = chunkType.GetProperty("Y",
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.DeclaredOnly);
        var directWidthProperty = chunkType.GetProperty("Width",
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.DeclaredOnly);
        var directHeightProperty = chunkType.GetProperty("Height",
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.DeclaredOnly);

        Assert.Null(directXProperty);
        Assert.Null(directYProperty);
        Assert.Null(directWidthProperty);
        Assert.Null(directHeightProperty);
    }

    [Fact]
    public void DocumentChunk_DeserializesArrayFromRustJson_Correctly()
    {
        // Arrange - Array format from Rust FFI (realistic output)
        var json = """
            [
                {"index":0,"page_number":1,"text":"First chunk","confidence":1.0,"x":0.0,"y":0.0,"width":0.0,"height":0.0},
                {"index":1,"page_number":1,"text":"Second chunk","confidence":1.0,"x":0.0,"y":0.0,"width":0.0,"height":0.0}
            ]
            """;

        // Act
        var chunks = JsonSerializer.Deserialize<List<DocumentChunk>>(json);

        // Assert
        Assert.NotNull(chunks);
        Assert.Equal(2, chunks.Count);
        Assert.Equal(0, chunks[0].Index);
        Assert.Equal(1, chunks[1].Index);
        Assert.Equal("First chunk", chunks[0].Text);
        Assert.Equal("Second chunk", chunks[1].Text);
    }

    [Fact]
    public void BoundingBox_CanBeCreatedWithValues()
    {
        // Arrange & Act
        var box = new BoundingBox
        {
            X = 10.0,
            Y = 20.0,
            Width = 100.0,
            Height = 50.0
        };

        // Assert
        Assert.Equal(10.0, box.X);
        Assert.Equal(20.0, box.Y);
        Assert.Equal(100.0, box.Width);
        Assert.Equal(50.0, box.Height);
    }
}
