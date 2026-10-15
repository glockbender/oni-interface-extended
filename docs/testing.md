# Testing

## Checks available now

Run `make check`, or `./tasks.ps1 check` without Make. Requires PowerShell 7, Python 3.10+ and `python -m pip install -r tests/requirements.txt`.

| Check | What it covers | What it does not prove |
| --- | --- | --- |
| `tests/test-geometry.ps1` | Presets, invalid inputs, scaled minimum, step regression, symmetry, center, stretched anchors and restoration mapping | Actual Unity layout, click targets or rendering |
| `tests/test-options.ps1` | First-install defaults, width bounds, missing shortcut keys and preservation of custom settings | JSON file writes, disk errors or game input |
| `scripts/localization.py` | English/template/catalog consistency, duplicates, stale text, fuzzy/plural entries and placeholders | Native startup order, font coverage or text clipping |
| `tests/test-game-contract.ps1 -GamePath <path>` | Named Harmony targets and reflected fields in installed DLL metadata; mod version and framework references | Harmony runtime injection or prefab hierarchy |

`make build` compiles against your installed ONI with warnings as errors and then runs the game contract check. `make package` does the same and creates `artifacts/InterfaceExtended-<version>.zip`. `make install` also copies to your local mods directory and requires ONI to be closed. `make translations` regenerates the translation template and validates catalogs. Set `ONI_GAME_PATH` for the game-dependent commands. All tasks are also available as `./tasks.ps1 -Task <name>`; `-GamePath` and `-Python` accept explicit paths.

GitHub Actions runs the game-independent checks. It does not have ONI installed, build the mod, or run a colony. No game assemblies are published for CI. See [VALIDATION.md](../VALIDATION.md) for completed runtime checks.

## Manual regression scenario

Use a new test colony, not Continue. Record the exact game build, DLC, complete world/settings coordinate, seed, difficulty, story/mixing settings, language, mod list, mod DLL hash, resolution and UI scale. Keep the camera in one place and pause the colony before comparing layouts.

1. In the main menu, open Settings. Confirm exactly one Interface Extended entry, open it, change a width and close it using the title-bar button. Confirm the native Settings panel returns.
2. Start a fresh colony using the chosen preset and seed. Confirm no mod load/patch exception and that the saved width is applied.
3. From Pause > Settings, open Interface Extended. Exercise full width, 3360 and 2560, then type 2720 in the field and commit it. Check the config and observed layout after each action.
4. Select full width and press minus. At 5120 px and 110% UI scale the requested width should become 4960, not the minimum. Press plus and confirm it returns to 5120.
5. Toggle boundary guides on/off. Check their visibility, positions and lack of mouse interception.
6. Enable shortcuts, verify the displayed combinations, exercise both, then disable them and confirm they stop acting. Reset to 2560, guides off, shortcuts off.
7. Close with Done and separately with Escape. Check the parent panel and pause state. Reopen repeatedly; confirm one window, balanced pause ownership and no accumulated displacement.
8. Click construction, tools, management/overlays and a selection details panel at their displayed positions. Check tooltip placement and world selection on both sides of the central HUD area.
9. Restart and verify persistence. Repeat in English and Russian. Exercise a changed resolution/UI scale and inspect Player.log for new exceptions.

Also test a malformed config and an unwritable config location in a disposable installation. Reading should fall back to defaults; saving should show an error in settings. These paths have not yet been exercised in ONI for 1.3.0.

## Automation research (2026-10-16)

**Recommendation:** use a separate development-only ONI test mod, driven by a small external runner. Keep game-independent checks in ordinary CI. This is a proposed implementation, not an existing automated game suite.

| Approach | Suitability for this mod |
| --- | --- |
| Playwright | Targets browsers. ONI's native Unity UI has no browser DOM to drive. Useful later for viewing an HTML report, not controlling ONI. [Documentation](https://playwright.dev/docs/intro) |
| Unity Test Framework | Supports Edit/Play Mode and player tests built from a Unity project. We have the retail game and a mod, not Klei's Unity project. It is not a drop-in runner for an existing ONI installation. [Documentation](https://docs.unity3d.com/Packages/com.unity.test-framework@1.4/manual/index.html) |
| AltTester | Provides object selectors, waits, input and PNG screenshots. Its documented setup instruments a Unity build with its SDK; without source, it asks for an instrumented build. Injecting it through ONI's mod loader would need a separate compatibility/dependency investigation. It has not been tried here. [Setup](https://alttester.com/docs/sdk/latest/pages/get-started.html), [API overview](https://www.alttester.com/docs/sdk/latest/home.html) |
| OS input and image matching | Can exercise real input without a game-side agent, but coordinates, focus, overlays, timing and translated captions make it more fragile. Useful as a small additional smoke check. |
| ONI test mod | Uses the existing mod loader, Unity hierarchy, UI events and game state. Needs implementation, but avoids rebuilding ONI or adding another injection framework. Best fit for the small set of controls under test. |

