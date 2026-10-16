# Workshop preparation

README is the source of the English product text and screenshot order. `scripts/workshop.py` exports its six marked sections: overview, compatibility, screenshots, usage, languages and support. Installation-path tables and capture metadata outside those markers are excluded. Build instructions, configuration internals and test evidence live in [development.md](development.md).

```text
make workshop
python scripts/workshop.py
python scripts/workshop.py --check
python -m unittest discover -s tests -p test_workshop.py
```

The exporter uses only Python's standard library. It writes ignored `artifacts/workshop/description.bbcode` and `screenshots.json`; do not commit generated copies. `--check` validates README markers, supported syntax and referenced repository files without writing. If an export exists, both files must match current generated content; missing or stale output fails the check. In a clean checkout, no generated files are required.

`make check` and GitHub Actions include exporter tests and source validation. After editing README, regenerate an existing local export before running the check. No source, description or image is uploaded by these commands.

The supported Markdown subset is paragraphs, headings up to level three, flat dash lists, bold, inline code, inline links and local JPEG/PNG images. Other syntax fails explicitly. Repository links become public GitHub URLs; screenshots become external image links in the description. The description uses Steam's documented [BBCode formatting](https://steamcommunity.com/comment/Announcement/formattinghelp).

`screenshots.json` lists the three local gallery files in README order: full-width HUD, centered HUD and settings. Captions, public URLs and file hashes come from those README references. External links in the description do not upload gallery images. Existing captures show the tested 1.3.0 UI; do not relabel them as 1.4.0 runtime evidence.

README declares exactly one standalone HTML image below its title: `docs/images/icon.png`, displayed at 96 × 96. That same 512 × 512 PNG is the Workshop item cover. It is outside the six product sections and is not a gallery screenshot. The manifest's separate `preview` entry records its source, public URL, dimensions and hash; `images` contains only the three gameplay/settings captures.

`docs/images/icon.svg` is the editable vector source. When changing it, re-export the 512 × 512 PNG, for example with Sharp, and regenerate the manifest. Node/Sharp is optional artwork tooling, not a dependency of normal checks, builds or Workshop export. Inspect the description, item cover and gallery previews before publication.

The exporter prepares local files only. Item identity, private creation, owner-page operations, interrupted-upload recovery and release verification are documented in [Publishing](publishing.md).
