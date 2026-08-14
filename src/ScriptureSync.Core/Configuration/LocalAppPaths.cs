namespace ScriptureSync.Core.Configuration;

public sealed class LocalAppPaths
{
    public LocalAppPaths(string? localApplicationData = null)
    {
        var basePath = localApplicationData ??
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        DataDirectory = Path.Combine(basePath, "ScriptureSync");
    }

    public string DataDirectory { get; }

    public string ConfigurationFile => Path.Combine(DataDirectory, "settings.json");

    public string SyncStateFile => Path.Combine(DataDirectory, "sync-state.json");

    public string LogFile => Path.Combine(DataDirectory, "scripture-sync.log");
}
