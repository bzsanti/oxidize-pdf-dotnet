# Core 5.5.4 adoption review

Exact registry artifact verified against index checksum and release commit; see registry-identity.json. No path overrides. Core and certificate dependency corrections preserve binding APIs and feature selections. Two independent reviewers found no source/lock/documentation issues in the bounded delta. Existing reviews cover unchanged Rust/C# production code. No new advisory suppression.

Certificate dependency corrections: rustls-webpki0.103.16 and webpki-roots1.0.11 remove RUSTSEC-2026-0382/0381. Core floor follow-up remains bzsanti/oxidizePdf#831. Python lock is tracked; .NET lock remains ignored, so its exact graph is local evidence and CI resolves/audits independently.

.NET: 275 native tests,1228 managed tests and15 Kernel Memory tests pass; Clippy passes. Fresh cargo audit passes under the repository's existing RSA exclusion, with no new suppression; preexisting unsound/yanked warnings remain. This is not a claim that every dependency is advisory-free.

Platform CI is pending at this local checkpoint. No new binding package has been published.
