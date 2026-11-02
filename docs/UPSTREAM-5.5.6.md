# Core 5.5.6 and token-counter contracts

OxidizePdf.NET 0.21.0 pins published core =5.5.6. Signed field Lock/P policies
are enforced across revisions. The core also allows dedicated invisible document
timestamps under P1; this binding does not expose that preparation API. Existing
APIs are retained.

`TokenCounterKind.WordProxy` remains the default. `Cl100kBase` selects ordinary
text BPE tokenization, including literal special-token spellings; it does not
count chat framing or promise compatibility with every model. `PdfTokenCounter`
returns an owned result with Count, Counter, CounterName and IsExact. No native
handle is retained. Input is limited to 16 MiB UTF-8; embedded NUL is preserved
and unpaired UTF-16 surrogates are rejected.

Normal native builds enable the tiktoken feature. Custom builds may use
`cargo build --no-default-features`; discovery returns false for Cl100kBase and
both standalone counting and chunking report NotSupportedException. No fallback
is used. Unknown enum values and malformed wire values are rejected.

Both semantic and hybrid configs accept TokenCounter. Their existing
TokenEstimate fields report that selected counter. Chunk limits still permit
indivisible oversized elements, flagged IsOversized; overlap is best effort and
can be omitted when it does not fit. Counts refer to chunk text, not external
prompt formatting or additional context assembled by the caller.

Validation uses real FFI, a fixed Python tiktoken 0.12.0 oracle and native builds
with/without the feature. This increment addresses #104, not total parity #81.
Other missing capabilities and the exhaustive inventory remain tracked separately.
