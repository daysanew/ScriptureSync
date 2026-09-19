using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ScriptureSync.App.ViewModels;
using ScriptureSync.Core.Configuration;
using ScriptureSync.Core.Presentations;
using ScriptureSync.ProPresenter;

namespace ScriptureSync.App;

public sealed class ProPresenterSyncWindow : Window
{
    private readonly ObservableCollection<PreviewRow> _rows = [];
    private readonly DataGrid _grid = new() { AutoGenerateColumns = false, CanUserAddRows = false, IsReadOnly = false, SelectionMode = DataGridSelectionMode.Single };
    private readonly TextBox _text = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 8, 0, 8) };
    private readonly Button _publish = new() { Content = "Sync to ProPresenter", Padding = new(20, 9, 20, 9), IsEnabled = false };
    private readonly Button _refresh = new() { Content = "Refresh preview", Margin = new(0, 0, 10, 0), Padding = new(16, 9, 16, 9) };
    private readonly ProPresenterConfiguration _configuration;
    private readonly ScriptureDraftItemViewModel[] _draft;
    private readonly string _stateDirectory;
    private readonly HttpClient _http = ProPresenterSettingsPanel.NewHttp();
    private readonly ProPresenterApiClient _api;
    private readonly ProPresenterPublisher _publisher;
    private CancellationTokenSource? _cancellation;
    private JsonElement _playlist;
    private bool _busy;

    public ProPresenterSyncWindow(ProPresenterConfiguration configuration, IEnumerable<ScriptureDraftItemViewModel> draft, string stateDirectory)
    {
        _configuration = configuration; _draft = draft.ToArray(); _stateDirectory = stateDirectory;
        _api = new(_http, new Uri(configuration.Address));
        _publisher = new(configuration, stateDirectory, _api);
        Title = "Preview ProPresenter sync"; Width = 1050; Height = 760; MinWidth = 800; MinHeight = 550;
        MaxHeight = SystemParameters.WorkArea.Height - 40; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel { Margin = new Thickness(24) };
        var header = new TextBlock
        {
            Text = $"Library: {configuration.LibraryName}\nPlaylist: {(string.IsNullOrEmpty(configuration.PlaylistId) ? "Library only" : configuration.PlaylistName)}\nTemplate: {configuration.TemplatePath}\nFolder: {configuration.LibraryDirectory}",
            TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 12)
        };
        DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        var footer = new StackPanel();
        footer.Children.Add(_status);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancel / Close", Padding = new(16, 9, 16, 9), Margin = new(0, 0, 10, 0) };
        cancel.Click += (_, _) => { if (_busy) _cancellation?.Cancel(); else Close(); };
        _refresh.Click += async (_, _) => await RefreshAsync();
        _publish.Click += async (_, _) => await PublishAsync();
        buttons.Children.Add(_refresh); buttons.Children.Add(cancel); buttons.Children.Add(_publish);
        footer.Children.Add(buttons); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var content = new Grid(); content.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) }); content.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        _grid.ItemsSource = _rows;
        foreach (var (label, binding, width) in new[] { ("Change", "Change", 90.0), ("Presentation", "Title", 250.0), ("Slides", "SlideCount", 60.0), ("Source", "Source", 170.0) })
            _grid.Columns.Add(new DataGridTextColumn { Header = label, Binding = new Binding(binding), Width = width, IsReadOnly = true });
        if (!string.IsNullOrEmpty(configuration.PlaylistId))
        {
            var combo = new FrameworkElementFactory(typeof(ComboBox));
            combo.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("Targets"));
            combo.SetBinding(ComboBox.SelectedItemProperty, new Binding("Target") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            combo.SetValue(ItemsControl.DisplayMemberPathProperty, "Label");
            _grid.Columns.Add(new DataGridTemplateColumn { Header = "Playlist mapping", CellTemplate = new DataTemplate { VisualTree = combo }, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        }
        _grid.SelectionChanged += (_, _) =>
        {
            if (_grid.SelectedItem is PreviewRow row)
                _text.Text = string.Join("\n\n", row.Content.Slides.Select((s, i) => $"Slide {i + 1} — {s.Reference} ({s.Translation})\n{s.Text}"));
        };
        content.Children.Add(_grid); Grid.SetRow(_text, 1); _text.Margin = new Thickness(0, 12, 0, 0); content.Children.Add(_text); root.Children.Add(content); Content = root;
        Loaded += async (_, _) => await RefreshAsync();
        Closing += (_, e) => { if (_busy) { _cancellation?.Cancel(); e.Cancel = true; } };
        Closed += (_, _) => { _http.Dispose(); _cancellation?.Dispose(); };
    }

    private void Begin(string status)
    {
        _busy = true; _publish.IsEnabled = false; _refresh.IsEnabled = false; _grid.IsEnabled = false;
        _cancellation?.Dispose(); _cancellation = new(); _status.Text = status;
    }
    private void End() { _busy = false; _refresh.IsEnabled = true; _grid.IsEnabled = true; }

    private async Task RefreshAsync()
    {
        Begin("Checking connection, Bible text, template, and owned presentations…"); _rows.Clear(); _text.Clear();
        try
        {
            var token = _cancellation!.Token;
            await _publisher.ValidateAsync(token);
            if (!string.IsNullOrEmpty(_configuration.PlaylistId))
            {
                _playlist = await _api.GetPlaylistContentsAsync(_configuration.PlaylistId, token);
                _ = ProPresenterPlaylistLinker.BuildItems(_playlist, []);
            }
            var provider = new ProPresenterBibleProvider(_configuration.BibleDirectory);
            var identities = new HashSet<string>();
            foreach (var draft in _draft)
            {
                var parsed = draft.ParseResult;
                if (!parsed.IsValid) throw new InvalidDataException($"Fix this draft row before syncing: {draft.RawText}");
                foreach (var translation in parsed.TranslationCodes)
                for (var i = 0; i < parsed.Passages.Count; i++)
                {
                    var identity = $"{draft.SourceKey ?? "Draft:" + draft.Id}:{translation}:{i}";
                    if (!identities.Add(identity)) throw new InvalidDataException("Duplicate imported PCO rows share an identity. Remove the duplicate or duplicate it as a separate manual row.");
                    var passage = await provider.GetPassageAsync(translation, parsed.Passages[i], token);
                    var content = new ScripturePresentationComposer().Compose(passage);
                    var plan = await Task.Run(() => _publisher.Preview(content, identity), token);
                    var targets = new List<PlaylistTarget> { new(null, "Append / keep existing link") };
                    _rows.Add(new(content, plan, draft.PcoItemName ?? draft.Source, targets));
                }
            }
            var stale = _publisher.FindStale(_rows.Select(r => r.Plan.Key));
            _status.Text = $"{_rows.Count} presentations • {_rows.Count(r => r.Plan.Change != PublishChange.Unchanged)} file changes. Review the slides and playlist mapping before syncing." +
                (stale.Count > 0 ? $"\nPreviously generated content outside this draft (kept): {string.Join(", ", stale)}" : "");
            _publish.IsEnabled = _rows.Count > 0;
            _grid.SelectedIndex = _rows.Count > 0 ? 0 : -1;
        }
        catch (OperationCanceledException) { _status.Text = "Preview cancelled."; }
        catch (Exception e) { _status.Text = e.Message; }
        finally { End(); }
    }

    private async Task PublishAsync()
    {
        _grid.CommitEdit(DataGridEditingUnit.Cell, true);
        _grid.CommitEdit(DataGridEditingUnit.Row, true);
        Begin("Publishing…");
        try
        {
            var token = _cancellation!.Token;
            var links = _rows.Select(row => new PlaylistLink(row.Plan.Id, row.Title, row.Target.Index)).ToArray();
            if (!string.IsNullOrEmpty(_configuration.PlaylistId)) _ = ProPresenterPlaylistLinker.BuildItems(_playlist, links);
            var published = 0;
            foreach (var row in _rows)
            {
                token.ThrowIfCancellationRequested();
                _status.Text = $"{row.Plan.Change}: {row.Title}…";
                await _publisher.ApplyAsync(row.Plan, token);
                published++;
            }
            if (!string.IsNullOrEmpty(_configuration.PlaylistId))
                await ProPresenterPlaylistLinker.LinkAsync(_api, _configuration.PlaylistId, _playlist, links, Path.Combine(_stateDirectory, "backups"), token);
            _status.Text = $"Sync complete: {published} presentations verified" + (string.IsNullOrEmpty(_configuration.PlaylistId) ? "." : " and playlist links verified.") + " Refresh preview to check for further changes.";
        }
        catch (OperationCanceledException) { _status.Text = "Sync cancelled. Completed presentations were kept. Refresh to continue safely."; }
        catch (Exception e) { _status.Text = e.Message + "\nCompleted files were kept. Refresh preview before retrying."; }
        finally { End(); }
    }

    public sealed record PlaylistTarget(int? Index, string Label);
    public sealed class PreviewRow(ScripturePresentation content, ProPresenterPublishPlan plan, string source, List<PlaylistTarget> targets)
    {
        public ScripturePresentation Content { get; } = content;
        public ProPresenterPublishPlan Plan { get; } = plan;
        public string Title => Content.Title;
        public string Change => Plan.Change.ToString();
        public int SlideCount => Content.Slides.Count;
        public string Source { get; } = source;
        public List<PlaylistTarget> Targets { get; } = targets;
        public PlaylistTarget Target { get; set; } = targets[0];
    }
}
