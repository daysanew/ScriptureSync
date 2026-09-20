using System.Net;
using System.Text;
using System.Text.Json;
using ScriptureSync.PlanningCenter;

namespace ScriptureSync.Tests;

public sealed class PlanningCenterAttachmentPublisherTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ScriptureSync-attachments-" + Guid.NewGuid());
    [Fact]
    public async Task Repeated_content_is_skipped_and_changed_content_updates_the_same_attachment()
    {
        using var handler = new Handler(); using var http = new HttpClient(handler);
        var publisher = new PlanningCenterAttachmentPublisher(http, "test", "secret", _directory);
        Assert.True(await publisher.PublishAsync("1", "2", "3", [1], default));
        Assert.False(await publisher.PublishAsync("1", "2", "3", [1], default));
        Assert.True(await publisher.PublishAsync("1", "2", "3", [2], default));
        Assert.Equal(1, handler.Creates); Assert.Equal(1, handler.Updates); Assert.Equal(2, handler.Uploads);
    }
    [Fact]
    public async Task Foreign_presentation_and_outside_edits_are_rejected_before_upload()
    {
        using var handler = new Handler { Filename = "Human.pro" }; using var http = new HttpClient(handler);
        var publisher = new PlanningCenterAttachmentPublisher(http, "test", "secret", _directory);
        await Assert.ThrowsAsync<InvalidOperationException>(() => publisher.PublishAsync("1", "2", "3", [1], default));
        Assert.Equal(0, handler.Uploads);
        handler.Filename = null;
        await publisher.PublishAsync("1", "2", "3", [1], default);
        handler.Stamp = "outside-edit";
        await Assert.ThrowsAsync<InvalidOperationException>(() => publisher.PublishAsync("1", "2", "3", [2], default));
        Assert.Equal(1, handler.Uploads);
    }
    [Fact]
    public async Task Unknown_create_outcome_does_not_duplicate_remote_attachment()
    {
        using var handler = new Handler { FailAfterCreate = true }; using var http = new HttpClient(handler);
        var publisher = new PlanningCenterAttachmentPublisher(http, "test", "secret", _directory);
        await Assert.ThrowsAsync<HttpRequestException>(() => publisher.PublishAsync("1", "2", "3", [1], default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => publisher.PublishAsync("1", "2", "3", [1], default));
        Assert.Equal(1, handler.Creates); Assert.Equal(1, handler.Uploads);
    }
    private sealed class Handler : HttpMessageHandler
    {
        public string? Filename; public string Stamp = "initial";
        public int Creates, Updates, Uploads; public bool FailAfterCreate;
        private object Attachment => new { id = "42", attributes = new { filename = Filename, updated_at = Stamp } };
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Assert.Equal("Basic", request.Headers.Authorization?.Scheme);
            if (request.RequestUri!.Host == "upload.planningcenteronline.com")
            {
                Uploads++;
                return Json(new { data = new[] { new { id = "upload-id" } } });
            }
            if (request.Method == HttpMethod.Get) return Json(new { data = Filename is null ? Array.Empty<object>() : new[] { Attachment } });
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            Filename = body.RootElement.GetProperty("data").GetProperty("attributes").GetProperty("filename").GetString();
            if (request.Method == HttpMethod.Post) { Creates++; if (FailAfterCreate) throw new HttpRequestException("Connection lost after remote create"); }
            else { Assert.Equal(HttpMethod.Patch, request.Method); Assert.EndsWith("/42", request.RequestUri.AbsolutePath); Updates++; }
            Stamp = "update-" + (Creates + Updates);
            return Json(new { data = Attachment });
        }
        private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json") };
    }
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
