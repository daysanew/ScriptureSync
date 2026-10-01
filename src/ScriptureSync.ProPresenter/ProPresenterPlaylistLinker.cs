using System.Text.Json;
using System.Text.Json.Nodes;

namespace ScriptureSync.ProPresenter;

public sealed record PlaylistLink(string PresentationId, string Title, int? TargetIndex);

public static class ProPresenterPlaylistLinker
{
    public static JsonElement BuildItems(JsonElement snapshot, IReadOnlyList<PlaylistLink> links)
    {
        var items = JsonNode.Parse(snapshot.GetProperty("items").GetRawText())!.AsArray();
        if (items.Any(item => item?["is_pco"]?.GetValue<bool>() == true))
            throw new InvalidOperationException("Publishing into a PCO-linked playlist is awaiting verified support. Select Library only or a regular playlist; existing PCO links will not be changed.");
        if (items.Any(item => item?["type"]?.GetValue<string>() != "presentation"))
            throw new InvalidOperationException("Use Library only or a playlist containing only presentations. ProPresenter rebuilds placeholder identities during playlist updates; mixed playlists are not yet supported.");
        var used = new HashSet<int>();
        foreach (var link in links)
        {
            var existing = items.Select((node, index) => (node, index))
                .Where(pair => Target(pair.node!) == link.PresentationId).ToArray();
            if (existing.Length > 1) throw new InvalidOperationException($"Multiple playlist items already reference {link.Title}. Resolve duplicates in ProPresenter before syncing.");
            var index = link.TargetIndex ?? (existing.Length == 1 ? existing[0].index : -1);
            if (existing.Length == 1 && existing[0].index != index)
                throw new InvalidOperationException($"{link.Title} is already linked elsewhere in this playlist. Keep its existing link or move it in ProPresenter first.");
            if (index >= items.Count || index < -1) throw new InvalidOperationException("Invalid playlist selection. Refresh preview.");
            if (index >= 0)
            {
                if (!used.Add(index)) throw new InvalidOperationException("Two presentations cannot replace the same playlist item. Select different targets or append them.");
                var item = items[index]!.AsObject();
                if (item["type"]?.GetValue<string>() == "placeholder")
                    throw new InvalidOperationException("The installed API cannot yet be used to link a placeholder while preserving its identity. Choose append / keep existing link.");
                if (item["type"]?.GetValue<string>() != "placeholder" && Target(item) != link.PresentationId)
                    throw new InvalidOperationException("Only an empty placeholder or the same ScriptureSync presentation can be linked. Choose another target.");
                if (item["type"]?.GetValue<string>() == "presentation" && Target(item) == link.PresentationId) continue;
                item["type"] = "presentation";
                item["target_uuid"] = link.PresentationId;
                item["presentation_info"] = new JsonObject { ["presentation_uuid"] = link.PresentationId, ["arrangement_name"] = "", ["arrangement_uuid"] = "" };
            }
            else
            {
                items.Add(new JsonObject
                {
                    ["id"] = new JsonObject { ["uuid"] = link.PresentationId, ["name"] = link.Title, ["index"] = items.Count },
                    ["type"] = "presentation", ["is_hidden"] = false, ["is_pco"] = false,
                    ["target_uuid"] = link.PresentationId,
                    ["presentation_info"] = new JsonObject { ["presentation_uuid"] = link.PresentationId, ["arrangement_name"] = "", ["arrangement_uuid"] = "" }
                });
            }
        }
        return JsonSerializer.SerializeToElement(items);
    }

    public static async Task LinkAsync(IProPresenterApiClient api, string playlistId, JsonElement snapshot,
        IReadOnlyList<PlaylistLink> links, string backupDirectory, CancellationToken token = default)
    {
        var items = BuildItems(snapshot, links);
        if (JsonElement.DeepEquals(snapshot.GetProperty("items"), items)) return;
        Directory.CreateDirectory(backupDirectory);
        await File.WriteAllTextAsync(Path.Combine(backupDirectory, $"playlist-{playlistId}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.json"), snapshot.GetRawText(), token);
        await api.UpdatePlaylistAsync(playlistId, snapshot, items, token);
        var result = await api.GetPlaylistContentsAsync(playlistId, token);
        var actual = JsonNode.Parse(result.GetProperty("items").GetRawText())!.AsArray();
        if (actual.Count != items.GetArrayLength()) throw new InvalidOperationException("Playlist verification failed: item count changed. Inspect ProPresenter before retrying.");
        foreach (var link in links)
            if (actual.Count(item => Target(item!) == link.PresentationId) != 1)
                throw new InvalidOperationException($"Playlist did not confirm the link for {link.Title}. Inspect ProPresenter before retrying.");
        var original = JsonNode.Parse(snapshot.GetProperty("items").GetRawText())!.AsArray();
        for (var i = 0; i < original.Count; i++)
        {
            var before = original[i]!;
            var after = actual[i]!;
            if (before["id"]?["uuid"]?.ToJsonString() != after["id"]?["uuid"]?.ToJsonString() ||
                before["id"]?["name"]?.ToJsonString() != after["id"]?["name"]?.ToJsonString() ||
                before["type"]?.ToJsonString() != after["type"]?.ToJsonString() ||
                before["is_hidden"]?.ToJsonString() != after["is_hidden"]?.ToJsonString() ||
                before["is_pco"]?.ToJsonString() != after["is_pco"]?.ToJsonString() || Target(before) != Target(after))
                throw new InvalidOperationException("ProPresenter changed an existing playlist item during verification. Inspect the playlist and its saved backup before continuing.");
        }
    }
    private static string? Target(JsonNode item) => item["target_uuid"]?.GetValue<string>() ?? item["presentation_info"]?["presentation_uuid"]?.GetValue<string>();
}
