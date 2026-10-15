"""Generate the translation template or check catalogs against the English source."""

import argparse
from collections import Counter
import json
from pathlib import Path
import re

import polib


ROOT = Path(__file__).resolve().parents[1]
CATALOGS = ROOT / "mod" / "translations"
PREFIX = "CenteredHud.Localization.STRINGS."
FIELD = re.compile(r'public static LocString (\w+) = ("(?:[^"\\]|\\.)*");')
PARAMETER = re.compile(r"\{\d+(?::[^{}]+)?\}")


def source_strings():
    source = (ROOT / "src" / "CenteredHud" / "Strings.cs").read_text(encoding="utf-8")
    fields = FIELD.findall(source)
    if not fields or len(fields) != source.count("public static LocString "):
        raise ValueError("Each LocString must use a single C# string literal")
    return {PREFIX + key: json.loads(value) for key, value in fields}


def parameters(value):
    remainder = PARAMETER.sub("", value)
    if "{" in remainder or "}" in remainder:
        raise ValueError(f"Malformed format string: {value!r}")
    return Counter(PARAMETER.findall(value))


def check_catalog(path, strings, template=False):
    catalog = polib.pofile(str(path), check_for_duplicates=True)
    seen = set()
    for entry in catalog:
        if entry.obsolete:
            raise ValueError(f"{path.name}: remove obsolete entry {entry.msgctxt}")
        key = entry.msgctxt
        if key not in strings or key in seen:
            raise ValueError(f"{path.name}: unknown or duplicate key {key}")
        seen.add(key)
        if entry.msgid != strings[key]:
            raise ValueError(f"{path.name}: outdated English text for {key}")
        if entry.msgid_plural or entry.msgstr_plural:
            raise ValueError(f"{path.name}: ONI does not load gettext plural entries")
        if "fuzzy" in entry.flags:
            raise ValueError(f"{path.name}: resolve fuzzy translation for {key}")
        if template and entry.msgstr:
            raise ValueError(f"{path.name}: template must have empty msgstr")
        if entry.msgstr and parameters(entry.msgstr) != parameters(entry.msgid):
            raise ValueError(f"{path.name}: changed format parameters for {key}")
    if template and seen != strings.keys():
        raise ValueError("Translation template is missing source strings")
    print(f"{path.name}: {len(seen)} entries checked")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--update-template", action="store_true")
    args = parser.parse_args()
    strings = source_strings()
    template_path = CATALOGS / "interface_extended.pot"
    if args.update_template:
        catalog = polib.POFile(wrapwidth=0)
        catalog.metadata = {
            "Project-Id-Version": "Interface Extended",
            "MIME-Version": "1.0",
            "Content-Type": "text/plain; charset=UTF-8",
            "Content-Transfer-Encoding": "8bit",
        }
        for key, value in strings.items():
            catalog.append(polib.POEntry(msgctxt=key, msgid=value, msgstr=""))
        catalog.save(str(template_path))
    check_catalog(template_path, strings, template=True)
    for path in sorted(CATALOGS.glob("*.po")):
        check_catalog(path, strings)


if __name__ == "__main__":
    main()
