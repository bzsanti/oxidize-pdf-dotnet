using System.Text;

namespace OxidizePdf.NET.Tests;

public class StreamReaderTests
{
    private static byte[] Pdf(int pages = 3)
    {
        using var doc = new PdfDocument();
        for (int i = 0; i < pages; i++)
        {
            using var page = new PdfPage(321 + i, 456);
            page.SetFont(StandardFont.Helvetica, 12).TextAt(20, 100, $"source-page-{i}");
            doc.AddPage(page);
        }
        return doc.SaveToBytes();
    }
    private sealed class PartialStream(byte[] bytes) : MemoryStream(bytes)
    {
        public bool FailReads { get; set; }
        public bool Disposed { get; private set; }
        public Action? BeforeRead { get; set; }
        public override int Read(Span<byte> buffer)
        {
            BeforeRead?.Invoke();
            if (FailReads) throw new IOException("intentional read failure");
            return base.Read(buffer[..Math.Min(buffer.Length, 7)]);
        }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
    [Fact]
    public void StreamingReadsRealPagesWithPartialReadsAndCacheBudgets()
    {
        using var stream = new PartialStream(Pdf());
        stream.Position = 7;
        using (var reader = PdfStreamingDocument.Open(stream, new() { BufferSize = 17, PageCacheSize = 1, MemoryLimit = 1024 }))
        {
            var configuration = reader.GetConfiguration();
            Assert.True(configuration.ProgressiveHintsUnsupported); Assert.False(configuration.Effective.ProgressiveHints);
            for (uint i = 0; i < 3; i++)
            {
                var page = reader.NextPage()!;
                Assert.Equal(i, page.PageIndex); Assert.Equal(321 + i, page.Width); Assert.Equal(456, page.Height);
                Assert.Contains($"source-page-{i}", page.Text);
                Assert.Contains($"source-page-{i}", Encoding.ASCII.GetString(page.Content));
                var current = reader.GetConfiguration(); Assert.InRange(current.CachedPages, 0u, 1u); Assert.InRange(current.MemoryUsage, 0ul, 1024ul);
            }
            Assert.Null(reader.NextPage()); reader.ClearCache(); Assert.Equal(0ul, reader.GetConfiguration().MemoryUsage);
        }
        Assert.False(stream.Disposed); // borrowed by default
    }
    [Fact]
    public void LazyObjectsKeepTypesGenerationsAndEffectiveOptions()
    {
        using var stream = new PartialStream(Pdf());
        using var reader = PdfLazyDocument.Open(stream, new() { CacheSize = 1, BufferSize = 0 });
        var c = reader.GetConfiguration(); Assert.Equal(3u, c.PageCount); Assert.Equal(1u, c.Effective.BufferSize);
        Assert.Equal(new[] { "MemoryMapping", "Streaming" }, c.Unsupported); Assert.False(c.Effective.MemoryMapping);
        var page = reader.GetPage(0); Assert.Equal(new[] { 0d, 0, 321, 456 }, page.MediaBox);
        var obj = reader.GetObject(page.Number, page.Generation);
        Assert.Equal(PdfLazyObjectKind.Dictionary, obj.Kind); Assert.Equal("Page", obj.Entries!["Type"].Name);
        Assert.Equal(page.Number, reader.GetPage(0).Number);
        reader.GetObject(page.Number, page.Generation);
        Assert.True(reader.GetConfiguration().Stats.CacheHits >= 1);
        Assert.Throws<PdfStreamReaderException>(() => reader.GetObject(page.Number, checked((ushort)(page.Generation + 1))));
        reader.PreloadPage(0); reader.ClearCache();
        Assert.Equal(0ul, reader.GetConfiguration().Stats.CachedObjects);
        Assert.Equal("Page", reader.GetObject(page.Number, page.Generation).Entries!["Type"].Name);
        Assert.Throws<PdfStreamReaderException>(() => reader.GetPage(3));
    }
    [Fact]
    public void OwnershipMalformedInputAndContentLimitsReleaseResources()
    {
        var input = new PartialStream(Pdf()); var reader = PdfStreamingDocument.Open(input, leaveOpen: false);
        reader.Dispose(); reader.Dispose(); Assert.True(input.Disposed);
        Assert.Throws<ObjectDisposedException>(() => reader.NextPage());
        var unreadable = new PartialStream(Pdf()) { FailReads = true };
        var readFailure = Assert.Throws<PdfStreamReaderException>(() => PdfStreamingDocument.Open(unreadable, leaveOpen: false));
        Assert.IsType<IOException>(readFailure.InnerException); Assert.True(unreadable.Disposed);
        var malformed = new PartialStream(Encoding.ASCII.GetBytes("bad"));
        Assert.Throws<PdfStreamReaderException>(() => PdfStreamingDocument.Open(malformed, leaveOpen: false)); Assert.True(malformed.Disposed);
        using var limitedInput = new PartialStream(Pdf());
        using var limited = PdfStreamingDocument.Open(limitedInput, new() { MaxContentStreamSize = 1 });
        Assert.Throws<PdfStreamReaderException>(() => limited.NextPage());
        Assert.Throws<InvalidOperationException>(() => limited.NextPage());
    }
    [Fact]
    public void CancellationReadErrorsAndReentryCannotEscapeNativeCallbacks()
    {
        using var input = new PartialStream(Pdf(10));
        using var reader = PdfStreamingDocument.Open(input, new() { BufferSize = 1 });
        Assert.Throws<OperationCanceledException>(() => reader.NextPage(new CancellationToken(true)));
        Assert.Equal(0u, reader.NextPage()!.PageIndex); // pre-cancel did not advance
        input.BeforeRead = () => Assert.Throws<InvalidOperationException>(() => reader.GetConfiguration());
        Assert.Equal(1u, reader.NextPage()!.PageIndex);
        input.BeforeRead = null; input.FailReads = true;
        // Parser may retain later objects, so consume until the next actual I/O boundary.
        PdfStreamReaderException? failure = null;
        for (int i = 0; i < 10 && failure is null; i++)
        {
            try { if (reader.NextPage() is null) break; }
            catch (PdfStreamReaderException e) { failure = e; }
        }
        Assert.NotNull(failure); Assert.IsType<IOException>(failure.InnerException);
    }
}
