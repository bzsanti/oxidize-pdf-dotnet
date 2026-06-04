using OxidizePdf.NET.Graphics;
using Xunit;

namespace OxidizePdf.NET.Tests.Graphics;

public class LabColorTests
{
    [Fact]
    public void New_StoresComponentsAndColorSpace()
    {
        var cs = LabColorSpace.D50();
        var color = new LabColor(50.0, 10.0, -20.0, cs);
        Assert.Equal(50.0, color.L);
        Assert.Equal(10.0, color.A);
        Assert.Equal(-20.0, color.B);
        Assert.Same(cs, color.ColorSpace);
    }

    [Fact]
    public void New_NullColorSpace_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new LabColor(50.0, 0.0, 0.0, null!));
    }
}
