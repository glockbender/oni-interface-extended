# Validation

Date: 2026-10-16 (Asia/Nicosia).

## Unreleased 1.3.0: source and build checks

- Built against ONI U59-744825 with Roslyn 4.14.0, warnings treated as errors.
- Game contract check passed: named Harmony patch targets, reflected OptionsMenuScreen/KButtonMenu/KModalButtonMenu fields, absence of host `System.Private.CoreLib` references, DLL version matching mod metadata.
- Production geometry checks passed, including the regression for decrementing from full width and invalid viewport/scale inputs.
- Production options checks passed for defaults, invalid widths, missing keys and preservation of intentional custom settings.
- English template and Russian catalog validated: 15 entries each, matching source text and format placeholders.
- Make check/build commands and the PowerShell package task completed locally. ZIP uses an explicit file allowlist; it does not contain game assemblies or research tools.
- The installed 1.2.0 mod was left unchanged while the user played. None of the runtime observations below certify 1.3.0.

Pending for 1.3.0: native localization loading, English/Russian text layout, settings-save error state, full-width step controls, fresh-colony regression and screenshots. The deterministic game test runner is researched in [testing](docs/testing.md) and is not implemented yet.

## Runtime environment for 1.0.0–1.2.0

- ONI U59-744825-SCRPAN, Unity 6000.3.5f2.
- Windows, 5120 × 1440, ONI UI scale 110%, Spaced Out enabled.
- Existing user mod collection remained enabled.

## Core HUD checks (initial 1.0.0 runtime)

- Roslyn compilation against the installed game's assemblies, with warnings treated as errors.
- DLL metadata checked: Harmony patch attribute present, `GameScreenManager.OnSpawn` exists, no `System.Private.CoreLib` reference from the host runtime.
- Geometry tests: 2560/3360/full-width bounds, narrower screens, minimum usable width, invalid scale/width, symmetric margins, fixed center, stretched containers, scaling and restoration.
- Runtime load: `[CenteredHud] Loaded 1.0.0` appeared in Player.log; Harmony patched successfully.
- Colony load: `[CenteredHud] Tracking 9 HUD containers at 5120x1440`.
- Visually verified at 2560 pixels: top-left controls, top-right management/overlays/resources, bottom construction and tools moved inward; world rendering retained the full width.
- Native Russian settings screen opened successfully; text and controls rendered correctly.
- Settings buttons switched to 5120/full-screen, 3360 and back to 2560. Original edge positions were restored in full-screen mode; repeated switches did not accumulate displacement.
- Boundary guides displayed at the expected positions and were hidden again.
- Config changes were written to the installed config.json.
- The user independently clicked part of the game UI and reported that it worked.

## Version 1.1.0 — passed

- Compiled with warnings treated as errors against the installed game's assemblies. Verified the `OptionsMenuScreen.OnPrefabInit` patch target, native button constructor, and injected field types.
- Installed DLL SHA-256 matches the built artifact. Player.log confirms `[CenteredHud] Loaded 1.1.0` after a normal Steam launch, with zero broken mod installations.
- Main menu: Settings shows one native `Интерфейс по центру` button immediately after Graphics. Opening works without a loaded colony; the screen explains that preview requires a colony.
- Main menu: selected 3360, confirmed config persistence, closed by mouse, and returned through the native Settings menu to the main menu.
- Restarted ONI and loaded the user's existing colony: nine HUD containers tracked at 5120 × 1440. The 3360 setting persisted and was applied.
- Colony: Pause → Settings → Интерфейс по центру opens the settings modal above the native menus. Current resolution and effective width display correctly.
- Selected 2560 by mouse: the HUD moved inward immediately and the config was saved. Closing returned to native Settings; Done returned to the pause menu. The colony remained paused throughout.
- Final installed config: enabled, width 2560, guides off, shortcuts off. All required controls are reachable with the mouse. Optional Ctrl+Shift shortcuts were not exercised in this release.

## Version 1.2.0

- Warning-free build against the installed ONI assemblies, including the game's own Unity.TextMeshPro assembly. Installed DLL SHA-256 matches the built artifact.
- Renamed display title to Interface Extended, native menu entry/title to Расширенный интерфейс; retained `prvz.CenteredHud` and the existing configuration path.
- Replaced custom uGUI text/buttons with cloned ONI title bar, close button, KButton presets/Done button, LocText fonts, checkbox rows, and KInputTextField. Background sprites/colors come from native Options/Graphics prefabs. No game assets are redistributed.
- Main-menu runtime check: correct title and layout, 2560 → 3360 preset updated status/config, native close button restored the original Settings menu. No managed exception was logged.
- Colony runtime check after restarting: native modal opened from Pause → Settings, with resolution/effective width displayed correctly. Guides checkbox toggled the boundary lines on/off; shortcuts checkbox toggled its checkmark and shortcut hint on/off.
- Selected the numeric field, typed 2560 and pressed Enter: effective HUD width and saved config changed from 3360 to 2560 immediately. Native Done restored the parent Settings menu while keeping the colony paused.
- Disabled redundant inherited tooltips after observing that ONI's global tooltip canvas renders behind this modal. Controls retain their native hover states and sounds.
- Final build restarted successfully through Steam and opened the completed window from the main menu. Installed defaults retained: enabled, width 2560, guides off, shortcuts off.

## Limits

- Resolution/UI-scale changes are handled by normalized anchors and recomputed canvas width; multiple scale factors are covered by geometry tests. Other resolutions and a live UI-scale change were not manually tested.
- Not every side screen, expansion screen, or third-party UI mod has been exercised.
- No long-session performance measurement was performed. The controller checks nine container anchors per frame and discovers roots once per second; it does not scan the scene per frame.
- The initial safe-mode notice was traced to an interrupted preceding boot; Centered HUD was disabled in that boot. A subsequent normal Steam launch with the mod enabled succeeded.
- One earlier 1.1.0 session logged a native Unity/Steam-overlay crash during application shutdown, after input and physics teardown, without a managed Centered HUD exception. Its cause is unconfirmed; the subsequent launch and colony/settings checks above succeeded.
