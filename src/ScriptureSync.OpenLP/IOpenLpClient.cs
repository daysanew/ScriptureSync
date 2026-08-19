namespace ScriptureSync.OpenLP;

public interface IOpenLpClient
{
    Task<OpenLpConnectionInfo> GetConnectionInfoAsync(CancellationToken cancellationToken = default);
    Task<OpenLpConnectionInfo> PrepareAsync(CancellationToken cancellationToken = default);
    Task<OpenLpAddResult?> AddScriptureAsync(
        string translationCode,
        string reference,
        CancellationToken cancellationToken = default);
}

public sealed record OpenLpConnectionInfo(
    int ApiVersion,
    int ApiRevision,
    IReadOnlyDictionary<string, string> InstalledBibles,
    string SelectedBible);

public sealed record OpenLpAddResult(
    string Reference,
    string Bible,
    string ServiceItemTitle);
