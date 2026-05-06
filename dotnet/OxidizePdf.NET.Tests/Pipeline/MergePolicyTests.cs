using OxidizePdf.NET.Pipeline;

namespace OxidizePdf.NET.Tests.Pipeline;

public class MergePolicyTests
{
    [Fact]
    public void MergePolicy_discriminants_match_rust_enum_order()
    {
        Assert.Equal((byte)0, (byte)MergePolicy.SameTypeOnly);
        Assert.Equal((byte)1, (byte)MergePolicy.AnyInlineContent);
    }
}
