using FluentAssertions;
using OxidizePdf.NET.Graphics;
using Xunit;

namespace OxidizePdf.NET.Tests.Graphics;

public class CalRgbColorSpaceTests
{
    [Fact]
    public void SRgb_Factory_HasExpectedWhitePoint()
    {
        var cs = CalRgbColorSpace.SRgb();
        cs.WhitePoint[0].Should().BeApproximately(0.9505, 0.0001);
        cs.WhitePoint[1].Should().BeApproximately(1.0000, 0.0001);
        cs.WhitePoint[2].Should().BeApproximately(1.0890, 0.0001);
    }

    [Fact]
    public void SRgb_Factory_HasCorrectGamma()
    {
        var cs = CalRgbColorSpace.SRgb();
        cs.Gamma.R.Should().BeApproximately(2.2, 0.01);
        cs.Gamma.G.Should().BeApproximately(2.2, 0.01);
        cs.Gamma.B.Should().BeApproximately(2.2, 0.01);
    }

    [Fact]
    public void SRgb_Factory_HasNineElementMatrix()
    {
        CalRgbColorSpace.SRgb().Matrix.Should().HaveCount(9);
    }

    [Fact]
    public void AdobeRgb_Factory_HasExpectedWhitePointY()
    {
        CalRgbColorSpace.AdobeRgb().WhitePoint[1].Should().BeApproximately(1.0, 0.0001);
    }

    [Fact]
    public void Validate_AcceptsValidSRgb()
    {
        CalRgbColorSpace.SRgb().Invoking(x => x.Validate()).Should().NotThrow();
    }

    [Fact]
    public void Validate_RejectsWrongMatrixLength()
    {
        var cs = CalRgbColorSpace.SRgb() with { Matrix = new double[8] };
        cs.Invoking(x => x.Validate()).Should().Throw<ArgumentException>()
            .WithMessage("*Matrix*9*");
    }

    [Fact]
    public void Validate_RejectsNonPositiveGamma()
    {
        var cs = CalRgbColorSpace.SRgb() with { Gamma = (-1.0, 2.2, 2.2) };
        cs.Invoking(x => x.Validate()).Should().Throw<ArgumentException>()
            .WithMessage("*Gamma*");
    }

    [Fact]
    public void Validate_RejectsWhitePointYNotOne()
    {
        var cs = CalRgbColorSpace.SRgb() with { WhitePoint = new double[] { 0.9505, 0.5, 1.089 } };
        cs.Invoking(x => x.Validate()).Should().Throw<ArgumentException>()
            .WithMessage("*WhitePoint*Y*");
    }
}
