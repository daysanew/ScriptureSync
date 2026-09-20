using Pro.SerializationInterop.RVProtoData;
using System.Text.Json;

namespace ScriptureSync.ProPresenter;

public sealed record PcoDestination(string ItemId, string PcoItemId, string Name, string? LinkedPath);
public sealed record PcoPlaylist(string Id, string ServiceId, string PlanId, IReadOnlyList<PcoDestination> Items);
public sealed record PcoLinkRequest(PcoDestination Target, ProPresenterPublishPlan Presentation);

/// <summary>Validates native PCO identities before the application orchestrates a restart.</summary>
public static class ProPresenterPcoSync
{
    public static string Workspace(string libraryDirectory, Uri address)
    {
        if (!address.IsLoopback) throw new InvalidOperationException("PCO linking requires ProPresenter on this computer.");
        var library = new DirectoryInfo(Path.GetFullPath(libraryDirectory));
        if (library.Parent?.Name != "Libraries" || library.Parent.Parent is null)
            throw new InvalidDataException("Select a library inside the ProPresenter workspace's Libraries folder.");
        var workspace = library.Parent.Parent.FullName;
        if (!File.Exists(Path.Combine(workspace, "Playlists", "Library")))
            throw new InvalidDataException("The native playlist file is missing from the selected workspace.");
        return workspace;
    }

    public static PcoPlaylist Read(byte[] bytes, string playlistId)
    {
        var matches = Walk(ProPresenterNativePlaylist.ReadRoot(bytes)).Where(p => p.Uuid?.String == playlistId).ToArray();
        if (matches.Length != 1 || matches[0].PcoPlan is not { } plan)
            throw new InvalidDataException("The selected playlist is not a uniquely identified native PCO playlist. Refresh ProPresenter and try again.");
        var playlist = matches[0];
        var planId = Id(plan.PlanIdStr, plan.PlanIdNum);
        var serviceId = Id(plan.ParentIdStr, plan.ParentIdNum);
        var items = (playlist.Items?.Items ?? []).Where(i => i.PlanningCenter?.Item is { } item &&
            item.ItemType != PlanningCenterPlan.Types.PlanItem.Types.PlanItemType.Header).Select(i =>
        {
            var pco = i.PlanningCenter.Item;
            if (Id(pco.ParentIdStr, pco.ParentIdNum) != planId || Id(pco.ServiceIdStr, pco.ServiceIdNum) != serviceId)
                throw new InvalidDataException("PCO item belongs to a different plan or service.");
            return new PcoDestination(i.Uuid.String, Id(pco.PcoIdStr, pco.PcoIdNum), pco.Name,
                i.PlanningCenter.LinkedData?.Presentation?.DocumentPath?.AbsoluteString);
        }).ToArray();
        if (items.Select(i => i.ItemId).Distinct().Count() != items.Length || items.Select(i => i.PcoItemId).Distinct().Count() != items.Length)
            throw new InvalidDataException("Duplicate PCO item identities; resolve them in ProPresenter before syncing.");
        return new(playlistId, serviceId, planId, items);
    }

    public static PcoDestination? Match(PcoPlaylist playlist, string? sourceKey)
    {
        if (sourceKey is null || !sourceKey.StartsWith("PCO:", StringComparison.Ordinal)) return null;
        var fields = sourceKey.Split(':');
        if (fields.Length != 5 || fields[1] != playlist.ServiceId || fields[2] != playlist.PlanId)
            throw new InvalidOperationException("This draft comes from a different PCO plan. Select its matching ProPresenter playlist.");
        return playlist.Items.SingleOrDefault(i => i.PcoItemId == fields[3])
            ?? throw new InvalidOperationException("A draft item is missing from this playlist. Refresh the plan in ProPresenter, then refresh this preview.");
    }

    public static bool Validate(PcoPlaylist playlist, IReadOnlyList<PcoLinkRequest> requests, JsonElement live)
    {
        if (requests.Select(r => r.Target.ItemId).Distinct().Count() != requests.Count)
            throw new InvalidOperationException("Each PCO item can link to one presentation. Use separate PCO items for multiple passages or translations.");
        var restart = false;
        foreach (var request in requests)
        {
            var target = playlist.Items.SingleOrDefault(i => i.ItemId == request.Target.ItemId);
            if (target is null || target.PcoItemId != request.Target.PcoItemId) throw new InvalidOperationException("PCO mapping changed. Refresh preview.");
            if (!string.IsNullOrEmpty(target.LinkedPath) && !string.Equals(Path.GetFullPath(target.LinkedPath), Path.GetFullPath(request.Presentation.Path), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"{target.Name} already links to a different presentation. Its existing link will be preserved.");
            var matches = live.GetProperty("items").EnumerateArray().Where(i => i.GetProperty("id").GetProperty("uuid").GetString() == target.ItemId).ToArray();
            if (matches.Length != 1 || !matches[0].GetProperty("is_pco").GetBoolean()) throw new InvalidOperationException("Native and running playlists disagree. Refresh ProPresenter and preview again.");
            var actual = matches[0].TryGetProperty("target_uuid", out var value) ? value.GetString() : null;
            if (!string.IsNullOrEmpty(actual) && actual != request.Presentation.Id) throw new InvalidOperationException($"{target.Name} already has a different live link.");
            if (string.IsNullOrEmpty(target.LinkedPath) || actual != request.Presentation.Id) restart = true;
        }
        return restart;
    }

    public static byte[] Prepare(byte[] source, string workspace, PcoPlaylist expected, IReadOnlyList<PcoLinkRequest> requests)
    {
        var current = Read(source, expected.Id);
        if (current.PlanId != expected.PlanId || current.ServiceId != expected.ServiceId) throw new InvalidOperationException("PCO plan changed during shutdown.");
        var result = source;
        foreach (var request in requests)
            result = ProPresenterNativePlaylist.PrepareLink(result, workspace, current.Id, request.Target.ItemId, current.PlanId, request.Target.PcoItemId, request.Presentation.Path);
        return result;
    }

    private static string Id(string text, uint number) => string.IsNullOrEmpty(text) ? number.ToString(System.Globalization.CultureInfo.InvariantCulture) : text;
    private static IEnumerable<Playlist> Walk(Playlist root)
    {
        yield return root;
        foreach (var child in root.Children.Concat(root.Playlists?.Playlists ?? []))
            foreach (var descendant in Walk(child)) yield return descendant;
    }
}
