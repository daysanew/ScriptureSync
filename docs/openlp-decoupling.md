# OpenLP decoupling handoff

Branch: `codex/propresenter-feasibility`.

## Change

The application previously called `IOpenLpClient` directly from
`MainWindowViewModel`. It now uses Core's `IScriptureSyncDestination`, which
accepts the parser's `PassageReference` and translation code. The OpenLP adapter
converts that reference to the same string previously sent to the bridge.

`PrepareAsync` returns a neutral installed-Bible count. `AddScriptureAsync`
returns a confirmed addition or null for no verses found. Typed neutral errors
distinguish an unavailable destination, missing translation, and destination
failure. The adapter retains original exceptions as inner exceptions for logs.
Cancellation and unexpected exceptions pass through unchanged.

`MainWindow` remains the composition boundary and constructs the OpenLP adapter.
The adapter borrows the supplied client; it does not change its ownership.

## Scope and deliberate limits

This is a reference-based sync abstraction, not the plan's eventual
`IPresentationPublisher` accepting composed slides. OpenLP still performs lookup
and service addition together. There is no speculative `IBibleTextProvider` or
slide-composer implementation: the current bridge search response contains
joined verse text rather than structured verses. A true text-provider/publisher
separation requires another supported data path and its own validation.

UI labels, public binding/method names such as `OpenLpStatus` and
`SyncToOpenLpAsync`, and log messages retain their OpenLP wording. Destination
selection and presentation-neutral UI copy belong to later integration work.
The view model has no OpenLP namespace, client, result, or exception dependencies.

The parser, Planning Center client/import behavior, persisted draft schema,
settings, installer, Python plugin, and HTTP bridge are unchanged. Duplicate
rows remain intentional additions. Missing translations skip the remaining
passages for that translation; other translations continue. Connection failure
stops the run. Existing timeout and UI cancellation behavior is unchanged; this
refactor does not add a Cancel command or reinterpret timeout exceptions.

No ProPresenter functionality was added to the application by this refactor.
The subsequent standalone spikes are documented in
[the feasibility assessment](propresenter-feasibility.md). PCO identity retention
and idempotency are still later work.

## Files

- `src/ScriptureSync.Core/Sync/IScriptureSyncDestination.cs`: neutral contract,
  preparation status, and confirmed-addition result.
- `src/ScriptureSync.Core/Sync/ScriptureSyncException.cs`: neutral failure types.
- `src/ScriptureSync.OpenLP/OpenLpSyncDestination.cs`: adapter and error mapping.
- `src/ScriptureSync.App/MainWindow.xaml.cs`: adapter construction.
- `src/ScriptureSync.App/ViewModels/MainWindowViewModel.cs`: neutral dependency.
- `tests/ScriptureSync.Tests/ManualDraftWorkflowTests.cs`: existing tests now run
  through the adapter; new cases cover missing translations and plugin failures.
- `tests/ScriptureSync.Tests/OpenLpSyncDestinationTests.cs`: reference/result
  fidelity, token forwarding, no-result semantics, exception mapping, cancellation.
- `docs/openlp-decoupling.md`: this handoff.

The branch also contains the earlier `docs/propresenter-feasibility.md` assessment.

## Validation

```powershell
dotnet build ScriptureSync.slnx
dotnet test tests\ScriptureSync.Tests\ScriptureSync.Tests.csproj --no-build
git diff --check
```

Build passed with 0 warnings and 0 errors. All 59 tests passed, 0 failed and
0 skipped (48 existing cases plus 11 new cases). Diff whitespace check passed.
The build/tests used access to the installed Windows SDK outside the sandbox.

No live OpenLP test was performed. Before release, start OpenLP with its plugin,
check the Bible count, sync a small multi-translation draft with a duplicate row,
and confirm the same service additions and status messages. This validates the
runtime environment beyond the automated adapter and bridge tests.
