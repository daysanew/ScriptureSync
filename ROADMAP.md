# ScriptureSync Roadmap

This file tracks the remaining work. The current OpenLP-only version is usable:
volunteers can paste or type scripture references, review the parsed results, and
send them to an active OpenLP service through the local ScriptureSync plugin.

## 1. Field-test the current version

- [ ] Test the installer on another church Windows computer.
- [ ] Confirm first-time plugin activation instructions are clear for a volunteer.
- [ ] Run several real service lists containing multiple translations, duplicate
      passages, multi-chapter references, and intentionally invalid references.
- [ ] Confirm that stopping or closing OpenLP during a sync produces a useful
      error and does not damage the saved draft.
- [ ] Collect confusing inputs and add each one as a parser regression test.

Completion: a volunteer unfamiliar with the project can install it, paste a
service's scriptures, correct any highlighted rows, and sync without assistance.

## 2. Finish the OpenLP-only release

- [ ] Replace the developer-oriented install script with a friendly, self-contained
      Windows installer. It must include the required .NET runtime so volunteers
      do not need to install the .NET SDK or runtime separately.
- [ ] Make installation a normal guided experience: install the application,
      copy the OpenLP plugin, create shortcuts, and explain the one-time OpenLP
      plugin activation step.
- [ ] Publish for the supported Windows architecture and test the installer on a
      clean computer that has no .NET SDK or runtime installed.
- [ ] Add application and plugin version numbers that are visible to the user.
- [ ] Improve the installer so it detects a running ScriptureSync/OpenLP instance
      and gives clear instructions instead of failing on locked files.
- [ ] Decide whether to provide an uninstaller and an upgrade-only installer.
- [ ] Add a simple way to open the local log when troubleshooting is necessary.
- [ ] Add a sync-in-progress indicator and disable all list-changing controls
      until the operation finishes.
- [ ] Decide whether a Cancel Sync button is needed after field testing.
- [ ] Produce a signed or checksummed release package for volunteer computers.

Completion: a volunteer can install or update ScriptureSync with a few clicks on
a clean Windows computer, without installing developer tools or having a
developer present.

## 3. Harden the OpenLP plugin

- [ ] Test supported OpenLP versions and record the minimum version.
- [ ] Verify behavior when the plugin port is already in use.
- [ ] Verify behavior when a Bible is removed or renamed while the app is open.
- [ ] Add plugin-side tests for request validation, missing passages, timeouts,
      and orderly shutdown.
- [ ] Confirm repeated rapid sync requests remain serialized on OpenLP's UI
      thread and cannot reproduce the Remote API Bible-switch crash.
- [ ] Document recovery steps if OpenLP or the plugin becomes unresponsive.

Completion: known OpenLP failure conditions are handled without silent additions,
duplicate unintended requests, or an OpenLP crash.

## 4. Planning Center integration (deferred from this version)

- [x] Choose the Planning Center authentication flow and store credentials only
      in the current user's protected local Windows storage.
- [x] Retrieve service types and plans within a configurable one-week window.
- [ ] Support Sunday, Wednesday, and Special service types without hard-coded IDs.
- [x] Retrieve the plan item or note fields that contain scripture references.
- [ ] Extract references and translations using the existing parser.
- [x] Preserve deliberate duplicate scriptures and their original plan order.
- [x] Load imported scriptures into the same editable preview used by pasted text.
- [x] Let the volunteer add, remove, reorder, or correct rows before syncing.
- [x] Clearly distinguish import errors from OpenLP sync errors.
- [x] Add mocked integration tests so normal development does not require a live
      Planning Center account.

Completion: selecting a service plan fills the editable preview, but nothing is
sent to OpenLP until the volunteer reviews it and clicks Sync.

## 5. Final usability and release work

- [ ] Write a one-page volunteer quick-start guide with screenshots.
- [ ] Add accessible keyboard navigation and verify readable scaling at 125% and
      150% Windows display settings.
- [ ] Test a clean install, upgrade, and uninstall on Windows 11.
- [ ] Add a release checklist covering tests, installer creation, versioning,
      release notes, and a basic real-OpenLP smoke test.
- [ ] Create the first tagged GitHub release only after the field-test checklist
      is complete.

## Recommended next step

Start with section 1. The next development task should come from actual volunteer
feedback unless field testing uncovers an OpenLP reliability problem. After the
OpenLP-only workflow is comfortable and dependable, begin section 4 on a separate
feature branch.
