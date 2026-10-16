"""Drive an already-running development test agent; never launch or install ONI."""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import html
import json
import os
from pathlib import Path
import platform
import re
import shutil
import struct
import sys
import time
import uuid
import zlib

BASELINE_NAME = "InterfaceExtended-TestBaseline.sav"
MAX_JSON_BYTES = 4 * 1024 * 1024
PNG_NAME = re.compile(r"[A-Za-z0-9][A-Za-z0-9_.-]*\.png\Z", re.ASCII)
CAPTURE_FIELDS = (("screenshot", "image"), ("dialogScreenshot", "dialogImage"))


class RunError(Exception):
    """Invalid setup, protocol or evidence."""


def utc_now():
    return dt.datetime.now(dt.timezone.utc).isoformat()


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def atomic_json(path, value):
    temporary = path.with_name(path.name + "." + uuid.uuid4().hex + ".tmp")
    try:
        with temporary.open("x", encoding="utf-8", newline="\n") as stream:
            json.dump(value, stream, ensure_ascii=False, indent=2)
            stream.write("\n")
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


def read_json(path):
    if path.stat().st_size > MAX_JSON_BYTES:
        raise RunError(f"JSON file is too large: {path.name}")
    try:
        value = json.loads(path.read_text(encoding="utf-8-sig"))
    except (UnicodeError, json.JSONDecodeError) as error:
        raise RunError(f"Invalid JSON in {path.name}: {error}") from error
    if not isinstance(value, dict):
        raise RunError(f"Expected a JSON object in {path.name}")
    return value


def check_envelope(value, run_id):
    try:
        if str(uuid.UUID(run_id)) != run_id:
            raise ValueError("non-canonical ID")
    except (ValueError, TypeError, AttributeError) as error:
        raise RunError("Run ID must be a canonical UUID") from error
    if type(value.get("schemaVersion")) is not int or value["schemaVersion"] != 1:
        raise RunError("Unsupported or missing schemaVersion")
    if value.get("id") != run_id:
        raise RunError("Run ID does not match the request")
    if type(value.get("ok")) is not bool:
        raise RunError("Response/results must contain a boolean ok")
    if "error" in value and value["error"] is not None and not isinstance(value["error"], str):
        raise RunError("error must be a string or null")


def inspect_png(path):
    """Check complete PNG chunks, CRCs and uncompressed scanline lengths."""
    if path.stat().st_size > 256 * 1024 * 1024:
        raise RunError("Screenshot is too large")
    data = path.read_bytes()
    if not data.startswith(b"\x89PNG\r\n\x1a\n"):
        raise RunError("Screenshot has no PNG signature")
    offset, chunks, compressed = 8, [], bytearray()
    dimensions = None
    while offset < len(data):
        if offset + 12 > len(data):
            raise RunError("Truncated PNG chunk")
        size, kind = struct.unpack_from(">I4s", data, offset)
        end = offset + 12 + size
        if end > len(data):
            raise RunError("Truncated PNG chunk data")
        payload = data[offset + 8 : end - 4]
        crc = struct.unpack_from(">I", data, end - 4)[0]
        if zlib.crc32(kind + payload) & 0xFFFFFFFF != crc:
            raise RunError("PNG chunk checksum does not match")
        if not chunks and kind != b"IHDR":
            raise RunError("PNG must begin with IHDR")
        if kind == b"IHDR":
            if chunks or size != 13:
                raise RunError("Invalid PNG header")
            width, height, depth, color, compression, filtering, interlace = struct.unpack(">IIBBBBB", payload
            )
            allowed = {0: (1, 2, 4, 8, 16), 2: (8, 16), 3: (1, 2, 4, 8), 4: (8, 16), 6: (8, 16)}
            if (not width or not height or width * height > 100_000_000 or depth not in allowed.get(color, ())
                    or compression or filtering or interlace):
                raise RunError("Unsupported PNG header (non-interlaced captures required)")
            dimensions = (width, height)
            channels = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}[color]
            row_bytes = (width * channels * depth + 7) // 8
        elif kind == b"IDAT":
            if b"IDAT" in chunks and chunks[-1] != b"IDAT":
                raise RunError("Non-contiguous PNG image data")
            compressed.extend(payload)
        elif kind == b"PLTE":
            if b"PLTE" in chunks or b"IDAT" in chunks or not size or size % 3 or size > 768:
                raise RunError("Invalid PNG palette")
        elif kind == b"IEND":
            if size or end != len(data):
                raise RunError("Invalid PNG ending")
            chunks.append(kind)
            offset = end
            break
        elif kind[:1].isupper() and kind != b"PLTE":
            raise RunError("Unknown critical PNG chunk")
        chunks.append(kind)
        offset = end
    if not chunks or chunks[-1] != b"IEND" or b"IDAT" not in chunks or dimensions is None:
        raise RunError("Incomplete PNG screenshot")
    if color == 3 and b"PLTE" not in chunks:
        raise RunError("Indexed PNG has no palette")
    expected = (row_bytes + 1) * dimensions[1]
    if expected > 256 * 1024 * 1024:
        raise RunError("Uncompressed screenshot is too large")
    try:
        decoder = zlib.decompressobj()
        pixels = decoder.decompress(compressed, expected + 1)
    except zlib.error as error:
        raise RunError(f"Invalid compressed PNG data: {error}") from error
    if len(pixels) != expected or not decoder.eof or decoder.unused_data:
        raise RunError("PNG scanline data is truncated or has the wrong length")
    if any(pixels[index] > 4 for index in range(0, expected, row_bytes + 1)):
        raise RunError("Invalid PNG scanline filter")
    return {"width": dimensions[0], "height": dimensions[1], "sha256": sha256(path), "bytes": len(data)}


