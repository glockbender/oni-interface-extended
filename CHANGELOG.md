# Changelog

## 1.4.0 (unreleased)

- Use `InterfaceExtended` as the public mod ID; replace the earlier local copy when updating.
- Store settings in ONI's `mods/config/InterfaceExtended/config.json` so installation updates cannot replace them. Import a legacy config only if the shared file is absent; preserve its source and report migration write failures.
- Load bundled translations from the installation directory independently of config storage.
- Stop shipping a default config; defaults come from the options class.
- Generate Workshop text and the screenshot upload list from README, with checks for unsupported syntax and stale output.
- Add repository working instructions and filesystem regression checks for migration and update persistence.
- Retry transient Windows sharing/lock errors when the test agent publishes bridge JSON.

## 1.3.0 (unreleased)

- Use English as the source language for all mod UI text.
- Register strings with ONI's localization system; include a Russian catalog and a translation template.
- Make width steps start from the effective HUD width, including when leaving full-width mode.
- Fix clipped digits in the native numeric field when used at the settings row height.
- Refresh status text when values change instead of formatting it every frame.
- Display a settings-save error in the window if writing the config fails.
- Add assembly version metadata, build instructions, repository checks and contribution templates.
- Add a separate development test agent and Python runner with HUD assertions, screenshot reports and headless protocol tests. The 20-step scenario passed in ONI with both local and strict baseline profiles; a deliberate wrong-width assertion failed as expected. See the validation record for the tested collection and remaining coverage.

## 1.2.0 (local build, 2026-10-16)

- Rename the mod to Interface Extended and the Russian menu entry to Расширенный интерфейс.
- Build settings with ONI's native UI prefabs, including the numeric input and checkboxes.
- Keep the original mod ID, DLL name and configuration path for existing installations.

## 1.1.0 (local build, 2026-10-16)

- Add settings to ONI's native Settings menu in both the main menu and a loaded colony.
- Make keyboard shortcuts optional and disabled by default.

## 1.0.0 (local build)

- Constrain HUD containers to a centered width without changing the world viewport.
- Add presets, a custom width, boundary guides and restoration to full width.
