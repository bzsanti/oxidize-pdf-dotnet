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
        Assert.Equal("MyRGB", profile.Name);
        Assert.Equal(IccColorSpace.Rgb, profile.ColorSpace);
        Assert.Equal(128, profile.Data.Length);
    }

    [Fact]
    public void New_NullName_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new IccProfile(null!, MinimalData(), IccColorSpace.Rgb));
    }

    [Fact]
    public void New_NullData_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new IccProfile("MyRGB", null!, IccColorSpace.Rgb));
    }

    [Fact]
    public void Validate_EmptyData_Throws()
    {
        var profile = new IccProfile("MyRGB", Array.Empty<byte>(), IccColorSpace.Rgb);
        var ex = Assert.Throws<ArgumentException>(() => profile.Validate());
        Assert.Contains("data", ex.Message);
    }

    [Fact]
    public void Validate_ValidProfile_DoesNotThrow()
    {
        // A throw here fails the test.
        new IccProfile("MyRGB", MinimalData(), IccColorSpace.Rgb).Validate();
    }

    [Fact]
    public void Validate_UnknownColorSpaceEnum_Throws()
    {
        var profile = new IccProfile("Bad", MinimalData(), (IccColorSpace)99);
        Assert.Throws<ArgumentException>(() => profile.Validate());
    }

    [Fact]
    public void ComponentCount_Rgb_IsThree()
    {
        Assert.Equal(3, IccColorSpace.Rgb.ComponentCount());
    }

    [Fact]
    public void ComponentCount_Gray_IsOne()
    {
        Assert.Equal(1, IccColorSpace.Gray.ComponentCount());
    }

    [Fact]
    public void ComponentCount_Cmyk_IsFour()
    {
        Assert.Equal(4, IccColorSpace.Cmyk.ComponentCount());
    }

    [Fact]
    public void PageColorSpace_IccBased_StoresNAndAlternate()
    {
        var pcs = PageColorSpace.IccBased(3, "DeviceRGB");
        Assert.Equal(PageColorSpaceKind.IccBased, pcs.Kind);
        Assert.Equal(3, pcs.IccN);
        Assert.Equal("DeviceRGB", pcs.IccAlternate);
    }

    [Fact]
    public void PageColorSpace_CalGray_StoresColorSpace()
    {
        var cs = CalGrayColorSpace.D65();
        var pcs = PageColorSpace.CalGray(cs);
        Assert.Equal(PageColorSpaceKind.CalGray, pcs.Kind);
        Assert.Same(cs, pcs.CalGrayCs);
    }
}
