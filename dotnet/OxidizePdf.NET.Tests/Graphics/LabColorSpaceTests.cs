using FluentAssertions;
using OxidizePdf.NET.Graphics;
using Xunit;

namespace OxidizePdf.NET.Tests.Graphics;

public class LabColorSpaceTests
{
    [Fact]
    public void D50_Factory_HasD50WhitePoint()
    {
        var cs = LabColorSpace.D50();
        cs.WhitePoint[0].Should().BeApproximately(0.9642, 0.0001);
        cs.WhitePoint[1].Should().BeApproximately(1.0000, 0.0001);
        cs.WhitePoint[2].Should().BeApproximately(0.8251, 0.0001);
    }

    [Fact]
    public void D65_Factory_HasD65WhitePoint()
    {
        LabColorSpace.D65().WhitePoint[0].Should().BeApproximately(0.9505, 0.0001);
    }

    [Fact]
    public void DefaultRange_IsStandardLabRange()
    {
        var cs = LabColorSpace.D50();
        cs.Range[0].Should().Be(-128.0);  // a_min
        cs.Range[1].Should().Be(127.0);   // a_max
        cs.Range[2].Should().Be(-128.0);  // b_min
        cs.Range[3].Should().Be(127.0);   // b_max
    }

    [Fact]
    public void Validate_AcceptsValidD50()
    {
        LabColorSpace.D50().Invoking(x => x.Validate()).Should().NotThrow();
    }

    [Fact]
    public void Validate_RejectsRangeWithMinGreaterThanMax()
    {
        var cs = LabColorSpace.D50() with { Range = new double[] { 127.0, -128.0, -128.0, 127.0 } };
        cs.Invoking(x => x.Validate()).Should().Throw<ArgumentException>()
            .WithMessage("*Range*");
    }

    [Fact]
    public void Validate_RejectsRangeWithWrongLength()
    {
        var cs = LabColorSpace.D50() with { Range = new double[] { -128.0, 127.0 } };
        cs.Invoking(x => x.Validate()).Should().Throw<ArgumentException>()
            .WithMessage("*Range*4*");
    }

    [Fact]
    public void Validate_RejectsWhitePointYNotOne()
    {
        var cs = LabColorSpace.D50() with { WhitePoint = new double[] { 0.9642, 0.5, 0.8251 } };
        cs.Invoking(x => x.Validate()).Should().Throw<ArgumentException>()
            .WithMessage("*WhitePoint*Y*");
    }
}
