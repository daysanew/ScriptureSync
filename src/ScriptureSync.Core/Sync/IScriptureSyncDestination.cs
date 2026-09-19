using ScriptureSync.Core.Models;

namespace ScriptureSync.Core.Sync;

/// <summary>
/// A reference-based destination that resolves and adds a passage as one operation.
/// This is not a publisher of precomposed slides or a Bible text provider.
/// </summary>
public interface IScriptureSyncDestination
{
    Task<ScriptureDestinationStatus> PrepareAsync(CancellationToken cancellationToken = default);

    /// <returns>The confirmed addition, or null when no verses were found.</returns>
    Task<ScriptureSyncResult?> AddScriptureAsync(
        string translationCode,
        PassageReference passage,
        CancellationToken cancellationToken = default);
}

public sealed record ScriptureDestinationStatus(int InstalledBibleCount);

public sealed record ScriptureSyncResult(string Reference, string Bible, string ItemTitle);
