# OxidizePdf.NET 0.19.0

- Update the exact Rust core dependency to the published 5.4.2.
- Add typed signature-slot creation, reading, removal, drawing and completion.
- Expose complete text-extraction and parse options, bounded recovery diagnostics,
  effective PDF version, original text colors and build identification controls.
- Initialize native-library resolution for static extraction APIs as well as documents.
- Enforce extraction contracts, defaults and core-version consistency in CI.

Targets .NET 8/9/10, with native libraries for eight supported runtime identifiers.
Implementation PRs #80, #82 and #83 passed all 14 CI checks each.
Full functional parity remains tracked in #81.

Known upstream limitation: core 5.4.2 does not enforce DocMDP on incremental
text-note and highlight mutations (#712 in oxidizePdf). Its merged upstream fix
is not part of this package's published core dependency. FreeText, Ink and
geometric editing use the existing permission checks.

Install: `dotnet add package OxidizePdf.NET --version 0.19.0`.
