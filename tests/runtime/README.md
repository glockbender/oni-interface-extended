# In-game regression test

Development harness for ONI U59-744825 on Windows. Its 20-step HUD scenario has passed in ONI with both local and strict baseline profiles, with PNG evidence. The external runner's protocol and evidence checks also have headless tests. Building or preparing a dry run does not install anything or contact the game. Exact runtime results and remaining checks are recorded in [VALIDATION.md](../../VALIDATION.md).

The agent is a separate local mod, excluded from Interface Extended release packages. It uses the existing ONI loader and Unity UI. No network service, arbitrary command execution, browser or Unity project is required. The runner uses Python 3.10+ and only its standard library.

## Prepare without touching ONI

First build the production mod to obtain the pinned compiler. Then build the agent:

```powershell
$env:ONI_GAME_PATH = 'D:\SteamLibrary\steamapps\common\OxygenNotIncluded'
make build
make test-agent
make check
```

Without Make, use `./tasks.ps1 -Task build`, `./tasks.ps1 -Task test-agent` and `./tasks.ps1 -Task check`. `make test-runner` runs only the Python runner tests.

The agent output is `artifacts/InterfaceExtendedTestAgent`. Building it does not copy files into ONI or change enabled mods. Normal `make package` continues to package only the production mod.

Prepare a disposable working save and request without accessing a bridge:

```powershell
python tests/runtime/runner.py `
  --save 'C:\absolute\fixtures\InterfaceExtended-TestBaseline.sav' `
  --bridge 'C:\absolute\mods\Local\InterfaceExtendedTestAgent\bridge' `
  --dry-run
```

Use a dedicated test save, never a personal colony renamed for convenience. The runner requires the exact basename above, preserves the source, verifies the copy's SHA-256 and loads only `fixture/InterfaceExtended-TestBaseline.sav` inside its new run directory. `--dry-run` creates files under the run directory; it does not read or write the bridge, start ONI, load a save or claim any game checks passed. `prepared-request.json` is an inspection artifact; a real invocation creates a new request and working copy.

## One-time game setup, when a restart is available

1. Close ONI. Copy the files from `artifacts/InterfaceExtendedTestAgent` into a separate `InterfaceExtendedTestAgent` folder under ONI's `mods/Local`. Keep the production mod installed separately. Do not merge these folders.
2. Enable **Interface Extended Test Agent (development only)** and Interface Extended, then restart as ONI requests. Select the intended other mods manually. The runner never changes that collection.
3. Reach the main menu, with no loaded colony or open dialogs. Use a rendered desktop at 5120 × 1440, UI scale 110% for the initial profile, and keep ONI focused. It must keep rendering; minimized, headless or unfocused operation is not supported.
4. Run the command below from another terminal. Keep ONI in the foreground during the scenario; give the terminal process time to start before switching focus back. A focus failure produces a failed report, rather than pretending the input worked.

The agent creates its `bridge` directory when loaded. It remains idle until it finds a new request. Remove/disable it after testing; ordinary players do not need it. Keep run evidence local until reviewed: logs and manifests include local paths and the active mod collection.

## Profiles and saved mod lists

The strict `default` profile requires a matching test baseline and stops on any mod-difference dialog. There is no mandatory wait for that popup. A baseline created before installing the agent will include a different mod list because the agent itself is an additional mod.

For the existing baseline and the user's re-enabled collection, use the explicit local gate:

```powershell
python tests/runtime/runner.py `
  --save 'C:\absolute\fixtures\InterfaceExtended-TestBaseline.sav' `
  --bridge 'C:\absolute\mods\Local\InterfaceExtendedTestAgent\bridge' `
  --profile local --allow-mod-difference-dialog
```

This permits the recognized native **MOD DIFFERENCES** notification to be absent. If present, the agent captures it, records the saved/current mod lists and native difference events, and clicks its sole OK button. It checks the localized native title, absent callbacks and hidden alternative actions first. It does not synchronize mods. Unknown dialogs, actions, mod load errors and loading timeouts stop the test. A local pass covers that recorded collection, not an isolated production-mod run.

