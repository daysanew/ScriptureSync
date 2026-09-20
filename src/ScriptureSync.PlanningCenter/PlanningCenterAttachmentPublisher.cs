using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ScriptureSync.PlanningCenter;

public sealed class PlanningCenterAttachmentPublisher(HttpClient http, string applicationId, string secret, string stateDirectory)
{
    private sealed record Receipt(string Filename, string? AttachmentId, string? Hash, string? UpdatedAt);
    public async Task<bool> PublishAsync(string serviceId, string planId, string itemId, byte[] bytes, CancellationToken token)
    {
        foreach (var id in new[] { serviceId, planId, itemId })
            if (string.IsNullOrEmpty(id) || !id.All(char.IsAsciiDigit)) throw new ArgumentException("Invalid PCO identity.");
        Directory.CreateDirectory(stateDirectory);
        var key = $"{serviceId}-{planId}-{itemId}";
        using var stateLock = new FileStream(Path.Combine(stateDirectory, key + ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var stateFile = Path.Combine(stateDirectory, key + ".json");
        var receipt = File.Exists(stateFile)
            ? JsonSerializer.Deserialize<Receipt>(File.ReadAllText(stateFile)) ?? throw new InvalidDataException("Attachment ownership record is unreadable.")
            : new Receipt($"ScriptureSync-{planId}-{itemId}-{Guid.NewGuid():N}.pro", null, null, null);
        void Save(Receipt value)
        {
            var temporary = stateFile + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(value));
            File.Move(temporary, stateFile, true);
        }
        Save(receipt); // Reserve a unique filename before any remote mutation; recover ambiguous creates by this name.
        var endpoint = $"https://api.planningcenteronline.com/services/v2/service_types/{serviceId}/plans/{planId}/items/{itemId}/attachments";
        async Task<JsonElement> Send(HttpMethod method, string url, HttpContent? content = null)
        {
            using var request = new HttpRequestMessage(method, url) { Content = content };
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(applicationId + ":" + secret)));
            using var response = await http.SendAsync(request, token);
            if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Planning Center returned {(int)response.StatusCode}. Refresh and retry; completed attachments are retained.", null, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            return json.RootElement.Clone();
        }
        var attachments = new List<JsonElement>();
        for (var offset = 0; ; offset += 100)
        {
            var page = await Send(HttpMethod.Get, endpoint + $"?per_page=100&offset={offset}");
            var rows = page.GetProperty("data").EnumerateArray().ToArray();
            attachments.AddRange(rows);
            if (rows.Length < 100) break;
        }
        var matches = attachments.Where(a => a.GetProperty("attributes").GetProperty("filename").GetString() == receipt.Filename).ToArray();
        if (attachments.Any(a => a.GetProperty("attributes").GetProperty("filename").GetString() is { } name &&
            name.EndsWith(".pro", StringComparison.OrdinalIgnoreCase) && name != receipt.Filename))
            throw new InvalidOperationException("This PCO item already has another ProPresenter attachment. Review it in PCO before sending, so ProPresenter cannot download the wrong presentation.");
        if (matches.Length > 1) throw new InvalidOperationException("Multiple ScriptureSync attachments were found for this item. Resolve duplicates in PCO before sending again.");
        var existing = matches.SingleOrDefault();
        var existingId = existing.ValueKind == JsonValueKind.Undefined ? null : existing.GetProperty("id").GetString();
        var stamp = existingId is null ? null : existing.GetProperty("attributes").GetProperty("updated_at").GetString();
        if (receipt.AttachmentId is not null && existingId != receipt.AttachmentId)
            throw new InvalidOperationException("The owned PCO attachment was removed or renamed. It will not be recreated or replaced automatically.");
        if (receipt.UpdatedAt is not null && stamp != receipt.UpdatedAt)
            throw new InvalidOperationException("The PCO attachment changed outside ScriptureSync. Review it before sending again.");
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        if (existingId is not null && receipt.Hash == hash) return false;
        // An interrupted create can leave a file present without a receipt. Do not duplicate or overwrite uncertain data.
        if (existingId is not null && receipt.AttachmentId is null)
            throw new InvalidOperationException("A previous upload may have completed, but its receipt was not saved. Review the existing ScriptureSync attachment in PCO before retrying.");
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(bytes), "file", receipt.Filename);
        var uploaded = await Send(HttpMethod.Post, "https://upload.planningcenteronline.com/v2/files", form);
        var identifier = uploaded.GetProperty("data")[0].GetProperty("id").GetString();
        var payload = JsonContent.Create(new { data = new { type = "Attachment", attributes = new { file_upload_identifier = identifier, filename = receipt.Filename } } });
        var result = await Send(existingId is null ? HttpMethod.Post : HttpMethod.Patch,
            existingId is null ? endpoint : endpoint + "/" + Uri.EscapeDataString(existingId), payload);
        var data = result.GetProperty("data");
        Save(receipt with { AttachmentId = data.GetProperty("id").GetString(), Hash = hash,
            UpdatedAt = data.GetProperty("attributes").GetProperty("updated_at").GetString() });
        return true;
    }
}
