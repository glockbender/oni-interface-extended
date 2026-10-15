# Validation

Date: 2026-10-16 (Asia/Nicosia).

## Unreleased 1.3.0: source and build checks

- Built against ONI U59-744825 with Roslyn 4.14.0, warnings treated as errors.
- Game contract check passed: named Harmony patch targets, reflected OptionsMenuScreen/KButtonMenu/KModalButtonMenu fields, absence of host `System.Private.CoreLib` references, DLL version matching mod metadata.
- Production geometry checks passed, including the regression for decrementing from full width and invalid viewport/scale inputs.
- Production options checks passed for defaults, invalid widths, missing keys and preservation of intentional custom settings.
- English template and Russian catalog validated: 15 entries each, matching source text and format placeholders.
- Make check/build commands and the PowerShell package task completed locally. ZIP uses an explicit file allowlist; it does not contain game assemblies or research tools.
- The installed 1.2.0 mod was initially left unchanged while the user played. After the user saved and closed ONI, 1.3.0 was installed for the checks below.

### 1.3.0 runtime check

Windows, ONI U59-744825-SCRPAN / Unity 6000.3.5f2, 5120 × 1440, UI scale 110%. Only Interface Extended was enabled; all other mods were disabled during this run. The user re-enabled their collection afterward; this validation does not cover that combination. All five installed DLC were available: Spaced Out!, Frosty Planet, Bionic Booster, Prehistoric Planet and Aquatic Planet. This does not establish base-game-only compatibility.

- Created a separate Survival / Spaced Out / Terrania colony, `InterfaceExtended-TestBaseline`, with cloud saving off. Seed `520304708`; complete settings coordinate `SNDST-C-520304708-0-V9-CKZRH4Y1`. Native story/mixing settings are encoded in that coordinate.
- Saved at cycle 1 with three starting duplicants, no construction orders, no selected object, no modal, initial camera unchanged and simulation paused. Preserved an immutable local copy outside autosave rotation, then explicitly loaded the working save through Load Game after a restart. No repeat world generation was needed.
- Baseline file: 1,115,076 bytes; SHA-256 `396C37A58E33C9E40F843E4E85E515D9D1CF67529B37B181FF4E50D6CDCDC08D`. Save and its environment manifest remain local under `artifacts/test-fixtures`.
- Found clipped numeric input text during the English check. Adjusted the cloned field's viewport/text alignment, rebuilt, installed while ONI was closed and repeated the check after a Steam launch. Digits and caret rendered inside the field; typing `2720` and pressing Enter changed both the effective layout and saved config.
- Final tested DLL SHA-256: `092C0BA4AC1193F907165DD6A791AF32613643DD40E2D342B75436D4AA220F6C`. Built and installed files match. The baseline was originally created with an earlier 1.3.0 build; its manifest retains that creation hash.
- Player.log recorded `Loaded 1.3.0`, nine tracked HUD containers, and the explicit baseline load. No managed mod exception or layout-restoration warning appeared during these interactions.
- English colony settings rendered their translated status, controls and shortcut hint. Full width restored the edge positions; minus from full width produced `4960`, plus returned to `5120`. The `3360`, `2560` and custom `2720` widths applied immediately and persisted to config.
- Boundary guides appeared at both 2560-region margins and disappeared when disabled. Their lack of input interception was inspected in source, not separately tested with a click on the line.
- Done and Escape restored native Options; closing Options and the pause menu kept the initially-paused colony paused. Repeated width changes did not visibly accumulate displacement.
- Clicked the shifted Base construction button, Dig tool, Vitals management button and Oxygen overlay. Each opened or activated its native interface. Selected a duplicant and terrain on both sides outside the central region; the details panel updated within the shifted right side. No orders were placed.
- Captured the same paused colony/camera at full width and 2560, plus English settings, in [README screenshots](README.md#screenshots). Complete local JPEG evidence and step configs are under `artifacts/runtime-1.3.0/isolated`.
- Restored Russian through ONI's language screen and restarted. Native Options showed one translated entry; the Russian settings captions and main-menu preview hint rendered. Width `2560`, guides off and shortcuts off persisted. The native close button restored Options. Left ONI at its Russian main menu.

### Open checks and observed limitations

- Injected Ctrl+Shift+F8/F9 did not trigger actions even with shortcuts enabled. The cause is unresolved; no physical-keyboard pass is claimed. Shortcuts were disabled again. Mouse controls passed.
- The language restart produced a native crash after input/physics shutdown. Its native stack contains UnityPlayer and Steam's `gameoverlayrenderer64`; there is no managed mod stack establishing a cause. The new Russian process started successfully. No mod-disabled comparison was performed, so attribution remains open.
- Settings-save error UI, malformed/unwritable config in game, initially-playing pause restoration, other resolutions/UI scales and long translations remain untested.
- The deterministic game test runner is researched in [testing](docs/testing.md) and is not implemented. This was a manual Computer Use run with screenshots and config evidence.

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
