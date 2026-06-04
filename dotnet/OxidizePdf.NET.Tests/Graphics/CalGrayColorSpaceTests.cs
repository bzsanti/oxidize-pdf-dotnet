using OxidizePdf.NET.Graphics;
using Xunit;

namespace OxidizePdf.NET.Tests.Graphics;

public class CalGrayColorSpaceTests
{
    [Fact]
    public void DefaultConstructor_SetsReasonableDefaults()
    {
        var cs = new CalGrayColorSpace();
        Assert.Equal(new double[] { 0.9505, 1.0, 1.0890 }, cs.WhitePoint);
        Assert.Equal(new double[] { 0.0, 0.0, 0.0 }, cs.BlackPoint);
        Assert.Equal(1.0, cs.Gamma);
    }

    [Fact]
    public void D50_Factory_MatchesD50Illuminant()
    {
        var cs = CalGrayColorSpace.D50();
        Assert.Equal(0.9642, cs.WhitePoint[0], 4);
        Assert.Equal(1.0000, cs.WhitePoint[1], 4);
        Assert.Equal(0.8251, cs.WhitePoint[2], 4);
    }

    [Fact]
    public void D65_Factory_MatchesD65Illuminant()
    {
        var cs = CalGrayColorSpace.D65();
        Assert.Equal(0.9505, cs.WhitePoint[0], 4);
        Assert.Equal(1.0000, cs.WhitePoint[1], 4);
        Assert.Equal(1.0890, cs.WhitePoint[2], 4);
    }

    [Fact]
    public void Validate_AcceptsValidWhitePoint()
    {
        // A throw here fails the test.
        CalGrayColorSpace.D65().Validate();
    }

    [Fact]
    public void Validate_RejectsNegativeWhitePointY()
    {
        var cs = new CalGrayColorSpace { WhitePoint = new double[] { 0.9505, -0.1, 1.089 } };
        var ex = Assert.Throws<ArgumentException>(() => cs.Validate());
        Assert.Contains("WhitePoint", ex.Message);
    }

    [Fact]
    public void Validate_RejectsWhitePointYNotOne()
    {
        var cs = new CalGrayColorSpace { WhitePoint = new double[] { 0.9505, 0.5, 1.089 } };
        var ex = Assert.Throws<ArgumentException>(() => cs.Validate());
        Assert.Contains("WhitePoint", ex.Message);
        Assert.Contains("Y", ex.Message);
    }

    [Fact]
    public void Validate_RejectsNonPositiveGamma()
    {
        var cs = CalGrayColorSpace.D65() with { Gamma = 0.0 };
        var ex = Assert.Throws<ArgumentException>(() => cs.Validate());
        Assert.Contains("Gamma", ex.Message);
    }

    [Fact]
    public void Validate_RejectsWrongWhitePointLength()
    {
        var cs = new CalGrayColorSpace { WhitePoint = new double[] { 0.9505, 1.0 } };
        var ex = Assert.Throws<ArgumentException>(() => cs.Validate());
        Assert.Contains("WhitePoint", ex.Message);
        Assert.Contains("3", ex.Message);
    }
}
