# Rust core 5.5.4 compatibility

Tracking: #111; core security correction bzsanti/oxidizePdf#828.
Development pins `oxidize-pdf = "=5.5.4"`. Binding package versions and public
APIs remain unchanged; this adoption does not publish a Python/NuGet package.

Core 5.5.4 updates the optional ECDSA dependencies and oxidize-webpki to remove
crypto-bigint versions affected by RUSTSEC-2026-0387. It enforces a patched
minimum for existing consumer lockfiles and preserves Rust 1.88 support.
No new advisory suppression is introduced.

The behavior changes from the previous released core pin 5.5.0 are described
in [the 5.5.3 compatibility notes](UPSTREAM-5.5.3.md). Those validation results
apply to 5.5.3 and do not establish validation of this new pin.

## Validation

Registry 5.5.4 is published and not yanked. Its checksum is
`360fa623da3712364af5cdf81645987edab40cb3f1fb2cce50eac1168534fc8d`;
VCS identity is `24bc1f844b3121f84edb17f00d9b90fdb7d27c10`.
The resolved graph contains crypto-bigint 0.7.10 and oxidize-webpki 0.1.1.
The consumer lock also updates rustls-webpki to 0.103.16 and webpki-roots to
1.0.11 for RUSTSEC-2026-0382/0381. These updates reject distrusted certificate paths;
core follow-up for minimum version requirements is tracked in
[bzsanti/oxidizePdf#831](https://github.com/bzsanti/oxidizePdf/issues/831).
Local validation: 275 native tests, 1,228 managed tests and 15 Kernel Memory
tests passed. Clippy passed. Platform CI is tracked by the adoption PR.
No local path override is used as a substitute for the published dependency.

The native Cargo.lock remains ignored under the repository's existing policy.
The certificate dependency versions above describe this validated local build;
CI resolves its own dependency graph and audits it. They are not a new minimum
version guarantee for existing external consumer lockfiles; core issue #831
tracks that follow-up.

[Review and validation evidence](evidence/core-5.5.4/review.md).
