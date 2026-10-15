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

    /// <summary>Runs core recovery in memory and reports remaining findings, including
    /// missing author metadata. Returns no PDF bytes. Unlike inspection, this checks
    /// editing permissions and rejects ambiguous recovery.</summary>
    public static Task<PdfTaggedValidation> PreflightAsync(byte[] pdf, PdfTaggedLimits? limits = null, CancellationToken ct = default)
        => CallAsync<PdfTaggedValidation>(pdf, "preflight", [], limits, ct);

    /// <summary>Prepares an internal baseline and plans subsequent mutations. The two
    /// object inventories are separate. A successful plan does not guarantee that
    /// final validation will permit publishing bytes.</summary>
    public static Task<PdfTaggedPreparationPlan> PlanPreparedAsync(byte[] pdf, IEnumerable<PdfTaggedMutation> mutations,
        PdfTaggedPreparationOptions preparation, PdfTaggedLimits? limits = null, CancellationToken ct = default)
        => CallAsync<PdfTaggedPreparationPlan>(pdf, "plan_prepared", mutations, limits, ct, preparation);

    /// <summary>Applies explicitly selected preparation and mutations. Returns owned
    /// bytes only after core final validation succeeds, preserving the exact input
    /// prefix. ValidationBefore describes the prepared baseline, not the original.
    /// Preparation cannot overwrite existing language or existing descriptions.</summary>
    public static Task<PdfTaggedPreparedEditResult> EditPreparedAsync(byte[] pdf, IEnumerable<PdfTaggedMutation> mutations,
        PdfTaggedPreparationOptions preparation, PdfTaggedLimits? limits = null, CancellationToken ct = default)
        => CallAsync<PdfTaggedPreparedEditResult>(pdf, "apply_prepared", mutations, limits, ct, preparation);

    private static Task<T> CallAsync<T>(byte[] pdf, string action, IEnumerable<PdfTaggedMutation> mutations,
        PdfTaggedLimits? limits, CancellationToken ct, PdfTaggedPreparationOptions? preparation = null)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        ArgumentNullException.ThrowIfNull(mutations);
        if (pdf.Length == 0) throw new ArgumentException("PDF bytes cannot be empty", nameof(pdf));
        ct.ThrowIfCancellationRequested();
        if (action is "plan_prepared" or "apply_prepared")
        {
            ArgumentNullException.ThrowIfNull(preparation);
            if (!preparation.RecoverIndexes && preparation.DocumentLanguage is null)
                throw new ArgumentException("Select index recovery or document language preparation", nameof(preparation));
            if (preparation.DocumentLanguage is not null && string.IsNullOrWhiteSpace(preparation.DocumentLanguage))
                throw new ArgumentException("Document language must not be blank", nameof(preparation));
        }
        var edits = mutations.Select(m => m switch
        {
            PdfTaggedMutation.CreateElement v => (object)new { operation = "CreateElement", parent = v.Parent, structure_type = v.StructureType, attributes = v.Attributes ?? [], index = v.Index },
            PdfTaggedMutation.SetElementAttribute v => new { operation = "SetElementAttribute", element = v.Element, key = v.Key, value = v.Value },
            PdfTaggedMutation.ReparentElement v => new { operation = "ReparentElement", element = v.Element, new_parent = v.NewParent, index = v.Index },
            PdfTaggedMutation.AssociateMcid v => new { operation = "AssociateMcid", element = v.Element, page = v.Page, mcid = v.Mcid },
            PdfTaggedMutation.SetParentTreeEntry v => new { operation = "SetParentTreeEntry", key = v.Key, value = v.Value },
            _ => throw new ArgumentException("Invalid tagged mutation", nameof(mutations))
        }).ToArray();
        // Keep the ordinary request schema compatible with the existing native API.
        object requestValue = preparation is null
            ? new { action, limits = limits ?? new(), mutations = edits }
            : new { action, limits = limits ?? new(), mutations = edits, preparation };
        var request = JsonSerializer.Serialize(requestValue, JsonOptions);
        var snapshot = pdf.ToArray();
        return Task.Run(() => Call<T>(snapshot, request, action is "preflight" or "plan_prepared" or "apply_prepared"), ct);
    }
    private static T Call<T>(byte[] pdf, string request, bool typedErrors)
    {
        var pinned = GCHandle.Alloc(pdf, GCHandleType.Pinned);
        IntPtr result = IntPtr.Zero;
        try
        {
            var status = TaggedNative(pinned.AddrOfPinnedObject(), (nuint)pdf.Length, request, out result);
            if (status != 0)
            {
                var message = NativeMethods.GetLastError() ?? "Tagged PDF operation failed";
                if (!typedErrors) throw new PdfExtractionException(message);
                var error = (NativeMethods.ErrorCode)status switch
                {
                    NativeMethods.ErrorCode.PermissionError => PdfTaggedError.PermissionDenied,
                    NativeMethods.ErrorCode.PdfParseError => PdfTaggedError.InvalidPdf,
                    NativeMethods.ErrorCode.InvalidArgument => PdfTaggedError.InvalidInputOrStructure,
                    NativeMethods.ErrorCode.IoError => PdfTaggedError.Io,
                    NativeMethods.ErrorCode.SerializationError => PdfTaggedError.Serialization,
                    _ => PdfTaggedError.NativeFailure,
                };
                throw new PdfTaggedException(error, message);
            }
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
