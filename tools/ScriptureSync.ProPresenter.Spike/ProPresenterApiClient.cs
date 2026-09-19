using System.Text.Json;

namespace ScriptureSync.ProPresenter.Spike;

public sealed record ProPresenterVersion(string Description, string ApiVersion, string Platform);
public sealed record ProPresenterItem(string Id, string Name);

public sealed class ProPresenterDiagnosticException(string message, Exception? inner = null)
    : Exception(message, inner);

/// <summary>Informational endpoints only. GET trigger/focus endpoints are deliberately excluded.</summary>
public sealed class ProPresenterApiClient
{
    private readonly HttpClient _http;
    private readonly Uri _address;

    // The caller owns the HTTP client and its timeout policy.
    public ProPresenterApiClient(HttpClient http, Uri address)
    {
        if (!address.IsAbsoluteUri || (address.Scheme != "http" && address.Scheme != "https") ||
            !string.IsNullOrEmpty(address.UserInfo) || address.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(address.Query) || !string.IsNullOrEmpty(address.Fragment))
            throw new ArgumentException("Use a server address such as http://127.0.0.1:49627 with no path or credentials.");
        _http = http;
        _address = address;
    }

    public Task<ProPresenterVersion> GetVersionAsync(CancellationToken token = default) =>
        ReadAsync("version", root =>
        {
            var version = new ProPresenterVersion(Text(root, "host_description"), Text(root, "api_version"), Text(root, "platform"));
            if (version.ApiVersion != "v1")
                throw new ProPresenterDiagnosticException($"Unsupported API version '{version.ApiVersion}'; this spike supports v1 only.");
            return version;
        }, token);

    public Task<IReadOnlyList<ProPresenterItem>> GetLibrariesAsync(CancellationToken token = default) =>
        ReadAsync<IReadOnlyList<ProPresenterItem>>("v1/libraries", root => root.EnumerateArray().Select(Item).ToArray(), token);

    public Task<IReadOnlyList<ProPresenterItem>> GetPresentationsAsync(string libraryId, CancellationToken token = default) =>
        ReadAsync<IReadOnlyList<ProPresenterItem>>($"v1/library/{Segment(libraryId)}",
            root => root.GetProperty("items").EnumerateArray().Select(Item).ToArray(), token);

    public Task<IReadOnlyList<ProPresenterItem>> GetPlaylistsAsync(CancellationToken token = default) =>
        ReadAsync<IReadOnlyList<ProPresenterItem>>("v1/playlists", root =>
        {
            var playlists = new List<ProPresenterItem>();
            AddPlaylists(root, playlists);
            return playlists;
        }, token);

    // Retain the response shape for investigation; do not assume PCO IDs or linkage fields exist.
    public Task<JsonElement> GetPlaylistContentsAsync(string playlistId, CancellationToken token = default) =>
        ReadAsync($"v1/playlist/{Segment(playlistId)}", root =>
        {
            _ = Item(root.GetProperty("id"));
            _ = root.GetProperty("items").GetArrayLength();
            return root.Clone();
        }, token);

    private static void AddPlaylists(JsonElement nodes, List<ProPresenterItem> result)
    {
        foreach (var node in nodes.EnumerateArray())
        {
            var type = Text(node, "field_type");
            if (type == "playlist") result.Add(Item(node.GetProperty("id")));
            AddPlaylists(node.GetProperty("children"), result);
        }
    }

    private static ProPresenterItem Item(JsonElement element) => new(Text(element, "uuid"), Text(element, "name"));

    private static string Text(JsonElement element, string property) =>
        element.GetProperty(property).GetString() ?? throw new JsonException($"'{property}' must be a string.");

    private static string Segment(string id)
    {
        // IDs returned by the API are UUIDs. Reject paths and special aliases to keep the allowlist narrow.
        if (!Guid.TryParse(id, out var uuid)) throw new ArgumentException("Expected a ProPresenter UUID.", nameof(id));
        return uuid.ToString();
    }

    private async Task<T> ReadAsync<T>(string endpoint, Func<JsonElement, T> parse, CancellationToken token)
    {
        try
        {
            using var response = await _http.GetAsync(new Uri(_address, endpoint), token);
            if (!response.IsSuccessStatusCode)
                throw new ProPresenterDiagnosticException($"GET /{endpoint}: HTTP {(int)response.StatusCode} ({response.ReasonPhrase}). Check the installed API documentation and network settings.");
            await using var stream = await response.Content.ReadAsStreamAsync(token);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: token);
            return parse(document.RootElement);
        }
        catch (OperationCanceledException exception) when (!token.IsCancellationRequested)
        {
            throw new ProPresenterDiagnosticException($"GET /{endpoint} timed out. Check that ProPresenter is responsive and its network API is enabled.", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new ProPresenterDiagnosticException($"Cannot reach {_address} for GET /{endpoint}. Check the host, port, and ProPresenter network settings. {exception.Message}", exception);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new ProPresenterDiagnosticException($"GET /{endpoint} returned malformed or unexpected JSON. Check compatibility with the installed ProPresenter version.", exception);
        }
    }
}
