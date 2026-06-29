using OxidizePdf.NET.KernelMemory;

namespace OxidizePdf.NET.KernelMemory.Tests;

public class SupportsMimeTypeTests
{
    [Theory]
    [InlineData("application/pdf", true)]
    [InlineData("application/pdf; charset=binary", true)]
    [InlineData("APPLICATION/PDF", true)]
    [InlineData("text/plain", false)]
    [InlineData("application/json", false)]
    [InlineData("", false)]
    public void SupportsMimeType_matches_only_pdf(string mime, bool expected)
    {
        var decoder = new OxidizePdfDecoder();
        Assert.Equal(expected, decoder.SupportsMimeType(mime));
    }
}
