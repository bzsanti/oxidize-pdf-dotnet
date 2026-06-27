using System.Collections.Concurrent;

namespace OxidizePdf.NET.Tests;

/// <summary>
/// Guards the thread-local error contract (issue #55). The native error channel
/// is thread-local: a function records its message on the calling thread and
/// <c>oxidize_get_last_error</c> reads that same thread's slot. The managed
/// wrappers honor this by reading the error synchronously, on the same thread,
/// immediately after the failing call (inside the <c>Task.Run</c> body of the
/// async surface) — never across an <c>await</c>.
///
/// This test exercises that contract under heavy concurrency: many threads each
/// make a failing call and read the error in the same synchronous step. Each
/// must read ITS OWN message; any cross-thread attribution would surface here.
/// </summary>
public class FfiErrorIsolationTests
{
    [Fact]
    public void Concurrent_failing_calls_each_read_their_own_thread_local_error()
    {
        const int iterations = 4000;
        var mismatches = new ConcurrentBag<string>();

        Parallel.For(
            0,
            iterations,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            i =>
            {
                // Two distinct native calls, each setting a function-specific
                // thread-local message on the null-pointer path. Call + read are
                // synchronous on one thread (the honored contract), so each must
                // observe its own message regardless of concurrent activity.
                string expected;
                int code;
                if ((i & 1) == 0)
                {
                    code = NativeMethods.oxidize_extract_text(IntPtr.Zero, (nuint)0, out _);
                    expected = "oxidize_extract_text";
                }
                else
                {
                    code = NativeMethods.oxidize_get_page_count(IntPtr.Zero, (nuint)0, out _);
                    expected = "oxidize_get_page_count";
                }

                if (code != (int)NativeMethods.ErrorCode.NullPointer)
                {
                    mismatches.Add($"unexpected code {code} for {expected}");
                    return;
                }

                var msg = NativeMethods.GetLastError();
                if (msg is null || !msg.Contains(expected, StringComparison.Ordinal))
                    mismatches.Add($"expected '{expected}', got '{msg ?? "<null>"}'");
            });

        Assert.True(
            mismatches.IsEmpty,
            $"{mismatches.Count} thread-local error mismatch(es) — cross-thread attribution (issue #55): "
            + string.Join(" | ", mismatches.Take(5)));
    }
}
