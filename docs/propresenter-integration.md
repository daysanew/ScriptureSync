# ProPresenter integration (PR in progress)

The app now supports Settings > Presentation software > ProPresenter, installed-Bible text retrieval, a slide-text preview, owned library publishing, and optional appending to an empty or presentation-only ordinary playlist. The OpenLP workflow remains available and is the default for existing installations.

## Setup

1. Run ProPresenter and enable its network API. Copy its address and port into ScriptureSync settings, then click **Test connection / load destinations**.
2. Select a library. For a local API address, ScriptureSync fills its folder when it exists under `%APPDATA%\RenewedVision\ProPresenter\LocalWorkspaces\ProPresenter\Libraries`. It follows library changes while retaining manually chosen custom paths. **Use default folders** restores detected library/Bible folders; use Browse for custom workspaces. Library publishing requires access to the actual directory; selecting a remote API alone is insufficient.
3. Export a simple Bible presentation as a `.pro` template. Its first slide must have exactly two text boxes named **Reference** and **Verse**, one slide action, one cue group, and plain text with consistent formatting. Timelines, linked elements, alternate text, and mixed formatting within the text run are rejected with an explanation.
4. Select the installed ProPresenter Bible directory. Translations are matched by package abbreviation, case-insensitively. The reader supports installed USX 3 packages and never modifies them. No Bible text is bundled.
5. Optionally select an empty or presentation-only ordinary playlist, or choose **Library only**. PCO-linked playlists, mixed playlists, and placeholder replacement are currently blocked pending validation of a method that preserves their PCO connection.
6. Add/paste references or import a Planning Center plan. Choose **Preview ProPresenter**, inspect all slide text and the Create/Update/Unchanged list, then **Sync to ProPresenter**.

One resolved passage and translation produces a presentation with one verse per slide. Template styling controls appearance; the text preview is not a rendered slide preview. Review long verses in ProPresenter for fit. No operation triggers live output.

## Repeat sync and recovery

Draft identities persist across restarts. Re-importing the same PCO plan updates its imported draft rows and order while retaining their identities; removed imported lines leave the draft. Published files outside the current draft are reported and kept. Translation changes create a separate presentation.

ScriptureSync owns only its generated files, identified by an ownership record, UUID, marker and content hash. It refuses to overwrite an unknown file or a generated file edited externally. Updates keep presentation and arrangement identities, back up the old file, and verify actual slide text through the API. Repeat sync reuses existing playlist links.

Ownership data and backups live under `%LOCALAPPDATA%\ScriptureSync\ProPresenter`. Do not remove ownership data while retaining its library files. An unreadable ownership file stops publishing; `ownership.json.bak` is retained for recovery. Interrupted publishing may leave completed files in the library; refresh the preview before retrying. A file interrupted during copying requires restoring its `.pro` backup before retrying. No automatic rollback removes published material.

Library files have stable `ScriptureSync-<identity>.pro` names, which this ProPresenter version uses as its library labels; the preview shows scripture titles. Keep generated files in the configured library. Editing or renaming them in ProPresenter will require recovery or a new output identity/library rather than silent replacement.

## Evidence and remaining merge gates

Verified on Windows with ProPresenter 21.4.2 (352584193), API v1, local installed KJV, and the previously validated exported template:

- Fresh automatic publication and discovery, with no import dialog.
- Ordinary playlist append, repeat sync without duplicate links, and changed passage update with the same presentation UUID.
- Exact normalized slide-text verification through `/v1/presentation/{uuid}` after writing, including an update from three to four Psalm 23 verses.
- Read-only installed-Bible access; original template is unchanged.
- Previously reported manual checks: generated slides looked correct, remained editable, and survived save/reopen.

Live testing found two implementation requirements: Windows ProPresenter observes copied files but not atomic rename placement; playlist PUT requires `arrangement_name` and `arrangement_uuid`, although the published schema omits the latter. New playlist entries must use the presentation UUID as their item UUID.

**Still blocking feature completion:** safe linking into a genuine PCO-connected placeholder has not been proven. A synthetic placeholder rejected replacement with its existing identity (HTTP 404). The code blocks PCO playlist mutation and placeholder replacement rather than claiming success. Desktop visual inspection of the new settings/preview is also pending because the computer-use runtime could not initialize; a WPF STA construction/layout test covers settings creation.

PR #2 should remain draft until native PCO linking and the visual/manual workflow checks are complete. The earlier feasibility and spike documents are historical evidence, not a claim that this integration is finished.

## Schema provenance

The production project now contains the 32 transitive protobuf schemas formerly held by the template spike, pinned to `greyshirtguy/ProPresenter7-Proto` commit `bf6325d243897a6c64dde46eec803ec29f5f8569`, with its MIT license and version marker. These are reverse-engineered schemas; arbitrary templates and untested ProPresenter versions are not supported by implication. The diagnostic tools reference the same production implementation.

Further live verification found that even an unchanged ordinary placeholder receives a new UUID on playlist PUT. Mixed playlists are now blocked before any file/playlist publishing, pending a verified preservation strategy. Final Release regression suite: 129 passed, 0 failed.
