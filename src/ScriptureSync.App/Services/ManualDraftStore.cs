using System.IO;
using System.Text.Json;
using ScriptureSync.Core.Configuration;
using ScriptureSync.Core.Logging;

namespace ScriptureSync.App.Services;

public sealed record StoredDraftItem(Guid Id, string RawText, string Source);

public sealed class ManualDraftStore(LocalAppPaths paths, IAppLogger logger)
{
    private readonly string _draftFile = Path.Combine(paths.DataDirectory, "manual-draft.json");
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public IReadOnlyList<StoredDraftItem> Load()
    {
        try
        {
            if (!File.Exists(_draftFile))
            {
                return [];
            }

            return JsonSerializer.Deserialize<List<StoredDraftItem>>(
                File.ReadAllText(_draftFile),
                _jsonOptions) ?? [];
        }
        catch (Exception exception)
        {
            logger.Error("The saved manual draft could not be loaded.", exception);
            return [];
        }
    }

    public void Save(IEnumerable<StoredDraftItem> items)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_draftFile)!);
            File.WriteAllText(_draftFile, JsonSerializer.Serialize(items, _jsonOptions));
        }
        catch (Exception exception)
        {
            logger.Error("The manual draft could not be saved.", exception);
        }
    }
}
