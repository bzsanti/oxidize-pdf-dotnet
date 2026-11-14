using OxidizePdf.NET;

// Consume the packed NuGet, never a project reference or a hand-copied native library.
if (!PdfTokenCounter.IsAvailable(TokenCounterKind.Cl100kBase))
    throw new Exception("Published package is missing cl100k_base");
var exact = PdfTokenCounter.Count("Hello, world!", TokenCounterKind.Cl100kBase);
if (exact.Count != 4 || !exact.IsExact || exact.CounterName != "cl100k_base")
    throw new Exception("Exact token counter failed");
if (PdfTokenCounter.Count("Hello, world!").Count != 2)
    throw new Exception("Default word estimate changed");
var xml = PdfXmp.Serialize(new() { Title = "package-smoke" });
if (PdfXmp.Parse(xml).Title != "package-smoke") throw new Exception("XMP round-trip failed");
var calculated = PdfCalculations.RunEngine(new PdfCalculationStep[] {
    new PdfCalculationStep.Set("value", new PdfFormValue.Number(7)),
    new PdfCalculationStep.Get("value") });
if (calculated[1].Value != new PdfFormValue.Number(7)) throw new Exception("Calculation failed");
using var document = new PdfDocument();
using var page = new PdfPage(321, 456);
document.AddPage(page);
byte[] pdf = document.SaveToBytes();
using (var input = new MemoryStream(pdf))
using (var lazy = PdfLazyDocument.Open(input))
    if (lazy.GetConfiguration().PageCount != 1) throw new Exception("Lazy reader failed");
using (var input = new MemoryStream(pdf))
using (var streaming = PdfStreamingDocument.Open(input))
    if (streaming.NextPage()?.Width != 321 || streaming.NextPage() is not null)
        throw new Exception("Streaming reader failed");
int called = 0;
var batch = PdfBatch.Run(new PdfBatchJob[] { new PdfBatchJob.Custom("smoke", () => Interlocked.Increment(ref called)) });
if (called != 1 || batch.Successful != 1) throw new Exception("Batch callback failed");
if (!PdfGeneration.IsAvailable || !PdfGeneration.SupportsRayon)
    throw new Exception("Package is missing optional generation engines");
string generated = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
try
{
    foreach (var mode in new[] { PdfGenerationMode.Serial, PdfGenerationMode.Rayon })
    {
        var result = PdfGeneration.Save(generated, new[] { new PdfGenerationPage(0, 210, 310) },
            spec => new PdfPage(spec.Width, spec.Height).DrawTextAt(OxidizePdf.NET.Models.StandardFont.Helvetica, 12, 20, 30, "Generation smoke"),
            new PdfGenerationOptions { Mode = mode });
        if (result.Pages != 1 || result.Mode != mode || !File.Exists(generated))
            throw new Exception("Package generation failed");
        using var input = File.OpenRead(generated);
        using var reader = PdfStreamingDocument.Open(input);
        if (reader.NextPage()?.Width != 210 || reader.NextPage() is not null)
            throw new Exception("Generated package page geometry failed");
    }
}
finally { File.Delete(generated); }
Console.WriteLine($"NuGet native/API smoke passed: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
