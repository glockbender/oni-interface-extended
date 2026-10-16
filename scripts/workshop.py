"""Export marked README product sections to Steam BBCode and an image-upload list."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import sys
from urllib.parse import quote, urlsplit

ROOT = Path(__file__).resolve().parents[1]
REPOSITORY = "https://github.com/glockbender/oni-interface-extended"
RAW = "https://raw.githubusercontent.com/glockbender/oni-interface-extended/main"
SECTIONS = ("overview", "compatibility", "screenshots", "usage", "languages", "support")
MARKER = re.compile(r"<!-- workshop:([a-z]+):(start|end) -->\Z")
INLINE = re.compile(r"(!?)\[([^\[\]\n]+)\]\(([^\s()]+)\)|\*\*([^*\n]+)\*\*|`([^`\n]+)`")


class ExportError(Exception):
    """Unsupported README syntax, invalid references or stale generated output."""


def sections(readme):
    selected, current, lines = [], None, []
    for line in readme.splitlines():
        if "<!-- workshop:" in line:
            marker = MARKER.fullmatch(line)
            if marker is None:
                raise ExportError(f"Invalid Workshop marker: {line}")
            name, action = marker.groups()
            if action == "start":
                if current is not None or name not in SECTIONS:
                    raise ExportError(f"Unexpected section start: {name}")
                current, lines = name, []
            else:
                if name != current or not "\n".join(lines).strip():
                    raise ExportError(f"Unmatched or empty section: {name}")
                selected.append((name, "\n".join(lines).strip()))
                current, lines = None, []
        elif current is not None:
            lines.append(line)
    if current is not None or tuple(name for name, _ in selected) != SECTIONS:
        raise ExportError("README must contain each Workshop section once, in the documented order")
    return selected


def reference(target, root, *, image=False):
    parsed = urlsplit(target)
    if "\\" in target or any(character in target for character in '<>"\r\n'):
        raise ExportError(f"Unsafe reference: {target}")
    if parsed.scheme or parsed.netloc:
        if image or parsed.scheme != "https" or not parsed.netloc or parsed.username or parsed.password:
            raise ExportError(f"Expected a local image or HTTPS link: {target}")
        return quote(target, safe=":/?#=&%+@~.-_"), None
    if parsed.query or parsed.path.startswith("/"):
        raise ExportError(f"Expected a repository-relative reference: {target}")
    relative = parsed.path or "README.md"
    candidate = (root / relative).resolve()
    if not candidate.is_relative_to(root.resolve()) or not candidate.is_file():
        raise ExportError(f"Reference does not resolve to a repository file: {target}")
    source = candidate.relative_to(root.resolve()).as_posix()
    if image and candidate.suffix.lower() not in (".jpg", ".jpeg", ".png"):
        raise ExportError(f"Unsupported upload image: {target}")
    base = RAW if image else REPOSITORY + "/blob/main"
    fragment = "#" + quote(parsed.fragment, safe="-_") if parsed.fragment else ""
    return base + "/" + quote(source, safe="/-_.") + fragment, source


def plain(text):
    if any(character in text for character in "[]`*\\<") or re.search(r"(?<!\w)_[^_]+_(?!\w)", text):
        raise ExportError(f"Unsupported inline syntax: {text}")
    return text


def inline(text, root, images):
    output, position = [], 0
    for token in INLINE.finditer(text):
        output.append(plain(text[position:token.start()]))
        image, caption, target, bold, code = token.groups()
        if target is not None:
            caption = plain(caption)
            url, source = reference(target, root, image=bool(image))
            output.append(f"[url={url}]{caption}[/url]")
            if image:
                if any(item["source"] == source for item in images):
                    raise ExportError(f"Duplicate Workshop upload image: {source}")
                images.append({"order": len(images) + 1, "caption": caption,
                               "source": source, "url": url,
                               "sha256": hashlib.sha256((root / source).read_bytes()).hexdigest()})
        elif bold is not None:
            output.append("[b]" + plain(bold) + "[/b]")
        else:
            # Steam's code tag is a block; short UI labels/filenames remain plain text.
            output.append(plain(code))
        position = token.end()
    output.append(plain(text[position:]))
    return "".join(output)


def convert(text, root, images):
    output, listing = [], False
    for line in text.splitlines():
        heading = re.fullmatch(r"(#{1,3}) (.+)", line)
        bullet = line.startswith("- ")
        if listing and not bullet:
            output.append("[/list]")
            listing = False
        if not line.strip():
            output.append("")
        elif heading:
            level = len(heading[1])
            output.append(f"[h{level}]" + inline(heading[2], root, images) + f"[/h{level}]")
        elif bullet:
            if not listing:
                output.append("[list]")
                listing = True
            output.append("[*]" + inline(line[2:], root, images))
        else:
            if (line[0].isspace() or line.startswith(("#", ">", "```", "~~~"))
                    or "|" in line or re.match(r"\d+[.)] ", line)
                    or re.match(r"[+*] ", line) or re.fullmatch(r"[-_*]{3,}", line)):
                raise ExportError(f"Unsupported block syntax: {line}")
            output.append(inline(line, root, images))
    if listing:
        output.append("[/list]")
    return "\n".join(output).strip()


def export(root):
    root = root.resolve()
    source = (root / "README.md").read_text(encoding="utf-8")
    images = []
    description = "\n\n".join(convert(text, root, images) for _, text in sections(source)) + "\n"
    if not images:
        raise ExportError("README must include actual screenshots in its Workshop sections")
    manifest = {"schemaVersion": 1, "readmeSha256": hashlib.sha256(source.encode("utf-8")).hexdigest(),
                "images": images}
    return {"description.bbcode": description,
            "screenshots.json": json.dumps(manifest, ensure_ascii=False, indent=2) + "\n"}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Validate without writing; reject stale outputs if present")
    args = parser.parse_args(argv)
    try:
        generated = export(ROOT)
        output = ROOT / "artifacts" / "workshop"
        if args.check:
            existing = [name for name in generated if (output / name).exists()]
            if existing and len(existing) != len(generated):
                raise ExportError("Incomplete Workshop export; regenerate with python scripts/workshop.py")
            for name in existing:
                if (output / name).read_bytes() != generated[name].encode("utf-8"):
                    raise ExportError(f"Stale {name}; regenerate with python scripts/workshop.py")
            print("Workshop README source and any existing export are valid")
        else:
            output.mkdir(parents=True, exist_ok=True)
            for name, content in generated.items():
                (output / name).write_text(content, encoding="utf-8", newline="\n")
            print(f"Prepared Workshop text and screenshot upload list: {output}")
        return 0
    except (OSError, ExportError) as error:
        print(f"Workshop export failed: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
