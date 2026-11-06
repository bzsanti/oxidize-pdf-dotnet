using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace OxidizePdf.NET.Tests;

public sealed class BatchRotationTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "batch-rotate-" + Guid.NewGuid().ToString("N"));
    private string Input => Path.Combine(directory, "input.pdf");
    private string Output => Path.Combine(directory, "output.pdf");
    private static readonly string[] Contents = ["q 1 0 0 rg 10 20 30 40 re f Q\n", "q 0 0 1 rg 50 60 70 80 re f Q\n"];

    public BatchRotationTests()
    {
        Directory.CreateDirectory(directory);
        string[] objects = [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] /Rotate 90 /Resources << >> /Contents 5 0 R >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 400 500] /Rotate 180 /Resources << >> /Contents 6 0 R >>",
            $"<< /Length {Contents[0].Length} >>\nstream\n{Contents[0]}endstream",
            $"<< /Length {Contents[1].Length} >>\nstream\n{Contents[1]}endstream"];
        using var pdf = new MemoryStream();
        void Write(string value) => pdf.Write(Encoding.ASCII.GetBytes(value));
        Write("%PDF-1.4\n");
        var offsets = new List<long>();
        for (int i = 0; i < objects.Length; i++)
        {
            offsets.Add(pdf.Position);
            Write($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        long xref = pdf.Position;
        Write("xref\n0 7\n0000000000 65535 f \n");
        foreach (long offset in offsets) Write($"{offset:D10} 00000 n \n");
        Write($"trailer\n<< /Size 7 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        File.WriteAllBytes(Input, pdf.ToArray());
    }

    // Narrow independent reader for this fixture's flat page tree and direct-length streams.
    // Resolve Kids/Contents references instead of assuming object serialization order.
    private static void AssertPdf(string path, int first, int second)
    {
        string text = Encoding.Latin1.GetString(File.ReadAllBytes(path));
        var objects = Regex.Matches(text, @"(?m)^(\d+) 0 obj\s*([\s\S]*?)\bendobj")
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
        string tree = Assert.Single(objects.Values, o => Regex.IsMatch(o, @"/Type\s*/Pages\b"));
        var kids = Regex.Matches(Regex.Match(tree, @"/Kids\s*\[([^\]]+)\]").Groups[1].Value, @"(\d+) 0 R");
        Assert.Equal(2, kids.Count);
        int[] rotations = [first, second];
        string[] boxes = ["0 0 200 300", "0 0 400 500"];
        for (int i = 0; i < 2; i++)
        {
            string page = objects[kids[i].Groups[1].Value];
            var rotation = Regex.Match(page, @"/Rotate\s+(-?\d+)");
            Assert.Equal(rotations[i], rotation.Success ? int.Parse(rotation.Groups[1].Value) : 0);
            Assert.Equal(boxes[i], Regex.Replace(Regex.Match(page, @"/MediaBox\s*\[([^\]]+)\]").Groups[1].Value.Trim(), @"\s+", " "));
            var contentRef = Regex.Match(page, @"/Contents\s*(?:\[\s*)?(\d+) 0 R");
            Assert.True(contentRef.Success);
            string stream = objects[contentRef.Groups[1].Value];
            int length = int.Parse(Regex.Match(stream, @"/Length\s+(\d+)").Groups[1].Value);
            var marker = Regex.Match(stream, @"\bstream\r?\n");
            Assert.True(marker.Success);
            byte[] bytes = Encoding.Latin1.GetBytes(stream.Substring(marker.Index + marker.Length, length));
            if (stream[..marker.Index].Contains("/FlateDecode"))
            {
                using var compressed = new MemoryStream(bytes);
                using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
                using var output = new MemoryStream(); zlib.CopyTo(output); bytes = output.ToArray();
            }
            Assert.Equal(Contents[i], Encoding.ASCII.GetString(bytes));
        }
    }

    [Theory]
    [InlineData(false, 90, 180, 180)]
    [InlineData(true, 90, 180, 270)]
    [InlineData(true, -90, 0, 90)]
    [InlineData(true, 450, 180, 270)]
    [InlineData(true, 0, 90, 180)]
    public void SelectedAndAllPagesComposeRotationAndPreserveContent(bool all, int angle, int first, int second)
    {
        AssertPdf(Input, 90, 180);
        byte[] original = File.ReadAllBytes(Input);
        var result = PdfBatch.Run([new PdfBatchJob.Rotate(Input, Output, angle, all ? null : [0])]);
        Assert.Equal(1, result.Successful);
        Assert.Equal(PdfBatchStatus.Success, Assert.Single(result.Results).Status);
        Assert.Equal(Output, Assert.Single(result.Results[0].OutputFiles!));
        AssertPdf(Output, first, second);
        Assert.Equal(original, File.ReadAllBytes(Input));
    }

    [Theory]
    [InlineData("angle", "rotation")]
    [InlineData("page", "page")]
    [InlineData("malformed", "")]
    [InlineData("missing", "")]
    public void InvalidJobsPreserveExistingOutput(string fault, string expectedError)
    {
        File.WriteAllText(Output, "existing-output");
        if (fault == "malformed") File.WriteAllText(Input, "not a PDF");
        string input = fault == "missing" ? Input + ".missing" : Input;
        var result = PdfBatch.Run([new PdfBatchJob.Rotate(input, Output, fault == "angle" ? 45 : 90, fault == "page" ? [2] : null)]);
        var failed = Assert.Single(result.Results);
        Assert.Equal(PdfBatchStatus.Failed, failed.Status);
        Assert.False(string.IsNullOrWhiteSpace(failed.Error));
        Assert.Contains(expectedError, failed.Error!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("existing-output", File.ReadAllText(Output));
        Assert.Equal(2, Directory.GetFiles(directory).Length);
    }

    [Fact]
    public void MixedJobsRetainOrderStopTimeoutAndCallbackLifetime()
    {
        int called = 0;
        var result = PdfBatch.Run([new PdfBatchJob.Rotate(Input, Output, 90, [1]),
            new PdfBatchJob.Custom("callback", () => { GC.Collect(); Interlocked.Increment(ref called); }),
            new PdfBatchJob.Rotate(Input, Output + ".bad", 45),
            new PdfBatchJob.Custom("after-error", () => Interlocked.Increment(ref called))], new() { Parallelism = 1 });
        Assert.Equal(Enumerable.Range(0, 4), result.Results.Select(r => r.Index));
        Assert.Equal(new[] { PdfBatchStatus.Success, PdfBatchStatus.Success, PdfBatchStatus.Failed, PdfBatchStatus.Success }, result.Results.Select(r => r.Status));
        Assert.Equal(2, called); Assert.False(File.Exists(Output + ".bad")); AssertPdf(Output, 90, 270);
        var stopped = PdfBatch.Run([new PdfBatchJob.Rotate(Input, Output, 45),
            new PdfBatchJob.Custom("never", () => called++), new PdfBatchJob.Rotate(Input, Output, 90)], new() { Parallelism = 1, StopOnError = true });
        Assert.True(stopped.Cancelled);
        Assert.All(stopped.Results.Skip(1), r => Assert.Equal(PdfBatchStatus.Cancelled, r.Status));
        Assert.Equal(2, called); AssertPdf(Output, 90, 270);
        var timed = PdfBatch.Run([new PdfBatchJob.Rotate(Input, Output, 180)], new() { JobTimeoutMs = 0 });
        Assert.Equal(PdfBatchStatus.Failed, Assert.Single(timed.Results).Status); AssertPdf(Output, 90, 270);
    }

    [Fact]
    public void UnsupportedOptionsRejectBeforeRotationOrCallbacks()
    {
        int called = 0;
        File.WriteAllText(Output, "existing-output");
        PdfBatchJob[] jobs = [new PdfBatchJob.Custom("never", () => called++), new PdfBatchJob.Rotate(Input, Output, 90)];
        Assert.Throws<PdfBatchException>(() => PdfBatch.Run(jobs.Append(new PdfBatchJob.Compress(Input, Output, 80)).ToArray()));
        Assert.Throws<PdfBatchException>(() => PdfBatch.Run(jobs, new() { MemoryLimitPerWorker = 1 }));
        Assert.Throws<PdfBatchException>(() => PdfBatch.Run(jobs.Append(new PdfBatchJob.Rotate(Input, Output, 90, [-1])).ToArray()));
        Assert.Equal(0, called); Assert.Equal("existing-output", File.ReadAllText(Output));
    }

    public void Dispose() => Directory.Delete(directory, true);
}
