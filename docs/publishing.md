# Publishing

Operational notes for the Workshop item. [Workshop preparation](workshop.md) owns the README exporter and image contract; [validation](../VALIDATION.md) owns binary hashes and game-test evidence.

## Item and current state

The permanent item is [Interface Extended — 3820540318](https://steamcommunity.com/sharedfiles/filedetails/?id=3820540318). Reuse this ID for every update. As of 2026-10-16:

- The 1.4.0 player package and item preview were uploaded. An authenticated SDK query confirmed Private visibility, title, description, creator/consumer app IDs and owner.
- The owner page's **Allow Comments** checkbox was unchecked. Support is through GitHub Issues.
- The three gallery images persisted in README order. The rendered description includes repository and Issues links; the preview and comment setting were checked again after saving.
- A subscribed download passed the HUD/settings scenario in ONI. The local duplicate and development agent are now disabled; the Workshop copy is enabled. Exact results and cleanup evidence are in validation.
- The owner made the item public after review. A subsequent SDK query confirmed Public (`0`), with the same package and preview sizes. Version 1.4.0 is the first public release.

## Why the first upload used the SDK

The installed ONI Uploader Add form has no visibility selector. Inspection of its native handler showed that an absent selector produces Public (`0`); its Publish button was not used to create this draft. Do not assume this form can create a private item.

Instead, the one-off local helper `artifacts/private-upload.py` called the installed uploader's Steam SDK using the existing desktop Steam session. The creator app is `636750`; the consumer is ONI, `457140`. It used the same legacy [PublishWorkshopFile](https://partner.steamgames.com/doc/api/ISteamRemoteStorage#PublishWorkshopFile) path as the native uploader, with explicit Private (`2`) in the creation call. Cloud ZIP and preview writes precede that call. [Visibility values](https://partner.steamgames.com/doc/api/ISteamRemoteStorage#ERemoteStoragePublishedFileVisibility) distinguish Private from Public and Unlisted.

This API is deprecated in Steam's documentation, but matches the installed tool. It is not a new general-purpose publishing system. No Steam credentials, SteamCMD login or SDK installation were needed. The helper and receipts are ignored local artifacts, not required repository dependencies.

Read-only diagnostics, when that reviewed helper is available:

```text
python artifacts/private-upload.py --probe
python artifacts/private-upload.py --query 3820540318
```

The probe initializes SDK interfaces and reads login, app context and Cloud quota; the query reads item details. Neither writes Cloud files or publishes. The helper's separate `--upload-private` flag creates an item and must not be used for updates to this existing draft.

The inspected Win64 SDK uses RemoteStorage interface `014`. Its creation callback is `1309`, with 8-byte packing and a 24-byte result; the item-details callback is `1318`. Recheck these contracts before rebuilding the helper against a different SDK. Valve's [public header](https://github.com/ValveSoftware/source-sdk-2013/blob/master/src/public/steam/isteamremotestorage.h) and [API call-result documentation](https://partner.steamgames.com/doc/api/ISteamUtils#GetAPICallResult) provide the ABI definitions.

## Interrupted operations and updates

`artifacts/private-upload-receipt.json` records the successful creation, item ID, input hashes, requested visibility and queried result. The helper creates and flushes a receipt before Cloud writes or the creation call, refuses an existing receipt and never retries creation automatically.

If an operation times out or the process exits, preserve the receipt. An unknown outcome is not a failed creation: inspect the owner's item list and query any recorded item ID before doing anything else. Do not delete the receipt or choose a new receipt path just to retry. A second creation would make a duplicate item.

For a routine update, regenerate the README export, build the clean player package and update this same item. The creation helper does not implement an update command. The verified description-only update used `CreatePublishedFileUpdateRequest(item)` → `UpdatePublishedFileDescription` → `UpdatePublishedFileVisibility(2)` → `CommitPublishedFileUpdate`. Its result callback is `1316`, with the same 24-byte Win64 result layout as creation. A subsequent details query confirmed exact description bytes and retained visibility, owner, title, file/preview sizes and tags. That operation's local helper and receipt are `artifacts/update-private-description.py` and `artifacts/private-description-update.json`.

After the owner made the item public, `artifacts/update-public-description.py` applied the release-status text using only the description setter. It required Public before committing and verified Public afterward, without calling a visibility setter. Receipt: `artifacts/public-description-update.json`. This avoids reverting the published item through the historical private helper.

Package or preview updates require their corresponding SDK setters as well; the description-only update is not evidence for those paths. Review inputs, persist a new operation receipt before committing, retain the original creation receipt, and query the result afterward. Preserve the item's current visibility; the historical private-update helper explicitly sets Private and must not be reused unchanged for this now-public item. Never switch to the uploader's Add form to work around an update problem.

## Owner-page work

Use `screenshots.json` from the current export for the cover and gallery order. Upload the three JPEG gallery images separately in order; the form limits a selection to 8 MB total. The native file picker can complete asynchronously, so wait for each thumbnail before selecting the next file. Refresh the item page afterward and verify persisted order and image rendering. A completed file-picker action alone is not upload evidence.

Keep comments disabled and verify that GitHub Issues is reachable from the rendered description. The observed **Add Links** editor offers Facebook, Twitter, YouTube, Polycount, Reddit and Sketchfab, with no GitHub option. Do not put a GitHub URL into an unrelated social field. Keep repository/support links in README's Support text, regenerate the description and apply it through the reviewed description-update path.

Before making the item public, inspect its actual visibility, title, preview, description, gallery order, tags and comment setting. Verify a subscribed download in ONI with the local duplicate disabled, preserving the user's collection and shared config. Check the public mod ID, persisted settings and the relevant HUD/settings scenario. Record the result in validation; do not report the local-install test as a subscribed-install pass.

For a future private draft, update README's release status and regenerate the description when publication is ready. Apply the matching description and verify it while private. Change visibility only as the final reviewed release action; neither the exporter nor the diagnostic commands do that. Routine updates to the published item retain Public visibility.
