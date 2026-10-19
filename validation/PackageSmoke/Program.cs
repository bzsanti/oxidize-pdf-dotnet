using OxidizePdf.NET;

// Consume the packed NuGet, never a project reference or a hand-copied native library.
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
Console.WriteLine($"NuGet native/API smoke passed: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
