# Working on Interface Extended

Interface Extended is an Oxygen Not Included UI mod. Keep changes focused on the
player's requested behavior and compatible with the retail game's runtime.

## Sources of truth

- README owns the English product description, usage, compatibility limits and
  support links. Generate Workshop text from it; do not maintain a second copy.
- [Publishing](docs/publishing.md) owns the Workshop item identity and verified
  upload/recovery procedure. Update that item rather than creating a replacement.
- Runtime source owns behavior and defaults. Mod metadata and assembly versions
  must agree. The public mod ID is `InterfaceExtended`; `prvz.CenteredHud` is a
  legacy local identity. Existing C# namespaces and DLL names are implementation
  details, not a reason for unrelated renaming.
- Translation catalogs follow the English game strings. Use the localization
  tooling described in [translation instructions](docs/translating.md).
- [VALIDATION.md](VALIDATION.md) records what actually ran, against which build,
  and what remains unverified. Preserve historical results when behavior changes.

## Project boundaries

Runtime code, distributable metadata/translations, development scripts, tests and
documentation are kept separately. Build outputs and local research belong in
ignored directories. Consult the task entry points and existing code instead of
adding a second build system or a detailed directory inventory here.

Do not commit or package game assemblies, extracted game assets, saves, personal
logs, local tool downloads or the development test agent in a player release.
Release contents use an explicit allowlist. User settings live outside the mod's
replaceable installation folder; packaging must not reset them.

## Implementation

- Reuse ONI's native UI controls, styling and localization. Inspect the installed
  game's APIs and prefabs before relying on names or signatures.
- Build against ONI's framework/Unity assemblies, not the host .NET runtime. Add
  no external runtime dependency without a concrete need.
- Prefer small, direct changes. Avoid speculative abstractions, duplicated
  documentation, comments that restate code, and claims unsupported by evidence.
- Preserve unrelated work and user settings. Identity, config or storage changes
  need explicit migration behavior and regression checks; never overwrite a
  newer config with a legacy one.
- Follow the existing style and `.editorconfig`. Public documentation and source
  UI text are English; bundled translations belong in catalogs.

## Workflow and evidence

Use the Make targets or equivalent PowerShell tasks documented in
[development guide](docs/development.md#build). Make is optional. `check` runs game-independent checks;
`build` checks the installed game contract; `package` validates release contents.
Read [CONTRIBUTING.md](CONTRIBUTING.md) for review expectations and
[testing](docs/testing.md) for game verification.

Add meaningful regression coverage for behavior changes, especially migration,
error handling and geometry. Do not add tests that only mirror implementation.
Run the relevant checks once, then broaden testing when failures or remaining
uncertainty justify it. Keep generated documentation in sync and check the diff
for whitespace errors before committing.

Build/check tasks must not install mods, launch ONI or alter the active collection.
When game work is authorized, close ONI before replacing DLLs, preserve the user's
mod collection, and use only disposable copies of a dedicated test save. Follow
the [runtime harness instructions](tests/runtime/README.md), including interrupted
request recovery. Disable the development agent after testing.

Distinguish build/headless checks, prepared requests and actual game runs. Record
the tested binary, environment and limitations. Use real in-game screenshots for
UI evidence; never present mockups or synthetic test PNGs as game captures. A
mixed-mod pass covers its scenario, not every interaction in the collection.

Support is handled on GitHub. Workshop text should direct users there; it must
not promise Steam comment support. Publishing binaries or changing remote
settings should stay within the user's authorized scope.
