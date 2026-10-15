# Translating Interface Extended

The source language is English. All game-visible captions and status messages are `LocString` fields in `src/CenteredHud/Strings.cs`. The mod registers them after `Localization.Initialize` and loads a catalog with ONI's own `.po` reader and `OverloadStrings` method. PLib is not required.

## Add a language

1. Copy `mod/translations/interface_extended.pot` to `mod/translations/<locale>.po`.
2. Fill the `Language` header and `msgstr` values using a text editor or gettext editor such as Poedit.
3. Keep `msgctxt` and `msgid` unchanged. Preserve format placeholders exactly, including `{1:0}`. You may reorder placeholders and translate surrounding text. `\n` is a line break.
4. Install the file in the mod's `translations` directory, select the corresponding game language and restart ONI.
5. Check the menu entry, all settings controls and both the main-menu and colony status messages. Report clipping with resolution and UI scale.

The filename matches `Localization.GetLocale().Code`. The loader first tries the full code, then removes ONI's built-in `_klei` suffix: `ru_klei.po` takes precedence over `ru.po`. Community language mods can use their own locale codes. Russian ships as `ru.po`. Missing files, missing entries and empty translations retain the English text on a normal startup. Invalid format parameters are rejected by ONI. A language change requires the game's normal restart.

Do not use gettext plural entries: the game's reader loads `msgctxt` / `msgstr` pairs. Resolve fuzzy entries before submitting them; ONI's reader does not honor gettext fuzzy flags. The catalog check allows partial translations, but the bundled Russian catalog is complete.

## Change English text

Edit the `LocString` literal in `Strings.cs`, then regenerate the template:

```powershell
python -m pip install -r tests/requirements.txt
python scripts/localization.py --update-template
```

Update affected catalog `msgid` and translated text, then run `python scripts/localization.py`. The checker detects stale source text, missing template entries, unknown/duplicate keys, fuzzy/plural entries and changed placeholders. It does not verify translation quality or screen layout.

The template generator reads simple C# string literals from this one flat string class. It does not modify C# source or generate runtime code. Keep strings in that form when adding captions.