To prepare a matching strict baseline, manually load a disposable copy with the desired test collection (including the agent), use native Save As while paused, and preserve that as a new profile-specific fixture with its own hash/metadata. This one-time preparation was used for the recorded strict run. Keep the original immutable baseline. Give the new file the required basename in a different fixture directory. Do not regenerate a world on every run or alter a save to hide a mismatch.

```powershell
python tests/runtime/runner.py `
  --save 'C:\absolute\strict-fixture\InterfaceExtended-TestBaseline.sav' `
  --bridge 'C:\absolute\mods\Local\InterfaceExtendedTestAgent\bridge'
```

The agent refuses to replace an already loaded colony. Each invocation therefore starts at the main menu. After a run, the test colony remains loaded and paused; return to the main menu manually before another invocation. No Continue action or personal save is used. The briefly-playing pause test can advance the disposable colony; the next run reloads a fresh copy.

## Assertions and screenshots

The fixed scenario checks:

- One mod entry in native Options, main-menu opening, title-bar closing and restoration of the parent panel.
- Explicit baseline loading, compatible save version/DLC, viewport, saved/active mod list and one baseline pause owner.
- Full width, 2560 and 3360 presets, including settings and unobstructed HUD captures.
- Numeric `2720`, minus from full width and plus back to viewport width, config writes, guide visibility/anchors and non-interception flag, and the shortcut setting toggle.
- All nine named HUD roots: anchors, screen-space corners, unchanged size/position offsets and canvas scale. Expected margins come from independent equations in the test, not calls into production geometry code. At 5120 px, the preset margins are 1280 and 880 px. Tolerances are one screen pixel and 0.00001 for anchors.
- Done/parent restoration, repeated opening and balanced pause ownership for initially-paused and initially-playing cases.
- No new error/exception/assert log entries during the run; restoration of the original in-memory settings and exact config bytes.

Controls are located by object names or native callback/user-data IDs. The driver raycasts at the visible center, checks focus/interactability and the first input receiver, then dispatches Unity pointer events. It fails when an overlay intercepts the click. Main-menu Options, pause-menu setup and save loading use native game methods; those navigation actions are not physical-input tests. Numeric input uses a raycast/focus check followed by a component text edit and end-edit commit. It does not prove hardware keyboard input, Escape or shortcuts work.

Layout readiness requires unchanged measured rectangles over consecutive rendered frames and bounded deadlines, rather than a long fixed sleep. Screenshots follow `WaitForEndOfFrame`; the agent decodes them and the runner independently validates PNG chunks, checksums, compressed scanlines, dimensions and hashes. The HTML gallery displays only validated local images. It supports visual review; it does not automatically decide whether styling, clipping or translation looks correct.

Run output defaults to a fresh `artifacts/runs/<timestamp>-<id>` directory:

| File | Contents |
| --- | --- |
| `manifest.json`, `manifest-game.json` | Request, source/copy save hashes, DLL hashes, locale, viewport, canvas scale, DLC/save header and active mod collection |
| `results.json`, `evidence.json` | Step results, click receivers, measured layout and validated image metadata |
| `*.png`, `layout-*.json` | Rendered steps and actual HUD measurements, including failure evidence where rendering is available |
| `player-log.txt` | Game log messages collected during the scenario |
| `index.html` | Local report with screenshots and assertions |
| `config-before.bin`, `config-recovery.json` | Original config bytes (if the file existed) and restoration status |

Neither these local files nor saves are committed. They are real run evidence when produced in ONI; headless tests use temporary synthetic fixtures and are reported separately.

## Recorded runtime checks

On 2026-10-16, at 5120 × 1440 / 110% UI scale:

