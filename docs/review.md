# Code and documentation review

Reviewed 2026-10-16 for the initial public repository; repeated after the unreleased 1.4.0 storage changes and Workshop preparation.

## Scope and findings

Reviewed every tracked C# source file, mod metadata, PowerShell/Python/Make build and check scripts, translation catalogs, workflow and repository documentation. Inspected relevant installed-game types; runtime verification is recorded separately in [validation](../VALIDATION.md).

| Finding | Resolution |
| --- | --- |
| Russian captions embedded in C# and in English mod metadata | English `LocString` source, ONI localization registration, Russian `.po` and checked template. |
| Minus from full width used the stored zero instead of the displayed width | Step from effective width and respect the viewport/scaled minimum; regression cases cover it. |
| Status text formatted every frame | Refresh only when width, resolution or settings change. |
| Config save errors only appeared in logs | Display a localized save-error hint; retrying a successful save clears the flag. |
| Invalid shortcut names could disagree with the displayed hint | Normalize missing/unknown keys to defaults and canonicalize valid KeyCode names when loading config. |
| Private game methods and fields could change without a clear diagnostic | Add a metadata contract check after builds, including inherited menu fields. Runtime prefab checks still need ONI. |
| Build depended on one developer's fixed Steam path | Accept `ONI_GAME_PATH` or explicit `-GamePath`; fail with a specific setup instruction. |
| Packaging could include stale output or research files | Clean the generated build directory with path/link checks before compilation; the current ZIP contains only the mod DLL, metadata, license and shipped translations. |
| Documentation mixed historical plans and current instructions | Remove the completed plan; separate README, changelog, translation/testing instructions and versioned validation. |
| No basic repository workflow | Add Make/PowerShell task entry points, headless checks, CI, MIT license, issue/PR templates and formatting rules. |
| Duplicate native clone names would make future UI selectors ambiguous | Give mod-owned controls stable names independent of language. |
| Native filename-input text insets clipped digits in the shorter settings row | Fit the text viewport to the row and align the text vertically; verified typing and committing 2720 in ONI. |

The original namespace/DLL name is retained. Version 1.4.0 adopts public ID `InterfaceExtended` and moves settings outside the replaceable installation folder, with byte-preserving legacy migration and precedence checks. Dead `Reload` and root-count members were removed. Geometry, option normalization and config I/O checks use the actual production code without Unity.

## Publication preparation follow-up

An independent second code review found two gaps in the development harness: a successful step could lack its own screenshot, and the production HUD's caught layout-error warning could escape the error collector. The runner now requires a validated primary PNG for every successful step; the agent treats that specific warning as failure. Regression tests cover missing and dialog-only evidence while preserving the original error for an already-failed step. The rebuilt agent has not been rerun in ONI; previous runtime results keep their original binary hashes.

CI now delegates to the shared check task, which propagates each command's failure. This removes a multi-command step that could hide a failed unittest invocation. Make targets share their identical recipe. The Workshop exporter validates a separate 512 × 512 cover, preserves the three README gallery images in order, and detects stale exports.

README now contains player-facing information. Build/config details live in the development guide; runtime setup and artifact definitions live in the harness reference; exact run/hash history lives only in validation. Russian support is mentioned once in README's Languages section. The translated settings capture remains archived evidence, outside the product gallery.

## Mod repository conventions

Compared [Peter Han's ONIMods](https://github.com/peterhaneve/ONIMods) and [Sgt Imalas' ONI mods](https://github.com/Sgt-Imalas/Sgt_Imalas-Oni-Mods). Their documentation makes installation, game/DLC compatibility, build prerequisites and binary/source distribution visible. This repository uses those conventions at the scale of a single mod: explicit tested game build, local installation, separate source and packaged output, build instructions against a locally installed game, translation files and a validation record. It does not copy their multi-mod build infrastructure or claim their compatibility coverage.

## Anti-slop review

Used the requested [article](https://www.opensourceprojects.dev/post/anti-slop) and its linked [Anti Slop rules](https://github.com/miqdadbadjuber/anti-slop) as review references. No agent plugin was installed. This is a source/documentation audit, not a claim of full conformance to every web-oriented rule.

- **Content:** descriptions state the one implemented feature and its controls. No invented metrics, testimonials, performance claims, future-feature advertising, decorative badges or mock screenshots.
- **UI direction:** use ONI's title bar, buttons, fonts, checkbox rows and input field because the window is part of ONI Settings. Boundary lines exist only as an optional layout guide.
- **Cover:** an original editable SVG depicts a wide screen, four HUD blocks and inward arrows. The same PNG serves README and Workshop. Inspected at 512 × 512 and 64 × 64: the frame, panels and direction remain distinguishable. No borrowed game assets, mock interface, decorative typography, gradients, glow or unrelated symbols. This is a visual review, not a measured accessibility certification.
- **Code:** comments explain anchor preservation, native modal ownership, locale codes or game-specific assumptions. No decorative banners, per-line narration or generic abstraction layer.
- **Function:** 1.3.0 mouse interactions, English/Russian captions, width steps and baseline loading have runtime evidence and real screenshots. Save-error UI and optional keyboard shortcuts remain unverified. A successful build is not recorded as a passed click-through.
- **Accessibility/layout:** Escape closes the modal and the input supports typing. Full keyboard traversal, contrast measurements, long translations and alternate resolutions have not been verified. Native styling alone is not proof of accessibility.

## Remaining risks

- Harmony patches and prefab paths depend on ONI internals; a game update can change them.
- Mods that alter the same anchors or modal hierarchy can interact with this mod.
- Headless geometry/options/catalog checks do not validate Unity rendering or mouse hit testing. The separate game agent now has complete local and strict runs with measured layout, raycast checks and PNG evidence, including initially-playing pause restoration. Its wrong-width negative control failed at the intended assertion.
- Other resolutions/scales, save-error UI and physical shortcuts still need runtime checks. The agent uses Unity events/component commits, not hardware keyboard input, and does not test gameplay. A native shutdown crash occurred during the language restart; its cause is unconfirmed. See [validation](../VALIDATION.md).
- Agent runs used 29 active mods including the development agent. They cover the recorded scenarios, not every interaction within that collection. Strict and negative-control results apply to the recorded binaries, not later harness changes. Exact builds and runs are in the validation record.
