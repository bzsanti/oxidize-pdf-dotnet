namespace OxidizePdf.NET.Tests;

/// <summary>
/// Tests for public enum types: StandardFont and PdfPermissions.
/// </summary>
public class EnumTests
{
    [Fact]
    public void StandardFont_HasAll14BaseValues()
    {
        var values = Enum.GetValues<StandardFont>();
        Assert.Equal(14, values.Length);
    }

    [Fact]
    public void StandardFont_Helvetica_IsZero()
    {
        Assert.Equal(0, (int)StandardFont.Helvetica);
    }

    [Fact]
    public void StandardFont_ZapfDingbats_Is13()
    {
        Assert.Equal(13, (int)StandardFont.ZapfDingbats);
    }

    [Fact]
    public void StandardFont_AllValuesAreContiguous()
    {
        var values = Enum.GetValues<StandardFont>().Select(v => (int)v).OrderBy(v => v).ToArray();
        for (int i = 0; i < values.Length; i++)
        {
            Assert.Equal(i, values[i]);
        }
    }

    [Fact]
    public void PdfPermissions_None_IsZero()
    {
        Assert.Equal(0u, (uint)PdfPermissions.None);
    }

    [Fact]
    public void PdfPermissions_All_Is0xFF()
    {
        Assert.Equal(0xFFu, (uint)PdfPermissions.All);
    }

    [Fact]
    public void PdfPermissions_Print_Is0x01()
    {
        Assert.Equal(0x01u, (uint)PdfPermissions.Print);
    }

    [Fact]
    public void PdfPermissions_FlagsCombine_Correctly()
    {
        var combined = PdfPermissions.Print | PdfPermissions.Copy;
        Assert.Equal(0x03u, (uint)combined);
    }

    [Fact]
    public void PdfPermissions_All_IncludesAllIndividualFlags()
    {
        var all = PdfPermissions.All;
        Assert.True(all.HasFlag(PdfPermissions.Print));
        Assert.True(all.HasFlag(PdfPermissions.Copy));
        Assert.True(all.HasFlag(PdfPermissions.ModifyContents));
        Assert.True(all.HasFlag(PdfPermissions.ModifyAnnotations));
        Assert.True(all.HasFlag(PdfPermissions.FillForms));
        Assert.True(all.HasFlag(PdfPermissions.Accessibility));
        Assert.True(all.HasFlag(PdfPermissions.Assemble));
        Assert.True(all.HasFlag(PdfPermissions.PrintHighQuality));
    }

    [Fact]
    public void PdfPermissions_IndividualBits_AreDistinct()
    {
        var flags = new[]
        {
            PdfPermissions.Print,
            PdfPermissions.Copy,
            PdfPermissions.ModifyContents,
            PdfPermissions.ModifyAnnotations,
            PdfPermissions.FillForms,
            PdfPermissions.Accessibility,
            PdfPermissions.Assemble,
            PdfPermissions.PrintHighQuality,
        };

        // Each flag should be a power of two (single bit)
        foreach (var flag in flags)
        {
            var value = (uint)flag;
            Assert.True((value & (value - 1)) == 0, $"{flag} is not a power of two");
        }
    }
}
