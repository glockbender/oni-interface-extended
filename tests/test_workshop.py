"""Check README export and synchronization without Steam or the game."""

import contextlib
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import struct
import tempfile
import unittest
from unittest import mock
import zlib

ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("workshop", ROOT / "scripts" / "workshop.py")
workshop = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(workshop)


def fixture_png(width=512, height=512):
    """Generate complete synthetic PNG bytes for file-contract tests, never game evidence."""
    def chunk(kind, payload):
        return (struct.pack(">I", len(payload)) + kind + payload
                + struct.pack(">I", zlib.crc32(kind + payload) & 0xFFFFFFFF))
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress((b"\x00" + b"\x12\x34\x56" * width) * height))
            + chunk(b"IEND", b""))


class WorkshopTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        (self.root / "capture.jpg").write_bytes(b"synthetic image bytes for exporter tests only")
        (self.root / "icon.png").write_bytes(fixture_png())
        (self.root / "details.md").write_text("Details", encoding="utf-8")
        self.cover = '<img src="icon.png" width="96" height="96" alt="Test cover">'
        self.readme = "# Fixture\n\n" + self.cover + "\n\n" + "\n\n".join(
            f"<!-- workshop:{name}:start -->\n"
            + ("![Real capture](capture.jpg)" if name == "screenshots" else "## " + name + "\n\nProduct text.")
            + f"\n<!-- workshop:{name}:end -->" for name in workshop.SECTIONS)
        (self.root / "README.md").write_text(self.readme, encoding="utf-8")

    def test_supported_markdown_maps_to_bbcode_and_absolute_public_links(self):
        images = []
        converted = workshop.convert(
            "## Controls\n\nUse **Options** and `2560`.\n\n- [Details](details.md#usage)\n"
            "- [Issues](https://example.com/issues)\n\n![Capture](capture.jpg)", self.root, images)
        self.assertIn("[h2]Controls[/h2]", converted)
        self.assertIn("Use [b]Options[/b] and 2560.", converted)
        self.assertIn("[list]\n[*][url=" + workshop.REPOSITORY + "/blob/main/details.md#usage]Details[/url]", converted)
        self.assertIn("[/list]", converted)
        self.assertIn("[url=" + workshop.RAW + "/capture.jpg]Capture[/url]", converted)
        self.assertEqual(images[0]["source"], "capture.jpg")
        self.assertNotIn(str(self.root), converted)

    def test_marker_order_pairs_duplicates_and_unknown_sections_are_rejected(self):
        for source in (self.readme.replace("overview:end", "usage:end"),
                       self.readme.replace("overview:start", "unknown:start"),
                       self.readme + "\n" + self.readme,
                       self.readme.replace("<!-- workshop:overview:start -->", " <!-- workshop:overview:start -->"),
                       self.readme.replace("<!-- workshop:overview:end -->", "")):
            with self.subTest(source=source[:80]), self.assertRaises(workshop.ExportError):
                workshop.sections(source)

    def test_unsupported_syntax_is_not_silently_discarded(self):
        for text in ("| Column | Value |", "```python", "> quote", "1. item", "    nested", "#### Heading", "+ item", "---",
                     "*italic*", "_italic_", "<script>bad</script>", "[b]raw[/b]", "`unclosed"):
            with self.subTest(text=text), self.assertRaises(workshop.ExportError):
                workshop.convert(text, self.root, [])

    def test_invalid_or_private_links_are_rejected(self):
        for target in ("missing.md", "../outside.md", "/local/file.md", "file:///secret", "C:\\secret.txt",
                       "http://example.com", "https://user:password@example.com", "//example.com"):
            with self.subTest(target=target), self.assertRaises(workshop.ExportError):
                workshop.reference(target, self.root)

    def test_images_require_local_supported_files_and_no_duplicates(self):
        with self.assertRaises(workshop.ExportError):
            workshop.convert("![Capture](https://example.com/a.jpg)", self.root, [])
        with self.assertRaises(workshop.ExportError):
            workshop.convert("![Document](details.md)", self.root, [])
        with self.assertRaises(workshop.ExportError):
            workshop.convert("![One](capture.jpg)\n![Two](capture.jpg)", self.root, [])

    def test_cover_is_a_separate_preview_and_never_a_gallery_image(self):
        generated = workshop.export(self.root)
        manifest = json.loads(generated["screenshots.json"])
        self.assertEqual(manifest["preview"], {
            "source": "icon.png", "url": workshop.RAW + "/icon.png",
            "sha256": hashlib.sha256((self.root / "icon.png").read_bytes()).hexdigest(),
            "width": 512, "height": 512})
        self.assertEqual([image["source"] for image in manifest["images"]], ["capture.jpg"])
        self.assertNotIn("icon.png", generated["description.bbcode"])

    def test_missing_or_multiple_html_covers_are_rejected(self):
        for source in (self.readme.replace(self.cover, ""), self.readme + "\n" + self.cover):
            (self.root / "README.md").write_text(source, encoding="utf-8")
            with self.subTest(source=source[:80]), self.assertRaisesRegex(workshop.ExportError, "one cover"):
                workshop.export(self.root)

    def test_cover_requires_existing_512_square_png(self):
        for data in (fixture_png(511, 512), fixture_png(512, 256), b"not a PNG"):
            (self.root / "icon.png").write_bytes(data)
            with self.subTest(data_length=len(data)), self.assertRaisesRegex(workshop.ExportError, "512 x 512 PNG"):
                workshop.export(self.root)
        (self.root / "icon.png").unlink()
        with self.assertRaisesRegex(workshop.ExportError, "repository file"):
            workshop.export(self.root)

    def test_changed_cover_invalidates_generated_manifest_without_rewriting(self):
        with mock.patch.object(workshop, "ROOT", self.root), contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(workshop.main([]), 0)
        manifest_path = self.root / "artifacts" / "workshop" / "screenshots.json"
        previous = manifest_path.read_bytes()
        # Ancillary metadata changes file identity while preserving a complete valid PNG.
        data = (self.root / "icon.png").read_bytes()
        payload = b"Fixture\x00Changed cover"
        chunk = struct.pack(">I", len(payload)) + b"tEXt" + payload
        chunk += struct.pack(">I", zlib.crc32(b"tEXt" + payload) & 0xFFFFFFFF)
        (self.root / "icon.png").write_bytes(data[:-12] + chunk + data[-12:])
        with mock.patch.object(workshop, "ROOT", self.root), contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(workshop.main(["--check"]), 1)
        self.assertEqual(manifest_path.read_bytes(), previous)

    def test_check_without_output_is_read_only(self):
        with mock.patch.object(workshop, "ROOT", self.root), contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(workshop.main(["--check"]), 0)
        self.assertFalse((self.root / "artifacts").exists())

    def test_check_rejects_stale_output_without_rewriting(self):
        with mock.patch.object(workshop, "ROOT", self.root), contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(workshop.main([]), 0)
        output = self.root / "artifacts" / "workshop"
        previous = {path.name: path.read_bytes() for path in output.iterdir()}
        (self.root / "README.md").write_text(self.readme.replace("Product text.", "Changed text.", 1), encoding="utf-8")
        with mock.patch.object(workshop, "ROOT", self.root), contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(workshop.main(["--check"]), 1)
        self.assertEqual(previous, {path.name: path.read_bytes() for path in output.iterdir()})

    def test_check_rejects_missing_half_of_existing_export(self):
        with mock.patch.object(workshop, "ROOT", self.root), contextlib.redirect_stdout(io.StringIO()):
            workshop.main([])
        (self.root / "artifacts" / "workshop" / "screenshots.json").unlink()
        with mock.patch.object(workshop, "ROOT", self.root), contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(workshop.main(["--check"]), 1)

    def test_actual_readme_export_is_deterministic_and_excludes_development(self):
        first, second = workshop.export(ROOT), workshop.export(ROOT)
        self.assertEqual(first, second)
        description = first["description.bbcode"]
        manifest = json.loads(first["screenshots.json"])
        self.assertEqual(manifest["preview"]["source"], "docs/images/icon.png")
        self.assertEqual((manifest["preview"]["width"], manifest["preview"]["height"]), (512, 512))
        cover_hash = hashlib.sha256((ROOT / "docs/images/icon.png").read_bytes()).hexdigest()
        self.assertEqual(manifest["preview"]["sha256"], cover_hash)
        self.assertNotIn(manifest["preview"]["source"], [item["source"] for item in manifest["images"]])
        self.assertNotIn("icon.png", description)
        self.assertNotIn("make build", description)
        self.assertNotIn("Roslyn", description)
        self.assertNotIn("20-step", description)
        self.assertNotIn("test agent", description.lower())
        self.assertNotIn("SHA-256", description)
        self.assertNotIn("config-recovery", description)
        self.assertNotIn("1.3.0", description)
        self.assertNotIn("Capture metadata", description)
        self.assertNotIn("settings-ru", description)
        self.assertEqual(description.count("Russian"), 1)
        self.assertNotIn("artifacts/", description)
        self.assertNotIn("file://", description)
        self.assertNotIn("C:\\", description)
        self.assertNotIn("](docs/", description)
        self.assertIn("[url=" + workshop.REPOSITORY + "/issues]", description)
        self.assertEqual([item["order"] for item in manifest["images"]], [1, 2, 3])
        self.assertEqual([item["source"] for item in manifest["images"]], [
            "docs/images/hud-full-width.jpg", "docs/images/hud-2560.jpg",
            "docs/images/settings-en.jpg"])
        for item in manifest["images"]:
            self.assertTrue((ROOT / item["source"]).is_file())
            self.assertTrue(item["url"].startswith(workshop.RAW + "/"))
            self.assertEqual(len(item["sha256"]), 64)


if __name__ == "__main__":
    unittest.main()
