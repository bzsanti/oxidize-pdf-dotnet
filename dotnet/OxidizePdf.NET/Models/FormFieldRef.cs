namespace OxidizePdf.NET.Models;

/// <summary>
/// An opaque handle to an AcroForm field created on a <see cref="PdfDocument"/>
/// via one of the <c>Add*Field</c> / <c>Add*</c> form methods.
/// </summary>
/// <remarks>
/// Returned by <see cref="PdfDocument.AddTextField"/> and the other field
/// factories, and consumed by <see cref="PdfPage.AddFormWidget(FormFieldRef)"/>
/// to place the field's widget annotation on a page. It also carries the
/// rectangle supplied at creation time so the common single-widget case does
/// not require repeating the geometry.
/// </remarks>
public readonly record struct FormFieldRef
{
    /// <summary>The field's PDF object number (generation is always 0).</summary>
    internal uint ObjectNumber { get; }

    internal double X1 { get; }
    internal double Y1 { get; }
    internal double X2 { get; }
    internal double Y2 { get; }

    internal FormFieldRef(uint objectNumber, double x1, double y1, double x2, double y2)
    {
        ObjectNumber = objectNumber;
        X1 = x1;
        Y1 = y1;
        X2 = x2;
        Y2 = y2;
    }
}
