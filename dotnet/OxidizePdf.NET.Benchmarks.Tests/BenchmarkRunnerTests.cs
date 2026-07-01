using OxidizePdf.NET.Benchmarks;

namespace OxidizePdf.NET.Benchmarks.Tests;

public class BenchmarkRunnerTests
{
    private sealed class FakeAdapter : IPdfExtractorAdapter
    {
        private readonly Func<byte[], ExtractResult> _fn;
        public FakeAdapter(string name, Func<byte[], ExtractResult> fn) { Name = name; _fn = fn; }
        public string Name { get; }
        public string License => "test";
        public string Version => "0.0.0";
        public ExtractResult Extract(byte[] pdfBytes) => _fn(pdfBytes);
    }

    private static readonly byte[] Dummy = new byte[] { 1, 2, 3 };

    [Fact]
    public void RunOne_Ok_WhenAdapterReturnsText()
    {
        var runner = new BenchmarkRunner(
            new[] { new FakeAdapter("A", _ => new ExtractResult(3, "hello")) },
            TimeSpan.FromSeconds(5));

        var r = runner.RunOne(runner.Adapters[0], "x.pdf", Dummy);

        Assert.Equal(ExtractStatus.Ok, r.Status);
        Assert.Equal(3, r.PageCount);
        Assert.Equal(5, r.TextLength);
        Assert.Null(r.ErrorType);
    }

    [Fact]
    public void RunOne_Empty_WhenAdapterReturnsZeroLengthText()
    {
        var runner = new BenchmarkRunner(
            new[] { new FakeAdapter("A", _ => new ExtractResult(2, "")) },
            TimeSpan.FromSeconds(5));

        var r = runner.RunOne(runner.Adapters[0], "x.pdf", Dummy);

        Assert.Equal(ExtractStatus.Empty, r.Status);
        Assert.Equal(0, r.TextLength);
    }

    [Fact]
    public void RunOne_Error_RecordsExceptionType_AndDoesNotThrow()
    {
        var runner = new BenchmarkRunner(
            new[] { new FakeAdapter("A", _ => throw new InvalidOperationException("boom")) },
            TimeSpan.FromSeconds(5));

        var r = runner.RunOne(runner.Adapters[0], "x.pdf", Dummy);

        Assert.Equal(ExtractStatus.Error, r.Status);
        Assert.Equal(nameof(InvalidOperationException), r.ErrorType);
    }

    [Fact]
    public void RunOne_Timeout_WhenAdapterExceedsBudget()
    {
        var runner = new BenchmarkRunner(
            new[] { new FakeAdapter("A", _ => { Thread.Sleep(2000); return new ExtractResult(1, "late"); }) },
            TimeSpan.FromMilliseconds(200));

        var r = runner.RunOne(runner.Adapters[0], "x.pdf", Dummy);

        Assert.Equal(ExtractStatus.Timeout, r.Status);
    }
}
