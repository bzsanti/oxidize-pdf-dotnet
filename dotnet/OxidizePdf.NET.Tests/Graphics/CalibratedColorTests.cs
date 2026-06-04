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
        Assert.True(color.IsCalGray);
        Assert.Equal(0.5, color.GrayValue);
        Assert.Same(cs, color.GrayColorSpace);
    }

    [Fact]
    public void CalRgb_StoresComponentsAndColorSpace()
    {
        var cs = CalRgbColorSpace.SRgb();
        var color = CalibratedColor.CalRgb(new double[] { 0.2, 0.4, 0.8 }, cs);
        Assert.False(color.IsCalGray);
        Assert.Equal(new double[] { 0.2, 0.4, 0.8 }, color.RgbValues);
        Assert.Same(cs, color.RgbColorSpace);
    }

    [Fact]
    public void CalGray_NullColorSpace_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CalibratedColor.CalGray(0.5, null!));
    }

    [Fact]
    public void CalRgb_WrongComponentCount_Throws()
    {
        var cs = CalRgbColorSpace.SRgb();
        var ex = Assert.Throws<ArgumentException>(() => CalibratedColor.CalRgb(new double[] { 0.5, 0.5 }, cs));
        Assert.Contains("3", ex.Message);
    }
}
