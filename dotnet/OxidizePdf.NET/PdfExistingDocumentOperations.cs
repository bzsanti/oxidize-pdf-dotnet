using System.Runtime.InteropServices;
using System.Text.Json;

namespace OxidizePdf.NET;

/// <summary>Policy-driven operations on existing PDFs. Page indexes are zero-based.</summary>
public static class PdfExistingDocumentOperations
{
    /// <summary>Plan a merge and inspect preservation or semantic loss without producing a PDF.</summary>
    public static Task<PdfExistingDocumentResult> PlanMergeAsync(IEnumerable<PdfExistingMergeInput> inputs, PdfExistingDocumentPolicy policy, CancellationToken ct = default) =>
        Merge(inputs, policy, true, ct);
    /// <summary>Merge PDFs with an explicit policy and return its semantic report.</summary>
    public static Task<PdfExistingDocumentResult> MergeAsync(IEnumerable<PdfExistingMergeInput> inputs, PdfExistingDocumentPolicy policy, CancellationToken ct = default) =>
        Merge(inputs, policy, false, ct);
    /// <summary>Plan extraction of selected pages.</summary>
    public static Task<PdfExistingDocumentResult> PlanExtractAsync(byte[] pdf, IEnumerable<int> pages, PdfExistingDocumentPolicy policy, CancellationToken ct = default) =>
        Run([Input(pdf)], new { operation = "extract", pages = Pages(pages), policy = Policy(policy) }, true, ct);
    /// <summary>Extract selected pages under the chosen preservation policy.</summary>
    public static Task<PdfExistingDocumentResult> ExtractAsync(byte[] pdf, IEnumerable<int> pages, PdfExistingDocumentPolicy policy, CancellationToken ct = default) =>
        Run([Input(pdf)], new { operation = "extract", pages = Pages(pages), policy = Policy(policy) }, false, ct);
    /// <summary>Plan split outputs, each defined by an explicit page selection.</summary>
    public static Task<PdfExistingDocumentResult> PlanSplitAsync(byte[] pdf, IEnumerable<IEnumerable<int>> groups, PdfExistingDocumentPolicy policy, CancellationToken ct = default) =>
        Split(pdf, groups, policy, true, ct);
    /// <summary>Split into selected groups, returning a semantic report per output.</summary>
    public static Task<PdfExistingDocumentResult> SplitAsync(byte[] pdf, IEnumerable<IEnumerable<int>> groups, PdfExistingDocumentPolicy policy, CancellationToken ct = default) =>
        Split(pdf, groups, policy, false, ct);
    /// <summary>Plan an atomic preserving mutation batch. Indexes refer to the state after preceding mutations.</summary>
    public static Task<PdfExistingDocumentResult> PlanMutationsAsync(byte[] pdf, IEnumerable<PdfPageMutation> mutations, CancellationToken ct = default) =>
        Mutate(pdf, mutations, true, ct);
    /// <summary>Apply an atomic incremental mutation batch with the original PDF as an exact prefix.</summary>
    public static Task<PdfExistingDocumentResult> MutateAsync(byte[] pdf, IEnumerable<PdfPageMutation> mutations, CancellationToken ct = default) =>
        Mutate(pdf, mutations, false, ct);
    /// <summary>Plan an exact permutation of every page.</summary>
    public static Task<PdfExistingDocumentResult> PlanReorderAsync(byte[] pdf, IEnumerable<int> order, CancellationToken ct = default) =>
        Run([Input(pdf)], new { operation = "reorder", order = Pages(order) }, true, ct);
    /// <summary>Reorder every page losslessly; order must be an exact permutation.</summary>
    public static Task<PdfExistingDocumentResult> ReorderAsync(byte[] pdf, IEnumerable<int> order, CancellationToken ct = default) =>
        Run([Input(pdf)], new { operation = "reorder", order = Pages(order) }, false, ct);

