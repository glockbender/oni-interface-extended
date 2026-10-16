# Contributing

For bugs, include steps to reproduce, ONI/mod versions, DLC, operating system, resolution, UI scale and other UI mods. A screenshot and relevant log excerpt help identify layout problems. Do not attach a private colony save unless you intend to share it publicly.

Keep pull requests focused on one behavior or documentation correction. Explain the problem, resulting behavior and checks actually run. State any game checks you could not run. AI-assisted changes need the same review and evidence as other changes.

Use four spaces in C# and PowerShell, two in YAML/JSON, UTF-8 and LF line endings. Follow the surrounding C# style and keep comments for game-specific assumptions or non-obvious decisions. Avoid wrappers, dependencies and generic helpers without a concrete use.

Run the checks in the [development guide](docs/development.md). Changes to Harmony targets or prefab paths also need a build against ONI and the relevant in-game regression steps in [testing](docs/testing.md). Keep validation records specific to the exact version tested.

For UI changes, reuse ONI's visual controls and provide screenshots from the game. Do not substitute mockups for runtime evidence. For text changes, update the [translation template and catalogs](docs/translating.md).

The [development game test](tests/runtime/README.md) is a separate mod, excluded from release packages. `make check` includes its external runner tests; `make test-agent` compiles the game-side agent without installing it. Clearly distinguish a prepared request, a headless protocol test and an actual game pass in reports. Record the profile, save/DLL hashes, active collection, step results and screenshots. A strict baseline must match the enabled collection, including the agent. A run with other mods does not certify their individual interactions; Unity event dispatch does not verify hardware keyboard input or gameplay. Negative controls must fail at the intended assertion or blocked click, not during loading.

Do not commit game DLLs, extracted assets, saves, local tool downloads or build output. Follow [AGENTS.md](AGENTS.md) for repository boundaries and migration requirements. The public mod ID is `InterfaceExtended`; `prvz.CenteredHud` identifies older local builds. Their settings are migrated without deleting the original file. Keep README's product sections as the source of [Workshop text](docs/workshop.md).
