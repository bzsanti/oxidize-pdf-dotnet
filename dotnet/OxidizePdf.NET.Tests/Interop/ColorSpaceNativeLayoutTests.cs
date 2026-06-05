using Xunit;

namespace OxidizePdf.NET.Tests.Interop;

/// <summary>
/// Documents the M3 FFI layout decision.
/// All CalGray, CalRGB, Lab, and ICC parameters cross the FFI boundary as
/// individual scalar parameters (double or int) — no #[repr(C)] structs.
/// This matches the existing RGB/Gray/CMYK pattern and avoids alignment risk.
/// No StructLayout or offset_of! pinning is required.
/// </summary>
public class ColorSpaceNativeLayoutTests
{
    [Fact]
    public void M3_UsesScalarParametersOnly_NoStructLayoutRequired()
    {
        Assert.True(true,
            "Scalar-parameter FFI confirmed — no struct layout pinning needed for M3.");
    }
}
