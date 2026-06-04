using FluentAssertions;
using OxidizePdf.NET.Graphics;
using Xunit;

namespace OxidizePdf.NET.Tests.Graphics;

public class CalibratedColorTests
{
    [Fact]
    public void CalGray_StoresValueAndColorSpace()
    {
        var cs = CalGrayColorSpace.D65();
        var color = CalibratedColor.CalGray(0.5, cs);
        color.IsCalGray.Should().BeTrue();
        color.GrayValue.Should().Be(0.5);
        color.GrayColorSpace.Should().BeSameAs(cs);
    }

    [Fact]
    public void CalRgb_StoresComponentsAndColorSpace()
    {
        var cs = CalRgbColorSpace.SRgb();
        var color = CalibratedColor.CalRgb(new double[] { 0.2, 0.4, 0.8 }, cs);
        color.IsCalGray.Should().BeFalse();
        color.RgbValues.Should().BeEquivalentTo(new double[] { 0.2, 0.4, 0.8 });
        color.RgbColorSpace.Should().BeSameAs(cs);
    }

    [Fact]
    public void CalGray_NullColorSpace_Throws()
    {
        Action act = () => CalibratedColor.CalGray(0.5, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CalRgb_WrongComponentCount_Throws()
    {
        var cs = CalRgbColorSpace.SRgb();
        Action act = () => CalibratedColor.CalRgb(new double[] { 0.5, 0.5 }, cs);
        act.Should().Throw<ArgumentException>().WithMessage("*3*");
    }
}
