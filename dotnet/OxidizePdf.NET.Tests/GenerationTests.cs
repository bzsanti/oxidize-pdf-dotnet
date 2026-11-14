using System.Text;
using System.Text.RegularExpressions;
using System.IO.Compression;
using OxidizePdf.NET.Models;

namespace OxidizePdf.NET.Tests;

public sealed class GenerationTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "generation-" + Guid.NewGuid().ToString("N"));
    private string Output => Path.Combine(directory, "out.pdf");
    public GenerationTests() => Directory.CreateDirectory(directory);
    public void Dispose() => Directory.Delete(directory, true);
    private static PdfPage Render(PdfGenerationPage spec) => new PdfPage(spec.Width, spec.Height)
        .DrawTextAt(StandardFont.Helvetica, 12, 20, 30, $"PageMarker{spec.Index}");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SerialWritesRealPagesInInputOrder(bool streaming)
    {
        var pages = new[] { new PdfGenerationPage(9, 200, 300), new PdfGenerationPage(2, 400, 500) };
        var result = PdfGeneration.Save(Output, pages, Render, new() { Mode = PdfGenerationMode.Serial, StreamingWriter = streaming, Compression = false });
        Assert.Equal(2, result.Pages);
        Assert.Equal(PdfGenerationMode.Serial, result.Mode);
        string pdf = Encoding.Latin1.GetString(File.ReadAllBytes(Output));
        Assert.StartsWith("%PDF-", pdf);
        AssertPages(pdf, ["PageMarker9", "PageMarker2"], ["0 0 200 300", "0 0 400 500"]);
    }

    // Fixture-scoped independent reader: resolve page-tree order and stream references.
    private static void AssertPages(string pdf, string[] markers, string[] boxes)
    {
        var objects = Regex.Matches(pdf, @"(?m)^(\d+) 0 obj\s*([\s\S]*?)\bendobj")
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
        string tree = Assert.Single(objects.Values, o => Regex.IsMatch(o, @"/Type\s*/Pages\b"));
        var kids = Regex.Matches(Regex.Match(tree, @"/Kids\s*\[([^\]]+)\]").Groups[1].Value, @"(\d+) 0 R");
        Assert.Equal(markers.Length, kids.Count);
        for (int i = 0; i < kids.Count; i++)
        {
            string page = objects[kids[i].Groups[1].Value];
            Assert.Equal(boxes[i], Regex.Replace(Regex.Match(page, @"/MediaBox\s*\[([^\]]+)\]").Groups[1].Value.Trim(), @"\s+", " "));
            string id = Regex.Match(page, @"/Contents\s*(?:\[\s*)?(\d+) 0 R").Groups[1].Value;
            string stream = objects[id];
            int length = int.Parse(Regex.Match(stream, @"/Length\s+(\d+)").Groups[1].Value);
            var marker = Regex.Match(stream, @"\bstream\r?\n");
            byte[] bytes = Encoding.Latin1.GetBytes(stream.Substring(marker.Index + marker.Length, length));
            if (stream[..marker.Index].Contains("/FlateDecode"))
            {
                using var input = new MemoryStream(bytes);
                using var zlib = new ZLibStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream(); zlib.CopyTo(output); bytes = output.ToArray();
            }
            Assert.Contains(markers[i], Encoding.Latin1.GetString(bytes));
        }
    }

    [Theory]
    [InlineData(PdfGenerationMode.Serial)]
    [InlineData(PdfGenerationMode.Rayon)]
    public void DuplicateIdentitiesPreserveInputOrderAndGeometry(PdfGenerationMode mode)
    {
        PdfGeneration.Save(Output, [new(7, 200, 300), new(7, 400, 500)], Render,
            new() { Mode = mode, ChunkSize = 1, MaxThreads = 2 });
        AssertPages(Encoding.Latin1.GetString(File.ReadAllBytes(Output)), ["PageMarker7", "PageMarker7"], ["0 0 200 300", "0 0 400 500"]);
    }

    [Fact]
    public void EmbeddedFontAndImageSurvivePageOwnershipTransfer()
    {
        byte[] font = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "fonts", "sample.ttf"));
        PdfPage? returned = null;
        PdfGeneration.Save(Output, [new(0, 200, 300)], s =>
        {
            using var image = PdfImage.FromPngData(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a3ioAAAAASUVORK5CYII="));
            returned = new PdfPage(s.Width, s.Height).SetCustomFont("fixture", 12).TextAt(20, 30, "Embedded")
                .AddImage("pixel", image).DrawImage("pixel", 40, 50, 10, 20);
            return returned;
        }, fonts: new Dictionary<string, byte[]> { ["fixture"] = font });
        Assert.NotNull(returned);
        Assert.Throws<ObjectDisposedException>(() => returned.TextAt(0, 0, "after transfer"));
        string pdf = Encoding.Latin1.GetString(File.ReadAllBytes(Output));
        Assert.Matches(@"/FontFile2\s+\d+ 0 R", pdf);
        Assert.Matches(@"/Subtype\s*/Image", pdf);
        Assert.Matches(@"/Width\s+1\b", pdf);
        Assert.Matches(@"/Height\s+1\b", pdf);
    }

    [Fact]
    public void SerialCallbacksStayOnCallingThreadAndRejectInvalidDimensionsBeforeWork()
    {
        int caller = Environment.CurrentManagedThreadId, calls = 0;
        PdfPage Callback(PdfGenerationPage s)
        {
            Assert.Equal(caller, Environment.CurrentManagedThreadId); calls++; return Render(s);
        }
        PdfGeneration.Save(Output, [new(0, 200, 300), new(1, 200, 300)], Callback, new() { Mode = PdfGenerationMode.Serial });
        Assert.Equal(2, calls);
        byte[] previous = File.ReadAllBytes(Output);
        Assert.Throws<PdfGenerationException>(() => PdfGeneration.Save(Output, [new(0, 200, 300), new(1, -1, 300)], Callback));
        Assert.Equal(2, calls);
        Assert.Equal(previous, File.ReadAllBytes(Output));
    }

    [Fact]
    public void DisposedCallbackPageIsReportedWithoutDereferencingFreedMemory()
    {
        Assert.Throws<PdfGenerationException>(() => PdfGeneration.Save(Output, [new(0, 200, 300)], s =>
        {
            var page = Render(s); page.Dispose(); return page;
        }));
        Assert.False(File.Exists(Output));
    }

    [Fact]
    public void ZeroRetentionBudgetStillWritesBufferedOutput()
    {
        var result = PdfGeneration.Save(Output, [new(0, 200, 300)], Render,
            new() { Mode = PdfGenerationMode.Serial, StreamingWriter = false, MemoryPoolSize = 0 });
        Assert.Equal(1, result.Pages);
        AssertPages(Encoding.Latin1.GetString(File.ReadAllBytes(Output)), ["PageMarker0"], ["0 0 200 300"]);
    }

    [Fact]
    public void UnsupportedPriorityAndInvalidFontFailBeforeWork()
    {
        int calls = 0;
        PdfPage Callback(PdfGenerationPage s) { calls++; return Render(s); }
        Assert.Throws<PdfGenerationException>(() => PdfGeneration.Save(Output, [new(0, 200, 300)], Callback,
            new() { ThreadPriority = PdfGenerationPriority.High }));
        Assert.Throws<PdfGenerationException>(() => PdfGeneration.Save(Output, [new(0, 200, 300)], Callback,
            fonts: new Dictionary<string, byte[]> { ["broken"] = [1, 2, 3] }));
        Assert.Equal(0, calls);
        Assert.False(File.Exists(Output));
    }

    [Fact]
    public void WriterFailurePreservesSentinelAndRemovesTemporary()
    {
        File.WriteAllText(Output, "sentinel");
        Assert.Throws<PdfGenerationException>(() => PdfGeneration.Save(Output, [new(0, 200, 300)], s =>
        {
            using var image = PdfImage.FromPngData(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a3ioAAAAASUVORK5CYII="));
            return new PdfPage(s.Width, s.Height).AddImage("pixel", image)
                .DrawImageWithTransparency("pixel", 0, 0, 10, 10, "MissingMask");
        }));
        Assert.Equal("sentinel", File.ReadAllText(Output));
        Assert.Single(Directory.GetFiles(directory));
    }

    [Fact]
    public void CallbackFailurePreservesDestinationAndJoinsCallbacks()
    {
        File.WriteAllText(Output, "sentinel");
        int active = 0;
        var error = Assert.Throws<PdfGenerationException>(() => PdfGeneration.Save(Output,
            Enumerable.Range(0, 16).Select(i => new PdfGenerationPage((uint)i, 200, 300)).ToArray(), s =>
            {
                Interlocked.Increment(ref active);
                try { GC.Collect(); if (s.Index == 3) throw new InvalidOperationException("render failed"); return Render(s); }
                finally { Interlocked.Decrement(ref active); }
            }, new() { Mode = PdfGenerationMode.Auto, ChunkSize = 1, MaxThreads = 2 }));
        Assert.IsType<InvalidOperationException>(error.InnerException);
        Assert.Equal(0, active);
        Assert.Equal("sentinel", File.ReadAllText(Output));
        Assert.Single(Directory.GetFiles(directory));
    }

    [Theory]
    [InlineData(0, 1, 0, true)]
    [InlineData(1, 0, 0, true)]
    [InlineData(1, 1, 100, true)]
    [InlineData(1, 1, 0, false)]
    public void InvalidControlsRejectBeforeCallback(int threads, int chunk, ulong memory, bool balancing)
    {
        int calls = 0;
        File.WriteAllText(Output, "sentinel");
        Assert.Throws<PdfGenerationException>(() => PdfGeneration.Save(Output, [new(0, 200, 300)], s => { calls++; return Render(s); },
            new() { MaxThreads = threads, ChunkSize = chunk, MemoryLimitPerCallback = memory, LoadBalancing = balancing }));
        Assert.Equal(0, calls);
        Assert.Equal("sentinel", File.ReadAllText(Output));
    }

    [Fact]
    public void RayonUsesBoundedConcurrentCallbacks()
    {
        Assert.True(PdfGeneration.SupportsRayon);
        using var barrier = new Barrier(2);
        int active = 0, peak = 0;
        var result = PdfGeneration.Save(Output, Enumerable.Range(0, 8).Select(i => new PdfGenerationPage((uint)i, 200, 300)).ToArray(), s =>
        {
            int count = Interlocked.Increment(ref active);
            int old;
            do { old = Volatile.Read(ref peak); } while (old < count && Interlocked.CompareExchange(ref peak, count, old) != old);
            try { Assert.True(barrier.SignalAndWait(TimeSpan.FromSeconds(15))); return Render(s); }
            finally { Interlocked.Decrement(ref active); }
        }, new() { Mode = PdfGenerationMode.Rayon, MaxThreads = 2, ChunkSize = 1 });
        Assert.Equal(2, peak);
        Assert.Equal(0, active);
        Assert.Equal(PdfGenerationMode.Rayon, result.Mode);
    }
}