def screenshot_path(output, name):
    if not isinstance(name, str) or not PNG_NAME.fullmatch(name):
        raise RunError("Screenshot must be a local PNG filename without directories")
    candidate = output / name
    if candidate.resolve().parent != output.resolve():
        raise RunError("Screenshot path escapes the run directory")
    if not candidate.is_file():
        raise RunError(f"Screenshot is missing: {name}")
    return candidate


def collect_evidence(output, results, width, height):
    steps = results.get("steps")
    if not isinstance(steps, list) or not steps:
        raise RunError("Results must contain at least one step")
    evidence = []
    for step in steps:
        if not isinstance(step, dict) or not isinstance(step.get("name"), str) or type(step.get("ok")) is not bool:
            raise RunError("Invalid step record")
        record = dict(step)
        record.pop("evidenceError", None)
        seen = set()
        for field, image_field in CAPTURE_FIELDS:
            record.pop(image_field, None)
            name = step.get(field)
            if not name:
                continue
            canonical_name = os.path.normcase(name) if isinstance(name, str) else None
            if canonical_name is not None and canonical_name in seen:
                continue
            if canonical_name is not None:
                seen.add(canonical_name)
            try:
                record[image_field] = inspect_png(screenshot_path(output, name))
                if (record[image_field]["width"], record[image_field]["height"]) != (width, height):
                    raise RunError(f"Screenshot dimensions differ from expected {width} x {height}")
            except (OSError, RunError) as error:
                record["ok"] = False
                message = f"{field}: {error}"
                previous_error = record.get("evidenceError")
                record["evidenceError"] = previous_error + "; " + message if previous_error else message
                record.pop(image_field, None)
        if record["ok"] and "image" not in record:
            record["ok"] = False
            record["evidenceError"] = "Passed step is missing its primary screenshot evidence"
        evidence.append(record)
    return evidence


def write_gallery(output, manifest, evidence, status=None):
    escape = lambda value: html.escape(str(value), quote=True)
    content = ["<!doctype html><html lang='en'><meta charset='utf-8'>",
               "<meta name='viewport' content='width=device-width, initial-scale=1'>",
               "<title>Interface Extended test run</title>",
               "<style>body{font:16px system-ui;max-width:1200px;margin:2rem auto;padding:0 1rem}"
               "img{max-width:100%;height:auto}pre{white-space:pre-wrap;overflow-wrap:anywhere}"
               ".fail{color:#a01818}</style><h1>Interface Extended test run</h1>",
               "<p>" + escape(manifest.get("state", "unknown")) + "</p>",
               "<pre>" + escape(json.dumps(manifest, ensure_ascii=False, indent=2)) + "</pre>"]
    if status:
        content.append("<h2>Last agent status</h2><pre>"
                       + escape(json.dumps(status, ensure_ascii=False, indent=2)) + "</pre>")
    for step in evidence:
        content.append("<h2>" + escape(step.get("name", "Unknown step")) + "</h2>")
        content.append("<p>" + ("PASS" if step.get("ok") else "FAIL") + "</p>")
        seen = set()
        for field, image_field in CAPTURE_FIELDS:
            # Only validated local names can become HTML attributes or links.
            name = step.get(field)
            if image_field in step and isinstance(name, str) and PNG_NAME.fullmatch(name):
                canonical_name = os.path.normcase(name)
                if canonical_name in seen:
                    continue
                seen.add(canonical_name)
                content.append("<a href='" + escape(name) + "'><img src='" + escape(name)
                               + "' alt='" + escape(step.get("name", "Screenshot")) + "'></a>")
        content.append("<pre>" + escape(json.dumps(step, ensure_ascii=False, indent=2)) + "</pre>")
    content.append("</html>")
    (output / "index.html").write_text("\n".join(content) + "\n", encoding="utf-8")


