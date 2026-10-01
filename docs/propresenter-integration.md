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

**Still blocking feature completion:** the native linking prototype is verified, but it is not integrated into the app workflow. Both synthetic and genuine PCO-connected placeholders rejected replacement with their existing identity (HTTP 404). The code blocks PCO playlist mutation and placeholder replacement rather than claiming success. Desktop visual inspection of the new settings/preview is also pending because the computer-use runtime could not initialize; a WPF STA construction/layout test covers settings creation.

PR #2 should remain draft until native PCO linking and the visual/manual workflow checks are complete. The earlier feasibility and spike documents are historical evidence, not a claim that this integration is finished.

## Schema provenance

The production project contains the 32 transitive protobuf schemas formerly held by the template spike plus the pinned `playlist.proto` and `planningCenter.proto` definitions, pinned to `greyshirtguy/ProPresenter7-Proto` commit `bf6325d243897a6c64dde46eec803ec29f5f8569`, with its MIT license and version marker. These are reverse-engineered schemas; arbitrary templates and untested ProPresenter versions are not supported by implication. The diagnostic tools reference the same production implementation.

Further live verification found that even an unchanged ordinary placeholder receives a new UUID on playlist PUT. Mixed playlists are now blocked before any file/playlist publishing, pending a verified preservation strategy. Final Release regression suite: 129 passed, 0 failed.

### Genuine PCO placeholder test

With explicit user approval, tested linking a one-item PCO playlist to an existing,
API-readable John 3:16 presentation. PUT retained the original item UUID, name,
position and `is_pco: true`, while setting the presentation target and arrangement
fields. ProPresenter returned HTTP 404. Subsequent reads confirmed that the item
remained a PCO placeholder; the local playlist file was byte-identical to its
pre-test backup. No live output was triggered.

The pinned native playlist schema represents a PCO item as a wrapper containing
its `PlanningCenterPlan.PlanItem` plus a separate `linked_data` PlaylistItem.
The public API response omits that full plan-item metadata. A successful native
manual link and a before/after comparison are needed before designing further
linking logic; changing only `is_pco` or replacing the outer UUID is not evidence
that the real plan connection is preserved. Automatic PCO linking remains blocked.

### Native-link prototype and one-button investigation

The user manually linked the prepared PCO item. Comparing the saved native files
confirmed that ProPresenter retains the outer playlist-item UUID and full PCO
PlanItem, and adds a nested linked presentation containing its file path and
arrangement UUID. The prototype reproduces those fields without rebuilding the
PCO plan or other playlists. It preserves unknown protobuf fields and rejects
mismatched plan/item identities, unowned target presentations, and replacement
of existing links unless explicitly requested by the caller.

With the user having saved and closed ProPresenter, a validated candidate was
applied with a backup, then ProPresenter was reopened. The API confirmed the
same outer item UUID, `is_pco: true`, and the new owned John 3:16 presentation.
The persisted PCO plan and item metadata compared equal after restart. Repeat
publication was Unchanged and reused the existing link. Refreshing the plan
from PCO after linking remains an additional acceptance check.

The restart changed ProPresenter's library UUID. Publishing now recovers only
when the saved library name uniquely matches an API library and the configured
folder's name; ambiguous or missing matches still require Settings.

The requested final workflow is: import the selected plan in ProPresenter,
close normally, generate/link scripture, reopen, and verify. The official
API's playlist-create operation supports only ordinary playlists and groups,
not importing a PCO plan. No documented command-line or URL-scheme alternative
was found. Desktop automation of the native PCO-import dialog remains untested:
the computer-use runtime failed before listing windows, including after reset,
with `failed to write kernel assets: The system cannot find the path specified.
(os error 3)`. This is a test-environment blocker, not proof that UI automation
is impossible. No UI-import automation or one-button completion is claimed.

The native class transforms a supplied byte array only; it does not write live
playlist files or close processes. The app still blocks PCO playlist mutation
until a guarded orchestration workflow is implemented and tested. Latest full
Debug regression suite: 136 passed, 0 failed. PR remains draft.

### Live PCO import / close / link / reopen test

Desktop control recovered after restarting Codex. On ProPresenter 21.4.2,
the Library + menu exposes **Planning Center Service…**, which opens
**Select a Plan**. Expanding the test service and selecting the September 20
plan successfully imported a native PCO playlist with its Sermon placeholder.
Importing an already imported plan creates a second playlist; it does not reuse
the existing one. Production orchestration must identify the existing PCO plan
and refresh it, or explicitly resolve duplicate matches, before importing.

