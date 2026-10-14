using System.Runtime.InteropServices;
using System.Text.Json;

namespace OxidizePdf.NET;

public static partial class PdfOperations
{
    /// <summary>Lists standard Geometric annotations with stable identities.</summary>
    public static Task<IReadOnlyList<PdfGeometricAnnotation>> GetGeometricAnnotationsAsync(
        byte[] pdfBytes, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0) throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ct.ThrowIfCancellationRequested();
        return Task.Run<IReadOnlyList<PdfGeometricAnnotation>>(() => CallGeometric(pdfBytes, null).Annotations, ct);
    }

    /// <summary>
    /// Applies Add/Update/Remove operations atomically in one incremental revision.
    /// Updates preserve identities and unrelated keys. Encrypted inputs are rejected;
    /// upstream enforces DocMDP annotation-modification permissions.
    /// </summary>
    public static Task<PdfGeometricUpdate> EditGeometricAnnotationsAsync(
        byte[] pdfBytes, IEnumerable<PdfGeometricMutation> mutations, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);
        ArgumentNullException.ThrowIfNull(mutations);
        if (pdfBytes.Length == 0) throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ct.ThrowIfCancellationRequested();
        var dtos = mutations.Select(mutation =>
        {
            switch (mutation)
            {
                case PdfGeometricMutation.Add add:
                    ValidateGeometric(add.Geometry, add.Style);
                    return (object)new { operation = "add", page_index = add.PageIndex, geometry = add.Geometry, style = add.Style };
                case PdfGeometricMutation.Update update when update.Id.ObjectNumber > 0:
                    ValidateGeometric(update.Geometry, update.Style);
                    return new { operation = "update", object_number = update.Id.ObjectNumber, generation_number = update.Id.GenerationNumber,
                        geometry = update.Geometry, style = update.Style };
                case PdfGeometricMutation.Remove remove when remove.Id.ObjectNumber > 0:
                    return new { operation = "remove", object_number = remove.Id.ObjectNumber, generation_number = remove.Id.GenerationNumber };
                default:
                    throw new ArgumentException("Invalid Geometric mutation or identity", nameof(mutations));
            }
        }).ToArray();
        if (dtos.Length == 0) throw new ArgumentException("At least one mutation is required", nameof(mutations));
        var json = JsonSerializer.Serialize(dtos);
        ct.ThrowIfCancellationRequested();
        return Task.Run(() => CallGeometric(pdfBytes, json), ct);
    }

    private static void ValidateGeometric(PdfGeometricGeometry geometry, PdfGeometricStyle style)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(style);
        if (!Enum.IsDefined(geometry.Kind)) throw new ArgumentOutOfRangeException(nameof(geometry));
        static void Coordinates(double[]? values, int count)
        {
            if (values is null || values.Length != count || values.Any(v => !double.IsFinite(v)))
                throw new ArgumentException("Geometry requires finite coordinates of the expected length");
        }
        var line = geometry.Kind == PdfGeometricKind.Line;
        var vertices = geometry.Kind is PdfGeometricKind.Polygon or PdfGeometricKind.PolyLine;
        var endings = line || geometry.Kind == PdfGeometricKind.PolyLine;
        if (line) { Coordinates(geometry.Start, 2); Coordinates(geometry.End, 2); }
        else if (geometry.Start is not null || geometry.End is not null) throw new ArgumentException("Only Line accepts start/end coordinates");
        if (vertices)
        {
            if (geometry.Vertices is null || geometry.Vertices.Length < (geometry.Kind == PdfGeometricKind.Polygon ? 3 : 2))
                throw new ArgumentException("Insufficient vertices");
            foreach (var vertex in geometry.Vertices) Coordinates(vertex, 2);
        }
        else if (geometry.Vertices is not null) throw new ArgumentException("Unexpected vertices");
        if (geometry.Kind is PdfGeometricKind.Square or PdfGeometricKind.Circle) Coordinates(geometry.Rectangle, 4);
        else if (geometry.Rectangle is not null) throw new ArgumentException("Unexpected rectangle");
        if (endings)
        {
            if (geometry.StartEnding is not { } start || !Enum.IsDefined(start) || geometry.EndEnding is not { } end || !Enum.IsDefined(end))
                throw new ArgumentException("Line and PolyLine require valid start and end decorations");
        }
        else if (geometry.StartEnding is not null || geometry.EndEnding is not null) throw new ArgumentException("Unexpected line decorations");
        static void Color(double[] color)
        {
            if (color is null || color.Length is not (1 or 3 or 4) || color.Any(v => !double.IsFinite(v) || v < 0 || v > 1))
                throw new ArgumentException("Color requires 1, 3 or 4 components in [0, 1]");
        }
        Color(style.StrokeColor);
        if (style.FillColor is not null) Color(style.FillColor);
        if (!double.IsFinite(style.Width) || style.Width <= 0) throw new ArgumentOutOfRangeException(nameof(style.Width));
        if (style.Opacity is double opacity && (!double.IsFinite(opacity) || opacity < 0 || opacity > 1))
            throw new ArgumentOutOfRangeException(nameof(style.Opacity));
        if (style.DashPattern is { } dash && (dash.Length is < 1 or > 256 || dash.Any(v => !double.IsFinite(v) || v < 0) || dash.All(v => v == 0)))
            throw new ArgumentException("Dash pattern must be bounded, nonnegative and not all zero");
    }

    private static PdfGeometricUpdate CallGeometric(byte[] pdfBytes, string? mutations)
    {
        var pinned = GCHandle.Alloc(pdfBytes, GCHandleType.Pinned);
        IntPtr result = IntPtr.Zero;
        try
        {
            var status = NativeMethods.oxidize_geometric_json(pinned.AddrOfPinnedObject(),
                (nuint)pdfBytes.Length, mutations, out result);
            ThrowIfError(status, "Failed to read or edit Geometrics");
            var value = JsonSerializer.Deserialize<PdfGeometricUpdate>(Marshal.PtrToStringUTF8(result)
                ?? throw new PdfExtractionException("Missing Geometric response"))
                ?? throw new PdfExtractionException("Invalid Geometric response");
            if (mutations is not null && value.PdfBytes.Length == 0)
                throw new PdfExtractionException("Missing updated PDF bytes");
            return value;
        }
        finally
        {
            if (result != IntPtr.Zero) NativeMethods.oxidize_free_string(result);
            pinned.Free();
        }
    }
}
