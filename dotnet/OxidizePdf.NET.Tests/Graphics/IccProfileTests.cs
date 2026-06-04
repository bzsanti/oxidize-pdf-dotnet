using FluentAssertions;
using OxidizePdf.NET.Graphics;
using Xunit;

namespace OxidizePdf.NET.Tests.Graphics;

public class IccProfileTests
{
    private static byte[] MinimalData() => new byte[128]; // minimal non-empty block

    [Fact]
    public void New_StoresNameDataAndColorSpace()
    {
        var profile = new IccProfile("MyRGB", MinimalData(), IccColorSpace.Rgb);
        profile.Name.Should().Be("MyRGB");
        profile.ColorSpace.Should().Be(IccColorSpace.Rgb);
        profile.Data.Should().HaveCount(128);
    }

    [Fact]
    public void New_NullName_Throws()
    {
        Action act = () => new IccProfile(null!, MinimalData(), IccColorSpace.Rgb);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void New_NullData_Throws()
    {
        Action act = () => new IccProfile("MyRGB", null!, IccColorSpace.Rgb);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Validate_EmptyData_Throws()
    {
        var profile = new IccProfile("MyRGB", Array.Empty<byte>(), IccColorSpace.Rgb);
        profile.Invoking(x => x.Validate()).Should().Throw<ArgumentException>()
            .WithMessage("*data*");
    }

    [Fact]
    public void Validate_ValidProfile_DoesNotThrow()
    {
        var profile = new IccProfile("MyRGB", MinimalData(), IccColorSpace.Rgb);
        profile.Invoking(x => x.Validate()).Should().NotThrow();
    }

    [Fact]
    public void ComponentCount_Rgb_IsThree()
    {
        IccColorSpace.Rgb.ComponentCount().Should().Be(3);
    }

    [Fact]
    public void ComponentCount_Gray_IsOne()
    {
        IccColorSpace.Gray.ComponentCount().Should().Be(1);
    }

    [Fact]
    public void ComponentCount_Cmyk_IsFour()
    {
        IccColorSpace.Cmyk.ComponentCount().Should().Be(4);
    }

    [Fact]
    public void PageColorSpace_IccBased_StoresNAndAlternate()
    {
        var pcs = PageColorSpace.IccBased(3, "DeviceRGB");
        pcs.Kind.Should().Be(PageColorSpaceKind.IccBased);
        pcs.IccN.Should().Be(3);
        pcs.IccAlternate.Should().Be("DeviceRGB");
    }

    [Fact]
    public void PageColorSpace_CalGray_StoresColorSpace()
    {
        var cs = CalGrayColorSpace.D65();
        var pcs = PageColorSpace.CalGray(cs);
        pcs.Kind.Should().Be(PageColorSpaceKind.CalGray);
        pcs.CalGrayCs.Should().BeSameAs(cs);
    }
}