The newly imported copy passed the complete test sequence: normal quit (including
ProPresenter's quit confirmation), verified process exit, native scripture link
with a full backup and unrelated-field equality check, reopen, then API and
visual verification. Its outer item UUID and PCO flag were retained, and the
expected owned John 3:16 presentation appeared. Clicking the PCO refresh control
after reopening retained the same item identity and presentation target.
This run reused the previously generated presentation; it did not retest Bible
generation. The original playlist and an empty menu-test playlist remain intact.
Local backups and API evidence are under ignored `artifacts/pco-import-cycle/`.

This proves the sequence can be driven on this installation, not that a
production one-button workflow is implemented. Menu/dialog navigation required
screenshots because the main-window accessibility tree did not expose popup
controls reliably. The app still needs a Windows automation adapter, duplicate
plan detection, guarded normal shutdown with prompt handling, backup/recovery,
and verified reopen before this workflow is ready for users. Startup can outlast
the desktop helper's launch timeout, so readiness must be checked separately.

### Existing-playlist application integration (in development)

The sync preview now recognizes local native PCO playlists, matches imported
drafts by service/plan/item identity, and offers explicit PCO-item mapping for
manual drafts. It refuses foreign existing links and multiple presentations
mapped to one item. Existing matching links use the normal live publisher;
missing links request normal ProPresenter shutdown, wait for process exit,
prepare the native changes from the latest saved file, back up and replace the
file, reopen ProPresenter, and verify the resulting links through the API.
ProPresenter's quit/save dialogs require the user to respond; the application
does not force termination or discard unsaved work. The restart is disclosed in
the preview. This application orchestration still needs end-to-end live testing.
Preview currently requires ProPresenter running. New-plan import automation,
offline preview, and grouping multiple passages into one PCO item remain pending.

A potentially simpler alternative is ProPresenter's documented automatic
download/link of `.pro` attachments on PCO items. This needs separate live
validation and user authorization for uploading generated presentations to PCO.
Name-based library matching is another built-in option, but reusing one shared
Sermon presentation would also affect older playlists pointing to it.

### PCO attachment import test — passed

With explicit user authorization, uploaded a uniquely named John 3:16 `.pro`
test copy through Planning Center's multipart file-upload API, then POSTed its
file upload identifier to the selected plan item's attachments endpoint. The
API returned 201 and a subsequent list confirmed the attachment. Credentials
were loaded using the application's credential store and were not logged.

ProPresenter already had Automatically Download Presentations and Media enabled
with Default as its download library; automatic upload was disabled. Importing
the test plan through Planning Center Service downloaded the presentation and
automatically linked Sermon while ProPresenter remained open. The API confirmed
one PCO presentation item with the unique test filename, and its single slide's
text exactly matched the original generated John 3:16 slide. ProPresenter
assigned a NEW presentation UUID during import, so attachment-backed publishing
must discover the imported identity rather than assume the uploaded UUID is
retained. No local native playlist editing was used in this attachment test.

This validates initial import only. Updating an existing attachment and refreshing
an already imported playlist, duplicate prevention, multi-reference composition,
and production UI integration remain untested/unimplemented for this route.
The uploaded attachment and the additional imported test playlist were retained.
Local receipt IDs and API evidence are under ignored `artifacts/pco-attachment-test/`.
This route is preferable for new PCO plans because ProPresenter creates the link
itself without a restart. The native local-link workflow above remains a fallback
under development, not the required path for attachment-backed initial imports.

### Existing attachment update test — passed with an import prompt

Updated the same authorized PCO test attachment through PATCH to contain two
slides, John 3:16-17. The API returned 200; listing afterward still showed just
the original attachment. Refreshing the already imported PCO playlist displayed
ProPresenter's File already exists dialog: Use Existing / New Version / Write
Over. Selecting Write Over loaded both verses without restarting, retained the
outer PCO item UUID and PCO flag, and kept one playlist item. ProPresenter again
assigned a new presentation UUID. The API verified two slides and John 3:17 text.

A second refresh with no further upload displayed the same collision prompt;
Use Existing retained the verified presentation. Consequently this route is
not an unattended refresh solution as currently configured. Production must
avoid unnecessary uploads/refreshes, distinguish unchanged content, and guide
the user through Write Over for a changed attachment. Do not globally accept
overwrites of unrelated files. Evidence: ignored
`artifacts/pco-attachment-test/playlist.after-update.json`.

### Attachment preview and next-step guidance

Drafts consisting of PCO-imported rows now open a PCO attachment preview from
the main ProPresenter button. Manual/mixed drafts retain the local publisher.
The attachment preview combines rows for the same PCO item into one presentation
(currently one book and translation per item), shows its text, and explicitly
offers Send presentations to Planning Center. No upload happens merely by opening
the preview. Stable cached presentation bytes allow unchanged sends to be skipped.
An item-scoped ownership receipt records the attachment ID, filename, content
hash and remote update timestamp. Changed content patches that same attachment;
foreign `.pro` attachments, external edits, and uncertain previous creates stop
with an explanation instead of creating ambiguous duplicates.

Before and after sending, the UI checks the local native playlist's PCO service
and plan IDs against running ProPresenter playlist IDs. It reports: import the
service plan; refresh the existing playlist and choose Write Over when prompted;
choose the intended playlist when multiple copies exist; or conditional guidance
when ProPresenter cannot be checked. An unavailable connection does not block
uploading. A Check ProPresenter again button refreshes guidance without uploading.
The check establishes plan import status only, not that the latest attachment
has been downloaded. Remote computers and unavailable/stale native files use
the unknown fallback. Latest regression suite: 143 passed. The new attachment
window still requires full live UI acceptance; the earlier upload/import tests
used the diagnostic harness. PR remains draft.
