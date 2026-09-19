using System.Net;
using System.Text;
using ScriptureSync.ProPresenter.Spike;

namespace ScriptureSync.Tests;

public sealed class ProPresenterApiClientTests
{
    private const string Id = "452b56ed-52ff-4111-92aa-7d3dea5423cc";

    [Fact]
    public async Task Enumerates_observed_response_shapes_using_only_informational_gets()
    {
        var paths = new List<string>();
        using var http = new HttpClient(new Handler((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(49627, request.RequestUri!.Port);
            var path = request.RequestUri.AbsolutePath;
            paths.Add(path);
            return Task.FromResult(Json(path switch
            {
                "/version" => """{"host_description":"ProPresenter 21.4.2","api_version":"v1","platform":"win"}""",
                "/v1/libraries" => $$"""[{"uuid":"{{Id}}","name":"Default"}]""",
                "/v1/library/" + Id => $$"""{"update_type":"all","items":[{"uuid":"{{Id}}","name":"Example"}]}""",
                "/v1/playlists" => $$"""[{"field_type":"folder","children":[{"id":{"uuid":"{{Id}}","name":"Sunday"},"field_type":"playlist","children":[]}]}]""",
                "/v1/playlist/" + Id => $$"""{"id":{"uuid":"{{Id}}","name":"Sunday"},"items":[]}""",
                _ => throw new InvalidOperationException(path)
            }));
        }));
        var client = Client(http);
        Assert.Equal("v1", (await client.GetVersionAsync()).ApiVersion);
        Assert.Equal("Default", Assert.Single(await client.GetLibrariesAsync()).Name);
        Assert.Equal("Example", Assert.Single(await client.GetPresentationsAsync(Id)).Name);
        Assert.Equal("Sunday", Assert.Single(await client.GetPlaylistsAsync()).Name);
        Assert.Equal(0, (await client.GetPlaylistContentsAsync(Id)).GetProperty("items").GetArrayLength());
        Assert.Equal(5, paths.Count);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{\"host_description\":42}")]
    public async Task Malformed_version_reports_endpoint(string body)
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(Json(body))));
        var error = await Assert.ThrowsAsync<ProPresenterDiagnosticException>(() => Client(http).GetVersionAsync());
        Assert.Contains("GET /version returned malformed", error.Message);
    }

    [Fact]
    public async Task Unsupported_api_is_explicit()
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(Json(
            """{"host_description":"Future","api_version":"v2","platform":"win"}"""))));
        var error = await Assert.ThrowsAsync<ProPresenterDiagnosticException>(() => Client(http).GetVersionAsync());
        Assert.Contains("Unsupported API version 'v2'", error.Message);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(404)]
    [InlineData(500)]
    public async Task Http_errors_are_actionable(int status)
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status))));
        var error = await Assert.ThrowsAsync<ProPresenterDiagnosticException>(() => Client(http).GetVersionAsync());
        Assert.Contains($"HTTP {status}", error.Message);
    }

    [Fact]
    public async Task Connection_failure_has_host_and_port()
    {
        using var http = new HttpClient(new Handler((_, _) => throw new HttpRequestException("Connection refused")));
        var error = await Assert.ThrowsAsync<ProPresenterDiagnosticException>(() => Client(http).GetVersionAsync());
        Assert.Contains("127.0.0.1:49627", error.Message);
        Assert.Contains("network settings", error.Message);
    }

    [Fact]
    public async Task Timeout_and_user_cancellation_are_distinct()
    {
        using var http = new HttpClient(new Handler((_, _) => throw new TaskCanceledException()));
        var error = await Assert.ThrowsAsync<ProPresenterDiagnosticException>(() => Client(http).GetVersionAsync());
        Assert.Contains("timed out", error.Message);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(http).GetVersionAsync(cancellation.Token));
    }

    [Theory]
    [InlineData("file:///tmp")]
    [InlineData("http://localhost:49627/v1/")]
    [InlineData("http://user:password@localhost:49627")]
    public void Rejects_non_server_addresses(string address)
    {
        using var http = new HttpClient();
        Assert.Throws<ArgumentException>(() => new ProPresenterApiClient(http, new Uri(address)));
    }

    [Fact]
    public async Task Rejects_endpoint_injection_in_ids()
    {
        using var http = new HttpClient();
        await Assert.ThrowsAsync<ArgumentException>(async () => await Client(http).GetPlaylistContentsAsync("active/trigger"));
    }

    private static ProPresenterApiClient Client(HttpClient http) => new(http, new Uri("http://127.0.0.1:49627"));
    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
