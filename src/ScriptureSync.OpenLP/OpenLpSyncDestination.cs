using ScriptureSync.Core.Models;
using ScriptureSync.Core.Sync;

namespace ScriptureSync.OpenLP;

/// <summary>
/// Adapts OpenLP's atomic lookup/add operation without changing its bridge protocol.
/// The caller retains ownership of the supplied client.
/// </summary>
public sealed class OpenLpSyncDestination(IOpenLpClient client) : IScriptureSyncDestination
{
    public Task<ScriptureDestinationStatus> PrepareAsync(CancellationToken cancellationToken = default) =>
        TranslateErrorsAsync(async () =>
        {
            var connection = await client.PrepareAsync(cancellationToken);
            return new ScriptureDestinationStatus(connection.InstalledBibles.Count);
        });

    public Task<ScriptureSyncResult?> AddScriptureAsync(
        string translationCode,
        PassageReference passage,
        CancellationToken cancellationToken = default) =>
        TranslateErrorsAsync(async () =>
        {
            var result = await client.AddScriptureAsync(translationCode, passage.ToString(), cancellationToken);
            return result is null
                ? null
                : new ScriptureSyncResult(result.Reference, result.Bible, result.ServiceItemTitle);
        }, translationCode);

    private static async Task<T> TranslateErrorsAsync<T>(Func<Task<T>> operation, string? translationCode = null)
    {
        try
        {
            return await operation();
        }
        catch (OpenLpBibleNotInstalledException exception) when (translationCode is not null)
        {
            throw new ScriptureTranslationNotInstalledException(translationCode, exception);
        }
        catch (OpenLpException exception)
        {
            throw new ScriptureSyncException(exception.Message, exception);
        }
        catch (HttpRequestException exception)
        {
            throw new ScriptureDestinationUnavailableException(exception.Message, exception);
        }
    }
}
