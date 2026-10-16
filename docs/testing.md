# Testing

## Checks available now

Run `make check`, or `./tasks.ps1 check` without Make. Requires PowerShell 7, Python 3.10+ and `python -m pip install -r tests/requirements.txt`.

| Check | What it covers | What it does not prove |
| --- | --- | --- |
| `tests/test-geometry.ps1` | Presets, invalid inputs, scaled minimum, step regression, symmetry, center, stretched anchors and restoration mapping | Actual Unity layout, click targets or rendering |
| `tests/test-options.ps1` | First-install defaults, width bounds, missing shortcut keys and preservation of custom settings | JSON file writes, disk errors or game input |
| `tests/test-config.ps1` | Actual config I/O: migration bytes/precedence, invalid data, write failure, atomic replacement and preservation across installation updates | ONI startup, native save-error UI or other operating systems |
| `scripts/workshop.py --check`, `tests/test_workshop.py` | README section selection, BBCode conversion, image references and stale export detection | Steam upload, owner controls or gallery rendering |
| `scripts/localization.py` | English/template/catalog consistency, duplicates, stale text, fuzzy/plural entries and placeholders | Native startup order, font coverage or text clipping |
| `tests/runtime/test_runner.py` | Request correlation, timeouts, baseline preservation, safe HTML paths, PNG validation and failed-evidence handling | Actual Unity events, rendering or any colony scenario |
| `tests/test-game-contract.ps1 -GamePath <path>` | Named Harmony targets and reflected fields in installed DLL metadata; mod version and framework references | Harmony runtime injection or prefab hierarchy |

`make build` compiles against your installed ONI with warnings as errors and then runs the game contract check. `make package` does the same and creates `artifacts/InterfaceExtended-<version>.zip`. `make install` also copies to your local mods directory and requires ONI to be closed. `make translations` regenerates the translation template and validates catalogs. `make test-agent` builds the separate development agent without installing it; `make test-runner` runs its headless checks alone. Set `ONI_GAME_PATH` for the game-dependent commands. All tasks are also available as `./tasks.ps1 -Task <name>`; `-GamePath` and `-Python` accept explicit paths.

`make workshop` generates description text and a screenshot-upload manifest from README. This prepares local files only; it does not publish. See [Workshop preparation](workshop.md). The config suite runs in its own PowerShell process to compile production config/options code against the host's bundled Json.NET without ONI or duplicate type definitions from the options suite.

GitHub Actions runs the game-independent checks. It does not have ONI installed, build the mod, or run a colony. No game assemblies are published for CI. See [VALIDATION.md](../VALIDATION.md) for completed runtime checks.

## Manual regression scenario

Create a dedicated test colony once and save it as `InterfaceExtended-TestBaseline`. Start routine runs by explicitly loading a working copy of that save through Load Game, rather than Continue or generating another world. Preserve the baseline outside the game's autosave rotation. Record its SHA-256 hash, exact game build, DLC, complete world/settings coordinate, seed, difficulty, story/mixing settings, language, mod list, mod DLL hash, resolution and UI scale. Keep the camera in one place and pause the colony before comparing layouts.

1. In the main menu, open Options. Confirm exactly one Interface Extended entry, open it, change a width and close it using the title-bar button. Confirm the native Options panel returns.
2. Load the baseline test save. Confirm no mod load/patch exception and that the saved HUD width is applied. Test fresh-world creation separately when it is relevant; it is not required for each HUD regression run.
3. From Pause > Options, open Interface Extended. Exercise full width, 3360 and 2560, then type 2720 in the field and commit it. Check the config and observed layout after each action.
4. Select full width and press minus. At 5120 px and 110% UI scale the requested width should become 4960, not the minimum. Press plus and confirm it returns to 5120.
5. Toggle boundary guides on/off. Check their visibility, positions and lack of mouse interception.
6. Enable shortcuts, verify the displayed combinations, exercise both, then disable them and confirm they stop acting. Reset to 2560, guides off, shortcuts off.
7. Close with Done and separately with Escape. Check the parent panel and pause state. Reopen repeatedly; confirm one window, balanced pause ownership and no accumulated displacement.
8. Click construction, tools, management/overlays and a selection details panel at their displayed positions. Check tooltip placement and world selection on both sides of the central HUD area.
9. Restart and verify persistence. Repeat in English and Russian. Exercise a changed resolution/UI scale and inspect Player.log for new exceptions.

### Optional mod-difference gate

The default profile requires a matching fixture and stops on a mod-difference dialog. The explicit local profile permits the recognized dialog when testing the user's collection; it never synchronizes mods. See [profiles and saved mod lists](../tests/runtime/README.md#profiles-and-saved-mod-lists) for commands, dialog checks and one-time matching-fixture setup. Record the actual active collection, and preserve the user's enabled mods unless explicitly asked to change them. A mixed-mod pass cannot be reported as an isolated compatibility check.

Also test a malformed config and an unwritable config location in a disposable installation. Reading should fall back to defaults; saving should show an error in settings. These paths have not yet been exercised in ONI for 1.3.0.

## Automation research (2026-10-16)

**Implementation status:** the separate development-only ONI test mod and external runner have completed the 20-step HUD scenario in local and strict profiles, with screenshots and measured assertions. The runner also has headless protocol/evidence tests. The recorded collection contained 29 mods including the agent; these runs do not certify all mod combinations or gameplay. [Setup, commands, assertions and recovery](../tests/runtime/README.md) document the implementation; [validation](../VALIDATION.md) identifies the exact runs and outstanding checks.

