using System.Runtime.InteropServices;

namespace OxidizePdf.NET;

/// <summary>
/// P/Invoke declarations for oxidize-pdf FFI library
/// </summary>
internal static class NativeMethods
{
    private const string LibraryName = "oxidize_pdf_ffi";

    /// <summary>
    /// Error codes returned by native functions
    /// </summary>
    internal enum ErrorCode
    {
        Success = 0,
        NullPointer = 1,
        InvalidUtf8 = 2,
        PdfParseError = 3,
        AllocationError = 4,
        SerializationError = 5,
        IoError = 6,
        EncryptionError = 7,
        PermissionError = 8,
    }

    /// <summary>
    /// Standard PDF fonts (the 14 base fonts) — mirrors Rust StandardFont enum
    /// </summary>
    internal enum StandardFont
    {
        Helvetica = 0,
        HelveticaBold = 1,
        HelveticaOblique = 2,
        HelveticaBoldOblique = 3,
        TimesRoman = 4,
        TimesBold = 5,
        TimesItalic = 6,
        TimesBoldItalic = 7,
        Courier = 8,
        CourierBold = 9,
        CourierOblique = 10,
        CourierBoldOblique = 11,
        Symbol = 12,
        ZapfDingbats = 13,
    }

    /// <summary>
    /// Page size presets — mirrors Rust PagePreset enum
    /// </summary>
    internal enum PagePreset
    {
        A4 = 0,
        A4Landscape = 1,
        Letter = 2,
        LetterLandscape = 3,
        Legal = 4,
        LegalLandscape = 5,
    }

    /// <summary>
    /// Text alignment — mirrors Rust TextAlign enum
    /// </summary>
    internal enum TextAlign
    {
        Left = 0,
        Right = 1,
        Center = 2,
        Justified = 3,
    }

    /// <summary>
    /// Line cap style — mirrors Rust LineCap enum
    /// </summary>
    internal enum LineCap
    {
        Butt = 0,
        Round = 1,
        Square = 2,
    }

    /// <summary>
    /// Line join style — mirrors Rust LineJoin enum
    /// </summary>
    internal enum LineJoin
    {
        Miter = 0,
        Round = 1,
        Bevel = 2,
    }

    /// <summary>
    /// Text rendering mode — mirrors Rust TextRenderingMode enum
    /// </summary>
    internal enum TextRenderingMode
    {
        Fill = 0,
        Stroke = 1,
        FillStroke = 2,
        Invisible = 3,
        FillClip = 4,
        StrokeClip = 5,
        FillStrokeClip = 6,
        Clip = 7,
    }

    /// <summary>
    /// Blend mode — mirrors Rust BlendMode enum
    /// </summary>
    internal enum BlendMode
    {
        Normal = 0,
        Multiply = 1,
        Screen = 2,
        Overlay = 3,
        SoftLight = 4,
        HardLight = 5,
        ColorDodge = 6,
        ColorBurn = 7,
        Darken = 8,
        Lighten = 9,
        Difference = 10,
        Exclusion = 11,
        Hue = 12,
        Saturation = 13,
        Color = 14,
        Luminosity = 15,
    }

    /// <summary>
    /// Chunk options for text extraction
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct ChunkOptionsNative
    {
        public nuint MaxChunkSize;
        public nuint Overlap;
        [MarshalAs(UnmanagedType.I1)]
        public bool PreserveSentenceBoundaries;
        [MarshalAs(UnmanagedType.I1)]
        public bool IncludeMetadata;
    }

    /// <summary>
    /// Free a C string allocated by Rust
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void oxidize_free_string(IntPtr ptr);

    /// <summary>
    /// Free a byte array previously allocated by an oxidize_* function
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void oxidize_free_bytes(IntPtr ptr, nuint len);

    // ── Document ──────────────────────────────────────────────────────────────

    /// <summary>Create a new empty document handle</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr oxidize_document_create();

    /// <summary>Free a document handle</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void oxidize_document_free(IntPtr handle);

    /// <summary>Set the document title metadata</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_set_title(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text);

    /// <summary>Set the document author metadata</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_set_author(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text);

    /// <summary>Set the document subject metadata</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_set_subject(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text);

    /// <summary>Set the document keywords metadata</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_set_keywords(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text);

    /// <summary>Set the document creator metadata</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_set_creator(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text);

