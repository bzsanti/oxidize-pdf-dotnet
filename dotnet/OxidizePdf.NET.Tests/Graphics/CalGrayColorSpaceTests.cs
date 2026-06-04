using FluentAssertions;
using OxidizePdf.NET.Graphics;
using Xunit;

namespace OxidizePdf.NET.Tests.Graphics;

public class CalGrayColorSpaceTests
{
    [Fact]
    public void DefaultConstructor_SetsReasonableDefaults()
    {
        var cs = new CalGrayColorSpace();
        cs.WhitePoint.Should().BeEquivalentTo(new double[] { 0.9505, 1.0, 1.0890 });
        cs.BlackPoint.Should().BeEquivalentTo(new double[] { 0.0, 0.0, 0.0 });
        cs.Gamma.Should().Be(1.0);
    }

    [Fact]
    public void D50_Factory_MatchesD50Illuminant()
    {
        var cs = CalGrayColorSpace.D50();
        cs.WhitePoint[0].Should().BeApproximately(0.9642, 0.0001);
        cs.WhitePoint[1].Should().BeApproximately(1.0000, 0.0001);
        cs.WhitePoint[2].Should().BeApproximately(0.8251, 0.0001);
    }

    [Fact]
    public void D65_Factory_MatchesD65Illuminant()
    {
        var cs = CalGrayColorSpace.D65();
        cs.WhitePoint[0].Should().BeApproximately(0.9505, 0.0001);
        cs.WhitePoint[1].Should().BeApproximately(1.0000, 0.0001);
        cs.WhitePoint[2].Should().BeApproximately(1.0890, 0.0001);
    }

    [Fact]
    public void Validate_AcceptsValidWhitePoint()
    {
        CalGrayColorSpace.D65().Invoking(x => x.Validate()).Should().NotThrow();
    }

    [Fact]
    public void Validate_RejectsNegativeWhitePointY()
    {
        var cs = new CalGrayColorSpace { WhitePoint = new double[] { 0.9505, -0.1, 1.089 } };
        cs.Invoking(x => x.Validate()).Should().Throw<ArgumentException>()
            .WithMessage("*WhitePoint*");
    }

    [Fact]
    public void Validate_RejectsWhitePointYNotOne()
    {
        var cs = new CalGrayColorSpace { WhitePoint = new double[] { 0.9505, 0.5, 1.089 } };
        cs.Invoking(x => x.Validate()).Should().Throw<ArgumentException>()
            .WithMessage("*WhitePoint*Y*");
    }

    [Fact]
    public void Validate_RejectsNonPositiveGamma()
    {
        var cs = CalGrayColorSpace.D65() with { Gamma = 0.0 };
        cs.Invoking(x => x.Validate()).Should().Throw<ArgumentException>()
            .WithMessage("*Gamma*");
    }

    [Fact]
    public void Validate_RejectsWrongWhitePointLength()
    {
        var cs = new CalGrayColorSpace { WhitePoint = new double[] { 0.9505, 1.0 } };
        cs.Invoking(x => x.Validate()).Should().Throw<ArgumentException>()
            .WithMessage("*WhitePoint*3*");
    }
}
