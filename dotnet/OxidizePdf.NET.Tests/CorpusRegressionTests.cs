using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Tests;

/// <summary>
/// Regression tests that exercise the .NET FFI bridge against the full PDF test corpus (9K+ PDFs).
/// Verifies that every PDF can be parsed and processed without crashes or unhandled exceptions.
/// Skips gracefully if the corpus is not available.
/// </summary>
[Trait("Category", "Corpus")]
public class CorpusRegressionTests : IDisposable
{
    private static readonly string CorpusRoot = Path.GetFullPath(
        Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!,
            "..", "..", "..", "..", "..", "..", "oxidize-pdf", "test-corpus"));

    private static readonly string ResultsDir = Path.Combine(CorpusRoot, "results", "dotnet");

    private static readonly TimeSpan PerPdfTimeout = TimeSpan.FromSeconds(30);

    private readonly PdfExtractor _extractor = new();

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task T0_Regression_AllPdfsParseWithoutCrash()
    {
        await RunTierTest("t0-regression");
    }

    [Fact]
    public async Task T1_Spec_AllPdfsParseWithoutCrash()
    {
        await RunTierTest("t1-spec");
    }

    [Fact]
    public async Task T2_Realworld_AllPdfsParseWithoutCrash()
    {
        await RunTierTest("t2-realworld");
    }

    [Fact]
    public async Task T3_Stress_AllPdfsParseWithoutCrash()
    {
        await RunTierTest("t3-stress");
    }

    [Fact]
    public async Task T4_AiTarget_AllPdfsParseWithoutCrash()
    {
        await RunTierTest("t4-ai-target");
    }

    [Fact]
    public async Task T5_Quality_AllPdfsParseWithoutCrash()
    {
        await RunTierTest("t5-quality");
    }

    [Fact]
    public async Task T6_Adversarial_AllPdfsParseWithoutCrash()
    {
        await RunTierTest("t6-adversarial");
    }

    private async Task RunTierTest(string tierDir)
    {
        var tierPath = Path.Combine(CorpusRoot, tierDir);
        if (!Directory.Exists(tierPath))
        {
            // Corpus not available (CI or dev without corpus) — skip gracefully
            return;
        }

        var pdfFiles = Directory.GetFiles(tierPath, "*.pdf", SearchOption.AllDirectories);
        if (pdfFiles.Length == 0)
        {
            // No PDFs in tier — skip gracefully
            return;
        }

        var results = new ConcurrentBag<PdfTestResult>();
        var totalSw = Stopwatch.StartNew();

        // Process PDFs with bounded parallelism to avoid memory exhaustion
        var semaphore = new SemaphoreSlim(Environment.ProcessorCount);
        var tasks = pdfFiles.Select(async pdfPath =>
        {
            await semaphore.WaitAsync();
            try
            {
                var result = await TestSinglePdf(pdfPath, tierDir);
                results.Add(result);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        totalSw.Stop();

        var report = BuildReport(tierDir, results, totalSw.Elapsed);
        await SaveReport(tierDir, report);

        // Output summary
        var output = $"""
            === {tierDir} Corpus Regression Report ===
            Total PDFs:      {report.TotalPdfs}
            Clean pass:      {report.Passed} ({report.PassRate:F1}%)
            Graceful errors: {report.GracefulErrors} (PdfExtractionException — bridge OK)
            Bridge crashes:  {report.BridgeCrashes}
            Timeouts:        {report.Timeouts}
            Duration:        {report.DurationSeconds:F1}s

            Operations breakdown:
              GetPageCount:    {report.OperationResults["GetPageCount"].Passed}/{report.OperationResults["GetPageCount"].Total} passed
              ExtractText:     {report.OperationResults["ExtractText"].Passed}/{report.OperationResults["ExtractText"].Total} passed
              ExtractMetadata: {report.OperationResults["ExtractMetadata"].Passed}/{report.OperationResults["ExtractMetadata"].Total} passed
              IsEncrypted:     {report.OperationResults["IsEncrypted"].Passed}/{report.OperationResults["IsEncrypted"].Total} passed
            """;

        // A regression = bridge crash (native crash, timeout, unexpected exception).
        // Graceful PdfExtractionException on malformed/encrypted PDFs is NOT a regression.
        Assert.True(report.BridgeCrashes == 0,
            $"BRIDGE REGRESSION: {report.BridgeCrashes}/{report.TotalPdfs} PDFs caused a bridge crash.\n{output}\n\nCrashed PDFs:\n{FormatFailures(results)}");
    }

    private async Task<PdfTestResult> TestSinglePdf(string pdfPath, string tier)
    {
        var relativePath = Path.GetRelativePath(CorpusRoot, pdfPath);
        var result = new PdfTestResult
        {
            FilePath = relativePath,
            Tier = tier,
            FileSizeBytes = new FileInfo(pdfPath).Length,
        };

        byte[] pdfBytes;
        try
        {
            pdfBytes = await File.ReadAllBytesAsync(pdfPath);
        }
        catch (Exception ex)
        {
            result.ReadError = ex.Message;
            return result;
        }

        // Operation 1: GetPageCount (most basic parse test)
        result.GetPageCount = await RunOperation("GetPageCount", async ct =>
        {
            var count = await _extractor.GetPageCountAsync(pdfBytes, ct);
            return $"{count} pages";
        });

        // Operation 2: ExtractText (exercises text extraction pipeline)
        result.ExtractText = await RunOperation("ExtractText", async ct =>
        {
            var text = await _extractor.ExtractTextAsync(pdfBytes, ct);
            return $"{text.Length} chars";
        });

        // Operation 3: ExtractMetadata (exercises metadata parsing)
        result.ExtractMetadata = await RunOperation("ExtractMetadata", async ct =>
        {
            var meta = await _extractor.ExtractMetadataAsync(pdfBytes, ct);
            return $"v{meta.Version}, {meta.PageCount}p";
        });

        // Operation 4: IsEncrypted (exercises security detection)
        result.IsEncrypted = await RunOperation("IsEncrypted", async ct =>
        {
            var encrypted = await _extractor.IsEncryptedAsync(pdfBytes, ct);
            return encrypted.ToString();
        });

        return result;
    }

    private static async Task<OperationResult> RunOperation(
        string name,
        Func<CancellationToken, Task<string>> operation)
    {
        var sw = Stopwatch.StartNew();
        using var cts = new CancellationTokenSource(PerPdfTimeout);
        try
        {
            var detail = await operation(cts.Token);
            sw.Stop();
            return new OperationResult
            {
                Name = name,
                Success = true,
                DurationMs = sw.Elapsed.TotalMilliseconds,
                Detail = detail,
            };
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return new OperationResult
            {
                Name = name,
                Success = false,
                DurationMs = sw.Elapsed.TotalMilliseconds,
                Error = "TIMEOUT",
                IsTimeout = true,
                IsBridgeCrash = true, // Timeouts indicate the native side may be hung
            };
        }
        catch (PdfExtractionException ex)
        {
            // Managed exception from the bridge — the bridge handled the error correctly.
            // This is NOT a regression; malformed/encrypted PDFs are expected to throw this.
            sw.Stop();
            return new OperationResult
            {
                Name = name,
                Success = false,
                DurationMs = sw.Elapsed.TotalMilliseconds,
                Error = ex.Message,
                IsGracefulError = true,
            };
        }
        catch (Exception ex) when (ex is AccessViolationException
                                       or System.Runtime.InteropServices.SEHException
                                       or OutOfMemoryException
                                       or StackOverflowException)
        {
            // Native crash — this IS a regression
            sw.Stop();
            return new OperationResult
            {
                Name = name,
                Success = false,
                DurationMs = sw.Elapsed.TotalMilliseconds,
                Error = $"NATIVE CRASH: {ex.GetType().Name}: {ex.Message}",
                IsBridgeCrash = true,
            };
        }
        catch (Exception ex)
        {
            // Unexpected managed exception — could indicate a bridge issue
            sw.Stop();
            return new OperationResult
            {
                Name = name,
                Success = false,
                DurationMs = sw.Elapsed.TotalMilliseconds,
                Error = $"{ex.GetType().Name}: {ex.Message}",
                IsBridgeCrash = true,
            };
        }
    }

    private static CorpusReport BuildReport(
        string tier,
        ConcurrentBag<PdfTestResult> results,
        TimeSpan duration)
    {
        var resultsList = results.OrderBy(r => r.FilePath).ToList();

        var opNames = new[] { "GetPageCount", "ExtractText", "ExtractMetadata", "IsEncrypted" };
        var opResults = new Dictionary<string, OperationSummary>();

        foreach (var opName in opNames)
        {
            var ops = resultsList
                .Select(r => opName switch
                {
                    "GetPageCount" => r.GetPageCount,
                    "ExtractText" => r.ExtractText,
                    "ExtractMetadata" => r.ExtractMetadata,
                    "IsEncrypted" => r.IsEncrypted,
                    _ => null,
                })
                .Where(o => o != null)
                .ToList();

            opResults[opName] = new OperationSummary
            {
                Total = ops.Count,
                Passed = ops.Count(o => o!.Success),
                Failed = ops.Count(o => !o!.Success && !o.IsTimeout),
                Timeouts = ops.Count(o => o!.IsTimeout),
                AvgDurationMs = ops.Count > 0 ? ops.Average(o => o!.DurationMs) : 0,
            };
        }

        var cleanPass = resultsList.Count(r => r.AllOperationsSucceeded);
        var gracefulErrors = resultsList.Count(r => r.HasGracefulErrors && !r.HasBridgeCrash);
        var bridgeCrashes = resultsList.Count(r => r.HasBridgeCrash);
        var timeouts = resultsList.Count(r => r.HasTimeout);

        return new CorpusReport
        {
            Tier = tier,
            Timestamp = DateTimeOffset.UtcNow,
            TotalPdfs = resultsList.Count,
            Passed = cleanPass,
            GracefulErrors = gracefulErrors,
            BridgeCrashes = bridgeCrashes,
            Failed = bridgeCrashes, // Only bridge crashes count as failures
            Timeouts = timeouts,
            PassRate = resultsList.Count > 0 ? (double)(cleanPass + gracefulErrors) / resultsList.Count * 100 : 0,
            DurationSeconds = duration.TotalSeconds,
            OperationResults = opResults,
            Failures = resultsList
                .Where(r => r.HasBridgeCrash)
                .Select(r => new FailureEntry
                {
                    FilePath = r.FilePath,
                    FileSizeBytes = r.FileSizeBytes,
                    ReadError = r.ReadError,
                    FailedOperations = GetFailedOps(r),
                })
                .ToList(),
        };
    }

    private static Dictionary<string, string> GetFailedOps(PdfTestResult r)
    {
        var failed = new Dictionary<string, string>();
        if (r.GetPageCount is { Success: false }) failed["GetPageCount"] = r.GetPageCount.Error ?? "unknown";
        if (r.ExtractText is { Success: false }) failed["ExtractText"] = r.ExtractText.Error ?? "unknown";
        if (r.ExtractMetadata is { Success: false }) failed["ExtractMetadata"] = r.ExtractMetadata.Error ?? "unknown";
        if (r.IsEncrypted is { Success: false }) failed["IsEncrypted"] = r.IsEncrypted.Error ?? "unknown";
        if (r.ReadError != null) failed["FileRead"] = r.ReadError;
        return failed;
    }

    private static string FormatFailures(ConcurrentBag<PdfTestResult> results)
    {
        var crashes = results
            .Where(r => r.HasBridgeCrash)
            .OrderBy(r => r.FilePath)
            .Take(50);

        var lines = crashes.Select(r =>
        {
            var failedOps = GetFailedOps(r);
            var opsStr = string.Join(", ", failedOps.Select(kv => $"{kv.Key}: {kv.Value}"));
            return $"  {r.FilePath} [{r.FileSizeBytes} bytes] — {opsStr}";
        });

        var total = results.Count(r => r.HasBridgeCrash);
        var suffix = total > 50 ? $"\n  ... and {total - 50} more" : "";
        return string.Join("\n", lines) + suffix;
    }

    private static async Task SaveReport(string tier, CorpusReport report)
    {
        var dateDir = Path.Combine(ResultsDir, DateTime.UtcNow.ToString("yyyy-MM-dd"));
        Directory.CreateDirectory(dateDir);

        var reportPath = Path.Combine(dateDir, $"{tier}.json");
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        };
        var json = JsonSerializer.Serialize(report, options);
        await File.WriteAllTextAsync(reportPath, json);

        // Update latest symlink
        var latestDir = Path.Combine(ResultsDir, "latest");
        if (Directory.Exists(latestDir) || File.Exists(latestDir))
        {
            try { Directory.Delete(latestDir); } catch { /* symlink */ }
        }

        try
        {
            Directory.CreateSymbolicLink(latestDir, dateDir);
        }
        catch
        {
            // Symlink may fail on some platforms — not critical
        }
    }

    // --- Models ---

    private sealed class PdfTestResult
    {
        public required string FilePath { get; init; }
        public required string Tier { get; init; }
        public long FileSizeBytes { get; init; }
        public string? ReadError { get; set; }
        public OperationResult? GetPageCount { get; set; }
        public OperationResult? ExtractText { get; set; }
        public OperationResult? ExtractMetadata { get; set; }
        public OperationResult? IsEncrypted { get; set; }

        /// <summary>
        /// A PDF passes the regression test if no operation caused a bridge crash.
        /// Graceful PdfExtractionException errors (malformed/encrypted PDFs) are NOT regressions.
        /// </summary>
        public bool PassesRegression =>
            ReadError == null && !HasBridgeCrash;

        /// <summary>Whether any operation caused a native crash, timeout, or unexpected exception.</summary>
        public bool HasBridgeCrash =>
            (GetPageCount?.IsBridgeCrash ?? false) ||
            (ExtractText?.IsBridgeCrash ?? false) ||
            (ExtractMetadata?.IsBridgeCrash ?? false) ||
            (IsEncrypted?.IsBridgeCrash ?? false);

        /// <summary>Whether all 4 operations succeeded (no errors at all).</summary>
        public bool AllOperationsSucceeded =>
            ReadError == null &&
            (GetPageCount?.Success ?? false) &&
            (ExtractText?.Success ?? false) &&
            (ExtractMetadata?.Success ?? false) &&
            (IsEncrypted?.Success ?? false);

        /// <summary>Whether any operation had a graceful error (PdfExtractionException).</summary>
        public bool HasGracefulErrors =>
            (GetPageCount?.IsGracefulError ?? false) ||
            (ExtractText?.IsGracefulError ?? false) ||
            (ExtractMetadata?.IsGracefulError ?? false) ||
            (IsEncrypted?.IsGracefulError ?? false);

        public bool HasTimeout =>
            (GetPageCount?.IsTimeout ?? false) ||
            (ExtractText?.IsTimeout ?? false) ||
            (ExtractMetadata?.IsTimeout ?? false) ||
            (IsEncrypted?.IsTimeout ?? false);
    }

    private sealed class OperationResult
    {
        public required string Name { get; init; }
        public bool Success { get; init; }
        public double DurationMs { get; init; }
        public string? Detail { get; init; }
        public string? Error { get; init; }
        public bool IsTimeout { get; init; }
        /// <summary>True if the error was a managed PdfExtractionException (bridge works correctly).</summary>
        public bool IsGracefulError { get; init; }
        /// <summary>True if the error was a native crash, timeout, or unexpected exception (actual regression).</summary>
        public bool IsBridgeCrash { get; init; }
    }

    private sealed class CorpusReport
    {
        public required string Tier { get; init; }
        public DateTimeOffset Timestamp { get; init; }
        public int TotalPdfs { get; init; }
        public int Passed { get; init; }
        public int GracefulErrors { get; init; }
        public int BridgeCrashes { get; init; }
        public int Failed { get; init; }
        public int Timeouts { get; init; }
        /// <summary>Pass rate includes both clean passes and graceful errors (bridge works correctly).</summary>
        public double PassRate { get; init; }
        public double DurationSeconds { get; init; }
        public required Dictionary<string, OperationSummary> OperationResults { get; init; }
        public List<FailureEntry>? Failures { get; init; }
    }

    private sealed class OperationSummary
    {
        public int Total { get; init; }
        public int Passed { get; init; }
        public int Failed { get; init; }
        public int Timeouts { get; init; }
        public double AvgDurationMs { get; init; }
    }

    private sealed class FailureEntry
    {
        public required string FilePath { get; init; }
        public long FileSizeBytes { get; init; }
        public string? ReadError { get; init; }
        public Dictionary<string, string>? FailedOperations { get; init; }
    }
}
