# 0.23.0

- Pin the published Rust core to 5.6.1, including strict indirect stream lengths and compact object-stream identifiers.
- Publish the generation APIs integrated in #101: page callbacks, serial and optional Rayon execution, and atomic file output.
- Preserve batch Rotate, ordered per-job outcomes, feature availability and preflight rejection of unsupported settings.

Generation and batch use separate core executors. Batch timeouts do not interrupt
running work or roll back output. Compress and nonzero batch memory caps remain
unsupported. See [generation contracts](docs/GENERATION.md) and
[core compatibility](docs/UPSTREAM-5.6.1.md).
