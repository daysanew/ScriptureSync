using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScriptureSync.OpenLP;

public sealed class OpenLpClient : IOpenLpClient, IDisposable
{
    private static readonly TimeSpan BibleStartupDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan BibleChangeDelay = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan AddCompletionTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan AddSettlingDelay = TimeSpan.FromSeconds(3);
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private IReadOnlyDictionary<string, string>? _installedBibles;
    private string? _selectedBible;
    private bool _isPrepared;

    public OpenLpClient(Uri baseAddress, HttpClient? httpClient = null)
    {
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.BaseAddress = baseAddress;
        _httpClient.Timeout = TimeSpan.FromSeconds(15);
    }

    public async Task<OpenLpConnectionInfo> GetConnectionInfoAsync(CancellationToken cancellationToken = default)
    {
        var system = await _httpClient.GetFromJsonAsync<SystemResponse>("core/system", cancellationToken)
            ?? throw new OpenLpException("OpenLP returned an empty system response.");
        var options = await _httpClient.GetFromJsonAsync<List<SearchOptionResponse>>(
            "plugins/bibles/search-options", cancellationToken) ?? [];
        var primary = options.FirstOrDefault(option =>
            option.Name.Equals("primary bible", StringComparison.OrdinalIgnoreCase))
            ?? throw new OpenLpException("OpenLP did not report its installed Bibles.");

        _installedBibles = primary.List
            .GroupBy(ToBibleCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        _selectedBible = primary.Selected;
        return new(system.ApiVersion, system.ApiRevision, _installedBibles, primary.Selected);
    }

    public async Task<OpenLpConnectionInfo> PrepareAsync(CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionInfoAsync(cancellationToken);
        if (_isPrepared)
        {
            return connection;
        }

        // OpenLP exposes its HTTP API before the Bible plugin and its Qt controls
        // are safe to manipulate. A switch during this window can crash Qt5Core.
        await Task.Delay(BibleStartupDelay, cancellationToken);

        // Force the currently selected Bible to initialize through the same read-only
        // path that the Web Remote uses before changing the primary Bible.
        _ = await FindScriptureAsync("John 3:16", cancellationToken);
        _isPrepared = true;
        return await GetConnectionInfoAsync(cancellationToken);
    }

    public async Task SelectBibleAsync(string requestedCode, CancellationToken cancellationToken = default)
    {
        if (!_isPrepared)
        {
            await PrepareAsync(cancellationToken);
        }
        else if (_installedBibles is null)
        {
            await GetConnectionInfoAsync(cancellationToken);
        }

        if (!_installedBibles!.TryGetValue(requestedCode, out var installedName))
        {
            throw new OpenLpBibleNotInstalledException(requestedCode);
        }

        if (string.Equals(_selectedBible, installedName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        using var response = await _httpClient.PostAsJsonAsync(
            "plugins/bibles/search-options",
            new { option = "primary bible", value = installedName }, cancellationToken);
        response.EnsureSuccessStatusCode();

        // This 15-second settling period was verified with consecutive Bible changes
        // on OpenLP 3.1.7 after the startup warm-up above.
        await Task.Delay(BibleChangeDelay, cancellationToken);

        var refreshed = await GetConnectionInfoAsync(cancellationToken);
        if (!string.Equals(refreshed.SelectedBible, installedName, StringComparison.OrdinalIgnoreCase))
        {
            throw new OpenLpException($"OpenLP did not switch to {requestedCode}.");
        }
    }

    public async Task<OpenLpSearchResult?> FindScriptureAsync(
        string reference,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            $"plugins/bibles/search?text={Uri.EscapeDataString(reference)}", cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
        {
            return null;
        }

        var first = root[0];
        if (first.ValueKind != JsonValueKind.Array || first.GetArrayLength() < 2)
        {
            throw new OpenLpException("OpenLP returned an incomplete Bible search result.");
        }

        var returnedReference = first[0].GetString()?.Trim() ?? string.Empty;
        var verseText = first[1].GetString()?.Trim() ?? string.Empty;
        if (returnedReference.Length == 0 || verseText.Length == 0 ||
            !ReferencesMatch(reference, returnedReference))
        {
            throw new OpenLpException(
                $"OpenLP returned an invalid result for {reference}.");
        }

        return new OpenLpSearchResult(returnedReference, returnedReference, verseText);
    }

    public async Task AddScriptureAndWaitAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var before = await GetServiceSnapshotAsync(cancellationToken);
        using var response = await _httpClient.PostAsJsonAsync(
            "plugins/bibles/add", new { id }, cancellationToken);
        response.EnsureSuccessStatusCode();

        var deadline = DateTimeOffset.UtcNow + AddCompletionTimeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            var after = await GetServiceSnapshotAsync(cancellationToken);
            if (after.Count > before.Count &&
                after.Items.Any(item => !before.Items.Contains(item)))
            {
                await Task.Delay(AddSettlingDelay, cancellationToken);
                return;
            }
        }

        throw new OpenLpException(
            $"OpenLP accepted {id} but did not finish adding it to the service.");
    }

    public void Dispose()
    {
        if (_ownsHttpClient) _httpClient.Dispose();
    }

    private static string ToBibleCode(string installedName)
    {
        var open = installedName.LastIndexOf('(');
        var close = installedName.LastIndexOf(')');
        return open >= 0 && close > open
            ? installedName[(open + 1)..close].Trim().ToUpperInvariant()
            : installedName.Trim().ToUpperInvariant();
    }

    private async Task<ServiceSnapshot> GetServiceSnapshotAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync("service/items", cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        var items = root.ValueKind == JsonValueKind.Array
            ? root.EnumerateArray().Select(item => item.GetRawText()).ToHashSet()
            : root.ValueKind == JsonValueKind.Object &&
              root.TryGetProperty("results", out var results) &&
              results.ValueKind == JsonValueKind.Array
                ? results.EnumerateArray().Select(item => item.GetRawText()).ToHashSet()
                : throw new OpenLpException("OpenLP returned an invalid service list.");
        return new ServiceSnapshot(items.Count, items);
    }

    private static bool ReferencesMatch(string requested, string returned) =>
        string.Equals(
            string.Concat(requested.Where(character => !char.IsWhiteSpace(character))),
            string.Concat(returned.Where(character => !char.IsWhiteSpace(character))),
            StringComparison.OrdinalIgnoreCase);

    private sealed record SystemResponse(
        [property: JsonPropertyName("api_version")] int ApiVersion,
        [property: JsonPropertyName("api_revision")] int ApiRevision);

    private sealed record SearchOptionResponse(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("list")] List<string> List,
        [property: JsonPropertyName("selected")] string Selected);

    private sealed record ServiceSnapshot(int Count, HashSet<string> Items);
}

public class OpenLpException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class OpenLpBibleNotInstalledException(string requestedCode)
    : OpenLpException($"Bible translation {requestedCode} is not installed in OpenLP.");
