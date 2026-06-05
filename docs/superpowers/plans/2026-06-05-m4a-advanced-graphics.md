# M4a — Advanced Graphics (FFI binding) — TDD Plan

**Issue:** #23 (M4). **Scope:** GFX-016, GFX-018, GFX-020, GFX-021, GFX-022, GFX-023, GFX-024.
**Excluded:** GFX-017 shadings/gradients — hard upstream gap (writer emits placeholder
`Function: Object::Integer(1)` in `graphics/shadings.rs:142,270`; no `sh` paint operator).
Deferred to M4b after an upstream core fix.

Core verified against `oxidize-pdf 2.12.0` (registry source resolved in `native/Cargo.lock`).

## Verified core API (2.12.0)

| Feature | Core entry point | Returns |
|---|---|---|
| GFX-016 | `TilingPattern::new(name:String, PaintType, TilingType, bbox:[f64;4], x_step, y_step)`, `.with_content_stream(Vec<u8>)`, `.with_matrix(PatternMatrix{matrix:[f64;6]})`; `Page::add_pattern(name, TilingPattern)->Result`; trait `PatternGraphicsContext::set_fill_pattern/set_stroke_pattern(&str)->Result` (emits `/Pattern cs /N scn` / `/Pattern CS /N SCN`) | — |
| GFX-018 | `FormXObject::new(Rectangle)`, `.with_content(Vec<u8>)`, `.with_matrix([f64;6])`; `Page::add_form_xobject(name, FormXObject)->Result`; invoke via `GraphicsContext::add_command("/N Do")` (pub, mod.rs:1177) | — |
| GFX-020 | `GraphicsContext::begin_transparency_group(TransparencyGroup)`, `end_transparency_group()`; `TransparencyGroup::new().with_isolated/with_knockout/with_blend_mode/with_opacity/with_color_space` | `&mut Self` |
| GFX-021 | `ExtGState::new()` + `set_soft_mask(SoftMask)`; `SoftMask::alpha(String)/luminosity(String)/none()`; apply via `GraphicsContext::apply_extgstate(ExtGState)` | `Result<&mut Self>` |
| GFX-022 | `GraphicsContext::draw_text(&str, x, y)` (emits BT/Tf/Td/Tj/ET); needs a prior font set | `Result<&mut Self>` |
| GFX-023 | `GraphicsContext::draw_image_with_transparency(name, x, y, w, h, mask_name:Option<&str>)` | `&mut Self` |
| GFX-024 | `GraphicsContext::clip_ellipse(cx, cy, rx, ry)` (emits m/c×4/W/n) | `Result<&mut Self>` |

`Rectangle::from_position_and_size(x,y,w,h)`. `BlendMode` FFI enum already in `types.rs` with `to_oxidize()`.

## FFI conventions (from native/src)

- `clear_last_error()` first line; null → `ErrorCode::NullPointer(1)`; bad UTF-8 → `InvalidUtf8(2)`;
  invalid arg → `InvalidArgument(9)`; upstream `Err(e)` → `set_last_error(format!())` + `PdfParseError(3)`.
- Strings in: `*const c_char` + `CStr::from_ptr(p).to_str()`. C#: `[MarshalAs(UnmanagedType.LPUTF8Str)] string`.
  Nullable string: `IntPtr` + `Marshal.StringToCoTaskMemUTF8`/`FreeCoTaskMem`.
- Byte buffers in: `(*const u8, usize)` → `slice::from_raw_parts(p,len).to_vec()`. C#: `fixed`/pinned.
- Optional `[f64;6]` matrix: `*const f64` nullable; non-null ⇒ exactly 6 values.
- Inline `#[cfg(test)]` per section: valid-returns-success + null-page-returns-1.
- C# tests: xUnit `[Fact]`, plain `Assert`, in `Tests/Graphics/`. Assert real content-stream bytes via
  `ContentStreamHelper` (`DecompressFirstContentStream`, `ToLatin1`). NO smoke tests.

## Cycles (RED → GREEN → REFACTOR)

Dependency order: GFX-018 before GFX-020/021 (soft mask references a registered FormXObject).

1. **GFX-016 Tiling patterns** — `oxidize_page_add_tiling_pattern`, `oxidize_page_set_fill_pattern`,
   `oxidize_page_set_stroke_pattern`. New C# `Graphics/PdfTilingPattern.cs` (+ PaintType/TilingType enums).
   Assert: `/Pattern` in resources; `scn`/`SCN` in content stream.
2. **GFX-018 FormXObject** — `oxidize_page_add_form_xobject`, `oxidize_page_invoke_xobject`.
   New `Graphics/PdfFormXObject.cs`. Assert: `/XObject`+`/Form` in resources; `/Fm1 Do` in stream.
3. **GFX-020 Transparency groups** — `oxidize_page_begin_transparency_group`,
   `oxidize_page_end_transparency_group`. New `Graphics/PdfTransparencyGroup.cs`. Assert real
   group markers + `gs`/`BM` in stream. (NO `Length>0` smoke test — corrected from draft plan.)
4. **GFX-021 Soft masks** — `oxidize_page_apply_soft_mask`. New `Graphics/PdfSoftMask.cs`.
   Assert: `/SMask` in ExtGState dict; `gs` in stream.
5. **GFX-022 Draw text from gfx ctx** — `oxidize_page_draw_text_at`. `PdfPage.DrawTextAt`.
   Assert: BT/Tf/Td/Tj/ET + text bytes in stream.
6. **GFX-023 Draw image w/ transparency** — `oxidize_page_draw_image_with_transparency` (image.rs).
   Assert: `gs` + `/Img Do`; `/SMask` when mask given.
7. **GFX-024 Clip ellipse** — `oxidize_page_clip_ellipse`. `PdfPage.ClipEllipse`.
   Assert: `m`,`c`×4,`W`,`n` in stream; zero radius → `InvalidArgument`.

Final: `cargo test` + `cargo clippy -D warnings` + `dotnet test -warnaserror` all green.