Inspection of the installed U59-744825 assemblies confirms these available entry points: `NewGameFlow.BeginFlow()`, `CustomGameSettings.SetQualitySetting(...)`, `CustomGameSettingConfigs.WorldgenSeed`, `GetSettingsCoordinate()`, `SpeedControlScreen.IsPaused`, and the Unity UI event system. The native new-game action uses `BeginFlow`; seed selection goes through game settings. This supports the approach, but does not prove an entire new-game scenario works unattended. World-generation progress, DLC flow and starting-duplicant selection still need an actual prototype.

The settings controls now have locale-independent object names such as `InterfaceExtendedSettings/Panel/Layout/Preset2560`, `FullWidth`, `Width`, `Guides`, `DecreaseWidth` and `Panel/Done`. The native options entry already has the unique user-data ID `prvz.CenteredHud.Options`. A driver can find these without matching visible translated text.

### Proposed first implementation

- Put the test agent in a separate local mod, excluded from normal release packages. An external PowerShell/Python runner supplies a fixed scenario through a local file queue with request IDs and timeouts. No network server or general-purpose code evaluation is needed.
- Perform Unity operations on the main thread. Wait for specific screens, active/interactable controls and stable measured layout over consecutive frames. Use bounded deadlines, not fixed sleeps. Wait for a rendered frame before capture.
- Start New Game through the normal flow, apply an explicit world preset/seed/settings, select starting duplicants and launch. Avoid Continue and existing personal saves. Pause immediately after the HUD is ready. A seed alone does not guarantee identical worlds across ONI versions, DLC or worldgen mods; record the full environment.
- For interaction tests, raycast at the visible target center and dispatch pointer/input events through Unity UI. Fail if another object blocks the target. Invoking `onClick` directly or setting config fields can help setup, but does not count as proof a player can click a control. Add an OS-input smoke scenario to cover game focus and hardware input separately.
- Assert actual container anchors/world-space corners, canvas scale, effective width, active mod window count, config contents and pause state. Compare against independently specified expectations: at a 5120 px viewport, 2560 and 3360 widths have 1280 and 880 px margins before panel-specific offsets. Do not only compare the setting to the same value just assigned by the runner.
- Run both initially-playing and initially-paused cases so a balanced modal pause count is observable after closing all menus. Test a lazy selection panel after it first appears. Deliberately change one expected width and verify that the suite fails; temporarily block a control and verify that the interaction check fails.

### Screenshot evidence

Unity exposes PNG capture through [`ScreenCapture.CaptureScreenshot`](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ScreenCapture.CaptureScreenshot.html). The installed game includes `UnityEngine.ScreenCaptureModule.dll`. Capture after [`WaitForEndOfFrame`](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/WaitForEndOfFrame.html), which follows camera and GUI rendering. Wait for a complete, decodable PNG before marking a capture successful. Use a rendered desktop session for UI tests; do not assume `-batchmode -nographics` can validate HUD rendering.

Each run should produce:

```text
artifacts/runs/<run-id>/
  manifest.json       # Build/DLL hash, environment, preset, scenario version
  results.json        # Steps, actions, assertions, timestamps, pass/fail
  01-full-width.png
  02-2560.png
  03-3360.png
  04-settings.png
  05-after-close.png
  failure-<step>.png  # Capture on failure, when rendering still works
  layout-<step>.json  # Measured rectangles, raycast target, pause state
  player-log.txt      # Relevant log interval
  index.html         # Local gallery linked to step results and measurements
```

This is a proposed output contract, not a fabricated sample run. Store image dimensions and SHA-256 hashes with step records. Include screenshots of the target after each mutation and after close/restore, not just a final image. Screenshots and numerical assertions serve different checks: one catches clipping or unexpected visuals, the other detects hidden state errors.

For visual comparisons, crop to HUD regions and mask world animation, cursor, clock and notifications. Review baseline changes manually with an overlay/diff image; pixel-perfect full-screen comparisons will be noisy. Separate a tightly controlled test profile from a later compatibility run with other mods. Exclude test saves and large run artifacts from Git; retain them as CI artifacts or attach selected evidence to an issue/release.

## Repository screenshots still needed

For the English README, capture the same paused colony and camera at full width and 2560 px, plus the English settings window. Keep 5120 × 1440 / 110% for the first comparison. These must be real 1.3.0 captures after runtime verification. No placeholder images are presented as screenshots.
