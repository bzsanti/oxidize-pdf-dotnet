# Extraction/recovery parity increment — Rust 5.4.2

## Scope and contracts

Typed managed extraction now exposes all core ExtractionOptions fields and builder
policies: reading order, carriage returns, links, unreliable figure text and NBSP.
Parser configuration carries all ParseOptions fields. Null text budget means
unlimited; zero remains a real zero budget. Explicit recovery carries a separate
per-stream decoded-byte limit and reports recovered/omitted contents or Form
identity, filter index, recovery kind and the original error. Ordinary extraction
still rejects incomplete Flate, including with tolerant parser settings.

Fragment results preserve original Gray/RGB/CMYK and space decisions. Core 5.4.2
currently emits no space decisions: the native synthetic serialization test proves
mapping only, not a working upstream decision-tracing algorithm. Parser warning
retrieval and the rest of the public API remain under #81; this increment does not
claim complete parser or bridge parity.

Effective version includes the catalog override. Document build identification
can be disabled without removing application metadata. Static entry points now
install the native resolver through a module initializer before first use.

## Validation and negative controls

224 native tests pass. Full restored managed regression: 1001 SDK + 15 KernelMemory
pass. Subsequent focused run after adding contract/CR/digital-slot cases: 18 pass.
Clippy with warnings denied passes. Tests use hand-authored PDFs and explicit
ToUnicode mappings, not only generator round trips. Float color assertions account
for core content parsing through f32. Digital slot state uses structural CMS bytes;
it tests stored signature state, not cryptographic authenticity.

Three compiled mutations are rejected: removing native startup initialization,
dropping recovery diagnostics, and ignoring the stream budget. Sources and binaries
were restored and full managed regression passed. Five contract mutations reject
missing options/results, changed defaults, missing tests and changed core version.
CI runs the recovery theory in a fresh test host to prevent previous document
construction masking resolver initialization.

## Findings corrected

- Static P/Invoke declarations outside NativeMethods could run before its type
  initializer installed the resolver. Module initialization fixes the assembly;
  isolated CI covers the actual startup order.
- Completed slot state includes either handwriting completion or an existing
  digital signature value. XML documentation and a combined draw/sign test now
  follow the Rust rule, including rejection of removal after digital signing.
- Build-identification interop uses DocumentSafeHandle to retain native lifetime.

## Test quality and security review

Kripteia Rust: 97, 224 tests / 20 files. C#: 95, 944 detected tests / 120 files
before the final added cases. C# analysis reports partial/error syntax trees for
modern syntax; automated coverage is incomplete. Manual review supplements both
analyses. Contract reflection checks compiled public types and actual defaults;
behavioral tests execute managed/native/core code and assert independently expected
text, diagnostics and publication metadata. Scores are not completeness evidence.

Manual security review traces owned byte snapshots and serialized options before
Task.Run, pin lifetime, UTF-8 marshalling, same-thread native error retrieval,
output reset before failure, panic containment, freeing outputs after deserialize
errors and SafeHandle lifetime. Native pointer/length validity remains the FFI
caller's obligation. Cancellation prevents queued execution, not interruption of
an active native call, as documented. No unresolved security finding identified
within this increment. Existing ABI remains compatible; fragment JSON gains fields.

Evidence: docs/evidence/parity-recovery. Platform CI pending at commit preparation.
Global full-parity issue #81 remains open.
