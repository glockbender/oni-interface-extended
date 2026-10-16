"""Check README export and synchronization without Steam or the game."""

import contextlib
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest import mock

ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("workshop", ROOT / "scripts" / "workshop.py")
workshop = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(workshop)


class WorkshopTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        (self.root / "capture.jpg").write_bytes(b"synthetic image bytes for exporter tests only")
        (self.root / "details.md").write_text("Details", encoding="utf-8")
        self.readme = "\n\n".join(
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
        self.assertNotIn("make build", description)
        self.assertNotIn("Roslyn", description)
        self.assertNotIn("artifacts/", description)
        self.assertNotIn("file://", description)
        self.assertNotIn("C:\\", description)
        self.assertNotIn("](docs/", description)
        self.assertIn("[url=" + workshop.REPOSITORY + "/issues]", description)
        self.assertEqual([item["order"] for item in manifest["images"]], [1, 2, 3, 4])
        self.assertEqual([item["source"] for item in manifest["images"]], [
            "docs/images/hud-full-width.jpg", "docs/images/hud-2560.jpg",
            "docs/images/settings-en.jpg", "docs/images/settings-ru.jpg"])
        for item in manifest["images"]:
            self.assertTrue((ROOT / item["source"]).is_file())
            self.assertTrue(item["url"].startswith(workshop.RAW + "/"))
            self.assertEqual(len(item["sha256"]), 64)


if __name__ == "__main__":
    unittest.main()