def prepare(args):
    save = Path(args.save)
    if not save.is_absolute() or save.name != BASELINE_NAME or not save.is_file():
        raise RunError(f"Use an absolute path to a dedicated {BASELINE_NAME} file")
    output = Path(args.output).resolve() if args.output else (
        Path(__file__).resolve().parents[2] / "artifacts" / "runs" /
        (dt.datetime.now(dt.timezone.utc).strftime("%Y%m%dT%H%M%SZ-") + uuid.uuid4().hex[:8])
    )
    output.mkdir(parents=True, exist_ok=False)
    fixture = output / "fixture" / BASELINE_NAME
    fixture.parent.mkdir()
    original_hash = sha256(save)
    shutil.copyfile(save, fixture)
    working_hash = sha256(fixture)
    if working_hash != original_hash or sha256(save) != original_hash:
        raise RunError("Baseline changed while preparing its working copy")
    run_id = str(uuid.uuid4())
    request = {"schemaVersion": 1, "id": run_id, "command": "run", "outputDirectory": str(output),
               "savePath": str(fixture), "profile": args.profile,
               "allowModDifferenceDialog": args.allow_mod_difference_dialog,
               "expectedSaveSha256": working_hash, "expectedViewportWidth": args.width,
               "expectedViewportHeight": args.height, "negativeControl": args.negative_control}
    manifest = {"schemaVersion": 1, "id": run_id, "createdAt": utc_now(), "state": "prepared",
                "dryRun": args.dry_run, "runnerSha256": sha256(Path(__file__)),
                "python": platform.python_version(), "platform": platform.platform(),
                "arguments": vars(args), "baselinePath": str(save.resolve()), "baselineSha256": original_hash,
                "workingSavePath": str(fixture), "workingSaveSha256": working_hash, "request": request}
    atomic_json(output / "prepared-request.json", request)
    atomic_json(output / "manifest.json", manifest)
    write_gallery(output, manifest, [])
    return output, request, manifest


def send_request(bridge, request):
    if not bridge.is_dir():
        raise RunError("Bridge directory does not exist; install/start the test agent separately")
    lock = bridge / "runner.lock"
    try:
        with lock.open("x", encoding="ascii") as stream:
            stream.write(request["id"])
    except FileExistsError as error:
        raise RunError("Bridge runner.lock already exists; inspect the previous run before removing it") from error
    try:
        previous_path = bridge / "request.json"
        if previous_path.exists():
            previous = read_json(previous_path)
            previous_id = previous.get("id")
            try:
                canonical_id = str(uuid.UUID(previous_id))
            except (ValueError, TypeError, AttributeError) as error:
                raise RunError("Existing bridge request has an invalid ID; inspect it manually") from error
            if (canonical_id != previous_id or type(previous.get("schemaVersion")) is not int
                    or previous["schemaVersion"] != 1 or previous.get("command") != "run"):
                raise RunError("Existing bridge request is malformed; inspect it manually")
            response_path = bridge / "response.json"
            if not response_path.exists():
                raise RunError("An unfinished bridge request exists; it will not be overwritten")
            previous_response = read_json(response_path)
            check_envelope(previous_response, previous_id)
            history = bridge / "history" / previous_id
            history.mkdir(parents=True, exist_ok=False)
            for name in ("request.json", "response.json", "status.json"):
                source = bridge / name
                if source.exists():
                    shutil.copyfile(source, history / name)
            previous_path.unlink()
        elif (bridge / "status.json").exists():
            previous_status = read_json(bridge / "status.json")
            response_path = bridge / "response.json"
            if not response_path.exists():
                raise RunError("Bridge has a status without a terminal response; inspect it manually")
            previous_response = read_json(response_path)
            check_envelope(previous_response, previous_status.get("id"))
        atomic_json(bridge / "request.json", request)
    finally:
        lock.unlink(missing_ok=True)


