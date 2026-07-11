using System.Runtime.InteropServices;

namespace OxidizePdf.NET.Tests.Interop;

/// <summary>
/// Layout-pinning tests for the <c>ExtractionOptionsNative</c> interop struct.
/// These tests freeze the binary representation of the C# side so that any
/// field reordering or insertion breaks here loudly rather than producing
/// silently misaligned values when crossing the FFI boundary.
///
/// The mirror tests in Rust (<c>native/src/parser.rs</c>, <c>ffi_layout_tests</c>
/// module) assert the same offsets via <c>std::mem::offset_of!</c>; if either
/// side changes, both tests must move in lockstep.
/// </summary>
public class ExtractionOptionsNativeLayoutTests
{
    [Fact]
    public void ExtractionOptionsNative_TotalSizeIs72Bytes()
    {
        // Matches Rust #[repr(C)] ExtractionOptionsFFI on x86_64: 64 bytes
        // through IncludeArtifacts, plus ReorderColumns (bool, 3.1.0) at byte 58
        // and MaxExtractedBytes (nuint, 4.0.0) 8-byte-aligned at byte 64 = 72.
        Assert.Equal(72, Marshal.SizeOf<NativeMethods.ExtractionOptionsNative>());
    }

    [Fact]
    public void ExtractionOptionsNative_FieldOffsetsMatchRust()
    {
        AssertOffset(0, nameof(NativeMethods.ExtractionOptionsNative.PreserveLayout));
        AssertOffset(8, nameof(NativeMethods.ExtractionOptionsNative.SpaceThreshold));
        AssertOffset(16, nameof(NativeMethods.ExtractionOptionsNative.NewlineThreshold));
        AssertOffset(24, nameof(NativeMethods.ExtractionOptionsNative.SortByPosition));
        AssertOffset(25, nameof(NativeMethods.ExtractionOptionsNative.DetectColumns));
        AssertOffset(32, nameof(NativeMethods.ExtractionOptionsNative.ColumnThreshold));
        AssertOffset(40, nameof(NativeMethods.ExtractionOptionsNative.MergeHyphenated));
        AssertOffset(48, nameof(NativeMethods.ExtractionOptionsNative.TjSpaceThreshold));
        AssertOffset(56, nameof(NativeMethods.ExtractionOptionsNative.ReconstructParagraphs));
        AssertOffset(57, nameof(NativeMethods.ExtractionOptionsNative.IncludeArtifacts));
        AssertOffset(58, nameof(NativeMethods.ExtractionOptionsNative.ReorderColumns));
        AssertOffset(64, nameof(NativeMethods.ExtractionOptionsNative.MaxExtractedBytes));
    }

    private static void AssertOffset(int expected, string fieldName)
    {
        var actual = (int)Marshal.OffsetOf<NativeMethods.ExtractionOptionsNative>(fieldName);
        Assert.Equal(expected, actual);
    }
}
