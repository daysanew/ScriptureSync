# ScriptureSync

A small, fully local Windows utility for preparing scripture references and
safely adding them to an OpenLP service.

## Current status

- .NET 10 WPF staging interface
- Editable and pasteable scripture list
- Resilient scripture-reference parser
- Multiple Bible translations per passage
- Local OpenLP community plugin with a main-thread operation queue
- Localhost-only WPF-to-OpenLP bridge on `127.0.0.1:4317`
- Automated parser and workflow tests

OpenLP 3.1.7 can crash when Remote API Bible operations overlap. ScriptureSync
avoids that path: every search and confirmed service addition is performed in
order by the OpenLP plugin without changing OpenLP's global Bible selection.

## Build and test

```powershell
dotnet build ScriptureSync.slnx
dotnet test tests\ScriptureSync.Tests\ScriptureSync.Tests.csproj
```

## Local installation

Double-click **Install ScriptureSync.cmd**. It publishes the WPF app under
`%LOCALAPPDATA%\ScriptureSync\App`, installs or updates the OpenLP community
plugin, and creates a ScriptureSync desktop shortcut. It does not require
administrator access. This is currently a developer-oriented installer and
requires the .NET SDK; replacing it with a self-contained volunteer installer is
tracked in the roadmap.

Restart OpenLP after installing a plugin update. On the first installation,
activate **ScriptureSync** under **Settings > Manage Plugins**. Normal use after
setup is one click from the desktop shortcut.

## Bible translation names

Each Bible installed in OpenLP needs a short name, such as `KJV`, `NKJV`, `NLT`,
`NIV`, or `AMP`. That name must match the translation written in parentheses at
the end of each scripture entry. Matching is case-insensitive.

Examples:

- OpenLP Bible `KJV` matches `Psalm 23:1 (KJV)`.
- OpenLP Bible `NLT` matches `Psalm 42:11 (NLT)`.
- OpenLP Bible `NLT` does not match an entry ending in `(NIV)`.

For multiple translations, put each matching short name at the end, such as
`1 Peter 1:3 (NKJV & NLT)`.

See [ROADMAP.md](ROADMAP.md) for the remaining field-testing, release, plugin,
and deferred integration work.
