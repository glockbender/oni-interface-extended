# Contributing

For bugs, include steps to reproduce, ONI/mod versions, DLC, operating system, resolution, UI scale and other UI mods. A screenshot and relevant log excerpt help identify layout problems. Do not attach a private colony save unless you intend to share it publicly.

Keep pull requests focused on one behavior or documentation correction. Explain the problem, resulting behavior and checks actually run. State any game checks you could not run. AI-assisted changes need the same review and evidence as other changes.

Use four spaces in C# and PowerShell, two in YAML/JSON, UTF-8 and LF line endings. Follow the surrounding C# style and keep comments for game-specific assumptions or non-obvious decisions. Avoid wrappers, dependencies and generic helpers without a concrete use.

Run the checks in [README](README.md#build). Changes to Harmony targets or prefab paths also need a build against ONI and the relevant in-game regression steps in [testing](docs/testing.md). Keep validation records specific to the exact version tested.

For UI changes, reuse ONI's visual controls and provide screenshots from the game. Do not substitute mockups for runtime evidence. For text changes, update the [translation template and catalogs](docs/translating.md).

Do not commit game DLLs, extracted assets, saves, local tool downloads or build output. The original mod ID `prvz.CenteredHud` and installed folder `CenteredHud` must remain compatible unless a migration is provided.
