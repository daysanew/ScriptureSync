using System.Text.Json;

namespace ScriptureSync.ProPresenter;

public interface IProPresenterApiClient
{
    Task<ProPresenterVersion> GetVersionAsync(CancellationToken token = default);
    Task<IReadOnlyList<ProPresenterItem>> GetLibrariesAsync(CancellationToken token = default);
    Task<IReadOnlyList<ProPresenterItem>> GetPresentationsAsync(string libraryId, CancellationToken token = default);
    Task<IReadOnlyList<string>> GetPresentationSlideTextsAsync(string presentationId, CancellationToken token = default);
    Task<IReadOnlyList<ProPresenterItem>> GetPlaylistsAsync(CancellationToken token = default);
    Task<JsonElement> GetPlaylistContentsAsync(string playlistId, CancellationToken token = default);
    Task UpdatePlaylistAsync(string playlistId, JsonElement expected, JsonElement items, CancellationToken token = default);
}
