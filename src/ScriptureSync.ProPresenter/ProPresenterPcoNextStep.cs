using Pro.SerializationInterop.RVProtoData;

namespace ScriptureSync.ProPresenter;

public enum PcoImportState { Imported, NotImported, Unknown, MultiplePlaylists }
public sealed record PcoNextStep(PcoImportState State, string Message);

public static class ProPresenterPcoNextStep
{
    public static PcoNextStep Unknown => new(PcoImportState.Unknown,
        "Could not check whether this plan is in ProPresenter. If you already imported it, refresh that playlist and choose Write Over for changed scripture files. Otherwise, import the service plan through Planning Center Service. Enable Automatically Download Presentations and Media in ProPresenter.");

    public static PcoNextStep Evaluate(byte[] nativeFile, IReadOnlyCollection<string> livePlaylistIds, string serviceId, string planId)
    {
        var root = ProPresenterNativePlaylist.ReadRoot(nativeFile);
        var playlists = Walk(root).Where(p => p.PcoPlan is not null).ToArray();
        // A native file that has not caught up with the running app cannot prove absence.
        var all = Walk(root).Where(p => p.Uuid is not null).Select(p => p.Uuid.String).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matches = playlists.Where(p => Id(p.PcoPlan.ParentIdStr, p.PcoPlan.ParentIdNum) == serviceId &&
            Id(p.PcoPlan.PlanIdStr, p.PcoPlan.PlanIdNum) == planId && livePlaylistIds.Contains(p.Uuid.String, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (matches.Length > 1) return new(PcoImportState.MultiplePlaylists,
            "This plan is imported more than once in ProPresenter. Refresh the playlist you intend to use and choose Write Over for changed scripture files. Do not import another copy.");
        if (matches.Length == 1) return new(PcoImportState.Imported,
            $"This plan is already in ProPresenter ({matches[0].Name}). Refresh that playlist to download the scripture attachment. If prompted for a changed scripture file, choose Write Over. No restart is needed.");
        if (livePlaylistIds.Any(id => !all.Contains(id))) return Unknown;
        return new(PcoImportState.NotImported,
            "You can now import this service plan in ProPresenter: Library + → Planning Center Service. With Automatically Download Presentations and Media enabled, the scripture presentation will download and link to its PCO item.");
    }

    public static async Task<PcoNextStep> CheckAsync(string libraryDirectory, Uri address, IProPresenterApiClient api,
        string serviceId, string planId, CancellationToken token = default)
    {
        try
        {
            var workspace = ProPresenterPcoSync.Workspace(libraryDirectory, address);
            var playlists = await api.GetPlaylistsAsync(token);
            var file = await File.ReadAllBytesAsync(Path.Combine(workspace, "Playlists", "Library"), token);
            return Evaluate(file, playlists.Select(p => p.Id).ToArray(), serviceId, planId);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or
            System.Net.Http.HttpRequestException or System.Text.Json.JsonException or Google.Protobuf.InvalidProtocolBufferException or OperationCanceledException)
        { return Unknown; }
    }

    private static string Id(string text, uint number) => string.IsNullOrEmpty(text) ? number.ToString(System.Globalization.CultureInfo.InvariantCulture) : text;
    private static IEnumerable<Playlist> Walk(Playlist root)
    {
        yield return root;
        foreach (var child in root.Children.Concat(root.Playlists?.Playlists ?? []))
            foreach (var descendant in Walk(child)) yield return descendant;
    }
}
