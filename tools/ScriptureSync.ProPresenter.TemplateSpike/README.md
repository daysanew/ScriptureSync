# Template-file spike

Status: template decoding and isolated file generation pass. The user reports
successful import, appearance, editing, and save/reopen checks for an imported
generated presentation. This tool is not a production publisher and is not
wired into the application UI or installer.

## Schema provenance

Source: [greyshirtguy/ProPresenter7-Proto](https://github.com/greyshirtguy/ProPresenter7-Proto),
pinned repository commit `bf6325d243897a6c64dde46eec803ec29f5f8569`.
The 32 files in `Schemas` are the transitive imports of
`autogen-proto/presentation.proto`; the upstream MIT license is included. Only trailing blank lines were normalized.
Google well-known descriptor imports are provided by the build tooling.

The latest schema-directory commit observed was
`6931e91501c2245de77aa8b9974f9f78532d805c`, dated 2026-06-30, with the message
`Update protobufs for ProPresenter 21.4,352583705`. The pinned `version.txt`
contains that version. The repository's scheduled workflow extracts schemas
from the macOS application. These schemas are unofficial and unsupported by
Renewed Vision; matching major/minor versions is not itself compatibility proof.

Pinned tooling: Google.Protobuf 3.36.2 and Grpc.Tools 2.84.0. Generated C# lives
under ignored `obj`, not in source control. A fresh protobuf compilation emits
upstream unused-import warnings; schema definitions were left intact.

## Observed template

The user's exported `ScriptureSync Template.pro` is 11,609 bytes, authored by
Windows ProPresenter 21.4.2 build 352584193. It contains three cues, one group,
and a Bible reference for John 3:16-18 KJV. Each cue has one presentation-slide
action and two text boxes named `Reference` and `Verse`. The translation is
part of the reference text, not a separate box. Decoding and serialization with
the pinned schema produced byte-for-byte identical data.

The exported file and generated files are not committed. Output lives in the
ignored `artifacts/propresenter-template-spike` directory. Bible text remains
local. The original export and the live ProPresenter library are not written.

## Transformation scope

The writer clones the first cue as the template for each verse, preserves its
protobuf styling/notes/actions, replaces the two named text fields, rebuilds cue
membership, and remaps defined UUIDs and their references. It updates the Bible
chapter/verse ranges and gives the presentation a `ScriptureSync TEST` name.
Unknown protobuf fields survive the generated types' clone/serialization path.

This experiment requires the same book and translation as the template, one
group, no timeline cues or active completion links, one slide action, two named text boxes, no data links or
child builds, and no alternate text. It supports only the observed simple RTF
text run ending after `\\cb2 `; embedded formatting/control sequences in that
run are rejected rather than flattened. RTF font/color/paragraph prefixes are
preserved and replacement text is escaped. This is deliberately not a general
RTF editor or a promise of compatibility with arbitrary templates.

No library import, playlist mutation, ownership reconciliation, stage-output
configuration, or production recovery workflow is implemented. Files are written
with CreateNew so an existing file is never overwritten. Output placement is a
local spike operation, not the later atomic production publishing protocol.

## Reproduce

From the repository root:

```powershell
dotnet run --project tools\ScriptureSync.ProPresenter.TemplateSpike -- "C:\Users\Chuck\Downloads\ScriptureSync Template.pro" artifacts\propresenter-template-spike "John 3:16-18 (KJV)"
```

Use a fresh output directory if that output filename already exists. The live
experiment also generated John 3:16 and John 3:16-20 (one and five slides). All
three output models parsed back equal after serialization, and the in-memory
source template remained byte-identical. No automatic import was attempted.

## Validation

`dotnet test tests\ScriptureSync.Tests\ScriptureSync.Tests.csproj` passed:
**94 passed, 0 failed, 0 skipped** in the final Release validation. Six synthetic-template test cases cover
one/three/five slide outputs, style retention, cue/group/arrangement references,
fresh identifiers, rejection of completion links, source immutability, RTF escaping/rejection, unknown-field
preservation, and protobuf round-trip equality. The existing source tests remain
green. No original Bible text or exported presentation is used as a committed
test fixture.

Manual verification procedure:

1. Import the generated three-slide file through ProPresenter's File > Import.
2. Confirm it appears as `ScriptureSync TEST ...`, separately from the original.
3. Verify verse text, reference, translation, typography, placement, and slides.
4. Verify audience output and stage output if configured.
5. Edit a generated text box, save, close, and reopen; confirm the edit persists.
6. Repeat with the one- and five-slide variants and check their group contents.

The user reported that import, appearance, editing, and save/reopen checks passed.
The exact variant(s) tested and stage-output coverage were not separately
identified. One- and five-slide outputs have automated structural checks; confirm
their live behavior and stage output before claiming complete acceptance coverage.
Production integration, arbitrary templates, and other ProPresenter versions
remain separate work.
