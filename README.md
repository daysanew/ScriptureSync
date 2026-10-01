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

## ProPresenter investigation

ProPresenter support is experimental and available only through standalone
diagnostic tools. The WPF application still syncs to OpenLP. See the
[feasibility assessment](docs/propresenter-feasibility.md),
[API diagnostic](tools/ScriptureSync.ProPresenter.Spike/README.md),
[Bible reader results](docs/propresenter-bible-spike.md), and
[template-file experiment](tools/ScriptureSync.ProPresenter.TemplateSpike/README.md).
The initial generated presentation passed user-reported manual checks; broader
template/version validation is still needed before production integration.

## Install

Run **Install ScriptureSync.cmd** from the repository root. The script builds
and installs the application, copies the OpenLP plugin, and creates a desktop
shortcut. Restart OpenLP after installation, then activate **ScriptureSync**
under **Settings > Manage Plugins**.

The install script publishes the application with the existing .NET runtime,
so the computer must have the .NET 10 SDK installed. The script does not install
the SDK or other .NET dependencies for you.

## Scripture reference formats

Use a colon (`:`) between chapter and verse. Semicolons (`;`) separate passages.
Enter one scripture entry per row, or paste multiple entries on separate lines.

| Example | Meaning |
| --- | --- |
| `Col 2:9 (NKJV)` | Colossians chapter 2, verse 9 |
| `John 1:1-5,7 (KJV)` | Verses 1–5 and 7 in John chapter 1 |
| `John 3:16; Romans 8:28 (KJV)` | Passages from different books |
| `John 1:1-5; 2:4-5 (KJV)` | Passages from different chapters in the same book |
| `Psalm 23 (KJV)` | A whole chapter |
| `Col 2; Col 3 (NKJV)` | Two whole chapters, with the book name repeated |
| `Acts 7:54-8:3 (KJV)` | A verse range spanning chapters |

`Col 2;9 (NKJV)` is ambiguous and is rejected with guidance to use `:` between
chapter and verse. It appears as needing attention and is skipped during sync.
Correct it to `Col 2:9 (NKJV)` to send only that verse. For separate whole chapters
joined by semicolons, repeat the book name as in `Col 2; Col 3 (NKJV)`.

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

