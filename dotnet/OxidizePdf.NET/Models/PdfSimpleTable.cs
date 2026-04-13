using System.Text.Json;

namespace OxidizePdf.NET;

/// <summary>
/// A simple table definition for use with layout builders.
/// Uses equal column widths and string cell content.
/// </summary>
public sealed class PdfSimpleTable
{
    /// <summary>Column widths in points.</summary>
    public double[] ColumnWidths { get; }

    /// <summary>Optional header row.</summary>
    public string[]? Headers { get; }

    /// <summary>Data rows.</summary>
    public List<string[]> Rows { get; } = new();

    /// <summary>
    /// Creates a simple table with the specified column widths.
    /// </summary>
    /// <param name="columnWidths">Width of each column in points.</param>
    /// <param name="headers">Optional header row texts.</param>
    public PdfSimpleTable(double[] columnWidths, string[]? headers = null)
    {
        ArgumentNullException.ThrowIfNull(columnWidths);
        if (columnWidths.Length == 0)
            throw new ArgumentException("At least one column width is required", nameof(columnWidths));
        ColumnWidths = columnWidths;
        Headers = headers;
    }

    /// <summary>
    /// Adds a data row to the table. Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="cells">Cell values for this row.</param>
    public PdfSimpleTable AddRow(params string[] cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        Rows.Add(cells);
        return this;
    }

    /// <summary>
    /// Serializes this table to the JSON format expected by the native FFI layer.
    /// </summary>
    internal string ToJson()
    {
        var obj = new
        {
            column_widths = ColumnWidths,
            headers = Headers ?? Array.Empty<string>(),
            rows = Rows,
        };
        return JsonSerializer.Serialize(obj);
    }
}