def wait_response(bridge, run_id, timeout, *, poll_interval=0.1, clock=time.monotonic, sleeper=time.sleep):
    deadline = clock() + timeout
    status = None
    while clock() < deadline:
        status_path = bridge / "status.json"
        if status_path.exists():
            value = read_json(status_path)
            if value.get("id") == run_id:
                status = value
        response_path = bridge / "response.json"
        if response_path.exists():
            value = read_json(response_path)
            if value.get("id") == run_id:
                check_envelope(value, run_id)
                return value, status
        sleeper(min(poll_interval, max(0, deadline - clock())))
    error = TimeoutError(f"Agent did not finish within {timeout:g} seconds")
    error.status = status
    raise error


def run(args):
    output, request, manifest = prepare(args)
    print(f"Run output: {output}")
    if args.dry_run:
        print("Prepared only; no bridge request was sent.")
        return 0
    evidence, status = [], None
    code = 2
    try:
        bridge = Path(args.bridge).resolve()
        send_request(bridge, request)
        manifest["state"] = "running"
        atomic_json(output / "manifest.json", manifest)
        response, status = wait_response(bridge, request["id"], args.timeout)
        atomic_json(output / "agent-response.json", response)
        result_path = output / "results.json"
        supplied = response.get("resultsPath")
        if supplied is not None and (not isinstance(supplied, str) or not Path(supplied).is_absolute()
                                     or Path(supplied).resolve() != result_path.resolve()):
            raise RunError("Agent resultsPath must be this run's results.json")
        if result_path.exists():
            results = read_json(result_path)
            check_envelope(results, request["id"])
            evidence = collect_evidence(output, results, args.width, args.height)
            if results["ok"] != response["ok"]:
                raise RunError("Agent response and results disagree")
            passed = results["ok"] and all(step["ok"] for step in evidence)
            if passed and args.negative_control != "none":
                raise RunError("Negative control unexpectedly passed; the failure detector was not demonstrated")
            manifest["error"] = results.get("error") or response.get("error")
            if not passed and not manifest["error"]:
                manifest["error"] = "A test step or screenshot evidence check failed"
            code = 0 if passed else 1
            manifest["state"] = "passed" if passed else "failed"
        else:
            if response["ok"]:
                raise RunError("Agent reported success without results.json")
            manifest["state"] = "failed"
            manifest["error"] = response.get("error") or "Agent failed without results"
            code = 1
        game_path = output / "manifest-game.json"
        if game_path.exists():
            manifest["game"] = read_json(game_path)
    except TimeoutError as error:
        manifest.update(state="timed-out", error=str(error))
        status = error.status
        code = 3
    except (OSError, RunError) as error:
        manifest.update(state="error", error=str(error))
        code = 2
    finally:
        manifest["finishedAt"] = utc_now()
        if status is not None:
            atomic_json(output / "last-status.json", status)
        atomic_json(output / "evidence.json", {"schemaVersion": 1, "id": request["id"], "steps": evidence})
        atomic_json(output / "manifest.json", manifest)
        write_gallery(output, manifest, evidence, status)
    print(f"{manifest['state']}: {output / 'index.html'}")
    return code


def parser():
    result = argparse.ArgumentParser(description=__doc__)
    result.add_argument("--save", required=True, help=f"Absolute path to dedicated {BASELINE_NAME}")
    result.add_argument("--bridge", required=True, help="Installed test agent's bridge directory")
    result.add_argument("--output", help="New run directory (must not exist)")
    result.add_argument("--profile", choices=("default", "local"), default="default")
    result.add_argument("--width", type=int, default=5120)
    result.add_argument("--height", type=int, default=1440)
    result.add_argument("--timeout", type=float, default=180)
    result.add_argument("--allow-mod-difference-dialog", action="store_true")
    result.add_argument("--negative-control", choices=("none", "wrong-width", "blocked-control"), default="none")
    result.add_argument("--dry-run", action="store_true", help="Prepare files only; do not read/write the bridge")
    return result


def main(argv=None):
    argument_parser = parser()
    args = argument_parser.parse_args(argv)
    if args.profile == "default" and args.allow_mod_difference_dialog:
        argument_parser.error("--allow-mod-difference-dialog requires --profile local")
    if not 0 < args.timeout <= 3600 or not 1 <= args.width <= 32768 or not 1 <= args.height <= 32768:
        argument_parser.error("Use timeout > 0 and <= 3600 seconds, and dimensions between 1 and 32768")
    try:
        return run(args)
    except (OSError, RunError) as error:
        print(f"Setup error: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
