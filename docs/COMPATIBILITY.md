# Compatibility — oxidize-pdf-dotnet

## Version Matrix

| Bridge version | Core (oxidize-pdf) | FFI ABI | .NET | Platforms |
|---|---|---|---|---|
| 0.3.x | 2.1.0 | cdylib stable | net8.0, net9.0 | Linux x86_64, Windows x86_64, macOS x64/arm64 |
| 0.4.x | >=2.3.1, <3.0.0 | cdylib stable | net8.0, net9.0 | Linux x86_64, Windows x86_64, macOS x64/arm64 |

## Minimum Rust toolchain: 1.77

## API changes in 0.4.0

- Added `PdfDocument` class (document creation, metadata, encryption)
- Added `PdfPage` class (page creation, text, graphics)
- Added `PdfOperations` static class (split, merge, rotate, extract pages)
- Added `StandardFont` and `PdfPermissions` enums
- Existing `PdfExtractor` API is unchanged (fully backward compatible)
