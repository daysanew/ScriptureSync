using Google.Protobuf;
using Pro.SerializationInterop.RVProtoData;
using ScriptureSync.Core.Bibles;
using ScriptureSync.ProPresenter;


namespace ScriptureSync.Tests;

public sealed class ProPresenterTemplateTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Clones_styles_updates_links_and_roundtrips_without_mutating_source(int count)
    {
        var template = Fixture();
        var original = template.ToByteArray();
        var passage = new PassageText("TEST", new("John", 3, $"1-{count}"),
            Enumerable.Range(1, count).Select(n => new VerseText("John", 3, n, $"Synthetic verse {n}.")).ToArray());
        var output = ProPresenterDocumentWriter.Create(template, passage);
        Assert.Equal(original, template.ToByteArray());
        Assert.NotEqual(template.Uuid, output.Uuid);
        Assert.Equal(count, output.Cues.Count);
        Assert.Equal(count, output.Cues.Select(c => c.Uuid.String).Distinct().Count());
        Assert.Equal(count, output.Cues.Select(c => c.Actions[0].Slide.Presentation.BaseSlide.Elements[0].Element_.Uuid.String).Distinct().Count());
        Assert.Equal(output.Cues.Select(c => c.Uuid.String), output.CueGroups[0].CueIdentifiers.Select(id => id.String));
        Assert.Equal(output.CueGroups[0].Group.Uuid, output.Arrangements[0].GroupIdentifiers[0]);
        Assert.Equal(output.Arrangements[0].Uuid, output.SelectedArrangement);
        Assert.Equal(output, Presentation.Parser.ParseFrom(output.ToByteArray()));
        foreach (var cue in output.Cues)
        {
            var box = cue.Actions[0].Slide.Presentation.BaseSlide.Elements[1].Element_;
            Assert.Equal(0.75, box.Opacity);
            Assert.Contains("\\fs110\\cb2 Synthetic verse", box.Text.RtfData.ToStringUtf8());
        }
        Assert.Equal(count, output.BibleReference.VerseRange.End);
    }

    [Fact]
    public void Escapes_rtf_metacharacters_and_unicode_without_altering_prefix()
    {
        var output = ProPresenterDocumentWriter.ReplaceSimpleRtf(ByteString.CopyFromUtf8("{\\rtf0\\fs110\\cb2 Sample}"), "A{B}\\é");
        Assert.Equal("{\\rtf0\\fs110\\cb2 A\\{B\\}\\\\\\u233?}", output.ToStringUtf8());
        Assert.Throws<InvalidDataException>(() => ProPresenterDocumentWriter.ReplaceSimpleRtf(
            ByteString.CopyFromUtf8("{\\rtf0\\cb2 Styled \\b text}"), "Replacement"));
    }

    [Fact]
    public void Rejects_cue_links_to_slides_that_would_be_removed()
    {
        var template = Fixture();
        template.Cues[0].CompletionTargetUuid = Id("another-cue");
        Assert.Throws<InvalidDataException>(() => ProPresenterDocumentWriter.Create(template,
            new PassageText("TEST", new("John", 3, "1"), [new("John", 3, 1, "Synthetic.")])));
    }

    [Fact]
    public void Unknown_protobuf_fields_survive_the_transformation()
    {
        var bytes = Fixture().ToByteArray().Concat(new byte[] { 0xA0, 0x06, 0x7B }).ToArray(); // field 100 = 123
        var template = Presentation.Parser.ParseFrom(bytes);
        var output = ProPresenterDocumentWriter.Create(template, new PassageText("TEST", new("John", 3, "1"), [new("John", 3, 1, "Synthetic.")]));
        Assert.Equal(new byte[] { 0xA0, 0x06, 0x7B }, output.ToByteArray()[^3..]);
    }

    internal static Presentation Fixture()
    {
        var slide = new Slide { Uuid = Id("slide") };
        foreach (var name in new[] { "Reference", "Verse" })
            slide.Elements.Add(new Slide.Types.Element
            {
                Element_ = new Graphics.Types.Element
                {
                    Uuid = Id(name), Name = name, Opacity = 0.75,
                    Text = new Graphics.Types.Text { RtfData = ByteString.CopyFromUtf8("{\\rtf0\\fs110\\cb2 Placeholder}") }
                }
            });
        var cue = new Cue
        {
            Uuid = Id("cue"),
            CompletionTargetUuid = Id(Guid.Empty.ToString()),
            CompletionActionUuid = Id(Guid.Empty.ToString())
        };
        cue.Actions.Add(new Pro.SerializationInterop.RVProtoData.Action
        {
            Uuid = Id("action"), Slide = new Pro.SerializationInterop.RVProtoData.Action.Types.SlideType
            { Presentation = new PresentationSlide { BaseSlide = slide } }
        });
        var result = new Presentation
        {
            Uuid = Id("presentation"), SelectedArrangement = Id("arrangement"),
            BibleReference = new Presentation.Types.BibleReference { BookName = "John", TranslationInternalAbbreviation = "TEST" }
        };
        result.Cues.Add(cue);
        var group = new Presentation.Types.CueGroup { Group = new Group { Uuid = Id("group") } };
        group.CueIdentifiers.Add(Id("cue"));
        result.CueGroups.Add(group);
        var arrangement = new Presentation.Types.Arrangement { Uuid = Id("arrangement") };
        arrangement.GroupIdentifiers.Add(Id("group"));
        result.Arrangements.Add(arrangement);
        return result;
    }

    private static UUID Id(string value) => new() { String = value };
}
