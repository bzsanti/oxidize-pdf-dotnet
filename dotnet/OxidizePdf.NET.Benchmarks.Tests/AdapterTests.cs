using OxidizePdf.NET.Benchmarks;
using OxidizePdf.NET.Benchmarks.Adapters;

namespace OxidizePdf.NET.Benchmarks.Tests;

public class AdapterTests
{
    private static byte[] SamplePdf() =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "sample.pdf"));

    [Fact]
    public void OxidizeAdapter_ExtractsRealTextFromSamplePdf()
    {
        var adapter = new OxidizeAdapter();
        var result = adapter.Extract(SamplePdf());

        Assert.Equal("OxidizePdf.NET", adapter.Name);
        Assert.False(string.IsNullOrEmpty(adapter.Version));
        Assert.True(result.PageCount >= 1, "sample.pdf should have at least 1 page");
        // "SEVILLA" is a contiguous ASCII uppercase token in the real document,
        // extracted identically across libraries — verifies genuine extraction.
        Assert.Contains("SEVILLA", result.Text);
    }

    [Fact]
    public void PdfPigAdapter_ExtractsRealTextFromSamplePdf()
    {
        var adapter = new PdfPigAdapter();
        var result = adapter.Extract(SamplePdf());

        Assert.Equal("PdfPig", adapter.Name);
        Assert.False(string.IsNullOrEmpty(adapter.Version));
        Assert.True(result.PageCount >= 1);
        Assert.Contains("SEVILLA", result.Text);
    }

    [Fact]
    public void IText7Adapter_ExtractsRealTextFromSamplePdf()
    {
        var adapter = new IText7Adapter();
        var result = adapter.Extract(SamplePdf());

        Assert.Equal("iText7", adapter.Name);
        Assert.Equal("AGPL", adapter.License);
        Assert.False(string.IsNullOrEmpty(adapter.Version));
        Assert.True(result.PageCount >= 1);
        Assert.Contains("SEVILLA", result.Text);
    }
}
