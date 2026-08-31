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
- Planning Center plan import using configurable service-item names

OpenLP 3.1.7 can crash when Remote API Bible operations overlap. ScriptureSync
avoids that path: every search and confirmed service addition is performed in
order by the OpenLP plugin without changing OpenLP's global Bible selection.

## Build and test

```powershell
dotnet build ScriptureSync.slnx
dotnet test tests\ScriptureSync.Tests\ScriptureSync.Tests.csproj
```

## Install from GitHub

1. Open the [ScriptureSync releases page](https://github.com/daysanew/ScriptureSync/releases).
2. Download the latest `ScriptureSync-<version>-win-x64.zip` file.
3. Extract the entire ZIP. Do not run the installer from inside the ZIP preview.
4. Double-click **Install ScriptureSync.cmd** in the extracted folder.
5. Restart OpenLP if the installer reports that it installed or updated the plugin.
6. On the first installation, activate **ScriptureSync** under
   **Settings > Manage Plugins** in OpenLP.

The package includes the required .NET runtime and does not require Git, the
.NET SDK, administrator access, or a separate .NET installation. It installs
the application under `%LOCALAPPDATA%\ScriptureSync\App`, installs the OpenLP
plugin under the current user's OpenLP data folder, and creates a desktop
shortcut. Running a newer package upgrades the application in place and keeps
the user's settings and Planning Center credentials.

The installer checks the existing OpenLP plugin files. It skips them when they
are already current and updates them when they are missing or different.

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

## Planning Center import

Create a Planning Center Personal Access Token for a user who can view Services.
In ScriptureSync, open **Settings**, enter its Application ID and Secret, then
enter each service-item name to import on its own line (for example, `Scripture`
and `Message Text`). Credentials are stored in Windows Credential Manager and
the saved secret is never displayed again in ScriptureSync.

Choose **Import from PCO**, select an upcoming plan, and import it. ScriptureSync
reads references from the Details field of every matching item, preserves their
plan order, and puts them into the normal editable draft. Review the rows before
choosing **Sync to OpenLP**.

See [ROADMAP.md](ROADMAP.md) for the remaining field-testing, release, plugin,
and remaining integration work.
