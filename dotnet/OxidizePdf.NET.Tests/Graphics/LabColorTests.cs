using FluentAssertions;
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
        color.L.Should().Be(50.0);
        color.A.Should().Be(10.0);
        color.B.Should().Be(-20.0);
        color.ColorSpace.Should().BeSameAs(cs);
    }

    [Fact]
    public void New_NullColorSpace_Throws()
    {
        Action act = () => new LabColor(50.0, 0.0, 0.0, null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