    private static string Policy(PdfExistingDocumentPolicy policy)
    {
        if (!Enum.IsDefined(policy)) throw new ArgumentOutOfRangeException(nameof(policy));
        return policy.ToString();
    }
    private static object Input(byte[] pdf, int[]? pages = null)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        if (pdf.Length == 0) throw new ArgumentException("PDF bytes cannot be empty", nameof(pdf));
        return new { pdf, pages };
    }
    private static int[] Pages(IEnumerable<int> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var values = pages.ToArray();
        if (values.Length == 0 || values.Any(p => p < 0)) throw new ArgumentException("A nonempty selection of zero-based pages is required", nameof(pages));
        return values;
    }
    private static Task<PdfExistingDocumentResult> Merge(IEnumerable<PdfExistingMergeInput> inputs, PdfExistingDocumentPolicy policy, bool plan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ct.ThrowIfCancellationRequested();
        var values = inputs.Select(i => { ArgumentNullException.ThrowIfNull(i); return Input(i.PdfBytes, i.Pages is null ? null : Pages(i.Pages)); }).ToArray();
        if (values.Length == 0) throw new ArgumentException("At least one PDF is required", nameof(inputs));
        return Run(values, new { operation = "merge", policy = Policy(policy) }, plan, ct);
    }
    private static Task<PdfExistingDocumentResult> Split(byte[] pdf, IEnumerable<IEnumerable<int>> groups, PdfExistingDocumentPolicy policy, bool plan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ct.ThrowIfCancellationRequested();
        var selections = groups.Select(Pages).ToArray();
        if (selections.Length == 0) throw new ArgumentException("At least one output group is required", nameof(groups));
        return Run([Input(pdf)], new { operation = "split", groups = selections, policy = Policy(policy) }, plan, ct);
    }
    private static Task<PdfExistingDocumentResult> Mutate(byte[] pdf, IEnumerable<PdfPageMutation> mutations, bool plan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(mutations);
        ct.ThrowIfCancellationRequested();
        var inputs = new List<object> { Input(pdf) };
        static int Index(int value) => value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
        var operations = mutations.Select(m =>
        {
            switch (m)
            {
                case PdfPageMutation.Move move: return (object)new { operation = "move", from = Index(move.From), to = Index(move.To) };
                case PdfPageMutation.Rotate rotate when rotate.Degrees % 90 == 0:
                    return new { operation = "rotate", page = Index(rotate.Page), degrees = rotate.Degrees };
                case PdfPageMutation.Delete delete: return new { operation = "delete", page = Index(delete.Page) };
                case PdfPageMutation.Duplicate duplicate: return new { operation = "duplicate", page = Index(duplicate.Page), at = Index(duplicate.At) };
                case PdfPageMutation.Insert insert:
                    var source = inputs.Count; inputs.Add(Input(insert.SourcePdf));
                    return new { operation = "insert", source_input = source, page = Index(insert.Page), at = Index(insert.At) };
                default: throw new ArgumentException("Invalid page mutation", nameof(mutations));
            }
        }).ToArray();
        if (operations.Length == 0) throw new ArgumentException("At least one mutation is required", nameof(mutations));
        return Run(inputs.ToArray(), new { operation = "mutate", mutations = operations }, plan, ct);
    }
    private static Task<PdfExistingDocumentResult> Run(object[] inputs, object action, bool plan, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var request = JsonSerializer.Serialize(new { inputs, plan_only = plan, action });
        return Task.Run(() =>
        {
            IntPtr result = IntPtr.Zero;
            try
            {
                var status = NativeMethods.oxidize_existing_pdf_json(request, out result);
                if (status != 0)
                    throw new PdfExtractionException(NativeMethods.GetLastError() ?? "Existing PDF operation failed");
                return JsonSerializer.Deserialize<PdfExistingDocumentResult>(Marshal.PtrToStringUTF8(result)
                    ?? throw new PdfExtractionException("Missing operation report"))
                    ?? throw new PdfExtractionException("Invalid operation report");
            }
            finally { if (result != IntPtr.Zero) NativeMethods.oxidize_free_string(result); }
        }, ct);
    }
}
