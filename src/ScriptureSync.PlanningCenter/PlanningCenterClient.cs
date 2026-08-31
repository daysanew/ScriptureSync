using System.Net.Http.Headers;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ScriptureSync.Core.Models;

namespace ScriptureSync.PlanningCenter;

public sealed partial class PlanningCenterClient : IPlanningCenterClient
{
    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;

    public PlanningCenterClient(
        string applicationId,
        string secret,
        HttpClient? httpClient = null,
        TimeProvider? timeProvider = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.BaseAddress ??= new Uri("https://api.planningcenteronline.com/services/v2/");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{applicationId}:{secret}")));
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<ServicePlan>> GetUpcomingPlansAsync(
        int windowDays,
        IReadOnlyCollection<string>? includedServiceTypeIds = null,
        CancellationToken cancellationToken = default)
    {
        var serviceTypes = await GetAllAsync("service_types?per_page=100", cancellationToken);
        var selectedTypes = serviceTypes.Where(type =>
            includedServiceTypeIds is null || includedServiceTypeIds.Count == 0 ||
            includedServiceTypeIds.Contains(type.Id, StringComparer.OrdinalIgnoreCase));
        var now = _timeProvider.GetLocalNow();
        var end = now.AddDays(Math.Max(1, windowDays));
        var plans = new List<ServicePlan>();

        foreach (var serviceType in selectedTypes)
        {
            var typeName = serviceType.String("name");
            var resources = await GetAllAsync(
                $"service_types/{Uri.EscapeDataString(serviceType.Id)}/plans?order=sort_date&per_page=100",
                cancellationToken);
            plans.AddRange(resources.Select(plan => new ServicePlan(
                    plan.Id,
                    serviceType.Id,
                    typeName,
                    plan.String("title"),
                    plan.Date("sort_date")))
                .Where(plan => plan.StartsAt >= now && plan.StartsAt <= end));
        }

        return plans.OrderBy(plan => plan.StartsAt).ToArray();
    }

    public async Task<IReadOnlyList<PlanningCenterScriptureItem>> GetScriptureItemsAsync(
        ServicePlan plan,
        IReadOnlyCollection<string> itemNames,
        CancellationToken cancellationToken = default)
    {
        var names = itemNames.Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var resources = await GetAllAsync(
            $"service_types/{Uri.EscapeDataString(plan.ServiceTypeId)}/plans/{Uri.EscapeDataString(plan.Id)}/items?per_page=100",
            cancellationToken);

        return resources
            .Where(item => names.Contains(item.String("title")))
            .OrderBy(item => item.Integer("sequence"))
            .Select(item => new PlanningCenterScriptureItem(
                item.Id,
                item.Integer("sequence"),
                item.String("title"),
                DetailsAsPlainText(item.String("html_details"), item.String("description"))))
            .ToArray();
    }

    private async Task<IReadOnlyList<ApiResource>> GetAllAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        var results = new List<ApiResource>();
        string? next = relativeUrl;
        while (next is not null)
        {
            using var response = await _httpClient.GetAsync(next, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var message = response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                    ? "Planning Center rejected the credentials or the user lacks access to Services."
                    : $"Planning Center returned {(int)response.StatusCode} ({response.ReasonPhrase}).";
                throw new PlanningCenterException(message);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            results.AddRange(document.RootElement.GetProperty("data").EnumerateArray().Select(ApiResource.FromJson));
            next = ReadNextLink(document.RootElement);
        }
        return results;
    }

    private static string? ReadNextLink(JsonElement root)
    {
        if (!root.TryGetProperty("links", out var links) ||
            !links.TryGetProperty("next", out var next) || next.ValueKind is JsonValueKind.Null)
            return null;
        var value = next.GetString();
        if (string.IsNullOrWhiteSpace(value)) return null;
        var uri = new Uri(value, UriKind.RelativeOrAbsolute);
        return uri.IsAbsoluteUri ? uri.PathAndQuery.TrimStart('/')
            .Replace("services/v2/", string.Empty, StringComparison.OrdinalIgnoreCase) : value;
    }

    internal static string DetailsAsPlainText(string htmlDetails, string description)
    {
        if (string.IsNullOrWhiteSpace(htmlDetails)) return description.Trim();
        var withLines = BlockTagRegex().Replace(htmlDetails, "\n");
        var withoutTags = HtmlTagRegex().Replace(withLines, string.Empty);
        return string.Join('\n', WebUtility.HtmlDecode(withoutTags)
            .Replace("\r\n", "\n").Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed record ApiResource(string Id, JsonElement Attributes)
    {
        public static ApiResource FromJson(JsonElement element) =>
            new(element.GetProperty("id").GetString() ?? string.Empty, element.GetProperty("attributes").Clone());
        public string String(string name) => Attributes.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString() ?? string.Empty : string.Empty;
        public int Integer(string name) => Attributes.TryGetProperty(name, out var value) && value.TryGetInt32(out var result)
            ? result : 0;
        public DateTimeOffset Date(string name) => DateTimeOffset.Parse(String(name));
    }

    [GeneratedRegex(@"</?(?:p|div|br|li|ul|ol|h[1-6])\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockTagRegex();
    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRegex();
}
