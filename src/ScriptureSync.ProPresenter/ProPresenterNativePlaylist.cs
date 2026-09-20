using Google.Protobuf;
using Pro.SerializationInterop.RVProtoData;

namespace ScriptureSync.ProPresenter;

/// <summary>Offline transformation only. Callers must not write playlist files while ProPresenter is running.</summary>
public static class ProPresenterNativePlaylist
{
    public static byte[] PrepareLink(byte[] source, string workspace, string playlistId,
        string itemId, string planId, string pcoItemId, string presentationPath, bool replaceExisting = false)
    {
        var fullPath = Path.GetFullPath(presentationPath);
        var relative = Path.GetRelativePath(Path.GetFullPath(workspace), fullPath).Replace('\\', '/');
        if (!relative.StartsWith("Libraries/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The presentation must be inside this workspace's Libraries directory.");
        var presentation = Presentation.Parser.ParseFrom(File.ReadAllBytes(fullPath));
        if (!Guid.TryParse(presentation.Uuid?.String, out _) ||
            !presentation.Notes.StartsWith("ScriptureSync-owned:", StringComparison.Ordinal))
            throw new InvalidDataException("Only a ScriptureSync-owned presentation can be linked automatically.");
        var fields = ReadFields(source);
        var rootField = fields.SingleOrDefault(f => f.Number == 3 && f.Data is not null)
            ?? throw new InvalidDataException("Unsupported playlist document: missing root.");
        var root = Playlist.Parser.ParseFrom(rootField.Data);
        if (root.Type != Playlist.Types.Type.Root) throw new InvalidDataException("Expected a playlist root document.");
        var matches = Walk(root).Where(p => p.Uuid?.String == playlistId).ToArray();
        if (matches.Length != 1) throw new InvalidDataException("Playlist identity is missing or ambiguous.");
        var playlist = matches[0];
        if (playlist.PcoPlan is null || PlanId(playlist.PcoPlan) != planId)
            throw new InvalidDataException("The selected playlist does not match the expected PCO plan.");
        var items = playlist.Items?.Items.Where(i => i.Uuid?.String == itemId).ToArray() ?? [];
        if (items.Length != 1 || items[0].PlanningCenter?.Item is not { } pco || ItemId(pco) != pcoItemId ||
            (string.IsNullOrEmpty(pco.ParentIdStr) ? pco.ParentIdNum.ToString() : pco.ParentIdStr) != planId)
            throw new InvalidDataException("The selected item does not match the expected PCO item and plan.");
        var item = items[0];
        var current = item.PlanningCenter.LinkedData;
        if (current is not null && !replaceExisting)
        {
            if (current.Presentation?.DocumentPath?.AbsoluteString is not { Length: > 0 } path ||
                !string.Equals(Path.GetFullPath(path), fullPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("This PCO item already has a different link. It will not be replaced automatically.");
            return source.ToArray();
        }
        var linked = current?.Clone() ?? new PlaylistItem { Uuid = new UUID { String = Guid.NewGuid().ToString() } };
        linked.Name = presentation.Name;
        linked.Presentation = new PlaylistItem.Types.Presentation
        {
            DocumentPath = new URL
            {
                AbsoluteString = fullPath, Platform = URL.Types.Platform.Win32,
                Local = new URL.Types.LocalRelativePath { Root = URL.Types.LocalRelativePath.Types.Root.Show, Path = relative }
            },
            Arrangement = presentation.SelectedArrangement?.Clone()
        };
        item.PlanningCenter.LinkedData = linked;
        item.Name = presentation.Name;
        using var output = new MemoryStream();
        foreach (var field in fields)
        {
            if (ReferenceEquals(field, rootField))
            {
                using var coded = new CodedOutputStream(output, leaveOpen: true);
                coded.WriteTag(3, WireFormat.WireType.LengthDelimited);
                coded.WriteBytes(root.ToByteString());
                coded.Flush();
            }
            else output.Write(field.Raw);
        }
        return output.ToArray();
    }

    public static Playlist ReadRoot(byte[] document) => Playlist.Parser.ParseFrom(
        ReadFields(document).Single(f => f.Number == 3 && f.Data is not null).Data);

    private static string PlanId(PlanningCenterPlan p) => string.IsNullOrEmpty(p.PlanIdStr) ? p.PlanIdNum.ToString() : p.PlanIdStr;
    private static string ItemId(PlanningCenterPlan.Types.PlanItem p) => string.IsNullOrEmpty(p.PcoIdStr) ? p.PcoIdNum.ToString() : p.PcoIdStr;
    private static IEnumerable<Playlist> Walk(Playlist node)
    {
        yield return node;
        foreach (var child in node.Children.Concat(node.Playlists?.Playlists ?? []))
            foreach (var descendant in Walk(child)) yield return descendant;
    }
    private sealed record Field(int Number, byte[] Raw, byte[]? Data);
    private static List<Field> ReadFields(byte[] source)
    {
        var fields = new List<Field>();
        using var input = new CodedInputStream(source);
        while (!input.IsAtEnd)
        {
            var start = (int)input.Position;
            var tag = input.ReadTag();
            byte[]? data = null;
            if (WireFormat.GetTagWireType(tag) == WireFormat.WireType.LengthDelimited) data = input.ReadBytes().ToByteArray();
            else input.SkipLastField();
            fields.Add(new(WireFormat.GetTagFieldNumber(tag), source[start..(int)input.Position], data));
        }
        return fields;
    }
}
