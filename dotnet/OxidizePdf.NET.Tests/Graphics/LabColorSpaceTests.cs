using OxidizePdf.NET.Graphics;
using Xunit;

namespace OxidizePdf.NET.Tests.Graphics;

public class LabColorSpaceTests
{
    [Fact]
    public void D50_Factory_HasD50WhitePoint()
    {
        var cs = LabColorSpace.D50();
        Assert.Equal(0.9642, cs.WhitePoint[0], 4);
        Assert.Equal(1.0000, cs.WhitePoint[1], 4);
        Assert.Equal(0.8251, cs.WhitePoint[2], 4);
    }

    [Fact]
    public void D65_Factory_HasD65WhitePoint()
    {
        Assert.Equal(0.9505, LabColorSpace.D65().WhitePoint[0], 4);
    }

    [Fact]
    public void DefaultRange_IsStandardLabRange()
    {
        var cs = LabColorSpace.D50();
        Assert.Equal(-128.0, cs.Range[0]);  // a_min
        Assert.Equal(127.0,  cs.Range[1]);  // a_max
        Assert.Equal(-128.0, cs.Range[2]);  // b_min
        Assert.Equal(127.0,  cs.Range[3]);  // b_max
    }

    [Fact]
    public void Validate_AcceptsValidD50()
    {
        // A throw here fails the test.
        LabColorSpace.D50().Validate();
    }

    [Fact]
    public void Validate_RejectsRangeWithMinGreaterThanMax()
    {
        var cs = LabColorSpace.D50() with { Range = new double[] { 127.0, -128.0, -128.0, 127.0 } };
        var ex = Assert.Throws<ArgumentException>(() => cs.Validate());
        Assert.Contains("Range", ex.Message);
    }

    [Fact]
    public void Validate_RejectsRangeWithWrongLength()
    {
        var cs = LabColorSpace.D50() with { Range = new double[] { -128.0, 127.0 } };
        var ex = Assert.Throws<ArgumentException>(() => cs.Validate());
        Assert.Contains("Range", ex.Message);
        Assert.Contains("4", ex.Message);
    }

    [Fact]
    public void Validate_RejectsWhitePointYNotOne()
    {
        var cs = LabColorSpace.D50() with { WhitePoint = new double[] { 0.9642, 0.5, 0.8251 } };
        var ex = Assert.Throws<ArgumentException>(() => cs.Validate());
        Assert.Contains("WhitePoint", ex.Message);
        Assert.Contains("Y", ex.Message);
    }
}