| Local run directory | Result |
| --- | --- |
| `20261015T233020Z-48008de9` | Local profile passed: 20 steps, 21 validated PNGs including the recognized mod-difference dialog. |
| `20261015T233250Z-083e6ed5` | Strict/default profile passed: 20 steps, 20 validated PNGs, no mod-difference dialog. |
| `20261015T233421Z-4cc97db4` | Wrong-width negative control returned exit 1 at `preset-2560`: measured left anchor `0.25`, deliberately expected `0.2421875`. Original config bytes were restored. |
| `20261015T234130Z-b7a4e162` | Final agent build, strict/default profile passed: exit 0, 20 steps, 20 validated PNGs. |
| `20261015T234212Z-e59aaa8e` | Final agent build, blocked-control negative returned exit 1 at `preset-2560`: `Preset2560` was blocked by `InterfaceExtendedTestBlocker`. Failure PNG and exact config restoration were recorded. |
| `20261015T234304Z-ffafc8b1` | Final agent build, wrong-width negative returned exit 1 at `preset-2560`, with the same intended anchor mismatch, failure PNG and exact config restoration. |

These runs used 29 active mods including the development agent. They verify the listed HUD scenario for that collection, not an isolated compatibility test or general gameplay. The final agent DLL SHA-256 is `0DCFBBC615E482DDD339B14AC2ADA82C5835ACEE9E07AC0CAC5B7EF298A13FC6`; earlier rows retain their original builds in their manifests. Later agent changes require another run; consult the validation record for full build and recovery details.

## Check that failure detection works

After the first successful game run, repeat from the main menu with one of these flags:

```text
--negative-control wrong-width
--negative-control blocked-control
```

The first deliberately expects 80 extra pixels at the 2560 step; the second adds an input-blocking overlay over that control. Both should fail at the intended assertion/interaction and leave screenshot evidence. A failure during loading does not demonstrate either detector. An unexpected pass is a runner error. Both checks have failed as intended in ONI; the blocked-control result followed the canvas-readiness fix and identifies the blocking object in its error and failure screenshot.

## Failure and recovery

Exit codes: `0` passed (or dry-run prepared), `1` scenario/evidence failed, `2` setup/protocol error, `3` timeout. The default external timeout is 180 seconds; `--timeout` changes it. Loading has its own 90-second deadline. Requests/responses use matching UUIDs and atomic JSON publication. A terminal response is written after cleanup and evidence. Completed bridge exchanges are archived under `bridge/history`; an unfinished request is not overwritten.

If the runner times out, inspect ONI, the run directory and `bridge/status.json`. Timeout does not stop an in-progress game coroutine. Do not immediately submit another run or delete the outstanding request. The agent claims each request durably under `bridge/claims/<id>` before executing it; restarting ONI does not replay an unfinished request or overwrite its recovery backup. Once the game has stopped or the old run has finished, inspect the response and recovery record before manually recovering a stale bridge or lock. Preserve the claim and old run artifacts; submit a new request ID only after recovery.

Normal success and handled failure restore the original settings/config, close menus owned by the test and restore the test's pause ownership. An ordinary component shutdown makes a best-effort cleanup. A process crash, forced termination or disk error can prevent restoration. `config-recovery.json` records the exact config path, whether it originally existed and whether restoration completed. With ONI closed, restore `config-before.bin` to that path if necessary; if no config existed before the run, remove only the newly created file at that recorded path. Review the record before doing either. The baseline remains unchanged; game autosaves may still affect a disposable working colony.

## Coverage still requiring manual work

The harness does not automate construction/tools/overlay clicks, lazy selection panels, tooltip placement, world selection, hardware shortcuts, Escape, language/restart persistence, alternate scales/resolutions, malformed/unwritable config or image baseline comparisons. It does not test gameplay. Existing manual evidence is recorded separately in [VALIDATION.md](../../VALIDATION.md). The complete runs cover the current selectors, modal lifecycle and capture path for their recorded environment; repeat them after agent, game or relevant mod changes.
