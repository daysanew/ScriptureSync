# ProPresenter feasibility assessment

Assessment date: 2026-09-18. Repository baseline: `7d9dce1`.
Working branch: `codex/propresenter-feasibility`.

Follow-up: the initial assessment below is retained as the baseline. A later
live connectivity test succeeded on ProPresenter 21.4.2; see the
[connectivity spike results](../tools/ScriptureSync.ProPresenter.Spike/README.md).
The later [Bible spike](propresenter-bible-spike.md) also demonstrated structured
read-only retrieval from the installed KJV package. Presentation generation
has passed isolated file generation in the
[template spike](../tools/ScriptureSync.ProPresenter.TemplateSpike/README.md), but
the user also reported successful import, appearance, editing, and save/reopen
checks. Stage-output coverage and the exact variants tested were not separately
identified. Additional Bible/template/version compatibility checks remain.

## Decision

Proceed with a staged investigation. The repository can accommodate a second
presentation destination, but end-to-end ProPresenter synchronization is not yet
proved. No ProPresenter version has been tested, and no application behavior was
changed during this assessment.

The supplied `ScriptureSync_ProPresenter_Implementation_Plan.md` is design input.
Its embedded "First Codex Assignment" was not executed: the user selected
feasibility assessment and branch preparation only.

## Current architecture and implications

| Area | Observed implementation | Implication |
| --- | --- | --- |
| Parsing | Core owns `ScriptureReferenceParser` and `PassageReference`; tests include whole chapters, cross-chapter ranges, aliases, and multiple translations. | Reuse the parser as the authority for user input. |
| OpenLP | `OpenLpBridgeClient` implements `IOpenLpClient`, with health/Bible discovery and `AddScriptureAsync`. | The current operation accepts a reference, not composed slide content. |
| Python plugin | Lookup and confirmed service addition run sequentially on OpenLP's main thread. The separate search endpoint returns joined `verse_text`. | A structured verse provider cannot simply wrap the existing search response: verse boundaries and identifiers are absent. |
| Application | `MainWindowViewModel` directly calls the OpenLP client and catches OpenLP exceptions; `MainWindow` constructs the client. | A narrow adapter boundary is practical, but renaming an interface alone will not separate text retrieval from publishing. |
| Draft | `StoredDraftItem` persists only local GUID, raw text, and source label. | Existing drafts cannot reliably identify their original PCO plan/item. |
| PCO import | Import sorts by sequence and creates a draft row per detail line, discarding item ID and retaining only a plan display label. | Preserve structured provenance before implementing deterministic playlist linkage or repeat synchronization. Core's `ScriptureRequest` already has PCO identity fields, but this draft path does not use them. |
| Sync behavior | Each row processes translations and passages in order; duplicate rows are deliberately preserved; connection errors stop further additions. | Preserve these semantics in the initial refactor. ProPresenter idempotency must not silently change OpenLP behavior. |

## External evidence and unresolved gates

### API connectivity: plausible, not tested

