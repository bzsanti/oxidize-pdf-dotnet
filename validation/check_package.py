"""Validate the release package layout without extracting executable payloads."""
import sys
import zipfile
import xml.etree.ElementTree as ET

with zipfile.ZipFile(sys.argv[1]) as package:
    names = package.namelist()
    assert len(names) == len(set(names)), "Duplicate package entries"
    expected = {
        f"runtimes/{rid}/native/{filename}"
        for rid, filename in (
            ("linux-x64", "liboxidize_pdf_ffi.so"),
            ("linux-arm64", "liboxidize_pdf_ffi.so"),
            ("linux-musl-x64", "liboxidize_pdf_ffi.so"),
            ("linux-musl-arm64", "liboxidize_pdf_ffi.so"),
            ("win-x64", "oxidize_pdf_ffi.dll"),
            ("win-arm64", "oxidize_pdf_ffi.dll"),
            ("osx-x64", "liboxidize_pdf_ffi.dylib"),
            ("osx-arm64", "liboxidize_pdf_ffi.dylib"),
        )
    }
    actual = {name for name in names if name.startswith("runtimes/") and not name.endswith("/")}
    assert actual == expected, f"Native assets differ: {actual ^ expected}"
    for tfm in ("net8.0", "net9.0", "net10.0"):
        for extension in ("dll", "xml"):
            expected.add(f"lib/{tfm}/OxidizePdf.NET.{extension}")
    expected.update(("README.md", "icon.png"))
    for name in expected:
        assert package.getinfo(name).file_size > 0, f"Empty asset: {name}"
    nuspec = ET.fromstring(package.read("OxidizePdf.NET.nuspec"))
    ns = {"n": "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"}
    assert nuspec.findtext("n:metadata/n:version", namespaces=ns) == "0.20.0"
print("Package layout passed: eight native RIDs, three managed TFMs, documentation and metadata")
