# Destination-independent scripture composition

The shared composition pipeline is now:

```text
ScriptureReferenceParser -> PassageReference
Bible text provider -> PassageText / VerseText
ScripturePresentationComposer -> ScripturePresentation / ScriptureSlide
Destination writer -> formatted presentation
```

The Bible models live in `ScriptureSync.Core.Bibles`; presentation models and
the composer live in `ScriptureSync.Core.Presentations`. Core remains a plain
`net10.0` library with no references to WPF, OpenLP, ProPresenter, Planning
Center, or protobuf tooling. The previous spike-local Bible records were moved
into Core, and the Bible reader and template writer now consume those types.

## Contract

One resolved passage and translation produces one presentation. The title uses
the parser's canonical passage reference plus translation. Each supplied verse
produces one slide with its individual reference, unchanged text, and translation.
Cross-chapter ranges retain each verse's chapter number. Discontiguous selections
do not synthesize intervening verses. Multi-translation input is composed once
per resolved translation, preserving that translation's own text.

Only `OneVersePerSlide` is supported. Long verses stay on one logical slide;
the composer does not trim, split, reflow, or style text. The destination template
remains responsible for appearance and any font fitting. Notes, attribution, and
typographic metadata are not yet represented by these minimal models.

The text provider remains responsible for resolving the entire requested
selection and reporting missing data. The composer checks for nonempty text and
translation, valid verse numbering, matching book names, and strictly ordered,
unique verses. It rejects invalid input instead of sorting, deduplicating, or
publishing an empty presentation. Composition snapshots slides into a read-only
collection so later changes to the provider's list do not alter a preview.

The ProPresenter template spike uses the shared composer for titles, references,
and text while retaining its existing template validation, metadata updates, and
UUID handling. This exercises the shared pipeline without changing the WPF
application or the existing OpenLP reference-sync operation.

## Validation

```powershell
dotnet build ScriptureSync.slnx -c Release
dotnet test tests\ScriptureSync.Tests\ScriptureSync.Tests.csproj -c Release --no-build
```

Build passed with the same 26 unused-import warnings in the vendored protobuf
schemas and no errors. All **114 tests passed**, including 20 new composer cases.
The tests cover parser-to-slide references, whole chapters, cross-chapter ranges,
numbered book names, aliases, long Unicode text, multiple translations,
discontiguous verses, repeatability, collection isolation, and invalid input.
Existing OpenLP, Bible reader, and template transformation tests remain green.
This step adds no API calls or writes to the ProPresenter library.

Next: define the production Bible-provider and publisher contracts around these
models, then adapt the validated ProPresenter paths. Publishing configuration,
ownership, backups, and PCO mapping still require their own work before UI sync.
