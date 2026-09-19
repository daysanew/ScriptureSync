using System.Text;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Pro.SerializationInterop.RVProtoData;
using ScriptureSync.ProPresenter.BibleSpike;

namespace ScriptureSync.ProPresenter.TemplateSpike;

public static class TemplateWriter
{
    public static Presentation Create(Presentation template, PassageText passage)
    {
        if (template.Cues.Count == 0 || template.CueGroups.Count != 1 ||
            template.BibleReference?.BookName != passage.Reference.Book ||
            template.BibleReference.TranslationInternalAbbreviation != passage.Translation ||
            passage.Verses.Count == 0 || template.Timeline is { Cues.Count: > 0 } or { CuesV2.Count: > 0 })
            throw new InvalidDataException("Spike requires one cue group, no timeline cues, and the template's book and translation.");
        var group = template.CueGroups[0].Group;
        if (string.IsNullOrEmpty(template.Uuid?.String) || string.IsNullOrEmpty(group?.Uuid?.String) ||
            template.Arrangements.Any(a => a.GroupIdentifiers.Any(id => id.String != group.Uuid.String)))
            throw new InvalidDataException("Template has missing identities or arrangement references outside its single group.");
        var source = template.Cues[0];
        if (string.IsNullOrEmpty(source.Uuid?.String) ||
            source.CompletionTargetType != Cue.Types.CompletionTargetType.None ||
            HasReference(source.CompletionTargetUuid) || HasReference(source.CompletionActionUuid))
            throw new InvalidDataException($"Template cue has missing identity or completion links that cannot safely be cloned (target: {source.CompletionTargetType}, cue: {source.CompletionTargetUuid?.String}, action: {source.CompletionActionUuid?.String}).");
        if (source.Actions.Count != 1 || source.Actions[0].Slide?.Presentation?.BaseSlide is not { } slide ||
            slide.Elements.Count != 2 || slide.Elements.Any(e => e.DataLinks.Count > 0 || e.ChildBuilds.Count > 0))
            throw new InvalidDataException("Spike requires a simple two-text-box slide with one slide action and no data links or child builds.");
        if (string.IsNullOrEmpty(slide.Uuid?.String) || string.IsNullOrEmpty(source.Actions[0].Uuid?.String) ||
            slide.Elements.Any(e => string.IsNullOrEmpty(e.Element_?.Uuid?.String)))
            throw new InvalidDataException("Template slide/action/text boxes require identities before cloning.");
        foreach (var name in new[] { "Reference", "Verse" })
            if (slide.Elements.Count(e => e.Element_?.Name == name && e.Element_.Text is not null) != 1)
                throw new InvalidDataException($"Expected exactly one named {name} text box.");

        var output = template.Clone();
        output.Name = $"ScriptureSync TEST {passage.Reference} ({passage.Translation})";
        output.Cues.Clear();
        output.CueGroups[0].CueIdentifiers.Clear();
        foreach (var verse in passage.Verses)
        {
            var cue = source.Clone();
            cue.Name = $"{verse.Book} {verse.Chapter}:{verse.Verse}";
            foreach (var element in cue.Actions[0].Slide.Presentation.BaseSlide.Elements)
            {
                var text = element.Element_.Text;
                if (text.AlternateTexts.Count > 0)
                    throw new InvalidDataException("Alternate text is outside this spike's supported template shape.");
                text.RtfData = ReplaceSimpleRtf(text.RtfData,
                    element.Element_.Name == "Verse" ? verse.Text : $"{cue.Name} ({passage.Translation})");
            }
            RemapDefinedIds(cue);
            output.Cues.Add(cue);
            output.CueGroups[0].CueIdentifiers.Add(cue.Uuid.Clone());
        }
        var first = passage.Verses[0];
        var last = passage.Verses[^1];
        output.BibleReference.ChapterRange = new IntRange { Start = first.Chapter, End = last.Chapter };
        output.BibleReference.VerseRange = new IntRange { Start = first.Verse, End = last.Verse };
        RemapDefinedIds(output);
        return output;
    }

    // Deliberately supports only the observed single plain-text run. Never flatten arbitrary RTF.
    public static ByteString ReplaceSimpleRtf(ByteString source, string replacement)
    {
        var rtf = source.ToStringUtf8();
        const string marker = "\\cb2 ";
        var start = rtf.LastIndexOf(marker, StringComparison.Ordinal);
        if (!rtf.StartsWith("{\\rtf", StringComparison.Ordinal) || start < 0 || !rtf.EndsWith('}'))
            throw new InvalidDataException("Unsupported RTF template; expected the observed simple text run.");
        start += marker.Length;
        if (rtf[start..^1].Any(c => c is '\\' or '{' or '}' || c > 127))
            throw new InvalidDataException("Styled/escaped RTF body requires a richer text transformation; refusing to flatten it.");
        var escaped = new StringBuilder();
        foreach (var c in replacement)
        {
            if (c is '\\' or '{' or '}') escaped.Append('\\').Append(c);
            else if (c == '\n') escaped.Append("\\line ");
            else if (c == '\r') continue;
            else if (c > 127) escaped.Append("\\u").Append((short)c).Append('?');
            else escaped.Append(c);
        }
        return ByteString.CopyFromUtf8(rtf[..start] + escaped + "}");
    }

    private static void RemapDefinedIds(IMessage root)
    {
        var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Walk(root, message =>
        {
            var field = message.Descriptor.FindFieldByName("uuid");
            if (field?.Accessor.GetValue(message) is UUID id && !string.IsNullOrEmpty(id.String))
                mapping.TryAdd(id.String, Guid.NewGuid().ToString());
        });
        Walk(root, message =>
        {
            if (message is UUID id && mapping.TryGetValue(id.String, out var replacement)) id.String = replacement;
        });
    }

    private static bool HasReference(UUID? id) => !string.IsNullOrEmpty(id?.String) &&
        (!Guid.TryParse(id.String, out var value) || value != Guid.Empty);

    private static void Walk(IMessage message, System.Action<IMessage> visit)
    {
        visit(message);
        foreach (var field in message.Descriptor.Fields.InFieldNumberOrder())
        {
            if (field.FieldType != FieldType.Message) continue;
            var value = field.Accessor.GetValue(message);
            if (value is null) continue;
            if (field.IsRepeated)
                foreach (IMessage child in (System.Collections.IEnumerable)value) Walk(child, visit);
            else Walk((IMessage)value, visit);
        }
    }
}
