# Validation

Date: 2026-10-16 (Asia/Nicosia).

## Publication preparation review

- Game-independent checks passed: production geometry/options/config I/O, localization (15 English and Russian entries), 31 runner tests and 13 Workshop exporter tests. Python protocol tests use synthetic fixtures; they do not exercise ONI rendering.
- The test agent now rejects the production HUD's layout-restoration warning; the runner requires its own validated primary PNG for every successful step. The agent rebuilt with warnings as errors and passed the installed-game contract. This later agent build has not been installed or rerun in ONI. Runtime results below identify the earlier binaries actually tested.
- CI uses the shared check task so a failed command cannot be hidden by a later successful one. README/Workshop text, the cover and three-image gallery were reviewed together. The icon was inspected at 512 and 64 pixels; the Steam-rendered item remains unverified.
- No production runtime code or release binary changed during this review. [Review notes](docs/review.md) record findings and remaining limits.

## Unreleased 1.4.0: identity and config storage

- Public static ID is `InterfaceExtended`; ONI's loader reported the new ID and zero broken installations. The local folder/DLL and C# namespaces remain `CenteredHud`; no duplicate installation was created.
- Built against U59-744825 with warnings as errors and the game contract check, including `KMod.Manager.GetDirectory`. Production DLL SHA-256: `7DBE7CF1693824462359C4FE077C07B87AA745172DA31B1FDAA1843E6E21A91D`.
- On the first normal Steam launch, the existing installation-folder config migrated into ONI's `mods/config/InterfaceExtended/config.json`. Source and destination both retained exact SHA-256 `D6AABDA2AA85190A7258AAC8FC1539E1C0FCC492C724C9A553CF176B5786F257`. The legacy file was left untouched. A second normal launch read the shared config successfully.
- The fixed HUD scenario passed all 20 steps with 20 independently validated PNGs in local run `artifacts/runs/20261016T000221Z-049c56a6`. Windows, U59-744825-SCRPAN, Russian, 5120 × 1440, 110% UI scale, all five installed DLC and 29 active mods including the development agent. The local profile allowed the known mod-difference gate, but no dialog appeared in this run. This is not a base-game-only or isolated compatibility pass.
- Agent DLL SHA-256: `95EC53609E5B0DD6694AE9B99399F0649D7C002166754386F366B1E45C50DF99`. Its native Options selector uses the new ID. Russian settings were visually inspected in the main-menu capture; translations still load from the installation directory, independently of config storage. The scenario verified writes at the new shared config path and restored its original bytes afterward.
- An earlier attempt (`20261016T000012Z-f121ac4b`) stopped at `preset-2560` because a Windows reader briefly denied replacement of the test bridge's status JSON. Cleanup restored config successfully. The agent now retries only sharing/lock violations up to five times at 10 ms intervals; the complete pass above used that build. This was a test-protocol failure, not a production config failure.
- Filesystem regression checks use production config/options code and PowerShell's bundled Json.NET without ONI. They cover fresh defaults, missing parent creation, install/local migration precedence, exact bytes/BOM/unknown-key preservation, existing or malformed shared config precedence, invalid/null legacy JSON, failed migration with usable legacy settings, locked-file replacement failure and preservation across an installation update. These do not exercise ONI's save-error UI.
- Workshop description and ordered image manifest are generated from README. At this storage-validation checkpoint, nine standard-library tests and source/staleness validation passed; later checks are recorded above. These checks do not upload to Steam or validate the rendered Workshop page.
- The package task passed all checks and produced `InterfaceExtended-1.4.0.zip`. Its six entries contain only the production DLL, two metadata files, MIT license and two translation files. No default config, game assemblies or test agent is shipped. The packaged DLL hash matches the installed runtime-tested binary.
- After testing, ONI was closed and only the development agent was disabled. The original 28 enabled mods match after mapping the requested ID change; the original fixture and both config files remain unchanged. Local migration/cleanup evidence and logs are under `artifacts/config-1.4.0`.

The first Steam Workshop delivery/update, base-game-only runtime, other operating systems, physical shortcuts and in-game config error UI remain untested. Earlier screenshots and runtime results below retain their original version labels.

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
- Settings-save error UI, malformed/unwritable config in game, other resolutions/UI scales and long translations remain untested. Initially-playing pause restoration passed in the agent scenario below.
- The manual run above used Computer Use with screenshots and config evidence. Agent-driven checks are recorded separately below.

## Development test harness: game runs

The separate test mod and standard-library Python runner are under [tests/runtime](tests/runtime/README.md). At this runtime checkpoint, the agent compiled with warnings as errors against U59-744825 and 29 Python protocol/evidence tests passed alongside geometry/options/localization checks. Those Python tests used synthetic saves, bridge responses and PNGs; actual game runs are listed below. Later harness changes and check counts are recorded above.

