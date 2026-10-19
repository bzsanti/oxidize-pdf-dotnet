"""Independent acceptance probe for #89 / upstream #708 (requires Poppler).

Run with the path of a freshly built native library. Exit 1 means the requested
20-point region does not match the actual selectable word bounds. This is an
explicit upstream acceptance probe, not a passing test of the current release.
"""

import argparse
import base64
import ctypes
import json
from pathlib import Path
import subprocess
import tempfile
import xml.etree.ElementTree as ET


def source_pdf():
    objects = [
        b"<< /Type /Catalog /Pages 2 0 R >>",
        b"<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
        b"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 504 360] "
        b"/Resources << >> /Contents 4 0 R >>",
        b"<< /Length 0 >>\nstream\nendstream",
    ]
    pdf = bytearray(b"%PDF-1.4\n")
    offsets = [0]
    for number, obj in enumerate(objects, 1):
        offsets.append(len(pdf))
        pdf.extend(f"{number} 0 obj\n".encode() + obj + b"\nendobj\n")
    xref = len(pdf)
    pdf.extend(b"xref\n0 5\n0000000000 65535 f \n")
    for offset in offsets[1:]:
        pdf.extend(f"{offset:010} 00000 n \n".encode())
    pdf.extend(f"trailer\n<< /Size 5 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n".encode())
    return bytes(pdf)


def apply_layer(library, pdf):
    library.oxidize_ocr_layer.argtypes = [
        ctypes.c_void_p, ctypes.c_size_t, ctypes.c_char_p,
        ctypes.POINTER(ctypes.c_void_p),
    ]
    library.oxidize_ocr_layer.restype = ctypes.c_int
    library.oxidize_free_string.argtypes = [ctypes.c_void_p]
    library.oxidize_free_string.restype = None
    request = json.dumps({"plan_only": False, "pages": [{
        "page_index": 0, "language": "spa", "fragments": [{
            "text": "El", "region": [32, 300, 20, 18],
            "confidence": 1, "reading_order": 0,
        }],
    }]}).encode()
    buffer = ctypes.create_string_buffer(pdf)
    output = ctypes.c_void_p()
    try:
        status = library.oxidize_ocr_layer(buffer, len(pdf), request, ctypes.byref(output))
        if status != 0 or not output.value:
            raise RuntimeError(f"OCR FFI failed with status {status}")
        result = json.loads(ctypes.string_at(output))
        return base64.b64decode(result["pdf_bytes"], validate=True)
    finally:
        if output.value:
            library.oxidize_free_string(output)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("native_library", type=Path)
    args = parser.parse_args()
    library = ctypes.CDLL(str(args.native_library.resolve(strict=True)))
    source = source_pdf()
    result = apply_layer(library, source)
    if not result.startswith(source):
        raise AssertionError("Incremental output lost the original prefix")
    with tempfile.TemporaryDirectory(prefix="oxidize-ocr-geometry-") as directory:
        output = Path(directory) / "result.pdf"
        bounds = Path(directory) / "bounds.html"
        output.write_bytes(result)
        subprocess.run(
            ["pdftotext", "-bbox", "-cropbox", str(output), str(bounds)],
            check=True, timeout=30, capture_output=True,
        )
        words = ET.parse(bounds).getroot().findall(".//{*}word")
        if len(words) != 1 or words[0].text != "El":
            raise AssertionError("Independent consumer did not extract the expected word El")
        word = words[0]
        width = float(word.attrib["xMax"]) - float(word.attrib["xMin"])
        print(json.dumps({"text": word.text, "requested_width": 20,
                          "actual_width": width, "bounds": word.attrib,
                          "prefix_preserved": True}, indent=2))
        if abs(width - 20) >= 3:
            raise AssertionError(f"Requested width 20 pt; independent width {width} pt (#708)")


if __name__ == "__main__":
    main()
