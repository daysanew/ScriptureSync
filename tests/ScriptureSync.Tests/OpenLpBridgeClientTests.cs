using System.Net;
using System.Text;
using ScriptureSync.OpenLP;

namespace ScriptureSync.Tests;

public sealed class OpenLpBridgeClientTests
{
    [Fact]
    public async Task Search_and_add_send_explicit_bible_without_remote_translation_switch()
    {
        var handler = new RecordingHandler(
            Json(HttpStatusCode.OK, """{"status":"ready","bridge_version":"0.1"}"""),
            Json(HttpStatusCode.OK,
                """{"bibles":["KJV","New Living Translation (NLT)"]}"""),
            Json(HttpStatusCode.OK,
                """{"reference":"John 3:16","bible":"KJV","added":true,"service_item_title":"John 3:16 (KJV)"}"""));
        using var httpClient = new HttpClient(handler);
        using var client = new OpenLpBridgeClient(
            new Uri("http://127.0.0.1:4317/v1/"), httpClient);

        var connection = await client.PrepareAsync();
        var result = await client.AddScriptureAsync("KJV", "John 3:16");

        Assert.Equal("KJV", connection.InstalledBibles["KJV"]);
        Assert.Equal("New Living Translation (NLT)", connection.InstalledBibles["NLT"]);
        Assert.Equal("John 3:16 (KJV)", result!.ServiceItemTitle);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal("GET /v1/health", handler.Requests[0].Summary);
        Assert.Equal("GET /v1/bibles", handler.Requests[1].Summary);
        Assert.Equal("POST /v1/scriptures/add", handler.Requests[2].Summary);
        Assert.Contains("\"bible\":\"KJV\"", handler.Requests[2].Body);
        Assert.Contains("\"reference\":\"John 3:16\"", handler.Requests[2].Body);
        Assert.NotNull(handler.Requests[2].ContentLength);
        Assert.DoesNotContain(handler.Requests,
            request => request.Summary.Contains("search-options", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Missing_translation_is_rejected_before_a_search_request()
    {
        var handler = new RecordingHandler(
            Json(HttpStatusCode.OK, """{"status":"ready","bridge_version":"0.1"}"""),
            Json(HttpStatusCode.OK, """{"bibles":["KJV"]}"""));
        using var httpClient = new HttpClient(handler);
        using var client = new OpenLpBridgeClient(
            new Uri("http://127.0.0.1:4317/v1/"), httpClient);

        await client.PrepareAsync();

        await Assert.ThrowsAsync<OpenLpBibleNotInstalledException>(
            () => client.AddScriptureAsync("NLT", "John 3:16"));
        Assert.Equal(2, handler.Requests.Count);
    }

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string json) =>
        new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class RecordingHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RecordedRequest(
                $"{request.Method} {request.RequestUri!.AbsolutePath}",
                body,
                request.Content?.Headers.ContentLength));
            return _responses.Dequeue();
        }
    }

    private sealed record RecordedRequest(string Summary, string Body, long? ContentLength);
}