    /// <summary>Set the document producer metadata</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_set_producer(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text);

    /// <summary>Set the document creation date from Unix timestamp (seconds)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_set_creation_date(IntPtr handle, long unixTimestampSecs);

    /// <summary>Set the document modification date from Unix timestamp (seconds)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_set_modification_date(IntPtr handle, long unixTimestampSecs);

    /// <summary>Save document to a file path</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_save_to_file(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path);

    /// <summary>Add a page to the document (page is cloned internally)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_add_page(IntPtr docHandle, IntPtr pageHandle);

    /// <summary>Serialize the document to PDF bytes</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_save_to_bytes(
        IntPtr handle,
        out IntPtr outBytes,
        out nuint outLen);

    /// <summary>Get the number of pages in the document</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_page_count(IntPtr handle, out nuint outCount);

    /// <summary>Register a custom font from byte data (TTF/OTF)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_add_font_from_bytes(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        IntPtr fontBytes,
        nuint fontLen);

    // ── Page ──────────────────────────────────────────────────────────────────

    /// <summary>Create a new page with explicit dimensions in PDF points</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr oxidize_page_create(double width, double height);

    /// <summary>Create a new page from a size preset</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr oxidize_page_create_preset(PagePreset preset);

    /// <summary>Free a page handle</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void oxidize_page_free(IntPtr handle);

    /// <summary>Set page margins in PDF points</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_margins(
        IntPtr handle,
        double top,
        double right,
        double bottom,
        double left);

    /// <summary>Get the page width in PDF points</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_get_width(IntPtr handle, out double outValue);

    /// <summary>Set page rotation in degrees</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_rotation(IntPtr handle, int degrees);

    /// <summary>Get page rotation in degrees</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_get_rotation(IntPtr handle, out int outDegrees);

    /// <summary>Get the page height in PDF points</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_get_height(IntPtr handle, out double outValue);

    // ── Text operations ───────────────────────────────────────────────────────

    /// <summary>Set a custom (embedded) font on a page by name</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_custom_font(
        IntPtr page,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string fontName,
        double size);

    /// <summary>Set the current font and size on a page</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_font(IntPtr page, StandardFont font, double size);

    /// <summary>Set text fill color using RGB components (each 0.0–1.0)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_text_color_rgb(IntPtr page, double r, double g, double b);

    /// <summary>Set text fill color using a gray value (0.0 = black, 1.0 = white)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_text_color_gray(IntPtr page, double value);

    /// <summary>Set text fill color using CMYK components (each 0.0–1.0)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_text_color_cmyk(IntPtr page, double c, double m, double y, double k);

    /// <summary>Set character spacing for subsequent text operations</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_character_spacing(IntPtr page, double spacing);

    /// <summary>Set word spacing for subsequent text operations</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_word_spacing(IntPtr page, double spacing);

    /// <summary>Set text leading (line spacing) for subsequent text operations</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_leading(IntPtr page, double leading);

    /// <summary>Set horizontal scaling for subsequent text operations</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_horizontal_scaling(IntPtr page, double scale);

    /// <summary>Set text rise (vertical offset) for subsequent text operations</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_text_rise(IntPtr page, double rise);

    /// <summary>Set the text rendering mode</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_rendering_mode(IntPtr page, TextRenderingMode mode);

    /// <summary>Write text at the given position on the page</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_text_at(
        IntPtr page,
        double x,
        double y,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text);

    // ── Graphics operations ───────────────────────────────────────────────────

    /// <summary>Set graphics fill color using RGB components (each 0.0–1.0)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_fill_color_rgb(IntPtr page, double r, double g, double b);

    /// <summary>Set graphics fill color using a gray value (0.0 = black, 1.0 = white)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_fill_color_gray(IntPtr page, double value);

    /// <summary>Set graphics fill color using CMYK components (each 0.0–1.0)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_fill_color_cmyk(IntPtr page, double c, double m, double y, double k);

    /// <summary>Set graphics stroke color using RGB components (each 0.0–1.0)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_stroke_color_rgb(IntPtr page, double r, double g, double b);

    /// <summary>Set graphics stroke color using a gray value (0.0 = black, 1.0 = white)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_stroke_color_gray(IntPtr page, double value);

    /// <summary>Set graphics stroke color using CMYK components (each 0.0–1.0)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_stroke_color_cmyk(IntPtr page, double c, double m, double y, double k);

    /// <summary>Set the stroke line width</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_line_width(IntPtr page, double width);

    /// <summary>Set fill opacity (0.0 = transparent, 1.0 = opaque)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_fill_opacity(IntPtr page, double opacity);

    /// <summary>Set stroke opacity (0.0 = transparent, 1.0 = opaque)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_stroke_opacity(IntPtr page, double opacity);

    /// <summary>Add a rectangle to the current path</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_rect(IntPtr page, double x, double y, double width, double height);

    /// <summary>Add a circle to the current path</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_circle(IntPtr page, double cx, double cy, double radius);

    /// <summary>Move the current point to (x, y) without drawing</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_move_to(IntPtr page, double x, double y);

    /// <summary>Draw a line from the current point to (x, y)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_line_to(IntPtr page, double x, double y);

    /// <summary>Draw a cubic Bezier curve using two control points and an endpoint</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_curve_to(
        IntPtr page,
        double x1, double y1,
        double x2, double y2,
        double x3, double y3);

    /// <summary>Close the current path</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_close_path(IntPtr page);

    /// <summary>Fill the current path</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_fill(IntPtr page);

    /// <summary>Stroke the current path</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_stroke(IntPtr page);

    /// <summary>Fill and then stroke the current path</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_fill_and_stroke(IntPtr page);

    /// <summary>Set the line cap style for stroke operations</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_line_cap(IntPtr page, LineCap cap);

    /// <summary>Set the line join style for stroke operations</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_line_join(IntPtr page, LineJoin join);

    /// <summary>Set the miter limit for stroke joins</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_miter_limit(IntPtr page, double limit);

    /// <summary>Set a dash pattern for stroke operations</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_dash_pattern(IntPtr page, double dashLength, double gapLength);

    /// <summary>Reset stroke to solid line (no dash pattern)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_line_solid(IntPtr page);

    /// <summary>Save the current graphics state</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_save_state(IntPtr page);

    /// <summary>Restore the most recently saved graphics state</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_restore_state(IntPtr page);

    /// <summary>Set a rectangular clipping region</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_clip_rect(IntPtr page, double x, double y, double width, double height);

    /// <summary>Set a circular clipping region</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_clip_circle(IntPtr page, double cx, double cy, double radius);

    /// <summary>Clear all clipping regions</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_clear_clipping(IntPtr page);

    /// <summary>Set the blend mode for compositing</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_blend_mode(IntPtr page, BlendMode mode);

    // ── Operations (bytes in / bytes out) ─────────────────────────────────────

    /// <summary>Split a PDF into individual single-page PDFs; returns JSON array of base64 strings</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_split_pdf_bytes(
        IntPtr pdfBytes,
        nuint pdfLen,
        out IntPtr outJson);

    /// <summary>Merge multiple PDFs (JSON array of base64 strings) into one PDF</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_merge_pdfs_bytes(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string pdfsJson,
        out IntPtr outBytes,
        out nuint outLen);

    /// <summary>Rotate all pages of a PDF by degrees (0, 90, 180, or 270)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_rotate_pdf_bytes(
        IntPtr pdfBytes,
        nuint pdfLen,
        int degrees,
        out IntPtr outBytes,
        out nuint outLen);

    /// <summary>Extract specific pages into a new PDF; pages_json is a JSON array of 0-based indices</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_extract_pages_bytes(
        IntPtr pdfBytes,
        nuint pdfLen,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string pagesJson,
        out IntPtr outBytes,
        out nuint outLen);

    /// <summary>Reorder pages according to a new order (JSON array of 0-based indices)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_reorder_pages_bytes(
        IntPtr pdfBytes,
        nuint pdfLen,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string orderJson,
        out IntPtr outBytes,
        out nuint outLen);

    /// <summary>Swap two pages by 0-based indices</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_swap_pages_bytes(
        IntPtr pdfBytes,
        nuint pdfLen,
        nuint pageA,
        nuint pageB,
        out IntPtr outBytes,
        out nuint outLen);

    /// <summary>Move a page from one position to another (0-based)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_move_page_bytes(
        IntPtr pdfBytes,
        nuint pdfLen,
        nuint fromIndex,
        nuint toIndex,
        out IntPtr outBytes,
        out nuint outLen);

    /// <summary>Reverse the order of all pages</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_reverse_pages_bytes(
        IntPtr pdfBytes,
        nuint pdfLen,
        out IntPtr outBytes,
        out nuint outLen);

    /// <summary>Overlay one PDF on top of another</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_overlay_pdf_bytes(
        IntPtr baseBytes,
        nuint baseLen,
        IntPtr overlayBytes,
        nuint overlayLen,
        out IntPtr outBytes,
        out nuint outLen);

    // ── Table ─────────────────────────────────────────────────────────────────

    /// <summary>Create a table builder with equal-width columns from JSON header array</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_table_builder_create(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string headersJson,
        double totalWidth,
        out IntPtr outHandle);

    /// <summary>Free a table builder handle</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void oxidize_table_builder_free(IntPtr handle);

    /// <summary>Set table position</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_table_builder_set_position(IntPtr handle, double x, double y);

    /// <summary>Add a data row to the table (JSON array of cell strings)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_table_builder_add_row(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string cellsJson);

    /// <summary>Build the table and add it to a page (consumes the builder)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_add_table(IntPtr page, IntPtr builder);

    // ── Header/Footer ────────────────────────────────────────────────────────

    /// <summary>Set a header on a page</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_header(
        IntPtr page,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string content,
        StandardFont font,
        double size);

    /// <summary>Set a footer on a page</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_footer(
        IntPtr page,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string content,
        StandardFont font,
        double size);

    // ── Lists ────────────────────────────────────────────────────────────────

    /// <summary>Add an ordered list to a page (items as JSON array)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_add_ordered_list(
        IntPtr page,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string itemsJson,
        double x, double y,
        OrderedListStyle style);

    /// <summary>Add an unordered list to a page (items as JSON array)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_add_unordered_list(
        IntPtr page,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string itemsJson,
        double x, double y,
        BulletStyle style);

    /// <summary>Ordered list style — mirrors Rust enum</summary>
    internal enum OrderedListStyle
    {
        Decimal = 0,
        LowerAlpha = 1,
        UpperAlpha = 2,
        LowerRoman = 3,
        UpperRoman = 4,
    }

    /// <summary>Bullet style — mirrors Rust enum</summary>
    internal enum BulletStyle
    {
        Disc = 0,
        Circle = 1,
        Square = 2,
        Dash = 3,
    }

    // ── Image ─────────────────────────────────────────────────────────────────

    /// <summary>Create an image from JPEG data</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_image_from_jpeg(IntPtr data, nuint dataLen, out IntPtr outHandle);

    /// <summary>Create an image from PNG data</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_image_from_png(IntPtr data, nuint dataLen, out IntPtr outHandle);

    /// <summary>Free an image handle</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void oxidize_image_free(IntPtr handle);

    /// <summary>Get image width in pixels</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_image_get_width(IntPtr handle, out uint outWidth);

    /// <summary>Get image height in pixels</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_image_get_height(IntPtr handle, out uint outHeight);

    /// <summary>Add an image to a page by name</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_add_image(
        IntPtr page,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        IntPtr image);

    /// <summary>Draw a previously added image at position and dimensions</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_draw_image(
        IntPtr page,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        double x, double y, double width, double height);

    // ── Security ──────────────────────────────────────────────────────────────

    /// <summary>Encrypt a document with user and owner passwords using default permissions</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_encrypt(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string userPw,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string ownerPw);

    /// <summary>Encrypt a document with user and owner passwords and explicit permission flags</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_encrypt_with_permissions(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string userPw,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string ownerPw,
        uint permissionsFlags);

    /// <summary>
    /// Extract plain text from PDF bytes
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_extract_text(
        IntPtr pdfBytes,
        nuint pdfLen,
        out IntPtr outText
    );

    /// <summary>
    /// Extract text chunks optimized for RAG/LLM pipelines
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_extract_chunks(
        IntPtr pdfBytes,
        nuint pdfLen,
        ref ChunkOptionsNative options,
        out IntPtr outJson
    );

    /// <summary>
    /// Get version string
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_version(out IntPtr outVersion);

    /// <summary>
    /// Get the last error message from native library
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_get_last_error(out IntPtr outError);

    /// <summary>
    /// Get the number of pages in a PDF
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_get_page_count(
        IntPtr pdfBytes,
        nuint pdfLen,
        out nuint outCount
    );

    /// <summary>
    /// Extract plain text from a specific page of a PDF
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_extract_text_from_page(
        IntPtr pdfBytes,
        nuint pdfLen,
        nuint pageNumber,
        out IntPtr outText
    );

    /// <summary>
    /// Extract text chunks from a specific page of a PDF
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_extract_chunks_from_page(
        IntPtr pdfBytes,
        nuint pdfLen,
        nuint pageNumber,
        ref ChunkOptionsNative options,
        out IntPtr outJson
    );

    // ── TextFlow operations ──────────────────────────────────────────────────

    /// <summary>Create a text flow context from a page handle</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr oxidize_text_flow_create(IntPtr page);

    /// <summary>Free a text flow handle</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void oxidize_text_flow_free(IntPtr handle);

    /// <summary>Set font and size on a text flow context</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_text_flow_set_font(IntPtr handle, StandardFont font, double size);

    /// <summary>Set text alignment on a text flow context</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_text_flow_set_alignment(IntPtr handle, TextAlign alignment);

    /// <summary>Write wrapped text into a text flow context</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_text_flow_write_wrapped(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text);

    /// <summary>Add a text flow's operations to a page</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_add_text_flow(IntPtr page, IntPtr flow);

    // ── Parser — additional operations ───────────────────────────────────────

    /// <summary>Check if a PDF is encrypted</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_is_encrypted(
        IntPtr pdfBytes,
        nuint pdfLen,
        [MarshalAs(UnmanagedType.I1)] out bool outEncrypted);

    /// <summary>Try to unlock an encrypted PDF with a password</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_unlock_pdf(
        IntPtr pdfBytes,
        nuint pdfLen,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string password,
        [MarshalAs(UnmanagedType.I1)] out bool outUnlocked);

    /// <summary>Get the PDF version string</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_get_pdf_version(
        IntPtr pdfBytes,
        nuint pdfLen,
        out IntPtr outVersion);

    /// <summary>Get the dimensions of a specific page from a parsed PDF (1-based)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_get_page_dimensions(
        IntPtr pdfBytes,
        nuint pdfLen,
        nuint pageNumber,
        out double outWidth,
        out double outHeight);

    /// <summary>
    /// Gets the last error message from the native library and clears it
    /// </summary>
    /// <returns>The error message, or null if no error was set</returns>
    internal static string? GetLastError()
    {
        IntPtr errorPtr = IntPtr.Zero;
        try
        {
            var result = oxidize_get_last_error(out errorPtr);
            if (result != (int)ErrorCode.Success || errorPtr == IntPtr.Zero)
                return null;

            return Marshal.PtrToStringUTF8(errorPtr);
        }
        finally
        {
            if (errorPtr != IntPtr.Zero)
                oxidize_free_string(errorPtr);
        }
    }

    /// <summary>
    /// Load native library for current platform
    /// </summary>
    static NativeMethods()
    {
        NativeLibrary.SetDllImportResolver(typeof(NativeMethods).Assembly, DllImportResolver);
    }

    private static IntPtr DllImportResolver(string libraryName, System.Reflection.Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != LibraryName)
            return IntPtr.Zero;

        // Determine platform-specific library name
        string fileName;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            fileName = $"{libraryName}.dll";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            fileName = $"lib{libraryName}.so";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            fileName = $"lib{libraryName}.dylib";
        else
            throw new PlatformNotSupportedException($"Unsupported platform: {RuntimeInformation.OSDescription}");

        // Determine runtime identifier
        string rid;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            rid = "win-x64";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            rid = "linux-x64";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            rid = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
        else
            throw new PlatformNotSupportedException($"Unsupported platform: {RuntimeInformation.OSDescription}");

        // Try to load from runtimes folder
        var baseDirectory = AppContext.BaseDirectory;
        var libraryPath = Path.Combine(baseDirectory, "runtimes", rid, "native", fileName);

        if (File.Exists(libraryPath))
        {
            if (NativeLibrary.TryLoad(libraryPath, out var handle))
                return handle;
        }

        // Try to load from current directory (development scenario)
        libraryPath = Path.Combine(baseDirectory, fileName);
        if (File.Exists(libraryPath))
        {
            if (NativeLibrary.TryLoad(libraryPath, out var handle))
                return handle;
        }

        throw new DllNotFoundException(
            $"Unable to load native library '{fileName}' for runtime '{rid}'. " +
            $"Searched paths: {baseDirectory}");
    }
}
