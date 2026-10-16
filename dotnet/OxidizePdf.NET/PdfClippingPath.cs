using System.Runtime.InteropServices;

namespace OxidizePdf.NET;

/// <summary>Rule used to determine the interior of a clipping path.</summary>
public enum PdfWindingRule
{
    /// <summary>Signed crossings determine the interior.</summary>
    NonZero = 0,
    /// <summary>An odd number of crossings determines the interior.</summary>
    EvenOdd = 1
}

[StructLayout(LayoutKind.Sequential)]
internal struct ClipCommand
{
    internal double X1, Y1, X2, Y2, X3, Y3;
    internal int Kind;
}

/// <summary>A reusable clipping path in PDF points, with at most 65,536 commands.</summary>
/// <remarks>Coordinates must be finite. Begin each subpath with MoveTo. Open
/// subpaths are implicitly closed by PDF clipping. This mutable builder is not
/// thread-safe; ClipPath copies its commands and retains no reference to it.</remarks>
public sealed class PdfClippingPath
{
    private readonly List<ClipCommand> _commands = new();
    private bool _hasCurrentPoint;

    /// <summary>Starts a subpath at the supplied point.</summary>
    public PdfClippingPath MoveTo(double x, double y)
        => Add(new ClipCommand { Kind = 0, X1 = x, Y1 = y });

    /// <summary>Adds a line from the current point.</summary>
    public PdfClippingPath LineTo(double x, double y)
        => Add(new ClipCommand { Kind = 1, X1 = x, Y1 = y });

    /// <summary>Adds a cubic Bézier with two control points and an endpoint.</summary>
    public PdfClippingPath CurveTo(double x1, double y1, double x2, double y2, double x3, double y3)
        => Add(new ClipCommand { Kind = 2, X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, X3 = x3, Y3 = y3 });

    /// <summary>Closes the current subpath. Its starting point becomes current.</summary>
    public PdfClippingPath ClosePath() => Add(new ClipCommand { Kind = 3 });

    private PdfClippingPath Add(ClipCommand command)
    {
        if (!double.IsFinite(command.X1) || !double.IsFinite(command.Y1)
            || !double.IsFinite(command.X2) || !double.IsFinite(command.Y2)
            || !double.IsFinite(command.X3) || !double.IsFinite(command.Y3))
            throw new ArgumentOutOfRangeException(nameof(command), "Coordinates must be finite.");
        if (command.Kind != 0 && !_hasCurrentPoint)
            throw new InvalidOperationException("Begin the path with MoveTo.");
        if (_commands.Count == 65_536)
            throw new InvalidOperationException("A clipping path supports at most 65,536 commands.");
        _commands.Add(command);
        _hasCurrentPoint = true;
        return this;
    }

    internal ClipCommand[] Snapshot()
    {
        if (_commands.Count == 0)
            throw new ArgumentException("The clipping path must not be empty.");
        return _commands.ToArray();
    }
}
