using System.Runtime.InteropServices;

namespace OxidizePdf.NET;

/// <summary>
/// Base class for owning a native oxidize-pdf handle. <see cref="SafeHandle"/>
/// guarantees the underlying <c>oxidize_*_free</c> runs exactly once, atomically,
/// regardless of whether <see cref="IDisposable.Dispose"/> is called multiple
/// times or races with the finalizer — eliminating the double-free that a manual
/// <c>IntPtr</c> + <c>bool</c> disposal pattern allows (issue #54).
/// </summary>
internal abstract class OxidizeSafeHandle : SafeHandle
{
    protected OxidizeSafeHandle()
        : base(invalidHandleValue: IntPtr.Zero, ownsHandle: true)
    {
    }

    protected OxidizeSafeHandle(IntPtr existing)
        : base(invalidHandleValue: IntPtr.Zero, ownsHandle: true)
    {
        SetHandle(existing);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;
}

internal sealed class DocumentSafeHandle : OxidizeSafeHandle
{
    public DocumentSafeHandle() { }

    public DocumentSafeHandle(IntPtr existing) : base(existing) { }

    protected override bool ReleaseHandle()
    {
        NativeMethods.oxidize_document_free(handle);
        return true;
    }
}

internal sealed class PageSafeHandle : OxidizeSafeHandle
{
    public PageSafeHandle() { }

    public PageSafeHandle(IntPtr existing) : base(existing) { }

    protected override bool ReleaseHandle()
    {
        NativeMethods.oxidize_page_free(handle);
        return true;
    }
}

internal sealed class ImageSafeHandle : OxidizeSafeHandle
{
    public ImageSafeHandle() { }

    public ImageSafeHandle(IntPtr existing) : base(existing) { }

    protected override bool ReleaseHandle()
    {
        NativeMethods.oxidize_image_free(handle);
        return true;
    }
}

internal sealed class TableBuilderSafeHandle : OxidizeSafeHandle
{
    public TableBuilderSafeHandle() { }

    public TableBuilderSafeHandle(IntPtr existing) : base(existing) { }

    protected override bool ReleaseHandle()
    {
        NativeMethods.oxidize_table_builder_free(handle);
        return true;
    }
}

internal sealed class TextFlowSafeHandle : OxidizeSafeHandle
{
    public TextFlowSafeHandle() { }

    public TextFlowSafeHandle(IntPtr existing) : base(existing) { }

    protected override bool ReleaseHandle()
    {
        NativeMethods.oxidize_text_flow_free(handle);
        return true;
    }
}

internal sealed class FlowLayoutSafeHandle : OxidizeSafeHandle
{
    public FlowLayoutSafeHandle() { }

    public FlowLayoutSafeHandle(IntPtr existing) : base(existing) { }

    protected override bool ReleaseHandle()
    {
        NativeMethods.oxidize_flow_layout_free(handle);
        return true;
    }
}

internal sealed class DocumentBuilderSafeHandle : OxidizeSafeHandle
{
    public DocumentBuilderSafeHandle() { }

    public DocumentBuilderSafeHandle(IntPtr existing) : base(existing) { }

    protected override bool ReleaseHandle()
    {
        NativeMethods.oxidize_document_builder_free(handle);
        return true;
    }
}
