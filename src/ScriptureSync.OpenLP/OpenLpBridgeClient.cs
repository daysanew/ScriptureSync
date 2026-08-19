using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScriptureSync.OpenLP;

public sealed class OpenLpBridgeClient : IOpenLpClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private IReadOnlyDictionary<string, string>? _installedBibles;

    public OpenLpBridgeClient(Uri baseAddress, HttpClient? httpClient = null)
    {
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.BaseAddress = baseAddress;
        _httpClient.Timeout = TimeSpan.FromSeconds(100);
    }

    public async Task<OpenLpConnectionInfo> GetConnectionInfoAsync(
        CancellationToken cancellationToken = default)
    {
        var health = await _httpClient.GetFromJsonAsync<HealthResponse>(
            "health", cancellationToken)
            ?? throw new OpenLpException("The ScriptureSync plugin returned an empty health response.");
        if (!string.Equals(health.Status, "ready", StringComparison.OrdinalIgnoreCase))
        {
            throw new OpenLpException("The ScriptureSync plugin is not ready.");
        }

        var response = await _httpClient.GetFromJsonAsync<BiblesResponse>(
            "bibles", cancellationToken)
            ?? throw new OpenLpException("The ScriptureSync plugin did not report its installed Bibles.");
        _installedBibles = response.Bibles
            .GroupBy(ToBibleCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        return new OpenLpConnectionInfo(1, 0, _installedBibles, string.Empty);
    }

    public Task<OpenLpConnectionInfo> PrepareAsync(
        CancellationToken cancellationToken = default) =>
        GetConnectionInfoAsync(cancellationToken);

    public async Task<OpenLpAddResult?> AddScriptureAsync(
        string translationCode,
        string reference,
        CancellationToken cancellationToken = default)
    {
        if (_installedBibles is null)
        {
            await GetConnectionInfoAsync(cancellationToken);
        }

        if (!_installedBibles!.TryGetValue(translationCode, out var installedName))
        {
            throw new OpenLpBibleNotInstalledException(translationCode);
        }

        using var response = await PostJsonAsync(
            "scriptures/add",
            new PassageRequest(installedName, reference),
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken);
            if (error?.Error.Contains("found no verses", StringComparison.OrdinalIgnoreCase) == true)
            {
                return null;
            }
            throw new OpenLpException(error?.Error ?? "The ScriptureSync plugin could not add the passage.");
        }
        await EnsureBridgeSuccessAsync(response, "add scripture", cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<AddResponse>(cancellationToken)
            ?? throw new OpenLpException("The ScriptureSync plugin returned an empty add response.");
        if (!result.Added)
        {
            throw new OpenLpException("OpenLP did not confirm the service addition.");
        }
        return new OpenLpAddResult(result.Reference, result.Bible, result.ServiceItemTitle);
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

    private async Task<HttpResponseMessage> PostJsonAsync<T>(
        string requestUri,
        T value,
        CancellationToken cancellationToken)
    {
        // OpenLP's bundled Python bridge intentionally uses a tiny stdlib HTTP
        // server. StringContent supplies Content-Length, unlike JsonContent's
        // chunked request body, and keeps the transport simple and predictable.
        var json = JsonSerializer.Serialize(value);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _httpClient.PostAsync(requestUri, content, cancellationToken);
    }

    private static async Task EnsureBridgeSuccessAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken);
        throw new OpenLpException(
            error?.Error ?? $"The ScriptureSync plugin could not {operation}.");
    }

    private sealed record PassageRequest(
        [property: JsonPropertyName("bible")] string Bible,
        [property: JsonPropertyName("reference")] string Reference);
    private sealed record HealthResponse(
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("bridge_version")] string BridgeVersion);
    private sealed record BiblesResponse(
        [property: JsonPropertyName("bibles")] List<string> Bibles);
    private sealed record AddResponse(
        [property: JsonPropertyName("reference")] string Reference,
        [property: JsonPropertyName("bible")] string Bible,
        [property: JsonPropertyName("added")] bool Added,
        [property: JsonPropertyName("service_item_title")] string ServiceItemTitle);
    private sealed record ErrorResponse(
        [property: JsonPropertyName("error")] string Error);
}
