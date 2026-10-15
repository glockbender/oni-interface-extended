"""Headless protocol/evidence tests. No game process or installed mod is accessed."""

import contextlib
import importlib.util
import io
import json
from pathlib import Path
import struct
import tempfile
import unittest
from unittest import mock
import uuid
import zlib

SPEC = importlib.util.spec_from_file_location("runtime_runner", Path(__file__).with_name("runner.py"))
runner = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(runner)


def png_bytes(width=2, height=1):
    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress((b"\x00" + b"\x11\x22\x33" * width) * height))
            + chunk(b"IEND", b""))


class RunnerTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.bridge = self.root / "bridge"
        self.bridge.mkdir()
        self.save = self.root / runner.BASELINE_NAME
        self.save.write_bytes(b"dedicated synthetic fixture, not an ONI save")
        self.output = self.root / "run"
        self.run_id = str(uuid.uuid4())

    def args(self, *extra):
        return runner.parser().parse_args(["--save", str(self.save), "--bridge", str(self.bridge),
                                          "--output", str(self.output), "--width", "2", "--height", "1", *extra])

    def response(self, run_id=None, ok=True):
        return {"schemaVersion": 1, "id": run_id or self.run_id, "ok": ok}

    def request(self, run_id=None):
        return {"schemaVersion": 1, "id": run_id or self.run_id, "command": "run"}

    def fake_wait(self, bridge, run_id, timeout):
        image = self.output / "01-layout.png"
        image.write_bytes(png_bytes())
        runner.atomic_json(self.output / "results.json", {
            **self.response(run_id), "steps": [{"name": "layout", "ok": True, "screenshot": image.name,
                                                "measurements": {"width": 2}}]})
        runner.atomic_json(self.output / "manifest-game.json", {"gameBuild": "test-fixture"})
        return {**self.response(run_id), "resultsPath": str(self.output / "results.json")}, None

    def test_dry_run_never_reads_or_writes_bridge_and_preserves_baseline(self):
        original = self.save.read_bytes()
        (self.bridge / "request.json").write_text("broken in-flight request", encoding="utf-8")
        with mock.patch.object(runner, "send_request", side_effect=AssertionError("bridge accessed")):
            with contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(runner.run(self.args("--dry-run")), 0)
        self.assertEqual(self.save.read_bytes(), original)
        self.assertEqual((self.bridge / "request.json").read_text(), "broken in-flight request")
        request = runner.read_json(self.output / "prepared-request.json")
        working = Path(request["savePath"])
        self.assertNotEqual(working, self.save)
        self.assertEqual(working.read_bytes(), original)
        working.write_bytes(b"changed working copy")
        self.assertEqual(self.save.read_bytes(), original)
        self.assertEqual(request["expectedSaveSha256"], runner.sha256(self.save))
        self.assertEqual(runner.read_json(self.output / "manifest.json")["state"], "prepared")

    def test_cli_rejects_mod_difference_optin_in_default_profile_before_preparing(self):
        argv = ["--save", str(self.save), "--bridge", str(self.bridge), "--output", str(self.output),
                "--profile", "default", "--allow-mod-difference-dialog", "--dry-run"]
        with mock.patch.object(runner, "prepare", side_effect=AssertionError("prepared before validation")):
            with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit) as caught:
                runner.main(argv)
        self.assertEqual(caught.exception.code, 2)
        self.assertFalse(self.output.exists())
        self.assertFalse((self.bridge / "request.json").exists())

    def test_cli_allows_explicit_local_mod_difference_optin(self):
        argv = ["--save", str(self.save), "--bridge", str(self.bridge), "--output", str(self.output),
                "--profile", "local", "--allow-mod-difference-dialog", "--dry-run"]
        with contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(runner.main(argv), 0)
        request = runner.read_json(self.output / "prepared-request.json")
        self.assertEqual(request["profile"], "local")
        self.assertTrue(request["allowModDifferenceDialog"])
        self.assertFalse((self.bridge / "request.json").exists())

    def test_rejects_personal_or_relative_save(self):
        for name in (str(self.root / "MyColony.sav"), runner.BASELINE_NAME):
            args = self.args()
            args.save = name
            with self.subTest(name=name), self.assertRaises(runner.RunError):
                runner.prepare(args)
        self.assertFalse(self.output.exists())

    def test_existing_output_is_not_overwritten(self):
        self.output.mkdir()
        with self.assertRaises(FileExistsError):
            runner.prepare(self.args())

    def test_inflight_request_and_broken_request_are_not_overwritten(self):
        for contents in (json.dumps(self.request()), "{invalid-json"):
            request_path = self.bridge / "request.json"
            request_path.write_text(contents, encoding="utf-8")
            with self.subTest(contents=contents), self.assertRaises(runner.RunError):
                runner.send_request(self.bridge, self.request(str(uuid.uuid4())))
            self.assertEqual(request_path.read_text(), contents)
            self.assertFalse((self.bridge / "runner.lock").exists())

    def test_mismatched_previous_response_does_not_complete_request(self):
        runner.atomic_json(self.bridge / "request.json", self.request())
        runner.atomic_json(self.bridge / "response.json", self.response(str(uuid.uuid4())))
        with self.assertRaises(runner.RunError):
            runner.send_request(self.bridge, self.request(str(uuid.uuid4())))
        self.assertEqual(runner.read_json(self.bridge / "request.json")["id"], self.run_id)

    def test_malformed_existing_request_with_response_is_refused(self):
        runner.atomic_json(self.bridge / "request.json", {"id": self.run_id})
        runner.atomic_json(self.bridge / "response.json", self.response())
        with self.assertRaises(runner.RunError):
            runner.send_request(self.bridge, self.request(str(uuid.uuid4())))

    def test_completed_request_is_archived_before_next_request(self):
        runner.atomic_json(self.bridge / "request.json", self.request())
        runner.atomic_json(self.bridge / "response.json", self.response())
        new_request = self.request(str(uuid.uuid4()))
        runner.send_request(self.bridge, new_request)
        self.assertEqual(runner.read_json(self.bridge / "request.json"), new_request)
        self.assertEqual(runner.read_json(self.bridge / "history" / self.run_id / "request.json"), self.request())

    def test_status_without_terminal_response_prevents_new_request(self):
        runner.atomic_json(self.bridge / "status.json", {"id": self.run_id, "step": "loading"})
        with self.assertRaises(runner.RunError):
            runner.send_request(self.bridge, self.request())
        self.assertFalse((self.bridge / "request.json").exists())

    def test_wait_correlates_response_and_status_ids(self):
        old_id = str(uuid.uuid4())
        runner.atomic_json(self.bridge / "response.json", self.response(old_id))
        runner.atomic_json(self.bridge / "status.json", {"id": old_id, "step": "stale"})
        tick = [0.0]
        def advance(duration):
            tick[0] += duration
            runner.atomic_json(self.bridge / "response.json", self.response())
            runner.atomic_json(self.bridge / "status.json", {"id": self.run_id, "step": "done"})
        response, status = runner.wait_response(self.bridge, self.run_id, 1, clock=lambda: tick[0], sleeper=advance)
        self.assertEqual(response["id"], self.run_id)
        self.assertEqual(status["step"], "done")

    def test_timeout_is_bounded_and_retains_last_status(self):
        runner.atomic_json(self.bridge / "status.json", {"id": self.run_id, "step": "loading"})
        tick = [0.0]
        def advance(duration):
            tick[0] += duration
        with self.assertRaises(TimeoutError) as caught:
            runner.wait_response(self.bridge, self.run_id, 0.25, clock=lambda: tick[0], sleeper=advance)
        self.assertAlmostEqual(tick[0], 0.25)
        self.assertEqual(caught.exception.status["step"], "loading")

    def test_matching_malformed_response_fails(self):
        runner.atomic_json(self.bridge / "response.json", {"schemaVersion": 1, "id": self.run_id, "ok": "yes"})
        with self.assertRaises(runner.RunError):
            runner.wait_response(self.bridge, self.run_id, 1)

    def test_json_syntax_error_fails_without_waiting(self):
        (self.bridge / "response.json").write_text("[", encoding="utf-8")
        with self.assertRaises(runner.RunError):
            runner.wait_response(self.bridge, self.run_id, 1)

    def test_png_hash_dimensions_and_truncation(self):
        image = self.root / "capture.png"
        image.write_bytes(png_bytes())
        actual = runner.inspect_png(image)
        self.assertEqual((actual["width"], actual["height"]), (2, 1))
        self.assertEqual(actual["sha256"], runner.sha256(image))
        for invalid in (png_bytes()[:-1], png_bytes()[:-12], png_bytes() + b"trailing", b"not-png"):
            image.write_bytes(invalid)
            with self.subTest(invalid=invalid[-10:]), self.assertRaises(runner.RunError):
                runner.inspect_png(image)

    def test_png_crc_and_invalid_image_data_fail(self):
        image = self.root / "capture.png"
        data = bytearray(png_bytes())
        data[20] ^= 1
        image.write_bytes(data)
        with self.assertRaises(runner.RunError):
            runner.inspect_png(image)

    def test_path_traversal_external_paths_and_missing_png_fail_evidence(self):
        self.output.mkdir()
        for name in ("../capture.png", "sub/capture.png", "C:\\secret.png", "https://example.com/a.png", "missing.png"):
            with self.subTest(name=name):
                evidence = runner.collect_evidence(self.output, {"steps": [{"name": "step", "ok": True, "screenshot": name}]}, 2, 1)
                self.assertFalse(evidence[0]["ok"])
                self.assertNotIn("image", evidence[0])

    def test_wrong_dimensions_and_missing_evidence_cannot_pass(self):
        self.output.mkdir()
        (self.output / "capture.png").write_bytes(png_bytes())
        for step in ({"name": "step", "ok": True, "screenshot": "capture.png"}, {"name": "step", "ok": True}):
            evidence = runner.collect_evidence(self.output, {"steps": [step]}, 5120, 1440)
            self.assertFalse(evidence[0]["ok"])

    def test_dialog_capture_has_hash_dimensions_and_gallery_entry(self):
        self.output.mkdir()
        (self.output / "layout.png").write_bytes(png_bytes())
        (self.output / "mod-difference.png").write_bytes(png_bytes())
        evidence = runner.collect_evidence(self.output, {"steps": [{
            "name": "Load <fixture>", "ok": True, "screenshot": "layout.png",
            "dialogScreenshot": "mod-difference.png"}]}, 2, 1)
        self.assertTrue(evidence[0]["ok"])
        self.assertEqual(evidence[0]["dialogImage"]["sha256"], runner.sha256(self.output / "mod-difference.png"))
        self.assertEqual(evidence[0]["dialogImage"]["width"], 2)
        self.assertEqual(evidence[0]["dialogImage"]["height"], 1)
        runner.write_gallery(self.output, {"state": "passed"}, evidence)
        rendered = (self.output / "index.html").read_text(encoding="utf-8")
        self.assertEqual(rendered.count("<img"), 2)
        self.assertIn("src='mod-difference.png'", rendered)
        self.assertNotIn("Load <fixture>", rendered)
        self.assertIn("Load &lt;fixture&gt;", rendered)

    def test_duplicate_dialog_capture_is_validated_and_shown_once(self):
        self.output.mkdir()
        (self.output / "capture.png").write_bytes(png_bytes())
        with mock.patch.object(runner, "inspect_png", wraps=runner.inspect_png) as inspect:
            evidence = runner.collect_evidence(self.output, {"steps": [{
                "name": "load", "ok": True, "screenshot": "capture.png", "dialogScreenshot": "capture.png"}]}, 2, 1)
        self.assertEqual(inspect.call_count, 1)
        self.assertTrue(evidence[0]["ok"])
        self.assertNotIn("dialogImage", evidence[0])
        runner.write_gallery(self.output, {"state": "passed"}, evidence)
        self.assertEqual((self.output / "index.html").read_text(encoding="utf-8").count("<img"), 1)

    def test_invalid_dialog_capture_fails_even_with_valid_primary_capture(self):
        self.output.mkdir()
        (self.output / "capture.png").write_bytes(png_bytes())
        (self.output / "truncated.png").write_bytes(png_bytes()[:-8])
        (self.output / "wrong-size.png").write_bytes(png_bytes(width=3))
        for name in ("../outside.png", "missing.png", "truncated.png", "wrong-size.png", "<script>.png"):
            with self.subTest(name=name):
                evidence = runner.collect_evidence(self.output, {"steps": [{
                    "name": "load", "ok": True, "screenshot": "capture.png", "dialogScreenshot": name}]}, 2, 1)
                self.assertFalse(evidence[0]["ok"])
                self.assertIn("image", evidence[0])
                self.assertNotIn("dialogImage", evidence[0])
                self.assertIn("dialogScreenshot:", evidence[0]["evidenceError"])
                runner.write_gallery(self.output, {"state": "failed"}, evidence)
                rendered = (self.output / "index.html").read_text(encoding="utf-8")
                self.assertEqual(rendered.count("<img"), 1)
                self.assertNotIn("<script>", rendered)

    def test_gallery_escapes_untrusted_fields_and_does_not_embed_invalid_paths(self):
        self.output.mkdir()
        attack = "<script>alert('x')</script>"
        runner.write_gallery(self.output, {"state": attack}, [
            {"name": attack, "ok": False, "screenshot": "../secret.png", "image": {}, "error": attack}],
            {"message": attack})
        rendered = (self.output / "index.html").read_text(encoding="utf-8")
        self.assertNotIn("<script>", rendered)
        self.assertIn("&lt;script&gt;", rendered)
        self.assertNotIn("<img", rendered)
        self.assertNotIn("href='../", rendered)

    def test_mock_agent_result_produces_gallery_and_merged_manifest(self):
        with mock.patch.object(runner, "wait_response", side_effect=self.fake_wait), contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(runner.run(self.args()), 0)
        manifest = runner.read_json(self.output / "manifest.json")
        self.assertEqual(manifest["state"], "passed")
        self.assertEqual(manifest["game"]["gameBuild"], "test-fixture")
        self.assertEqual(self.save.read_bytes(), b"dedicated synthetic fixture, not an ONI save")
        self.assertIn("<img", (self.output / "index.html").read_text(encoding="utf-8"))

    def test_timeout_report_retains_request_and_status(self):
        error = TimeoutError("test deadline")
        error.status = {"step": "loading", "message": "fixture status"}
        with mock.patch.object(runner, "wait_response", side_effect=error), contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(runner.run(self.args()), 3)
        self.assertTrue((self.bridge / "request.json").exists())
        self.assertEqual(runner.read_json(self.output / "manifest.json")["state"], "timed-out")
        self.assertIn("fixture status", (self.output / "index.html").read_text(encoding="utf-8"))

    def test_success_without_results_is_protocol_error(self):
        with mock.patch.object(runner, "wait_response", side_effect=lambda bridge, run_id, timeout: (self.response(run_id), None)):
            with contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(runner.run(self.args()), 2)
        self.assertEqual(runner.read_json(self.output / "manifest.json")["state"], "error")

    def test_results_path_cannot_escape_output(self):
        with mock.patch.object(runner, "wait_response", side_effect=lambda bridge, run_id, timeout: (
            {**self.response(run_id), "resultsPath": str(self.root / "outside.json")}, None)):
            with contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(runner.run(self.args()), 2)

    def test_negative_control_cannot_silently_pass(self):
        with mock.patch.object(runner, "wait_response", side_effect=self.fake_wait), contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(runner.run(self.args("--negative-control", "wrong-width")), 2)
        self.assertIn("Negative control unexpectedly passed", runner.read_json(self.output / "manifest.json")["error"])

    def test_agent_failure_without_results_is_not_a_pass(self):
        with mock.patch.object(runner, "wait_response", side_effect=lambda bridge, run_id, timeout: (
            {**self.response(run_id, ok=False), "error": "intentional failure"}, None)):
            with contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(runner.run(self.args()), 1)
        self.assertEqual(runner.read_json(self.output / "manifest.json")["state"], "failed")

    def test_corrupted_screenshot_changes_success_to_failed_evidence(self):
        def corrupted_wait(bridge, run_id, timeout):
            response, status = self.fake_wait(bridge, run_id, timeout)
            (self.output / "01-layout.png").write_bytes(png_bytes()[:-8])
            return response, status
        with mock.patch.object(runner, "wait_response", side_effect=corrupted_wait), contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(runner.run(self.args()), 1)
        evidence = runner.read_json(self.output / "evidence.json")["steps"][0]
        self.assertFalse(evidence["ok"])
        self.assertIn("Truncated", evidence["evidenceError"])


if __name__ == "__main__":
    unittest.main()
