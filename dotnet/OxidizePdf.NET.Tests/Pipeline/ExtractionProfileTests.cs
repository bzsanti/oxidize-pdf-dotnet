using OxidizePdf.NET.Pipeline;

namespace OxidizePdf.NET.Tests.Pipeline;

public class ExtractionProfileTests
{
    [Theory]
    [InlineData(ExtractionProfile.Standard, 0)]
    [InlineData(ExtractionProfile.Academic, 1)]
    [InlineData(ExtractionProfile.Form, 2)]
    [InlineData(ExtractionProfile.Government, 3)]
    [InlineData(ExtractionProfile.Dense, 4)]
    [InlineData(ExtractionProfile.Presentation, 5)]
    [InlineData(ExtractionProfile.Rag, 6)]
    public void Discriminants_match_rust_enum_order(ExtractionProfile profile, int expected)
    {
        Assert.Equal((byte)expected, (byte)profile);
    }
}
