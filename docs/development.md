# Development

Read [AGENTS.md](../AGENTS.md) and [Contributing](../CONTRIBUTING.md) before changing code or documentation. README owns the English product description, usage, compatibility limits and support links; the [Workshop exporter](workshop.md) selects those sections for Steam.

## Build

Requires an installed copy of ONI, PowerShell 7 and a [.NET 9 runtime or newer](https://dotnet.microsoft.com/download/dotnet). A .NET SDK is not required. The first build downloads the pinned Roslyn 4.14.0 compiler from NuGet into `.tools`.

```powershell
./build.ps1 -GamePath 'D:\SteamLibrary\steamapps\common\OxygenNotIncluded'
```

Alternatively, set `ONI_GAME_PATH`. The compiler uses the installed game's framework and Unity assemblies; those assemblies are never included in the output or this repository. Builds also check the installed game's patch targets and reflected fields.

Output is `artifacts/CenteredHud`. On Windows, append `-Install` to copy it to the local mods directory. Installation refuses to run while ONI is open and does not replace user settings. A normal build does not change an installed mod. `make package` produces the release ZIP from an explicit allowlist; packages contain no default config or development test agent.

## Routine tasks

The game-independent checks require Python 3.10+ and the pinned localization dependency:

```text
python -m pip install -r tests/requirements.txt
make check
```

| Task | Purpose |
| --- | --- |
| `make check` | Geometry, options/config migration, localization, Workshop export and runner checks without ONI. |
| `make build` | Compile against the installed game and check its metadata contract. |
| `make package` | Check and build the player release ZIP. |
| `make install` | Install the built player mod locally while ONI is closed. |
| `make translations` | Regenerate the translation template and validate catalogs. |
| `make workshop` | Prepare the README-derived description and image-upload manifest. |
| `make test-agent` | Build the separate development mod without installing it. |
| `make test-runner` | Run only the external runner's headless tests. |

Make is optional. `./tasks.ps1 -Task check`, or another task name, runs the same commands. Set `ONI_GAME_PATH` for game-dependent tasks; `-GamePath` and `-Python` accept explicit paths. Build/check tasks do not launch ONI or change enabled mods.

## Configuration

Version 1.4.0 uses public mod ID `InterfaceExtended` and stores user settings in ONI's `mods/config/InterfaceExtended/config.json`, outside the replaceable installation folder. The existing C# namespace and DLL filename remain implementation details.

An existing shared config takes precedence. Otherwise the mod imports the installation-folder `config.json`, then the legacy `mods/Local/CenteredHud/config.json`, or uses built-in defaults. Legacy files are left unchanged. If migration cannot write the new file, valid legacy settings remain usable and the settings window reports a save error. Malformed files are not migrated or automatically replaced; a warning is logged and defaults are used. If no current or legacy file exists, the shared file is created on the first settings change.

Close ONI before editing settings manually. Enable only one installation, including when replacing the legacy `prvz.CenteredHud` identity.

| Key | Default | Meaning |
| --- | --- | --- |
| `Enabled` | `true` | Constrain HUD width. |
| `WidthPixels` | `2560` | Requested physical width; `0` uses the full viewport. Saved values are clamped to 0–32768. |
| `ShowGuides` | `false` | Show the two HUD boundaries. |
| `EnableShortcuts` | `false` | Enable optional Ctrl+Shift key combinations. |
| `CycleKey` | `F8` | Unity KeyCode name used to cycle 2560 / 3360 / full width. |
| `SettingsKey` | `F9` | Unity KeyCode name used to open settings. |

The injected Ctrl+Shift+F8/F9 check did not trigger actions; physical-keyboard verification remains open. Mouse controls cover every setting.

## Runtime evidence

[VALIDATION.md](../VALIDATION.md) records exact binaries, game/DLC versions, fixture hashes, completed interactions and unresolved checks. The 1.4.0 local build passed legacy config migration; both local and subscribed Workshop installations passed the 20-step HUD scenario in ONI. Earlier 1.3.0 runs include both strict matching-baseline and explicit local mod-difference profiles, plus deliberate wrong-width and blocked-control failures. These runs used a 29-mod collection including the development agent; they do not certify every interaction in that collection.

The [game test harness](../tests/runtime/README.md) measures HUD layout, checks raycast receivers and stores PNG evidence. It dispatches Unity UI events and edits numeric fields through their component API, so it does not verify OS keyboard input or gameplay. Headless protocol tests use synthetic fixtures and are separate from real game results. See [testing](testing.md) for coverage and the broader manual regression procedure, and [review notes](review.md) for the code/documentation audit.
