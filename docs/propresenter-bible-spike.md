# Installed Bible investigation

Target: local Windows ProPresenter 21.4.2. Result: structured read-only retrieval
works with the installed KJV package. This is a standalone spike, not a
production provider or application integration.

## Observed layout

The Bible root was initially empty. After the user installed KJV, it contained:

```text
C:\ProgramData\RenewedVision\ProPresenter\Bibles\
  BibleData.proPref
  <package UUID>\
    rvmetadata.xml
    metadata.xml
    license.xml
    Release\USX_1\<book code>.usx
    Release\styles.xml
    Release\versification.vrs
    SearchIndex\...
```

`rvmetadata.xml` uses the root spelling `RVBibleMetdata`. Its `abbreviation`
identifies KJV; `name` identifies King James Version. The installed metadata
reports version 2.2.1 and revision 24. The USX files declare version 3.0 and
explicit chapter/verse milestones. The reader does not use binary preferences,
search indexes, or license files. No package data is redistributed in Git.

## Implementation

`tools/ScriptureSync.ProPresenter.BibleSpike` contains a catalog, USX reader, and
console diagnostic. It references the existing Core parser and now returns
shared `ScriptureSync.Core.Bibles` verse/passage models used by the
[destination-independent composer](scripture-composition.md).

The catalog discovers package directories containing `rvmetadata.xml`, maps
abbreviations case-insensitively, rejects ambiguous codes and missing metadata,
and accepts an explicit root override. The default path is based on this
installation, not a guarantee about all ProPresenter versions.

The reader maps the parser's canonical English book names to 66 USX book codes.
Aliases, numeric prefixes, and alternate names are resolved by the existing
parser. It uses verse start/end milestones, preserves inline character text,
omits notes and headings, and normalizes whitespace between paragraphs.
A numbered verse in an `iex` paragraph occurs at Psalm 72:20 in this package;
that explicit verse is retained. Unnumbered introductory paragraphs are omitted.

Files are opened with `FileAccess.Read`. XML external resolution is disabled and
DTDs are prohibited. Missing translations, books, chapters and verses produce
errors. Duplicate or empty verses and mismatched verse milestones are rejected.
USX versions other than 3.0 and bridged/suffixed verse numbers are deliberately
unsupported. Unknown paragraph styles containing verses fail explicitly.
The reader does not reproduce typography, red-letter styling, poetry layout,
footnotes, or copyright display metadata; production publishing still needs
those requirements considered separately.

USX semantics were checked against the
[USX 3.0 element reference](https://ubsicap.github.io/usx/elements.html).
The installed format is not established here as a supported Renewed Vision
integration contract. Other packages, languages, layouts, and versions require
additional fixtures and live checks.

## Running

From the repository root:

```powershell
dotnet run --project tools\ScriptureSync.ProPresenter.BibleSpike
# Optional location override:
dotnet run --project tools\ScriptureSync.ProPresenter.BibleSpike -- --bible-root "C:\path\to\Bibles"
```

The diagnostic discovers installed translations and runs the KJV cases below.
It prints references/counts only, never Bible text. Its reader returns structured
text in memory. Exit codes are 0 for success, 1 for data/access failure, and 2
for invalid command syntax.

## Live checks

| Passage | Returned verses |
| --- | ---: |
| John 3:16 | 1 |
| Psalm 23:1-4 | 4 |
| 1 Corinthians 13:4-7 | 4 |
| 1 John 4:8 | 1 |
| Song of Solomon 2:1 | 1 |
| Psalm 119:105 | 1 |
| Jude 1:24-25 | 2 |
| John 3:16-4:2 | 23 |
| Psalm 23 | 6 |
| Song of Songs 2:1 | 1 |
| 1 Jn 4:7-10 | 4 |
| John 3:16,18 | 2 |

All returned ordered, nonempty verses with the expected counts; the process
exited 0. Before/after SHA-256 manifests covered **77 package files**, with no
added, removed, or changed files. Manifests remained outside the repository.

These are structural retrieval checks against the installed source. No manual
side-by-side comparison with ProPresenter's displayed text was performed, and no
claim is made that all 66 books or all USX features have been validated.

## Automated checks

```powershell
dotnet test tests\ScriptureSync.Tests\ScriptureSync.Tests.csproj
git diff --check
```

At the initial Bible-spike checkpoint, **86 tests passed**. The final PR Release
validation passes **94 tests, 0 failed, 0 skipped**, including two additional
chapter-milestone rejection cases. Fourteen Bible-reader test cases use
synthetic text only. They cover multiple translation discovery, case-insensitive
mapping, ambiguity, aliases, chapter/range/list selection, inline text, note and
heading exclusion, paragraph spacing, read-only source preservation, missing
data, unsupported versions, malformed milestones, DTD rejection, and malformed
metadata. Whitespace validation passed.

## Remaining gate

The observed KJV package is usable for the next presentation-file experiment.
Before production, compare sample text with the ProPresenter Bible window and
validate each additional supported translation. No slide generation, template
editing, playlist mutation, or production Bible-provider integration was added.
