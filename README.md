# Interface Extended

Bring Oxygen Not Included's HUD closer to the center of a wide monitor while keeping the world view at full width.

Choose a 2560 px or 3360 px HUD, enter a custom width, or restore the full screen width. Panels keep the game's UI scale. Settings use ONI's own buttons, fonts and window styling.

## Status and compatibility

- **1.2.0:** tested in a colony on Windows, ONI U59-744825-SCRPAN, Spaced Out!, 5120 × 1440, UI scale 110%.
- **1.3.0 source:** English default text, Russian translation, translation support and maintenance fixes. Compiles against that game build; in-game verification is pending. See [validation](VALIDATION.md).
- No external mod dependency. Harmony and UI components come from ONI.
- Metadata allows the base game and DLC. Base-game-only play, other operating systems and other resolutions have not been tested in game.
- Other mods that reposition the same HUD containers may conflict. Public testing branches are untested.

There is no Steam Workshop listing or public binary release yet. Build from source using the instructions below.

## Install and use

Build the mod, then copy the contents of `artifacts/CenteredHud` into a folder named `CenteredHud` inside your ONI local mods directory:

| System | Local mods directory |
| --- | --- |
| Windows | Your Documents folder, then `Klei\OxygenNotIncluded\mods\Local` |
| Linux | `~/.config/unity3d/Klei/Oxygen Not Included/mods/Local` |
| macOS | `~/Library/Application Support/unity.Klei.Oxygen Not Included/mods/Local` |

On Windows, Documents can be redirected to OneDrive or another location. Use the actual Documents folder. Linux and macOS paths follow common ONI mod conventions; this project's build/install script is tested on Windows only.

Enable **Interface Extended** in ONI's Mods menu and restart when prompted. Use **Settings > Interface Extended**, either from the main menu or the colony pause menu. With Russian selected, the entry is **Расширенный интерфейс**. Main-menu changes take effect when a colony loads; colony changes apply immediately.

The window provides width presets, a numeric field, 160 px steps, boundary guides and an optional shortcuts checkbox. Width is measured in physical viewport pixels. Small values are clamped to a minimum of 1280 logical UI pixels, so a large UI scale can produce a wider HUD than requested. Set width to `0` for full width.

Shortcuts are off by default. When enabled, **Ctrl+Shift+F8** cycles 2560 / 3360 / full width and **Ctrl+Shift+F9** opens settings.

To update a local installation, close ONI and replace the DLL, metadata and shipped translation files. Keep your existing `config.json`. Enable only one copy of the mod. To remove it, disable it in Mods and restart, then remove its folder if desired. The mod writes settings to its own folder and does not add save-file data.

## Build

Requires an installed copy of ONI, PowerShell 7 and a [.NET 9 runtime or newer](https://dotnet.microsoft.com/download/dotnet). A .NET SDK is not required. The first build downloads the pinned Roslyn 4.14.0 compiler from NuGet into `.tools`.

```powershell
./build.ps1 -GamePath 'D:\SteamLibrary\steamapps\common\OxygenNotIncluded'
```

Alternatively, set `ONI_GAME_PATH`. The compiler uses the installed game's framework and Unity assemblies. Those assemblies are never included in the output or this repository.

The output is `artifacts/CenteredHud`. On Windows, append `-Install` to copy it to the local mods directory while preserving an existing config. Installation refuses to run while ONI is open. A normal build does not change an installed mod.

```powershell
./tests/test-geometry.ps1
python -m pip install -r tests/requirements.txt
python scripts/localization.py
```

These checks run without ONI. [Testing](docs/testing.md) describes game-dependent checks, the manual regression scenario and research into deterministic automation with screenshot evidence.

For routine work, use `make check`, `make build`, `make package`, `make install` or `make translations`. Set `ONI_GAME_PATH` for game-dependent tasks. Make is optional: `./tasks.ps1 -Task check` (or another task name) runs the same commands on Windows without it. The localization check requires Python 3.10+ and the dependency shown above. Builds also check the installed game's patch targets and reflected fields.

## Configuration and translations

Settings are stored in the installed mod's `config.json`. Close ONI before editing it manually.

| Key | Default | Meaning |
| --- | --- | --- |
| `Enabled` | `true` | Constrain HUD width. |
| `WidthPixels` | `2560` | Requested width; `0` uses the full viewport. Saved values are clamped to 0–32768. |
| `ShowGuides` | `false` | Show the two HUD boundaries. |
| `EnableShortcuts` | `false` | Enable the optional key combinations. |
| `CycleKey` | `F8` | Unity KeyCode name, with Ctrl+Shift modifiers. |
| `SettingsKey` | `F9` | Unity KeyCode name, with Ctrl+Shift modifiers. |

The UI defaults to English and follows the game's selected language when a translation is available. Russian is bundled. Other languages fall back to English. See [translation instructions](docs/translating.md) to contribute a `.po` file.

## Support and development

Report a problem in [GitHub Issues](https://github.com/glockbender/oni-interface-extended/issues), including the game/mod version, resolution, UI scale, reproduction steps and relevant `Player.log` output. Remove personal paths from logs before posting.

[Contributing](CONTRIBUTING.md) · [Changelog](CHANGELOG.md) · [Code and documentation review](docs/review.md)

MIT licensed. Oxygen Not Included and its runtime assets belong to Klei Entertainment. This is a community mod.
