"""Independent Poppler pixel oracle for #99. Pass a trusted native library path.

Requires pdftoppm. Fixed fixtures only; all files live in a private temporary
directory. Exit nonzero if either winding rule or restored clip is incorrect.
"""
import ctypes as c
from pathlib import Path
import subprocess
import sys
import tempfile


class Command(c.Structure):
    _fields_ = [(name, c.c_double) for name in ("x1", "y1", "x2", "y2", "x3", "y3")] + [("kind", c.c_int32)]


def main():
    library = c.CDLL(str(Path(sys.argv[1]).resolve(strict=True)))

    def bind(name, arguments, result=c.c_int):
        fn = getattr(library, "oxidize_" + name)
        fn.argtypes, fn.restype = arguments, result
        return fn

    ptr, number = c.c_void_p, c.c_double
    create = bind("document_create", [], ptr)
    page_create = bind("page_create", [number, number], ptr)
    free = bind("document_free", [ptr], None)
    page_free = bind("page_free", [ptr], None)
    add = bind("document_add_page", [ptr, ptr])
    save = bind("document_save_to_file", [ptr, c.c_char_p])
    clip = bind("page_clip_path", [ptr, c.POINTER(Command), c.c_size_t, c.c_int])
    push = bind("page_save_state", [ptr])
    pop = bind("page_restore_state", [ptr])
    rect = bind("page_rect", [ptr, number, number, number, number])
    fill = bind("page_fill", [ptr])

    def check(code):
        if code != 0:
            raise RuntimeError(f"Native operation failed: {code}")

    commands = []
    for low, high in [(10, 90), (30, 70)]:
        for kind, x, y in [(0, low, low), (1, high, low), (1, high, high), (1, low, high), (3, 0, 0)]:
            commands.append(Command(x, y, 0, 0, 0, 0, kind))
    array = (Command * len(commands))(*commands)
    with tempfile.TemporaryDirectory(prefix="clipping-render-") as directory:
        for rule in (0, 1):
            document, page = create(), page_create(100, 100)
            if not document or not page:
                if page:
                    page_free(page)
                if document:
                    free(document)
                raise RuntimeError("Native allocation failed")
            try:
                check(push(page))
                check(clip(page, array, len(array), rule))
                check(rect(page, 0, 0, 100, 100))
                check(fill(page))
                check(pop(page))
                check(rect(page, 0, 0, 5, 5))
                check(fill(page))
                check(add(document, page))
                pdf = Path(directory) / f"rule-{rule}.pdf"
                check(save(document, str(pdf).encode()))
            finally:
                page_free(page)
                free(document)
            output = Path(directory) / f"rule-{rule}"
            subprocess.run(["pdftoppm", "-r", "72", "-gray", "-singlefile", str(pdf), str(output)],
                           check=True, capture_output=True, timeout=30)
            with output.with_suffix(".pgm").open("rb") as stream:
                assert stream.readline().strip() == b"P5"
                assert stream.readline().strip() == b"100 100"
                assert stream.readline().strip() == b"255"
                pixels = stream.read()
            assert len(pixels) == 10_000
            center, ring, outside, restored = [pixels[y * 100 + x] for x, y in [(50, 50), (20, 50), (5, 50), (2, 97)]]
            assert center == (0 if rule == 0 else 255), (rule, "hole", center)
            assert (ring, outside, restored) == (0, 255, 0), (rule, ring, outside, restored)
            print(f"rule={rule}: center={center}, ring={ring}, outside={outside}, restored={restored}: PASS")


if __name__ == "__main__":
    main()
