using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Google.Protobuf;
using Pro.SerializationInterop.RVProtoData;
using ScriptureSync.App.Services;
using ScriptureSync.App.ViewModels;
using ScriptureSync.Core.Configuration;
using ScriptureSync.Core.Presentations;
using ScriptureSync.PlanningCenter;
using ScriptureSync.ProPresenter;

namespace ScriptureSync.App;

public sealed class PlanningCenterPresentationWindow : Window
{
    private sealed record Attachment(string Service, string Plan, string Item, string Title, byte[] Bytes);
    private readonly List<Attachment> _attachments = [];
    private readonly TextBox _preview = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 12, 0, 12) };
    private readonly TextBlock _next = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 12, 0, 12) };
    private readonly Button _send = new() { Content = "Send presentations to Planning Center", Padding = new(15, 8, 15, 8), IsEnabled = false };
    private readonly Button _check = new() { Content = "Check ProPresenter again", Padding = new(12, 8, 12, 8), Margin = new(0, 0, 10, 0) };
    private readonly CancellationTokenSource _cancellation = new();
    private readonly HttpClient _http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(45) };
    private readonly HttpClient _ppHttp = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(4) };
    private readonly ProPresenterConfiguration _configuration;
    private readonly PlanningCenterAttachmentPublisher _publisher;
    private bool _busy;
    private bool _sent;

    public PlanningCenterPresentationWindow(ProPresenterConfiguration configuration, IEnumerable<ScriptureDraftItemViewModel> draft,
        PlanningCenterCredentials credentials, string stateDirectory)
    {
        if (!credentials.IsComplete) throw new InvalidOperationException("Add Planning Center credentials in Settings before sending presentations.");
        _configuration = configuration;
        _publisher = new(_http, credentials.ApplicationId, credentials.Secret, Path.Combine(stateDirectory, "attachments"));
        Title = "Preview presentations for Planning Center"; Width = 880; Height = 740; MinWidth = 650; MinHeight = 450;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        MaxHeight = SystemParameters.WorkArea.Height - 40;
        var root = new DockPanel { Margin = new Thickness(24) };
        var heading = new TextBlock { Text = "Review the slides, then send them as attachments to their PCO items. ProPresenter can download and link them when you import or refresh the plan.", TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 12) };
        DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
        var footer = new StackPanel(); footer.Children.Add(_status); footer.Children.Add(_next);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        _check.Click += async (_, _) => await CheckAsync();
        _send.Click += async (_, _) => await SendAsync();
        buttons.Children.Add(_check); buttons.Children.Add(_send); footer.Children.Add(buttons);
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer); root.Children.Add(_preview); Content = root;
        var rows = draft.ToArray();
        Loaded += async (_, _) =>
        {
            _busy = true; _check.IsEnabled = false;
            try
            {
                var templateBytes = File.ReadAllBytes(configuration.TemplatePath);
                var provider = new ProPresenterBibleProvider(configuration.BibleDirectory);
                var parsedRows = rows.Select(r => (Row: r, Parts: r.SourceKey?.Split(':'))).ToArray();
                if (parsedRows.Any(r => r.Parts is not { Length: 5 } || r.Parts[0] != "PCO" || !r.Row.ParseResult.IsValid))
                    throw new InvalidOperationException("This preview requires valid scripture rows imported from Planning Center.");
                var cache = Path.Combine(stateDirectory, "attachment-previews"); Directory.CreateDirectory(cache);
                var text = new StringBuilder();
                foreach (var group in parsedRows.GroupBy(r => string.Join(":", r.Parts!.Take(4))))
                {
                    var first = group.First(); var ids = first.Parts!;
                    var slides = new List<ScriptureSlide>();
                    var books = new HashSet<string>();
                    foreach (var row in group)
                    foreach (var translation in row.Row.ParseResult.TranslationCodes)
                    foreach (var passage in row.Row.ParseResult.Passages)
                    {
                        books.Add(passage.Book);
                        var content = await provider.GetPassageAsync(translation, passage, _cancellation.Token);
                        slides.AddRange(new ScripturePresentationComposer().Compose(content).Slides);
                    }
                    if (books.Count != 1 || slides.Select(s => s.Translation).Distinct().Count() != 1)
                        throw new InvalidOperationException("Use one Bible book and translation per PCO item with the current template support.");
                    var title = first.Row.PcoItemName ?? "Scripture";
                    var presentation = new ScripturePresentation(title, slides);
                    var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(group.Key + JsonSerializer.Serialize(presentation)).Concat(templateBytes).ToArray()));
                    var cached = Path.Combine(cache, key + ".pro");
                    byte[] bytes;
                    if (File.Exists(cached)) bytes = File.ReadAllBytes(cached);
                    else
                    {
                        var document = ProPresenterDocumentWriter.Create(Presentation.Parser.ParseFrom(templateBytes), presentation);
                        document.Notes = "ScriptureSync PCO attachment:" + group.Key;
                        bytes = document.ToByteArray(); File.WriteAllBytes(cached, bytes);
                    }
                    _attachments.Add(new(ids[1], ids[2], ids[3], title, bytes));
                    text.AppendLine($"{title} — plan {ids[2]} — {slides.Count} slides");
                    foreach (var slide in slides) text.AppendLine($"\n{slide.Reference} ({slide.Translation})\n{slide.Text}\n");
                }
                _preview.Text = text.ToString();
                _status.Text = $"Ready to send {_attachments.Count} presentation(s). Existing ScriptureSync attachments will be updated; unrelated attachments are preserved.";
                _send.IsEnabled = _attachments.Count > 0;
            }
            catch (Exception ex) { _status.Text = ex.Message; }
            finally { _busy = false; _check.IsEnabled = true; }
            await CheckAsync();
        };
        Closing += (_, e) => { if (_busy) { _cancellation.Cancel(); e.Cancel = true; } };
        Closed += (_, _) => { _cancellation.Dispose(); _http.Dispose(); _ppHttp.Dispose(); };
    }

    private async Task CheckAsync()
    {
        if (_busy) return;
        _busy = true; _check.IsEnabled = false;
        var wasEnabled = _send.IsEnabled; _send.IsEnabled = false;
        try
        {
            var messages = new List<string>();
            foreach (var plan in _attachments.Select(a => (a.Service, a.Plan)).Distinct())
            {
                var result = Uri.TryCreate(_configuration.Address, UriKind.Absolute, out var address)
                    ? await ProPresenterPcoNextStep.CheckAsync(_configuration.LibraryDirectory, address, new ProPresenterApiClient(_ppHttp, address), plan.Service, plan.Plan, _cancellation.Token)
                    : ProPresenterPcoNextStep.Unknown;
                messages.Add($"Plan {plan.Plan}: {result.Message}");
            }
            _next.Text = (_sent ? "Next step\n" : "After sending\n") + string.Join("\n\n", messages);
        }
        catch (OperationCanceledException) { _next.Text = "ProPresenter check cancelled. Close this preview and reopen it to continue."; }
        finally { _busy = false; _check.IsEnabled = !_cancellation.IsCancellationRequested; _send.IsEnabled = wasEnabled && !_cancellation.IsCancellationRequested; }
    }

    private async Task SendAsync()
    {
        _busy = true; _send.IsEnabled = false; _check.IsEnabled = false; _next.Text = "";
        try
        {
            var changed = 0;
            foreach (var attachment in _attachments)
            {
                _status.Text = $"Sending {attachment.Title} to Planning Center…";
                if (await _publisher.PublishAsync(attachment.Service, attachment.Plan, attachment.Item, attachment.Bytes, _cancellation.Token)) changed++;
            }
            _sent = true;
            _status.Text = $"Ready in Planning Center: {changed} uploaded or updated, {_attachments.Count - changed} unchanged. This does not confirm that ProPresenter has downloaded the latest slides.";
        }
        catch (OperationCanceledException) { _status.Text = "Sending cancelled. Completed attachments were kept. Reopen preview to check and continue."; }
        catch (Exception ex) { _status.Text = ex.Message + " Completed attachments were kept."; }
        finally { _busy = false; _check.IsEnabled = !_cancellation.IsCancellationRequested; }
        if (!_cancellation.IsCancellationRequested) await CheckAsync();
    }
}
