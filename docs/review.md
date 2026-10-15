# Code and documentation review

Reviewed 2026-10-16 for the initial public repository and the unreleased 1.3.0 source.

## Scope and findings

Reviewed every tracked C# source file, mod metadata/default config, PowerShell/Python/Make build and check scripts, translation catalogs, workflow, README, contribution guidance and validation record. Also inspected relevant installed-game types without running game code.

| Finding | Resolution |
| --- | --- |
| Russian captions embedded in C# and in English mod metadata | English `LocString` source, ONI localization registration, Russian `.po` and checked template. |
| Minus from full width used the stored zero instead of the displayed width | Step from effective width and respect the viewport/scaled minimum; regression cases cover it. |
| Status text formatted every frame | Refresh only when width, resolution or settings change. |
| Config save errors only appeared in logs | Display a localized save-error hint; retrying a successful save clears the flag. |
| Invalid shortcut names could disagree with the displayed hint | Normalize missing/unknown keys to defaults and canonicalize valid KeyCode names when loading config. |
| Private game methods and fields could change without a clear diagnostic | Add a metadata contract check after builds, including inherited menu fields. Runtime prefab checks still need ONI. |
| Build depended on one developer's fixed Steam path | Accept `ONI_GAME_PATH` or explicit `-GamePath`; fail with a specific setup instruction. |
| Packaging could include stale output or research files | Clean the generated build directory with path/link checks before compilation; ZIP contains only the mod DLL, metadata, default config, license and shipped translations. |
| Documentation mixed historical plans and current instructions | Remove the completed plan; separate README, changelog, translation/testing instructions and versioned validation. |
| No basic repository workflow | Add Make/PowerShell task entry points, headless checks, CI, MIT license, issue/PR templates and formatting rules. |
| Duplicate native clone names would make future UI selectors ambiguous | Give mod-owned controls stable names independent of language. |

The original namespace/DLL name, static ID and config location are retained intentionally for upgrade compatibility. Dead `Reload` and root-count members were removed. Geometry and option normalization remain independent of Unity so their checks use the actual production code.

## Mod repository conventions

Compared [Peter Han's ONIMods](https://github.com/peterhaneve/ONIMods) and [Sgt Imalas' ONI mods](https://github.com/Sgt-Imalas/Sgt_Imalas-Oni-Mods). Their documentation makes installation, game/DLC compatibility, build prerequisites and binary/source distribution visible. This repository uses those conventions at the scale of a single mod: explicit tested game build, local installation, separate source and packaged output, build instructions against a locally installed game, translation files and a validation record. It does not copy their multi-mod build infrastructure or claim their compatibility coverage.

## Anti-slop review

Used the requested [article](https://www.opensourceprojects.dev/post/anti-slop) and its linked [Anti Slop rules](https://github.com/miqdadbadjuber/anti-slop) as review references. No agent plugin was installed. This is a source/documentation audit, not a claim of full conformance to every web-oriented rule.

- **Content:** descriptions state the one implemented feature and its controls. No invented metrics, testimonials, performance claims, future-feature advertising, decorative badges or mock screenshots.
- **UI direction:** use ONI's title bar, buttons, fonts, checkbox rows and input field because the window is part of ONI Settings. Boundary lines exist only as an optional layout guide. No new visual theme or generated artwork.
- **Code:** comments explain anchor preservation, native modal ownership, locale codes or game-specific assumptions. No decorative banners, per-line narration or generic abstraction layer.
- **Function:** existing 1.2.0 mouse interactions have runtime evidence. The new 1.3.0 captions/error state and refactored width steps still require game checks. A successful build is not recorded as a passed click-through.
- **Accessibility/layout:** Escape closes the modal and the input supports typing. Full keyboard traversal, contrast measurements, long translations and alternate resolutions have not been verified. Native styling alone is not proof of accessibility.

## Remaining risks

- Harmony patches and prefab paths depend on ONI internals; a game update can change them.
- Mods that alter the same anchors or modal hierarchy can interact with this mod.
- Automated geometry/options/catalog checks do not validate Unity rendering or mouse hit testing.
- English/Russian 1.3.0 runtime checks, new-game regression, screenshot evidence and a deterministic game-side test runner are still pending. No public release is claimed ready on the strength of static checks alone.
