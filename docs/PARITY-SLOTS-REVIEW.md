# Signature-slot parity increment — Rust 5.4.2

## Resumen ejecutivo

Adds the six application-facing slot operations available in Rust and Python:
create/list/read/remove/draw/complete. Managed APIs expose typed slot state and all
options, including metadata, geometry, label and the completion flag. Existing
CMS APIs are unchanged. This increment does not establish complete Rust parity;
#81 remains open. Review covers native transport/ownership, managed snapshots,
state transitions, errors and tests. Platform CI is pending.

## Contratos y evidencia

| Contract/source | Evidence | Status |
| --- | --- | --- |
| Core signatures/preparation: incremental operations retain prior revision | Create/draw/complete/remove checked for exact input prefix | Demonstrated |
| Slot fields round-trip | Name, Unicode metadata, page, rotation, rectangle, completion and digital state asserted | Demonstrated |
| Handwriting completion is not CMS; completed slots reject further drawing | Lifecycle checks false/true flag, rejection after completion, digital state remains false | Demonstrated |
| Caller completion flag must reach core | Mutation forces false: lifecycle assertion fails; restored suite passes | Demonstrated |
| Draw supports labels and completion | Label-only completion and stroke-plus-label cases exercise both | Demonstrated |
| Invalid input is rejected | Missing/duplicate target, invalid page/rect/strokes, pre-cancellation and empty bytes | Demonstrated |
| FFI never returns an allocated result on failure | Null input and malformed/unknown request tests; output initialized to null | Demonstrated |
| Caller mutation after queuing cannot change PDF/options | PDF cloned and request serialized before Task.Run; pinned snapshot freed in finally | Manually traced |

Validation: 222 native tests pass; 996 managed tests pass. Following the additional
strict-deserialization check, all 222 native tests and three managed slot tests
pass. Clippy passes with warnings denied; native and managed format verified.
Existing CI discovers tests on every configured platform. Native logs and mutation
evidence: `docs/evidence/parity-slots/`.

## Hallazgos

1. Serde's tagged unit `List` variant ignored unknown members despite the enum
   deny-unknown-fields setting. A direct deserialization assertion exposed the
   problem (the initial invalid-PDF test could fail later for another reason).
   Corrected to a zero-field struct variant; direct parsing and FFI tests now pass.

## Calidad de Tests (Kripteia)

Native analysis: score 97, 222 tests / 19 files. Managed: score 95, 938 tests /
118 files. New slot tests score 100. Native tool notes unwrap/assertion messages
in fixture setup; these unwrap only fixed literal C strings and are not reachable
from product input. Manual review confirms tests reach native/core code and the
mutation fails on the completion assertion, not compilation or infrastructure.

## Análisis de Seguridad (Kripteia Security)

Rust identifies 284 existing/new unsafe/FFI surfaces, including the new exported
function. Its pointer/length obligations are documented; managed code pins the
owned snapshot, native rejects nulls, panic guard contains Rust panics, and JSON
allocation is freed in a managed finally block. Native JSON requests remain a
private transport, not the public managed API.

C# scanner executes but reports partial/error syntax trees, including the two new
C# files, so its automated coverage is incomplete. Manual review additionally
traces cancellation, input snapshots, UTF-8 marshalling, native error retrieval on
the same thread, output freeing after deserialization failures and pin release.
No confirmed unresolved security finding within this increment; scanner scores
are not proof of memory safety or complete coverage.

## Métricas

- Product/test files reviewed: native module and registration, managed API and tests.
- Findings: one corrected strict-deserialization issue.
- Product edits made by read-only review: 0; correction followed separately.
- Pending: platform CI and remaining Rust capabilities in #81.