| Approach | Suitability for this mod |
| --- | --- |
| Playwright | Targets browsers. ONI's native Unity UI has no browser DOM to drive. Useful later for viewing an HTML report, not controlling ONI. [Documentation](https://playwright.dev/docs/intro) |
| Unity Test Framework | Supports Edit/Play Mode and player tests built from a Unity project. We have the retail game and a mod, not Klei's Unity project. It is not a drop-in runner for an existing ONI installation. [Documentation](https://docs.unity3d.com/Packages/com.unity.test-framework@1.4/manual/index.html) |
| AltTester | Provides object selectors, waits, input and PNG screenshots. Its documented setup instruments a Unity build with its SDK; without source, it asks for an instrumented build. Injecting it through ONI's mod loader would need a separate compatibility/dependency investigation. It has not been tried here. [Setup](https://alttester.com/docs/sdk/latest/pages/get-started.html), [API overview](https://www.alttester.com/docs/sdk/latest/home.html) |
| OS input and image matching | Can exercise real input without a game-side agent, but coordinates, focus, overlays, timing and translated captions make it more fragile. Useful as a small additional smoke check. |
| ONI test mod | Uses the existing mod loader, Unity hierarchy, UI events and game state. The separate agent has completed local and strict HUD regression scenarios. Avoids rebuilding ONI or adding another injection framework. |

Routine HUD tests load a disposable copy of a dedicated baseline through the native load path. Automated world generation, DLC selection and starting-duplicant selection are outside this implementation; creating a new world is a separate check when relevant.

The settings controls have locale-independent object names such as `InterfaceExtendedSettings/Panel/Layout/Preset2560`, `FullWidth`, `Width`, `Guides`, `DecreaseWidth` and `Panel/Done`. In 1.4.0 the native options entry uses the unique user-data ID `InterfaceExtended.Options` (previously `prvz.CenteredHud.Options`). A driver can find these without matching visible translated text.

### Implemented prototype and remaining coverage

- The separate local test mod is excluded from release packaging. A standard-library Python runner supplies one fixed scenario through atomic JSON requests with IDs and timeouts. There is no network listener or general code-evaluation endpoint.
- Unity operations run on the main thread with screen/readiness checks, bounded waits and stable measured layout over consecutive rendered frames. The scenario loads a disposable baseline copy explicitly, preserving the original hash. It refuses to replace a loaded colony and never edits the enabled mod list.
- Controls under test receive pointer events after a center raycast proves that the expected control is the first input receiver. Setup navigation/load actions use native methods. Numeric editing commits through the input component; this is not a physical keyboard test.
- Independent equations check the nine named HUD roots' anchors, screen corners and preserved offsets. The scenario checks preset/custom/step widths, guides, config persistence, parent modal restoration and paused/playing pause ownership. The wrong-width negative control failed at its intended assertion; after the canvas-readiness adjustment, the blocked-control check failed at the intended intercepted click. Validation identifies the binaries used for these controls and each complete scenario.
- Construction/tools/overlay interactions, lazy selection panels, tooltips, world selection, hardware keyboard input, language/restart checks and alternate resolutions remain outside this prototype. A separate manual regression scenario above covers that broader scope.

### Screenshot evidence

Unity exposes PNG capture through [`ScreenCapture.CaptureScreenshot`](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ScreenCapture.CaptureScreenshot.html). The installed game includes `UnityEngine.ScreenCaptureModule.dll`. Capture after [`WaitForEndOfFrame`](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/WaitForEndOfFrame.html), which follows camera and GUI rendering. Wait for a complete, decodable PNG before marking a capture successful. Use a rendered desktop session for UI tests; do not assume `-batchmode -nographics` can validate HUD rendering.

Each invocation writes a separate local run directory. The [runner reference](../tests/runtime/README.md#assertions-and-screenshots) owns the output file definitions, image-validation requirements and gallery format. Every successful step requires a validated primary PNG. Settings, unobstructed HUD, optional-dialog and failure captures support human review alongside numerical assertions; automatic visual baseline comparison is not implemented. Exact completed runs belong in [validation](../VALIDATION.md).

The baseline should be a small, unbuilt cycle-1 colony, paused with a fixed camera and no modal or selected object. Keep an immutable original plus a working copy; test actions and autosaves may change the latter. Reset mod config separately because ONI saves do not contain this mod's settings. Use a separate baseline per game/DLC profile and revalidate it after an ONI update rather than silently replacing it. Save files remain local and are ignored by Git.

For visual comparisons, crop to HUD regions and mask world animation, cursor, clock and notifications. Review baseline changes manually with an overlay/diff image; pixel-perfect full-screen comparisons will be noisy. Separate a tightly controlled test profile from a later compatibility run with other mods. Exclude test saves and large run artifacts from Git; retain them as CI artifacts or attach selected evidence to an issue/release.

Before sharing evidence, inspect screenshots and metadata for account details, personal save names, paths and overlays. Main-menu captures can reveal the last colony through Continue even when the scenario loads a dedicated baseline. Local JSON manifests and logs contain filesystem paths and mod lists; review and redact a separate sharing copy rather than changing the original evidence.

## Recorded manual run

The 2026-10-16 manual run created `InterfaceExtended-TestBaseline`, preserved an immutable local copy and explicitly loaded the save after restarting ONI. Only Interface Extended was enabled. [Validation](../VALIDATION.md) records the environment, hashes, passed interactions and unresolved checks.

Selected unedited 1.3.0 captures are committed in [images](images): full width and 2560 from the same paused camera, plus English and Russian settings. The complete local evidence is in `artifacts/runtime-1.3.0/isolated`: JPEG captures, per-step timestamps/config, image hashes, a manifest and an HTML gallery. It records Computer Use actions and human visual assessment; it is not a deterministic runner or a set of machine-asserted Unity measurements. Saves and full logs remain local.