After the user handed over control, the agent was installed as a separate development mod. The user's 28 enabled mods were retained; adding the agent made 29. Runs used Windows, U59-744825-SCRPAN / Unity 6000.3.5f2, Russian, 5120 × 1440, UI scale 110% (canvas scale 1.22100008), with all five installed DLC. This is coverage of the recorded scenario with that collection, not individual compatibility certification or an isolated run.

| Run directory under `artifacts/runs` (UTC) | Profile / control | Result |
| --- | --- | --- |
| `20261015T233020Z-48008de9` | Local, explicit native mod-difference gate | Passed: 20 steps, 21 PNGs including the dialog |
| `20261015T233250Z-083e6ed5` | Default, matching fixture | Passed: 20 steps, 20 PNGs, no mod-difference dialog |
| `20261015T233421Z-4cc97db4` | Wrong-width negative | Expected exit 1 at `preset-2560`: actual anchor 0.25 vs expected 0.2421875 |
| `20261015T234130Z-b7a4e162` | Final agent, default profile | Passed: 20 steps, 20 validated PNGs, no mod-difference dialog |
| `20261015T234212Z-e59aaa8e` | Final agent, blocked-control negative | Expected exit 1 at `preset-2560`: first raycast hit was `InterfaceExtendedTestBlocker`, not `Preset2560`; `failure-06.png` captured before cleanup |
| `20261015T234304Z-ffafc8b1` | Final agent, wrong-width negative | Expected exit 1 at `preset-2560`, same anchor mismatch as above; failure screenshot preserved |

The first three runs used agent SHA-256 `FFAF73B1F3DA7A3558209B604BC3E714410C9370F4F719A1FD9A5192BCD55F3B`. The final three used `0DCFBBC615E482DDD339B14AC2ADA82C5835ACEE9E07AC0CAC5B7EF298A13FC6`, after adding rendered-frame readiness for the negative control's blocking canvas. Production DLL SHA-256 remained `092C0BA4AC1193F907165DD6A791AF32613643DD40E2D342B75436D4AA220F6C`.

The scenario checks native Options entry/closing, explicit loading of a fresh working copy, full/2560/3360/custom 2720 widths, minus/plus, config persistence, guides, shortcut setting, repeated opening, and paused/playing pause ownership. All nine HUD roots are measured against independent expected anchors, screen corners and preserved offsets. Width changes restored full-width positions without cumulative displacement. Numeric text, including TMP's trailing zero-width sentinel, stayed inside its viewport. No error/exception/assert log was captured in the passing scenarios.

The original immutable baseline has the hash recorded above. For the default profile, native Save As on a disposable copy created a separate matching fixture with the agent enabled; SHA-256 `479EF2EEFFDC55AFACB9E12A8D7424F16F61BBF604609AB19EEFC5117C8BCB6E`. The original fixture was preserved. Each run copied and verified its source before loading only that working copy. Successful and handled-failure runs restored the original config bytes, SHA-256 `D6AABDA2AA85190A7258AAC8FC1539E1C0FCC492C724C9A553CF176B5786F257`.

Local reports contain the HTML screenshot gallery, JSON measurements, DLL/save hashes, active/saved mod lists, logs, config backup and recovery record. The runner independently checks complete PNG chunks, CRC, decompression, dimensions and hashes before declaring a pass. Screenshots and saves remain local; they are excluded from source and release packages.

Pointer tests use Unity EventSystem raycasts and pointer events; numeric commit uses the native input component. They do not prove hardware keyboard/hotkey handling, Escape behavior, every gameplay panel or long-session stability. The agent does not synchronize mods, generate a world, use Continue, or load a personal colony.

An intermediate agent run caught a title-bar selector mismatch (`TitleBar(Clone)`); another caught TMP's trailing zero-width sentinel in the display-text assertion. Both were corrected before the complete passes. The first blocked-control attempt failed on the later Done button because the new canvas had not registered for the initial preset click; the final run above verifies the intended first interaction instead. An ONI boot interrupted while replacing the agent caused a native safe-mode notice on the next launch; exiting from the main menu and launching normally restored the enabled collection before the final runs. No automatic restart or safe-mode dismissal is part of the harness.

After the final negative control, ONI was closed and only the test agent was disabled. The 28 enabled pre-test mods match the original collection; all other mod entries are structurally unchanged. Production DLL, exact config bytes and both source fixture hashes were verified unchanged. Local cleanup evidence is in `artifacts/runtime-agent-setup/cleanup.json`.

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
