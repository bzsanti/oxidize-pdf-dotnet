using System.Runtime.InteropServices;
using System.Text.Json;

namespace OxidizePdf.NET;

/// <summary>Bounded inspection and incremental editing of existing tagged PDFs.</summary>
public static class PdfTaggedOperations
{
    private static readonly JsonSerializerOptions JsonOptions = new() { MaxDepth = 1024 };
    /// <summary>Inspects structure, MCIDs, dictionaries, artifacts, and validation findings.</summary>
    public static Task<PdfTaggedValidation> InspectAsync(byte[] pdf, PdfTaggedLimits? limits = null, CancellationToken ct = default)
        => CallAsync<PdfTaggedValidation>(pdf, "inspect", [], limits, ct);
    /// <summary>Validates the requested mutations and reports which objects would change.</summary>
    public static Task<PdfTaggedEditPlan> PlanAsync(byte[] pdf, IEnumerable<PdfTaggedMutation> mutations,
        PdfTaggedLimits? limits = null, CancellationToken ct = default)
        => CallAsync<PdfTaggedEditPlan>(pdf, "plan", mutations, limits, ct);
    /// <summary>
    /// Appends an atomic tagged-structure revision. Preserves original bytes and checks
    /// upstream permissions. Returns validation before and after; inspect findings for
    /// remaining problems. Cancellation does not interrupt an active native operation.
    /// </summary>
    public static Task<PdfTaggedEditResult> EditAsync(byte[] pdf, IEnumerable<PdfTaggedMutation> mutations,
        PdfTaggedLimits? limits = null, CancellationToken ct = default)
        => CallAsync<PdfTaggedEditResult>(pdf, "apply", mutations, limits, ct);

    private static Task<T> CallAsync<T>(byte[] pdf, string action, IEnumerable<PdfTaggedMutation> mutations,
        PdfTaggedLimits? limits, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        ArgumentNullException.ThrowIfNull(mutations);
        if (pdf.Length == 0) throw new ArgumentException("PDF bytes cannot be empty", nameof(pdf));
        ct.ThrowIfCancellationRequested();
        var edits = mutations.Select(m => m switch
        {
            PdfTaggedMutation.CreateElement v => (object)new { operation = "CreateElement", parent = v.Parent, structure_type = v.StructureType, attributes = v.Attributes ?? [], index = v.Index },
            PdfTaggedMutation.SetElementAttribute v => new { operation = "SetElementAttribute", element = v.Element, key = v.Key, value = v.Value },
            PdfTaggedMutation.ReparentElement v => new { operation = "ReparentElement", element = v.Element, new_parent = v.NewParent, index = v.Index },
            PdfTaggedMutation.AssociateMcid v => new { operation = "AssociateMcid", element = v.Element, page = v.Page, mcid = v.Mcid },
            PdfTaggedMutation.SetParentTreeEntry v => new { operation = "SetParentTreeEntry", key = v.Key, value = v.Value },
            _ => throw new ArgumentException("Invalid tagged mutation", nameof(mutations))
        }).ToArray();
        var request = JsonSerializer.Serialize(new { action, limits = limits ?? new(), mutations = edits }, JsonOptions);
        return Task.Run(() => Call<T>(pdf, request), ct);
    }
    private static T Call<T>(byte[] pdf, string request)
    {
        var pinned = GCHandle.Alloc(pdf, GCHandleType.Pinned);
        IntPtr result = IntPtr.Zero;
        try
        {
            var status = TaggedNative(pinned.AddrOfPinnedObject(), (nuint)pdf.Length, request, out result);
            if (status != 0) throw new PdfExtractionException(NativeMethods.GetLastError() ?? "Tagged PDF operation failed");
            return JsonSerializer.Deserialize<T>(Marshal.PtrToStringUTF8(result)
                ?? throw new PdfExtractionException("Missing tagged PDF response"), JsonOptions)
                ?? throw new PdfExtractionException("Invalid tagged PDF response");
        }
        finally
        {
            if (result != IntPtr.Zero) NativeMethods.oxidize_free_string(result);
            pinned.Free();
        }
    }
    [DllImport("oxidize_pdf_ffi", EntryPoint = "oxidize_tagged_existing", CallingConvention = CallingConvention.Cdecl)]
    private static extern int TaggedNative(IntPtr pdf, nuint length,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string request, out IntPtr result);
}
