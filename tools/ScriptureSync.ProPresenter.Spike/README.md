# ProPresenter connectivity spike

A standalone read-only diagnostic. It is not connected to the WPF application
and cannot create presentations, change playlists, or trigger slides.

Run from the repository root with the host and port shown in ProPresenter's
Network settings:

```powershell
dotnet run --project tools\ScriptureSync.ProPresenter.Spike -- --address http://127.0.0.1:49627
```

The address is required; 49627 is the tested installation's setting, not a
hard-coded default. Another reachable host may be used. Requests have a
10-second timeout and Ctrl+C cancels the run. Exit codes: 0 success, 1 diagnostic
failure, 2 usage error, 130 cancellation. Redirects are disabled in the CLI.

## Endpoint scope

Only these informational GET requests are made:

- `/version`
- `/v1/libraries`
- `/v1/library/{uuid}`
- `/v1/playlists`
- `/v1/playlist/{uuid}`

The tool lists presentation identities from each library and reads playlist
contents. It does not fetch slide text. Playlist item identity/type fields are
printed when present. It does not infer PCO linkage from display names.

The client requires API `v1`. Connection failure, timeout, HTTP errors,
unsupported API versions, and malformed or unexpected JSON produce an endpoint
specific diagnostic and a nonzero exit code. A successful version request is
reported before enumeration; later enumeration can still fail.

Reference: [official ProPresenter API](https://openapi.propresenter.com/).
Use the target installation's API documentation to investigate differences.
Some ProPresenter GET endpoints trigger actions; only the listed informational
endpoints are used here.

## Live test result

Tested on the user's running local Windows installation:

- Executable product version: **21.4.2 (352584193)**; file version **21.4.2.1**.
- API response: **ProPresenter 21.4.2**, API **v1**, platform **win**.
- Address: `http://127.0.0.1:49627`.
- User reported being signed out; the diagnostic does not independently inspect
  account/license state. The tested reads succeeded in that state.
- Library: **Default**, containing one presentation (**sf**).
- Playlists: **Default** and **Playlist**, both empty.
- All six requests (version, libraries, library contents, playlists, and two
  playlist contents) succeeded. CLI exited 0. No content changed or triggered.

Initially the running application had no listening API port. After the user
provided the configured network port, live enumeration succeeded.

## Acceptance status and limits

Connectivity, library enumeration, presentation enumeration, and empty playlist
reads are proved for this installation. Mock tests cover nested playlists,
malformed JSON, HTTP failures, connection failure, timeout, cancellation,
unsupported versions, address validation, and rejecting non-UUID item paths.

No populated or PCO-connected playlist was available, so item structure and PCO
placeholder linkage remain unverified. No presentation creation/import endpoint
has been validated. The public API lists playlist mutation operations, but their
behavior with PCO playlists has not been tested. Do not treat connectivity as
proof of publishing feasibility. No other ProPresenter version is supported by
live evidence yet.

Subsequent Bible-file and template-generation results are linked from the
[feasibility assessment](../../docs/propresenter-feasibility.md). No Bible files,
templates, or ProPresenter library files were modified or examined by this tool.

## Automated validation

```powershell
dotnet test tests\ScriptureSync.Tests\ScriptureSync.Tests.csproj
```

Build and test passed: **74 passed, 0 failed, 0 skipped**, including 15 new API
diagnostic cases and the existing OpenLP/refactor suite. The test project builds
the standalone spike; production application projects do not reference it.