The [official API reference](https://openapi.propresenter.com/) lists library and
playlist reads, including `GET /v1/libraries`, `GET /v1/library/{library_id}`,
`GET /v1/playlists`, and `GET /v1/playlist/{playlist_id}`. It also lists playlist
creation and replacement operations. This supports a read-only connectivity
spike, but does not prove PCO placeholder linkage or editable presentation creation.

Renewed Vision documents access to the installed application's API documentation
through Network settings in its [API support article](https://support.renewedvision.com/hc/en-us/articles/31606866768147-TCP-IP-Connections-with-ProPresenter-API).
Use the actual installation's documentation and configured host/port as the
compatibility baseline. Do not assume the public reference matches every version.

The diagnostic must allowlist informational endpoints: an HTTP GET alone is not
a guarantee of read-only behavior because the API also exposes trigger operations
as GET requests. Do not test those against an active service.

### Installed Bibles: unproved

No installed Bible files were inspected. The plan's USX layout, metadata, package
accessibility, and translation mapping remain hypotheses for the target version.
Synthetic USX fixtures can test parsing here, but cannot establish compatibility
with an installed package. Do not select hard-coded discovery paths or infer
verse numbers by splitting the current OpenLP plugin's joined text.

### Editable presentation generation: highest uncertainty

[ProPresenter7-Proto](https://github.com/greyshirtguy/ProPresenter7-Proto) is a
candidate schema source. Its repository identifies an MIT license and explicitly
describes the schemas as unofficial and unsupported. Its README describes daily
automatic extraction updates, and the repository includes versioned schema
folders. This is evidence of a possible route, not a completed maintenance or
compatibility audit. Before adoption, verify recent successful updates, pin a
commit, record the license, and match it to the exact target application build.

A protobuf round trip alone cannot prove that ProPresenter will render a file,
preserve its actions and styling, or save/reopen it correctly. The template spike
must use a real exported template and complete those checks in ProPresenter.
Presentation and cue identifiers, unknown fields, and references to external
assets need explicit preservation or remapping tests. No library has been adopted.

## What can be done on this PC

| Work | Locally verifiable | Requires ProPresenter or target-machine samples |
| --- | --- | --- |
| Narrow OpenLP decoupling | Adapter tests, workflow regressions, full build | Live OpenLP smoke check for final behavioral confidence |
| API diagnostic | Mock HTTP responses, timeouts, cancellation, malformed payloads | Successful connection, actual version and response shapes |
| Bible reader | Synthetic USX and parser-to-verse tests | Real package layout, mapping, installed translation availability |
| Template transformation | Fixture-based serialization after a template is supplied | Audience/stage rendering, editing, save/close/reopen |
| PCO mapping | Deterministic matching and ambiguity tests | Real PCO-connected playlist identifiers and linkage behavior |

The API diagnostic could run here against another reachable ProPresenter machine
using its configured address. That does not make its local Bible or presentation
files accessible; file-dependent spikes still need samples or execution there.

## Recommended adjustments to the supplied plan

1. Keep the first refactor small. Introduce a neutral reference-sync boundary
   around the existing atomic OpenLP operation, with neutral results/errors.
   Preserve UI wording, ordering, duplicate behavior, and the plugin protocol.
   Keep adapter construction at the application composition boundary.
2. Do not claim that boundary is an implementation of a slide-based publisher.
   A true `IBibleTextProvider` needs structured verses, and a publisher consuming
   composed slides needs a different input contract. Introduce those when their
   data paths are proved; do not implement fake providers or discard slide content
   just to satisfy an interface. A larger OpenLP protocol change should be a
   separate decision with integration validation.
3. Retain the three independent feasibility gates from the plan: connectivity,
   local Bible reading, then template generation. Prepare offline tooling here;
   record each live gate as pending until tested on the target version.
4. Before playlist synchronization, add backward-compatible draft provenance:
   service type/plan/item identity plus a stable identity for multiple detail rows
   from one PCO item. Decide how edits, duplicate rows, and reimports affect it.
   The proposed `PCO:{planId}:{itemId}:{translation}` key alone can collide when
   an item produces several draft rows. Legacy drafts need explicit mapping.
5. Keep production UI/settings, automatic playlist mutation, installer changes,
   and ownership/recovery implementation behind the feasibility gates.

## Target-machine handoff

Collect these during a later test session:

- Exact ProPresenter version/build and operating system; configured API address
  and installed API documentation.
- A small test library and playlist, plus a PCO-connected test playlist with a
  scripture placeholder and an already-linked presentation for comparison.
- Read-only Bible package layout/metadata and a locally retained KJV sample for
  the plan's passage checks. Keep Bible packages and proprietary text out of Git.
- An exported scripture template with text/reference/translation fields and the
  desired audience/stage styling, plus a manually created John 3:16-18 example.
- A session in which generated copies can be imported, inspected, edited, saved,
  closed, and reopened. Verify both shorter and longer slide counts.

Record actual results separately from mocked tests. Stop at a failed gate and
revise the approach rather than treating offline tests as proof of compatibility.

## Baseline validation

On the unmodified application at `7d9dce1`:

```powershell
dotnet build ScriptureSync.slnx
dotnet test tests\ScriptureSync.Tests\ScriptureSync.Tests.csproj --no-build
```

- Build: passed, 0 warnings and 0 errors.
- Tests: 48 passed, 0 failed, 0 skipped.
- The first sandboxed build could not access the installed Windows SDK directory;
  rerunning with the required filesystem access succeeded.
- No live OpenLP or ProPresenter integration test was performed.
- This assessment adds documentation only. No production code, dependencies,
  settings, installer changes, API calls to ProPresenter, or Bible modifications.
