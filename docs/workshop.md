# Workshop preparation

README is the source of the English product text and screenshot order. `scripts/workshop.py` exports its six marked sections: overview, compatibility, screenshots, usage, languages and support. Installation-path tables, build instructions and development notes are excluded.

```text
make workshop
python scripts/workshop.py
python scripts/workshop.py --check
python -m unittest discover -s tests -p test_workshop.py
```

The exporter uses only Python's standard library. It writes ignored `artifacts/workshop/description.bbcode` and `screenshots.json`; do not commit generated copies. `--check` validates README markers, supported syntax and referenced repository files without writing. If an export exists, both files must match current generated content; missing or stale output fails the check. In a clean checkout, no generated files are required.

`make check` and GitHub Actions include exporter tests and source validation. After editing README, regenerate an existing local export before running the check. No source, description or image is uploaded by these commands.

The supported Markdown subset is paragraphs, headings up to level three, flat dash lists, bold, inline code, inline links and local JPEG/PNG images. Other syntax fails explicitly. Repository links become public GitHub URLs; screenshots become external image links in the description. The description uses Steam's documented [BBCode formatting](https://steamcommunity.com/comment/Announcement/formattinghelp).

`screenshots.json` lists local source files in README order, with captions, public URLs and file hashes. Upload those real images separately to the Workshop gallery in that order; an external link in the description does not upload a gallery image. Inspect the generated description and gallery preview before publication. Existing captures show the tested 1.3.0 UI; do not relabel them as 1.4.0 runtime evidence.

The Workshop item and binary release are not published yet. When publication is authorized, set the description from the export, upload the listed images, link support to GitHub Issues and disable Workshop comments. Then update README's publication/version status and regenerate. This script prepares local files only; it does not publish, change remote settings or require Steam credentials.
