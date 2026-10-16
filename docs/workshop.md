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

`screenshots.json` lists the three local gallery files in README order: full-width HUD, centered HUD and settings. Captions, public URLs and file hashes come from those README references. Upload the real images separately in that order; an external link in the description does not upload a gallery image. Existing captures show the tested 1.3.0 UI; do not relabel them as 1.4.0 runtime evidence.

README declares exactly one standalone HTML image below its title: `docs/images/icon.png`, displayed at 96 × 96. That same 512 × 512 PNG is the Workshop item cover. It is outside the six product sections and is not a gallery screenshot. The manifest's separate `preview` entry records its source, public URL, dimensions and hash; `images` contains only the three gameplay/settings captures. Upload the cover as the item preview, then the three gallery images in their listed order.

`docs/images/icon.svg` is the editable vector source. When changing it, re-export the 512 × 512 PNG, for example with Sharp, and regenerate the manifest. Node/Sharp is optional artwork tooling, not a dependency of normal checks, builds or Workshop export. Inspect the description, item cover and gallery previews before publication.

## First upload and release

The Workshop item and binary release are not published yet. Oxygen Not Included Uploader was installed and its Add form prepared locally on 2026-10-16, but Publish was not submitted. This version's ONI form has no visibility selector; do not assume it creates a private draft. Establish a route that explicitly sets Private before making the first upload. Steam documents Private as visibility value `2`, distinct from Public (`0`) and Unlisted (`3`), in its [visibility enum](https://partner.steamgames.com/doc/api/ISteamRemoteStorage#ERemoteStoragePublishedFileVisibility).

Upload only the clean package directory, use the README-derived description and preview, then add the three gallery images. Record the returned Workshop ID and reuse it for updates; never create a replacement item for a routine release. Verify the item's actual visibility, rendered description, gallery order, GitHub Issues support link and disabled comments while it is private.

Before public release, verify a subscribed download in ONI with the local duplicate disabled, checking the public ID, persisted settings, translations and HUD/settings scenario. Keep the user's collection and config intact. Update README's publication/version status and regenerate the description once the item URL and release status are known. Public visibility is a separate final action after review. This exporter prepares local files only; it does not publish, change remote settings or require Steam credentials.
