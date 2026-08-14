namespace ScriptureSync.OpenLP;

public interface IOpenLpClient
{
    Task<OpenLpConnectionInfo> GetConnectionInfoAsync(CancellationToken cancellationToken = default);
    Task<OpenLpConnectionInfo> PrepareAsync(CancellationToken cancellationToken = default);
    Task SelectBibleAsync(string requestedCode, CancellationToken cancellationToken = default);
    Task<OpenLpSearchResult?> FindScriptureAsync(string reference, CancellationToken cancellationToken = default);
    Task AddScriptureAndWaitAsync(string id, CancellationToken cancellationToken = default);
}

public sealed record OpenLpConnectionInfo(
    int ApiVersion,
    int ApiRevision,
    IReadOnlyDictionary<string, string> InstalledBibles,
    string SelectedBible);

public sealed record OpenLpSearchResult(string Id, string Reference, string VerseText);
