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
        InvalidArgument = 9,

        /// <summary>A Rust panic was caught at the FFI boundary (see last error message).</summary>
        Panic = 10,
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

    /// <summary>
    /// Create a new A4 page bound to the document's FontMetricsStore.
    /// Required for correct custom-font measurement (oxidize-pdf 2.8.0+).
    /// Returned handle must be freed with <c>oxidize_page_free</c>.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr oxidize_document_new_page_a4(IntPtr docHandle);

    /// <summary>
    /// Create a new US Letter page bound to the document's FontMetricsStore.
    /// Returned handle must be freed with <c>oxidize_page_free</c>.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr oxidize_document_new_page_letter(IntPtr docHandle);

    /// <summary>
    /// Create a new page with explicit dimensions bound to the document's FontMetricsStore.
    /// Returned handle must be freed with <c>oxidize_page_free</c>.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr oxidize_document_new_page(
        IntPtr docHandle, double width, double height);

    /// <summary>Serialize the document to PDF bytes</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_save_to_bytes(
        IntPtr handle,
        out IntPtr outBytes,
        out nuint outLen);

    // ── document metadata — M1 (DOC-014/015/017/018/020) ───────────────────────

    /// <summary>Set the document open action (GoTo/URI) from a JSON payload.</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_set_open_action_json(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string json);

    /// <summary>Set the document viewer preferences from a JSON payload.</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_set_viewer_preferences_json(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string json);

    /// <summary>Register a named destination (name → destination) from a JSON payload.</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_add_named_destination_json(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string json);

    /// <summary>Set the document page-label ranges from a JSON payload.</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_set_page_labels_json(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string json);

    /// <summary>Serialize the document with an explicit WriterConfig (version, xref/object streams, compression).</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_save_to_bytes_with_config(
        IntPtr handle,
        int useXrefStreams,
        int useObjectStreams,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string pdfVersion,
        int compressStreams,
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

    /// <summary>Register a custom font from a file path (TTF/OTF)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_add_font_from_file(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path);

    /// <summary>Register a CID-keyed (CID = GID) TrueType font with a mapping JSON</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_add_cid_keyed_font(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        IntPtr fontBytes,
        nuint fontLen,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string mappingJson);

    /// <summary>Draw a positioned glyph run over a CID-keyed font on a page</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_show_cid_array(
        IntPtr page,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string fontName,
        double size,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string elementsJson,
        double x,
        double y);

    /// <summary>Set the document outline (bookmarks) from a JSON tree</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_set_outline(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string outlineJson);

    // ── Forms (M2 write-path) ───────────────────────────────────────────────────

    /// <summary>Enable interactive forms on the document (idempotent).</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_enable_forms(IntPtr handle);

    /// <summary>Create an AcroForm field from a tagged JSON DTO; returns its object number.</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_add_form_field_json(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string json,
        out uint outObjNum);

    /// <summary>Set a form field's value in-process (updates /V and regenerates appearance).</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_fill_field(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string value);

    /// <summary>Attach a widget annotation to a page, linked to a field by its object number.</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_add_form_widget_json(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string json,
        uint fieldObjNum);

    // ── Page ──────────────────────────────────────────────────────────────────

    /// <summary>Create a new page with explicit dimensions in PDF points</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr oxidize_page_create(double width, double height);

    /// <summary>Create a new page from a size preset</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr oxidize_page_create_preset(PagePreset preset);

    /// <summary>
    /// PAGE-010: Build a writable page from a parsed page of an existing PDF,
    /// preserving original content and resources. Returns a page handle (or
    /// IntPtr.Zero on error) that must be freed with <c>oxidize_page_free</c>
    /// or handed to <c>oxidize_document_add_page</c>.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr oxidize_page_from_parsed_bytes(
        IntPtr bytes,
        nuint bytesLen,
        uint pageIndex);

    /// <summary>
    /// PAGE-011: Switch the page to a screen-space (top-left origin) coordinate
    /// system with a uniform scale, emitting a Y-flip CTM at the head of the
    /// page's graphics stream.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_begin_screen_space(IntPtr handle, double scale);

    /// <summary>PAGE-009: begin a marked-content sequence; returns the assigned MCID via outMcid.</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_begin_marked_content(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string tag,
        out uint outMcid);

    /// <summary>PAGE-009: end the most recent marked-content sequence.</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_end_marked_content(IntPtr handle);

    /// <summary>TXT-014: flow text across columns on the page from a JSON description.</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_render_columns_json(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string json);

    /// <summary>DOC-019: attach a Tagged-PDF structure tree to the document from JSON.</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_set_struct_tree_json(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string json);

    /// <summary>DOC-021: mark a region as a typed semantic entity.</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_mark_entity(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string id,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string entityType,
        double x, double y, double width, double height, uint page);

    /// <summary>DOC-021: set the content text of a marked entity.</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_set_entity_content(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string id,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string content);

    /// <summary>DOC-021: add a metadata key/value pair to a marked entity.</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_add_entity_metadata(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string id,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string value);

    /// <summary>DOC-021: set the confidence (0..1) of a marked entity.</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_set_entity_confidence(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string id,
        float confidence);

    /// <summary>DOC-021: record a relationship between two marked entities.</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_relate_entities(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string fromId,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string toId,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string relation);

    /// <summary>DOC-021: export semantic entities as a plain JSON array (full fidelity).</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_export_semantic_entities_json(IntPtr handle, out IntPtr outJson);

    /// <summary>DOC-021: export semantic entities as Schema.org JSON-LD.</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_export_semantic_entities_json_ld(IntPtr handle, out IntPtr outJson);

    /// <summary>TXT-016: validate contract-style text; returns JSON TextValidationResult.</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_text_validate_contract(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text,
        out IntPtr outJson);

    /// <summary>TXT-016: search text for a target string; returns JSON TextValidationResult.</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_text_search_target(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string target,
        out IntPtr outJson);

    /// <summary>TXT-016: extract key info (dates, amounts, organizations) as JSON.</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_text_extract_key_info(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text,
        out IntPtr outJson);

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

    /// <summary>Get all four page margins in PDF points</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_get_margins(
        IntPtr handle,
        out double outTop,
        out double outRight,
        out double outBottom,
        out double outLeft);

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

    /// <summary>Set text stroke color using RGB components (each 0.0–1.0)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_text_stroke_color_rgb(IntPtr page, double r, double g, double b);

    /// <summary>Set text stroke color using a gray value (0.0 = black, 1.0 = white)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_text_stroke_color_gray(IntPtr page, double value);

    /// <summary>Set text stroke color using CMYK components (each 0.0–1.0)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_text_stroke_color_cmyk(IntPtr page, double c, double m, double y, double k);

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

    /// <summary>Register an axial/radial gradient shading on the page from a JSON definition.</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_add_shading_json(
        IntPtr page,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string json);

    /// <summary>Paint a registered shading with the `sh` operator.</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_paint_shading(
        IntPtr page,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    /// <summary>End the current path without filling or stroking (the `n` operator).</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_end_path(IntPtr page);

    /// <summary>Set a circular clipping region</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_clip_circle(IntPtr page, double cx, double cy, double radius);

    /// <summary>Clear all clipping regions</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_clear_clipping(IntPtr page);

    /// <summary>Set the blend mode for compositing</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_blend_mode(IntPtr page, BlendMode mode);

    /// <summary>Translate the coordinate system by (tx, ty)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_translate(IntPtr page, double tx, double ty);

    /// <summary>Scale the coordinate system by (sx, sy)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_scale(IntPtr page, double sx, double sy);

    /// <summary>Rotate the coordinate system by the given angle in radians</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_rotate_radians(IntPtr page, double angle);

    /// <summary>Apply a 6-element CTM transformation matrix [a b c d e f]</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_transform(IntPtr page, double a, double b, double c, double d, double e, double f);

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

    /// <summary>
    /// Fill AcroForm fields on an existing (already-serialized) PDF via an ISO 32000-1 §7.5.6
    /// incremental update, returning the updated bytes. fields_json is a JSON array of
    /// {"name":..,"value":..} entries. Works on any parsed PDF (Acrobat, pdftk, ReportLab, …),
    /// unlike <see cref="oxidize_document_fill_field"/> which only fills in-process fields.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_fill_existing_form_json(
        IntPtr pdfBytes,
        nuint pdfLen,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string fieldsJson,
        out IntPtr outBytes,
        out nuint outLen);

    /// <summary>
    /// Extract all images from a PDF. Returns a JSON array of image objects with base64-encoded data.
    /// The returned string must be freed with <see cref="oxidize_free_string"/>.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_extract_images_bytes(
        IntPtr pdfBytes,
        nuint pdfLen,
        out IntPtr outJson);

    /// <summary>
    /// Split a PDF with configurable split options (JSON object with "mode" tag).
    /// Returns a JSON array of base64-encoded PDF strings, one per resulting chunk.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_split_pdf_bytes_with_options(
        IntPtr pdfBytes,
        nuint pdfLen,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string optionsJson,
        out IntPtr outJson);

    /// <summary>
    /// Merge multiple PDFs with per-input page range selection.
    /// inputsJson is a JSON array of {pdf: base64, pages: {kind: ...} | null} objects.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_merge_pdfs_with_ranges(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string inputsJson,
        out IntPtr outBytes,
        out nuint outLen);

    /// <summary>
    /// Rotate specific pages of a PDF. pagesJson is a JSON object with a "kind" tag,
    /// or null to rotate all pages.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_rotate_pages_bytes(
        IntPtr pdfBytes,
        nuint pdfLen,
        int degrees,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? pagesJson,
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

    /// <summary>Create an image from a file path (auto-detects JPEG/PNG/TIFF)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_image_from_file(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        out IntPtr outHandle);

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

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_draw_image_with_transparency(
        IntPtr page,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string imageName,
        double x, double y, double width, double height,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? maskName);

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

    /// <summary>Encrypt a document with AES-128 and default permissions</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_encrypt_aes128(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string userPw,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string ownerPw);

    /// <summary>Encrypt a document with AES-128 and explicit permission flags</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_encrypt_aes128_with_permissions(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string userPw,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string ownerPw,
        uint permissionsFlags);

    /// <summary>Encrypt a document with AES-256 and default permissions</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_encrypt_aes256(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string userPw,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string ownerPw);

    /// <summary>Encrypt a document with AES-256 and explicit permission flags</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_encrypt_aes256_with_permissions(
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

    // ── Parser — pipeline (partition + RAG chunks) ────────────────────────────

    /// <summary>Partition PDF into typed semantic elements (JSON array)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_partition(
        IntPtr pdfBytes, nuint pdfLen, out IntPtr outJson);

    /// <summary>Extract structure-aware RAG chunks from a PDF (JSON array)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_rag_chunks(
        IntPtr pdfBytes, nuint pdfLen, out IntPtr outJson);

    /// <summary>
    /// Partition PDF using a pre-configured ExtractionProfile. <paramref name="profile"/>
    /// is the byte discriminant of <c>OxidizePdf.NET.Pipeline.ExtractionProfile</c>
    /// (0 = Standard ... 6 = Rag).
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_partition_with_profile(
        IntPtr pdfBytes, nuint pdfLen, byte profile, out IntPtr outJson);

    /// <summary>
    /// Partition PDF using a <c>PartitionConfig</c> serialised as UTF-8 JSON
    /// (matches the serde shape of <c>oxidize_pdf::pipeline::PartitionConfig</c>).
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_partition_with_config(
        IntPtr pdfBytes, nuint pdfLen,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string configJson,
        out IntPtr outJson);

    /// <summary>Extract RAG chunks using a pre-configured ExtractionProfile.</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_rag_chunks_with_profile(
        IntPtr pdfBytes, nuint pdfLen, byte profile, out IntPtr outJson);

    /// <summary>
    /// Extract RAG chunks with optional partition and hybrid configs (pass
    /// <c>null</c> for either to use the upstream default for that stage).
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_rag_chunks_with_config(
        IntPtr pdfBytes, nuint pdfLen,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? partitionConfigJson,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? hybridConfigJson,
        out IntPtr outJson);

    /// <summary>
    /// Extract semantic chunks (element-boundary-aware). The semantic config is
    /// REQUIRED — passing <c>null</c> returns <c>NullPointer</c>; the partition
    /// config is optional and falls back to <c>PartitionConfig::default()</c>.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_semantic_chunks(
        IntPtr pdfBytes, nuint pdfLen,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? partitionConfigJson,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string semanticConfigJson,
        out IntPtr outJson);

    // ── Parser — structured export ─────────────────────────────────────────────

    /// <summary>Export PDF content as Markdown</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_to_markdown(
        IntPtr pdfBytes, nuint pdfLen, out IntPtr outText);

    /// <summary>
    /// Export PDF content as Markdown with explicit <c>MarkdownOptions</c>
    /// (RAG-012). Accepts the JSON serialisation of
    /// <c>OxidizePdf.NET.Ai.MarkdownOptions</c> — required.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_to_markdown_with_options(
        IntPtr pdfBytes, nuint pdfLen,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string optionsJson,
        out IntPtr outText);

    /// <summary>Export PDF content in contextual format (LLM-optimized)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_to_contextual(
        IntPtr pdfBytes, nuint pdfLen, out IntPtr outText);

    /// <summary>Export PDF content as structured JSON</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_to_json(
        IntPtr pdfBytes, nuint pdfLen, out IntPtr outText);

    // ── AI / text chunking (no PDF) ───────────────────────────────────────────

    /// <summary>
    /// Chunk arbitrary text using a fixed-size + overlap strategy (RAG-008).
    /// <paramref name="chunkSize"/> and <paramref name="overlap"/> are in
    /// whitespace-separated tokens; <paramref name="overlap"/> must be
    /// strictly less than <paramref name="chunkSize"/>.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_chunk_text(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text,
        nuint chunkSize, nuint overlap,
        out IntPtr outJson);

    /// <summary>
    /// Estimate token count for arbitrary text (RAG-009). Heuristic:
    /// <c>floor(words * 1.33)</c> where <c>words</c> is the count of
    /// whitespace-separated tokens.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_estimate_tokens(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text,
        out nuint outCount);

    // ── ai — chunking, language detection, token-efficient export (2.13.0) ──────

    /// <summary>
    /// Chunk a PDF into <c>DocumentChunk</c> records via the core
    /// <c>DocumentChunker</c>. When <paramref name="detectLanguage"/> is non-zero,
    /// per-chunk language detection populates each chunk's
    /// <c>metadata.language</c>. Returns a <c>DocumentChunkDto[]</c> JSON array.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_chunk_pdf(
        IntPtr pdfBytes, nuint pdfLen,
        nuint chunkSize, nuint overlap, byte detectLanguage,
        out IntPtr outJson);

    /// <summary>
    /// Compute the dominant language across a <c>DocumentChunkDto[]</c> JSON set
    /// that already carries per-chunk languages. Emits a
    /// <c>DetectedLanguageDto</c> JSON object, or the literal <c>null</c>.
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_language(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string chunksJson,
        out IntPtr outJson);

    /// <summary>
    /// Serialize a <c>DocumentChunkDto[]</c> JSON set into the token-efficient
    /// TOON-style payload (<c>TokenEfficientExporter::export_chunks</c>).
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_export_chunks_token_efficient(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string chunksJson,
        out IntPtr outStr);

    /// <summary>
    /// Parse a token-efficient payload back into a <c>DocumentChunkDto[]</c> JSON
    /// array (inverse of <see cref="oxidize_export_chunks_token_efficient"/>).
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_parse_chunks_token_efficient(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string input,
        out IntPtr outJson);

    // ── Parser — extraction options ────────────────────────────────────────────

    /// <summary>
    /// FFI-compatible extraction options struct matching Rust ExtractionOptionsFFI.
    /// Field order MUST match <c>ExtractionOptionsFFI</c> in <c>native/src/parser.rs</c>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct ExtractionOptionsNative
    {
        [MarshalAs(UnmanagedType.I1)]
        public bool PreserveLayout;
        public double SpaceThreshold;
        public double NewlineThreshold;
        [MarshalAs(UnmanagedType.I1)]
        public bool SortByPosition;
        [MarshalAs(UnmanagedType.I1)]
        public bool DetectColumns;
        public double ColumnThreshold;
        [MarshalAs(UnmanagedType.I1)]
        public bool MergeHyphenated;
        public double TjSpaceThreshold;
        [MarshalAs(UnmanagedType.I1)]
        public bool ReconstructParagraphs;
        [MarshalAs(UnmanagedType.I1)]
        public bool IncludeArtifacts;
    }

    /// <summary>Extract text from PDF bytes using custom extraction options</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_extract_text_with_options(
        IntPtr pdfBytes,
        nuint pdfLen,
        ref ExtractionOptionsNative options,
        out IntPtr outText);

    // ── Parser — metadata ─────────────────────────────────────────────────────

    /// <summary>Analyze a page's content to determine if it's text, scanned, or mixed</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_analyze_page_content(
        IntPtr pdfBytes,
        nuint pdfLen,
        nuint pageNumber,
        out IntPtr outJson);

    // ── Digital Signatures ─────────────────────────────────────────────────────

    /// <summary>Check if a PDF contains any digital signature fields</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_has_signatures(
        IntPtr pdfBytes,
        nuint pdfLen,
        [MarshalAs(UnmanagedType.I1)] out bool hasSignatures);

    /// <summary>Extract all digital signature fields from a PDF as JSON array</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_get_signatures(
        IntPtr pdfBytes,
        nuint pdfLen,
        out IntPtr outJson);

    /// <summary>Verify all digital signatures in a PDF and return verification results as JSON</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_verify_signatures(
        IntPtr pdfBytes,
        nuint pdfLen,
        out IntPtr outJson);

    // ── Forms ─────────────────────────────────────────────────────────────────

    // ── Annotations (write) ────────────────────────────────────────────────────

    /// <summary>Text annotation icon</summary>
    internal enum TextAnnotationIcon
    {
        Comment = 0, Key = 1, Note = 2, Help = 3, NewParagraph = 4, Paragraph = 5, Insert = 6,
    }

    /// <summary>Standard stamp names</summary>
    internal enum StampNameFFI
    {
        Approved = 0, Draft = 1, Confidential = 2, Final = 3, NotApproved = 4,
        Experimental = 5, AsIs = 6, Expired = 7, NotForPublicRelease = 8,
        Sold = 9, Departmental = 10, ForComment = 11, TopSecret = 12, ForPublicRelease = 13,
        Custom = 14,
    }

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_add_link_uri(
        IntPtr page, double x, double y, double width, double height,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string uri);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_add_link_goto(
        IntPtr page, double x, double y, double width, double height, uint targetPage);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_add_highlight(
        IntPtr page, double x, double y, double width, double height,
        double r, double g, double b);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_add_underline(
        IntPtr page, double x, double y, double width, double height);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_add_strikeout(
        IntPtr page, double x, double y, double width, double height);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_add_text_note(
        IntPtr page, double x, double y,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string contents,
        TextAnnotationIcon icon, [MarshalAs(UnmanagedType.I1)] bool open);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_add_stamp(
        IntPtr page, double x, double y, double width, double height,
        StampNameFFI stamp,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? customName);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_add_annotation_line(
        IntPtr page, double x1, double y1, double x2, double y2,
        double r, double g, double b);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_add_annotation_rect(
        IntPtr page, double x, double y, double width, double height,
        double strokeR, double strokeG, double strokeB,
        double fillR, double fillG, double fillB,
        [MarshalAs(UnmanagedType.I1)] bool hasFill);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_add_annotation_circle(
        IntPtr page, double x, double y, double width, double height,
        double strokeR, double strokeG, double strokeB,
        double fillR, double fillG, double fillB,
        [MarshalAs(UnmanagedType.I1)] bool hasFill);

    /// <summary>Check if a PDF contains any form fields</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_has_form_fields(
        IntPtr pdfBytes,
        nuint pdfLen,
        [MarshalAs(UnmanagedType.I1)] out bool outHasFields);

    /// <summary>Extract all form fields from a PDF as JSON array</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_get_form_fields(
        IntPtr pdfBytes,
        nuint pdfLen,
        out IntPtr outJson);

    /// <summary>Get page resources (fonts, images, resource keys) as JSON</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_get_page_resources(
        IntPtr pdfBytes,
        nuint pdfLen,
        nuint pageNumber,
        out IntPtr outJson);

    /// <summary>Get raw content streams for a page as base64-encoded JSON</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_get_page_content_stream(
        IntPtr pdfBytes,
        nuint pdfLen,
        nuint pageNumber,
        out IntPtr outJson);

    /// <summary>Extract all annotations from a PDF as JSON array</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_get_annotations(
        IntPtr pdfBytes,
        nuint pdfLen,
        out IntPtr outJson);

    /// <summary>Extract document metadata as JSON from PDF bytes</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_get_metadata(
        IntPtr pdfBytes,
        nuint pdfLen,
        out IntPtr outJson);

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

    /// <summary>Measure the width and height of a string using an embedded TTF/OTF font.</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_measure_text(
        IntPtr fontBytes,
        nuint fontLen,
        float fontSize,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text,
        out float outWidth,
        out float outHeight);

    // ── Layout / FlowLayout ────────────────────────────────────────────────

    /// <summary>Create a FlowLayout with A4 page size and 72pt margins</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_flow_layout_create_a4(out IntPtr outHandle);

    /// <summary>Create a FlowLayout with custom dimensions and margins</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_flow_layout_create(
        double width, double height,
        double marginLeft, double marginRight,
        double marginTop, double marginBottom,
        out IntPtr outHandle);

    /// <summary>Free a FlowLayout handle</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void oxidize_flow_layout_free(IntPtr handle);

    /// <summary>Add a text block with default line height (1.2)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_flow_layout_add_text(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text,
        StandardFont font,
        double fontSize);

    /// <summary>Add a text block with custom line height</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_flow_layout_add_text_with_line_height(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text,
        StandardFont font,
        double fontSize,
        double lineHeight);

    /// <summary>Add vertical spacing in points</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_flow_layout_add_spacer(IntPtr handle, double points);

    /// <summary>Add a simple table from JSON</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_flow_layout_add_table(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string tableJson);

    /// <summary>Add rich text from a JSON array of spans</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_flow_layout_add_rich_text(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string spansJson);

    /// <summary>Add an image, left-aligned</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_flow_layout_add_image(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        IntPtr imageHandle,
        double maxWidth,
        double maxHeight);

    /// <summary>Add an image, centered horizontally</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_flow_layout_add_image_centered(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        IntPtr imageHandle,
        double maxWidth,
        double maxHeight);

    /// <summary>Build the layout into a document, creating pages as needed</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_flow_layout_build_into(IntPtr handle, IntPtr doc);

    /// <summary>Get the content width (page width minus margins)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_flow_layout_content_width(IntPtr handle, out double outWidth);

    /// <summary>Get the usable height (page height minus margins)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_flow_layout_usable_height(IntPtr handle, out double outHeight);

    // ── Layout / DocumentBuilder ─────────────────────────────────────────

    /// <summary>Create a DocumentBuilder with A4 page size and 72pt margins</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_builder_create_a4(out IntPtr outHandle);

    /// <summary>Create a DocumentBuilder with custom dimensions and margins</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_builder_create(
        double width, double height,
        double marginLeft, double marginRight,
        double marginTop, double marginBottom,
        out IntPtr outHandle);

    /// <summary>Free a DocumentBuilder handle</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void oxidize_document_builder_free(IntPtr handle);

    /// <summary>Add a text block with default line height (1.2)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_builder_add_text(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text,
        StandardFont font,
        double fontSize);

    /// <summary>Add a text block with custom line height</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_builder_add_text_with_line_height(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text,
        StandardFont font,
        double fontSize,
        double lineHeight);

    /// <summary>Add vertical spacing in points</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_builder_add_spacer(IntPtr handle, double points);

    /// <summary>Add a simple table from JSON</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_builder_add_table(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string tableJson);

    /// <summary>Add rich text from a JSON array of spans</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_builder_add_rich_text(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string spansJson);

    /// <summary>Add an image, left-aligned</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_builder_add_image(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        IntPtr imageHandle,
        double maxWidth,
        double maxHeight);

    /// <summary>Add an image, centered horizontally</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_builder_add_image_centered(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        IntPtr imageHandle,
        double maxWidth,
        double maxHeight);

    /// <summary>Build the document, creating pages as needed (consumes the builder)</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_document_builder_build(IntPtr handle, out IntPtr outDoc);

    // ── Calibrated color spaces (CalGray / CalRGB / Lab) ─────────────────────────

    // ── CalGray (hardcoded name) ──────────────────────────────────────────────

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_fill_color_cal_gray(
        IntPtr page, double value,
        double wpX, double wpY, double wpZ,
        double bpX, double bpY, double bpZ,
        double gamma);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_stroke_color_cal_gray(
        IntPtr page, double value,
        double wpX, double wpY, double wpZ,
        double bpX, double bpY, double bpZ,
        double gamma);

    // ── CalGray (named) ───────────────────────────────────────────────────────

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_fill_color_cal_gray_named(
        IntPtr page,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        double value,
        double wpX, double wpY, double wpZ,
        double bpX, double bpY, double bpZ,
        double gamma);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_stroke_color_cal_gray_named(
        IntPtr page,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        double value,
        double wpX, double wpY, double wpZ,
        double bpX, double bpY, double bpZ,
        double gamma);

    // ── CalRGB (hardcoded name) ───────────────────────────────────────────────

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_fill_color_cal_rgb(
        IntPtr page, double r, double g, double b,
        double wpX, double wpY, double wpZ,
        double bpX, double bpY, double bpZ,
        double gammaR, double gammaG, double gammaB,
        double m0, double m1, double m2,
        double m3, double m4, double m5,
        double m6, double m7, double m8);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_stroke_color_cal_rgb(
        IntPtr page, double r, double g, double b,
        double wpX, double wpY, double wpZ,
        double bpX, double bpY, double bpZ,
        double gammaR, double gammaG, double gammaB,
        double m0, double m1, double m2,
        double m3, double m4, double m5,
        double m6, double m7, double m8);

    // ── CalRGB (named) ────────────────────────────────────────────────────────

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_fill_color_cal_rgb_named(
        IntPtr page,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        double r, double g, double b,
        double wpX, double wpY, double wpZ,
        double bpX, double bpY, double bpZ,
        double gammaR, double gammaG, double gammaB,
        double m0, double m1, double m2,
        double m3, double m4, double m5,
        double m6, double m7, double m8);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_stroke_color_cal_rgb_named(
        IntPtr page,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        double r, double g, double b,
        double wpX, double wpY, double wpZ,
        double bpX, double bpY, double bpZ,
        double gammaR, double gammaG, double gammaB,
        double m0, double m1, double m2,
        double m3, double m4, double m5,
        double m6, double m7, double m8);

    // ── Lab (hardcoded name) ──────────────────────────────────────────────────

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_fill_color_lab(
        IntPtr page, double l, double a, double b,
        double wpX, double wpY, double wpZ,
        double bpX, double bpY, double bpZ,
        double rangeAMin, double rangeAMax,
        double rangeBMin, double rangeBMax);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_stroke_color_lab(
        IntPtr page, double l, double a, double b,
        double wpX, double wpY, double wpZ,
        double bpX, double bpY, double bpZ,
        double rangeAMin, double rangeAMax,
        double rangeBMin, double rangeBMax);

    // ── Lab (named) ───────────────────────────────────────────────────────────

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_fill_color_lab_named(
        IntPtr page,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        double l, double a, double b,
        double wpX, double wpY, double wpZ,
        double bpX, double bpY, double bpZ,
        double rangeAMin, double rangeAMax,
        double rangeBMin, double rangeBMax);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_stroke_color_lab_named(
        IntPtr page,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        double l, double a, double b,
        double wpX, double wpY, double wpZ,
        double bpX, double bpY, double bpZ,
        double rangeAMin, double rangeAMax,
        double rangeBMin, double rangeBMax);

    // ── Page color-space registration ─────────────────────────────────────────

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_add_color_space_cal_gray(
        IntPtr page,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        double wpX, double wpY, double wpZ,
        double bpX, double bpY, double bpZ,
        double gamma);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_add_color_space_cal_rgb(
        IntPtr page,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        double wpX, double wpY, double wpZ,
        double bpX, double bpY, double bpZ,
        double gammaR, double gammaG, double gammaB,
        double m0, double m1, double m2,
        double m3, double m4, double m5,
        double m6, double m7, double m8);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_add_color_space_lab(
        IntPtr page,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        double wpX, double wpY, double wpZ,
        double bpX, double bpY, double bpZ,
        double rangeAMin, double rangeAMax,
        double rangeBMin, double rangeBMax);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_add_color_space_icc_based(
        IntPtr page,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        int n,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string alternate);

    // ── ICC draw ──────────────────────────────────────────────────────────────

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_fill_color_icc(
        IntPtr page,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        IntPtr components,
        nuint componentsLen);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_stroke_color_icc(
        IntPtr page,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        IntPtr components,
        nuint componentsLen);

    // ── ICC embedded profile registration ────────────────────────────────────

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_add_icc_color_space(
        IntPtr page,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        IntPtr data,
        nuint dataLen,
        int colorSpaceKind);

    // ── Tiling patterns (GFX-016) ─────────────────────────────────────────────

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_add_tiling_pattern(
        IntPtr page,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        int paintType,
        int tilingType,
        double bboxX,
        double bboxY,
        double bboxW,
        double bboxH,
        double xStep,
        double yStep,
        IntPtr content,
        nuint contentLen,
        IntPtr matrix);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_fill_pattern(
        IntPtr page,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_set_stroke_pattern(
        IntPtr page,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    // ── Form XObjects (GFX-018) ───────────────────────────────────────────────

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_add_form_xobject(
        IntPtr page,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        double x,
        double y,
        double width,
        double height,
        IntPtr content,
        nuint contentLen,
        IntPtr matrix,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? groupColorSpace,
        int isolated,
        int knockout);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_invoke_xobject(
        IntPtr page,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_apply_soft_mask(
        IntPtr page,
        int maskType,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? groupRef);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_draw_text_at(
        IntPtr page,
        StandardFont font,
        double size,
        double x,
        double y,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_page_clip_ellipse(
        IntPtr page,
        double cx,
        double cy,
        double rx,
        double ry);

    /// <summary>
    /// Gets the last error message from the native library and clears it
    /// </summary>
    /// <returns>The error message, or null if no error was set</returns>
    /// <summary>
    /// Reads the native library's last error message for the current thread.
    /// </summary>
    /// <remarks>
    /// The native error channel is THREAD-LOCAL (issue #55). This MUST be called
    /// synchronously, on the same thread, immediately after the failing native
    /// call — before any <c>await</c>. An <c>await</c> may resume the
    /// continuation on a different thread-pool thread, which would read that
    /// thread's (empty or unrelated) error slot. All wrappers satisfy this by
    /// invoking it from synchronous <c>ThrowIfError</c> helpers; async methods
    /// keep the whole call+check sequence inside a single <c>Task.Run</c> body.
    /// </remarks>
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

        // Runtime identifiers to probe, most specific first. Must account for
        // both architecture (x64/arm64) and, on Linux, the C library
        // (glibc vs musl) — otherwise ARM and Alpine consumers get a spurious
        // DllNotFoundException even when the matching native binary is shipped.
        var rids = GetCandidateRids();

        var baseDirectory = AppContext.BaseDirectory;
        foreach (var rid in rids)
        {
            var ridPath = Path.Combine(baseDirectory, "runtimes", rid, "native", fileName);
            if (File.Exists(ridPath) && NativeLibrary.TryLoad(ridPath, out var handle))
                return handle;
        }

        // Fall back to the application base directory (development scenario).
        var localPath = Path.Combine(baseDirectory, fileName);
        if (File.Exists(localPath) && NativeLibrary.TryLoad(localPath, out var localHandle))
            return localHandle;

        throw new DllNotFoundException(
            $"Unable to load native library '{fileName}' for runtime(s) '{string.Join(", ", rids)}'. " +
            $"Searched under '{Path.Combine(baseDirectory, "runtimes")}' and '{baseDirectory}'.");
    }

    /// <summary>
    /// Builds the ordered list of runtime identifiers to probe for the native
    /// binary, derived from the current OS, process architecture, and (on
    /// Linux) the C library implementation.
    /// </summary>
    private static string[] GetCandidateRids()
    {
        string arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            _ => throw new PlatformNotSupportedException(
                $"Unsupported process architecture: {RuntimeInformation.ProcessArchitecture}"),
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new[] { $"win-{arch}" };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return new[] { $"osx-{arch}" };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // The musl build of .NET (Alpine) reports a musl RID; use that as
            // the signal and try the matching libc first, the other as fallback.
            bool isMusl = RuntimeInformation.RuntimeIdentifier
                .Contains("musl", StringComparison.OrdinalIgnoreCase);
            return isMusl
                ? new[] { $"linux-musl-{arch}", $"linux-{arch}" }
                : new[] { $"linux-{arch}", $"linux-musl-{arch}" };
        }

        throw new PlatformNotSupportedException($"Unsupported platform: {RuntimeInformation.OSDescription}");
    }
}
