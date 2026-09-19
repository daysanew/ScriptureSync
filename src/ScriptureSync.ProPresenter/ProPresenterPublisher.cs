using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Google.Protobuf;
using Pro.SerializationInterop.RVProtoData;
using ScriptureSync.Core.Configuration;
using ScriptureSync.Core.Presentations;

namespace ScriptureSync.ProPresenter;

public sealed record OwnedPresentation(string Key, string Id, string Title, string Path, string ContentHash,
    string FileHash, string? PreviousFileHash = null, string? PreviousContentHash = null);
public sealed record ProPresenterPublishPlan(string Identity, string Key, string Title, string Id, string Path,
    PublishChange Change, string ContentHash, string? ExpectedFileHash, byte[] Bytes, IReadOnlyList<string> SlideTexts);

public sealed class ProPresenterPublisher(
    ProPresenterConfiguration configuration, string stateDirectory, IProPresenterApiClient api) : IPresentationPublisher
{
    private string StatePath => Path.Combine(stateDirectory, "ownership.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task ValidateAsync(CancellationToken token = default)
    {
        if (!File.Exists(configuration.TemplatePath)) throw new InvalidDataException("Choose an exported ProPresenter scripture template in Settings.");
        if (!Directory.Exists(configuration.LibraryDirectory)) throw new InvalidDataException("Choose the existing folder for the selected ProPresenter library.");
        if (!Guid.TryParse(configuration.LibraryId, out _)) throw new InvalidDataException("Connect and select a ProPresenter library in Settings.");
        await api.GetVersionAsync(token);
        if (!(await api.GetLibrariesAsync(token)).Any(l => l.Id.Equals(configuration.LibraryId, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("The selected library is no longer available. Select it again in Settings.");
    }

    public ProPresenterPublishPlan Preview(ScripturePresentation content, string identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identity);
        var directory = Path.GetFullPath(configuration.LibraryDirectory);
        var key = Hash(Encoding.UTF8.GetBytes(directory.ToUpperInvariant() + "\n" + identity));
        var path = Path.Combine(directory, $"ScriptureSync-{key[..24]}.pro");
        var source = File.ReadAllBytes(configuration.TemplatePath);
        var contentHash = Hash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(content) + Hash(source)));
        var entries = LoadState();
        entries.TryGetValue(key, out var previous);
        Presentation? existing = null;
        var actualHash = File.Exists(path) ? Hash(File.ReadAllBytes(path)) : null;
        if (actualHash is not null)
        {
            if (previous is null || !string.Equals(previous.Path, path, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Refusing to replace an unowned presentation: {path}");
            existing = Presentation.Parser.ParseFrom(File.ReadAllBytes(path));
            if (existing.Uuid?.String != previous.Id || !existing.Notes.Contains(Marker(key), StringComparison.Ordinal))
                throw new InvalidDataException($"Ownership check failed for {previous.Title}. No file was changed.");
            if (actualHash == previous.PreviousFileHash)
                previous = previous with { FileHash = actualHash, ContentHash = previous.PreviousContentHash ?? "" };
            else if (actualHash != previous.FileHash)
                throw new InvalidDataException($"{previous.Title} was edited outside ScriptureSync. Restore its backup or choose a new output library before syncing.");
        }
        var document = ProPresenterDocumentWriter.Create(Presentation.Parser.ParseFrom(source), content);
        if (existing is not null)
        {
            // Playlist arrangement selections must survive updates to the same owned presentation.
            if (existing.CueGroups.Count != 1 || existing.Arrangements.Count != document.Arrangements.Count)
                throw new InvalidDataException("The template's arrangement structure changed. Use a new draft or library for this template.");
            document.CueGroups[0].Group.Uuid = existing.CueGroups[0].Group.Uuid.Clone();
            for (var i = 0; i < document.Arrangements.Count; i++)
            {
                document.Arrangements[i].Uuid = existing.Arrangements[i].Uuid.Clone();
                document.Arrangements[i].GroupIdentifiers.Clear();
                document.Arrangements[i].GroupIdentifiers.Add(document.CueGroups[0].Group.Uuid.Clone());
            }
            document.SelectedArrangement = existing.SelectedArrangement?.Clone();
        }
        var id = previous?.Id ?? Guid.NewGuid().ToString();
        document.Uuid = new UUID { String = id };
        document.Notes = Marker(key);
        var bytes = document.ToByteArray();
        if (!document.Equals(Presentation.Parser.ParseFrom(bytes))) throw new InvalidDataException("Generated presentation failed validation.");
        var change = actualHash is null ? PublishChange.Create : previous!.ContentHash == contentHash ? PublishChange.Unchanged : PublishChange.Update;
        return new(identity, key, content.Title, id, path, change, contentHash, actualHash, bytes,
            content.Slides.Select(slide => $"{slide.Reference} ({slide.Translation})\n{slide.Text}").ToArray());
    }

    public IReadOnlyList<string> FindStale(IEnumerable<string> activeKeys) => LoadState().Values
        .Where(entry => !activeKeys.Contains(entry.Key) &&
            string.Equals(Path.GetDirectoryName(entry.Path), Path.GetFullPath(configuration.LibraryDirectory), StringComparison.OrdinalIgnoreCase))
        .Select(entry => entry.Title).ToArray();

    public async Task<PublishResult> PublishAsync(ScripturePresentation presentation, string identity, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(cancellationToken);
        return await ApplyAsync(Preview(presentation, identity), cancellationToken);
    }

    public async Task<PublishResult> ApplyAsync(ProPresenterPublishPlan plan, CancellationToken token = default)
    {
        Directory.CreateDirectory(stateDirectory);
        // An exclusive lock also covers other ScriptureSync windows/processes.
        using var stateLock = new FileStream(Path.Combine(stateDirectory, "publish.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        token.ThrowIfCancellationRequested();
        await api.GetVersionAsync(token);
        var currentHash = File.Exists(plan.Path) ? Hash(File.ReadAllBytes(plan.Path)) : null;
        if (currentHash != plan.ExpectedFileHash) throw new InvalidOperationException("A presentation changed since preview. Refresh before publishing.");
        if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(plan.Path)), Path.GetFullPath(configuration.LibraryDirectory), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Publication plan does not belong to the configured library.");
        if (plan.Change != PublishChange.Unchanged)
        {
            var state = LoadState();
            state.TryGetValue(plan.Key, out var previous);
            if (currentHash is not null && (previous is null || previous.Id != plan.Id))
                throw new InvalidOperationException("Ownership changed since preview. Refresh before publishing.");
            var document = Presentation.Parser.ParseFrom(plan.Bytes);
            if (document.Uuid?.String != plan.Id || document.Notes != Marker(plan.Key))
                throw new InvalidDataException("Invalid publication plan.");
            state[plan.Key] = new(plan.Key, plan.Id, plan.Title, plan.Path, plan.ContentHash, Hash(plan.Bytes),
                currentHash, previous?.ContentHash);
            // Reserve ownership before placement; pending writes are recoverable by hash on the next run.
            SaveState(state);
            var temporary = plan.Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await File.WriteAllBytesAsync(temporary, plan.Bytes, token);
                token.ThrowIfCancellationRequested();
                // ProPresenter on Windows ignores rename events. A copy produces the create/change events it watches.
                // The validated source and previous destination remain available for recovery after interrupted copies.
                if (currentHash is null) File.Copy(temporary, plan.Path, false);
                else
                {
                    var backups = Path.Combine(stateDirectory, "backups");
                    Directory.CreateDirectory(backups);
                    File.Copy(plan.Path, Path.Combine(backups, $"{plan.Key}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.pro"));
                    File.Copy(temporary, plan.Path, true);
                }
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var items = await api.GetPresentationsAsync(configuration.LibraryId, token);
            if (items.Any(item => item.Id.Equals(plan.Id, StringComparison.OrdinalIgnoreCase)))
            {
                var slides = await api.GetPresentationSlideTextsAsync(plan.Id, token);
                if (slides.Select(Normalize).SequenceEqual(plan.SlideTexts.Select(Normalize)))
                    return new(plan.Identity, plan.Id, plan.Change, plan.Title);
            }
            await Task.Delay(500, token);
        }
        throw new InvalidOperationException($"Saved {plan.Title}, but ProPresenter has not confirmed its slides. Check that the library folder matches the selected library; no playlist was changed. Refresh and retry.");
    }

    private Dictionary<string, OwnedPresentation> LoadState()
    {
        if (!File.Exists(StatePath)) return new();
        try { return JsonSerializer.Deserialize<Dictionary<string, OwnedPresentation>>(File.ReadAllText(StatePath)) ?? throw new JsonException(); }
        catch (JsonException e) { throw new InvalidDataException("ScriptureSync ownership data is unreadable. Restore ownership.json from backup before publishing.", e); }
    }
    private void SaveState(Dictionary<string, OwnedPresentation> entries)
    {
        Directory.CreateDirectory(stateDirectory);
        var temporary = StatePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(entries, JsonOptions));
        if (File.Exists(StatePath)) File.Copy(StatePath, StatePath + ".bak", true);
        File.Move(temporary, StatePath, true);
    }
    private static string Marker(string key) => $"ScriptureSync-owned:{key}";
    private static string Normalize(string value) => string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