public sealed class GenerationAvailabilityTests
{
    [Fact]
    public void LoadedBinaryMatchesExpectedFeaturesAndFallback()
    {
        string expected = Environment.GetEnvironmentVariable("OXIDIZE_GENERATION_EXPECT") ?? "rayon";
        Assert.Equal(expected != "none", PdfGeneration.IsAvailable);
        Assert.Equal(expected == "rayon", PdfGeneration.SupportsRayon);
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
        int calls = 0;
        PdfPage Render(PdfGenerationPage s) { calls++; return new PdfPage(s.Width, s.Height); }
        try
        {
            if (expected == "none")
            {
                Assert.Throws<PdfGenerationException>(() => PdfGeneration.Save(path, [new(0, 200, 300)], Render));
                Assert.Equal(0, calls);
                Assert.False(File.Exists(path));
            }
            else
            {
                var result = PdfGeneration.Save(path, [new(0, 200, 300)], Render);
                Assert.Equal(expected == "rayon" ? PdfGenerationMode.Rayon : PdfGenerationMode.Serial, result.Mode);
                Assert.Equal(1, calls);
                Assert.True(File.Exists(path));
                if (expected == "serial")
                {
                    Assert.Throws<PdfGenerationException>(() => PdfGeneration.Save(path, [new(0, 200, 300)], Render, new() { Mode = PdfGenerationMode.Rayon }));
                    Assert.Equal(1, calls);
                }
            }
        }
        finally { File.Delete(path); }
    }
}
