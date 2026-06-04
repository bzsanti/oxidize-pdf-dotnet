using OxidizePdf.NET.Graphics;
using Xunit;

namespace OxidizePdf.NET.Tests.Graphics;

public class CalRgbColorSpaceTests
{
    [Fact]
    public void SRgb_Factory_HasExpectedWhitePoint()
    {
        var cs = CalRgbColorSpace.SRgb();
        Assert.Equal(0.9505, cs.WhitePoint[0], 4);
        Assert.Equal(1.0000, cs.WhitePoint[1], 4);
        Assert.Equal(1.0890, cs.WhitePoint[2], 4);
    }

    [Fact]
    public void SRgb_Factory_HasCorrectGamma()
    {
        var cs = CalRgbColorSpace.SRgb();
        Assert.Equal(2.2, cs.Gamma.R, 2);
        Assert.Equal(2.2, cs.Gamma.G, 2);
        Assert.Equal(2.2, cs.Gamma.B, 2);
    }

    [Fact]
    public void SRgb_Factory_HasNineElementMatrix()
    {
        Assert.Equal(9, CalRgbColorSpace.SRgb().Matrix.Length);
    }

    [Fact]
    public void AdobeRgb_Factory_HasExpectedWhitePointY()
    {
        Assert.Equal(1.0, CalRgbColorSpace.AdobeRgb().WhitePoint[1], 4);
    }

    [Fact]
    public void Validate_AcceptsValidSRgb()
    {
        // A throw here fails the test.
        CalRgbColorSpace.SRgb().Validate();
    }

    [Fact]
    public void Validate_RejectsWrongMatrixLength()
    {
        var cs = CalRgbColorSpace.SRgb() with { Matrix = new double[8] };
        var ex = Assert.Throws<ArgumentException>(() => cs.Validate());
        Assert.Contains("Matrix", ex.Message);
        Assert.Contains("9", ex.Message);
    }

    [Fact]
    public void Validate_RejectsNonPositiveGamma()
    {
        var cs = CalRgbColorSpace.SRgb() with { Gamma = (-1.0, 2.2, 2.2) };
        var ex = Assert.Throws<ArgumentException>(() => cs.Validate());
        Assert.Contains("Gamma", ex.Message);
    }

    [Fact]
    public void Validate_RejectsWhitePointYNotOne()
    {
        var cs = CalRgbColorSpace.SRgb() with { WhitePoint = new double[] { 0.9505, 0.5, 1.089 } };
        var ex = Assert.Throws<ArgumentException>(() => cs.Validate());
        Assert.Contains("WhitePoint", ex.Message);
        Assert.Contains("Y", ex.Message);
    }
}
