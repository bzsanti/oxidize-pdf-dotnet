using System.Runtime.InteropServices;
using OxidizePdf.NET.Tests.TestHelpers;

namespace OxidizePdf.NET.Tests;

public class ClippingPathTests
{
    private static PdfClippingPath Ring() => new PdfClippingPath()
        .MoveTo(10, 10).LineTo(90, 10).LineTo(90, 90).LineTo(10, 90).ClosePath()
        .MoveTo(30, 30).LineTo(70, 30).LineTo(70, 70).LineTo(30, 70).ClosePath();

    [Fact]
    public void WindingRulesAndSaveRestoreEmitDistinctScopedOperators()
    {
        foreach (var rule in new[] { PdfWindingRule.NonZero, PdfWindingRule.EvenOdd })
        {
            using var document = new PdfDocument();
            using var page = new PdfPage(100, 100);
            page.SaveGraphicsState().ClipPath(Ring(), rule).DrawRect(0, 0, 100, 100).Fill()
                .RestoreGraphicsState().DrawRect(0, 0, 5, 5).Fill();
            document.AddPage(page);
            string content = ContentStreamHelper.DecompressAllContentStreams(document.SaveToBytes());
            string clip = rule == PdfWindingRule.EvenOdd ? "W*\nn\n" : "W\nn\n";
            Assert.Contains("10.000 10.000 m\n90.000 10.000 l", content);
            Assert.Contains("30.000 30.000 m\n70.000 30.000 l", content);
            Assert.Contains(clip, content);
            Assert.DoesNotContain(rule == PdfWindingRule.EvenOdd ? "W\n" : "W*\n", content);
            int start = content.IndexOf("q\n", StringComparison.Ordinal);
            int applied = content.IndexOf(clip, StringComparison.Ordinal);
            int paint = content.IndexOf("f\n", applied, StringComparison.Ordinal);
            int restored = content.IndexOf("Q\n", paint, StringComparison.Ordinal);
            int outside = content.IndexOf("f\n", restored, StringComparison.Ordinal);
            Assert.True(start >= 0 && start < applied && applied < paint && paint < restored && restored < outside);
        }
    }

    [Fact]
    public void CubicOpenSubpathsAndReusableSnapshotsPreserveCoordinates()
    {
        var path = new PdfClippingPath().MoveTo(1, 2).CurveTo(3, 4, 5, 6, 7, 8);
        using var document = new PdfDocument();
        using var page = PdfPage.A4();
        Assert.Same(page, page.ClipPath(path));
        path.LineTo(123, 456).ClosePath();
        document.AddPage(page);
        string content = ContentStreamHelper.DecompressAllContentStreams(document.SaveToBytes());
        Assert.Contains("1.000 2.000 m\n3.000 4.000 5.000 6.000 7.000 8.000 c\nW\nn\n", content);
        Assert.DoesNotContain("123.000", content);
        using var second = PdfPage.A4();
        second.ClipPath(path, PdfWindingRule.EvenOdd);
        Assert.Throws<InvalidOperationException>(() => page.ClipPath(path));
        second.Dispose();
        Assert.Throws<ObjectDisposedException>(() => second.ClipPath(path));
    }

    [Fact]
    public void InvalidPathsDoNotMutatePageOrBuilder()
    {
        using var page = PdfPage.A4();
        Assert.Throws<ArgumentNullException>(() => page.ClipPath(null!));
        Assert.Throws<ArgumentException>(() => page.ClipPath(new PdfClippingPath()));
        Assert.Throws<ArgumentOutOfRangeException>(() => page.ClipPath(Ring(), (PdfWindingRule)2));
        var path = new PdfClippingPath();
        Assert.Throws<InvalidOperationException>(() => path.LineTo(1, 2));
        Assert.Throws<InvalidOperationException>(() => path.CurveTo(1, 2, 3, 4, 5, 6));
        Assert.Throws<InvalidOperationException>(() => path.ClosePath());
        foreach (double value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => path.MoveTo(value, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => path.MoveTo(0, value));
            path.MoveTo(0, 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => path.LineTo(value, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => path.CurveTo(1, 2, 3, value, 5, 6));
            Assert.Throws<ArgumentOutOfRangeException>(() => path.CurveTo(1, 2, 3, 4, 5, value));
        }
        page.ClipPath(new PdfClippingPath().MoveTo(2, 3)); // zero-area is valid
        using var document = new PdfDocument();
        document.AddPage(page);
        string content = ContentStreamHelper.DecompressAllContentStreams(document.SaveToBytes());
        Assert.Contains("2.000 3.000 m\nW\nn\n", content);
        Assert.Equal(1, content.Split("W\n").Length - 1);
    }

    [Fact]
    public void CommandBudgetAndAbiLayoutAreExplicit()
    {
        Assert.Equal(48, Marshal.OffsetOf<ClipCommand>(nameof(ClipCommand.Kind)).ToInt32());
        Assert.Equal(56, Marshal.SizeOf<ClipCommand>());
        var path = new PdfClippingPath().MoveTo(0, 0);
        for (int i = 1; i < 65_536; i++) path.LineTo(i % 100, i % 99);
        Assert.Throws<InvalidOperationException>(() => path.ClosePath());
        using var page = PdfPage.A4();
        page.ClipPath(path);
    }
}
