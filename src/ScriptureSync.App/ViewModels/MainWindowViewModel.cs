using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Net.Http;
using ScriptureSync.App.Services;
using ScriptureSync.Core.Logging;
using ScriptureSync.Core.Parsing;
using ScriptureSync.OpenLP;
using ScriptureSync.PlanningCenter;

namespace ScriptureSync.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly ScriptureReferenceParser _parser;
    private readonly ManualDraftStore _draftStore;
    private readonly IOpenLpClient? _openLpClient;
    private readonly IAppLogger? _logger;
    private ScriptureDraftItemViewModel? _selectedItem;
    private bool _isSyncing;
    private string _openLpStatus = "Not checked";

    public MainWindowViewModel(
        ScriptureReferenceParser parser,
        ManualDraftStore draftStore,
        IOpenLpClient? openLpClient = null,
        IAppLogger? logger = null)
    {
        _parser = parser;
        _draftStore = draftStore;
        _openLpClient = openLpClient;
        _logger = logger;
        Items.CollectionChanged += ItemsOnCollectionChanged;

        AddCommand = new RelayCommand(AddEmpty);
        RemoveCommand = new RelayCommand(RemoveSelected, () => SelectedItem is not null);
        DuplicateCommand = new RelayCommand(DuplicateSelected, () => SelectedItem is not null);
        MoveUpCommand = new RelayCommand(MoveUp, () => SelectedIndex > 0);
        MoveDownCommand = new RelayCommand(MoveDown, () =>
            SelectedIndex >= 0 && SelectedIndex < Items.Count - 1);
        SyncCommand = new AsyncRelayCommand(SyncToOpenLpAsync,
            () => !IsSyncing && ReadyCount > 0 && _openLpClient is not null);

        foreach (var storedItem in _draftStore.Load())
        {
            AddItem(new ScriptureDraftItemViewModel(
                _parser, storedItem.Id, storedItem.RawText, storedItem.Source));
        }

        RefreshSummary();
    }

    public ObservableCollection<ScriptureDraftItemViewModel> Items { get; } = [];

    public ScriptureDraftItemViewModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value)) RaiseCommandStates();
        }
    }

    public int SelectedIndex => SelectedItem is null ? -1 : Items.IndexOf(SelectedItem);
    public int ReadyCount => Items.Count(item => item.IsValid);
    public int AttentionCount => Items.Count - ReadyCount;
    public string SummaryText => Items.Count == 0
        ? "Add or paste scripture references to begin."
        : $"{ReadyCount} ready • {AttentionCount} need attention";

    public bool IsSyncing
    {
        get => _isSyncing;
        private set
        {
            if (SetProperty(ref _isSyncing, value))
            {
                SyncCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(SyncButtonText));
            }
        }
    }

    public string SyncButtonText => IsSyncing ? "Syncing..." : "Sync to OpenLP";

    public string OpenLpStatus
    {
        get => _openLpStatus;
        private set => SetProperty(ref _openLpStatus, value);
    }

    public RelayCommand AddCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public RelayCommand DuplicateCommand { get; }
    public RelayCommand MoveUpCommand { get; }
    public RelayCommand MoveDownCommand { get; }
    public AsyncRelayCommand SyncCommand { get; }

    public void AddPastedText(string text)
    {
        var lines = text.Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            AddItem(new ScriptureDraftItemViewModel(_parser, Guid.NewGuid(), line));
        }
        SelectedItem = Items.LastOrDefault();
        SaveDraft();
    }

    public void ClearAll()
    {
        Items.Clear();
        SelectedItem = null;
        SaveDraft();
    }

    public int AddPlanningCenterItems(
        IEnumerable<PlanningCenterScriptureItem> items,
        string planDisplayName)
    {
        var added = 0;
        foreach (var item in items.OrderBy(item => item.Sequence))
        {
            var lines = item.Details.Replace("\r\n", "\n")
                .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                AddItem(new ScriptureDraftItemViewModel(
                    _parser, Guid.NewGuid(), line, $"Planning Center — {planDisplayName}"));
                added++;
            }
        }
        SelectedItem = Items.LastOrDefault();
        SaveDraft();
        return added;
    }

    public void RefreshValidation()
    {
        foreach (var item in Items) item.RefreshValidation();
        RefreshSummary();
    }

    public async Task<bool> CheckOpenLpPluginAsync()
    {
        if (_openLpClient is null)
        {
            OpenLpStatus = "ScriptureSync OpenLP integration is unavailable.";
            return false;
        }

        OpenLpStatus = "Checking OpenLP plugin...";
        try
        {
            var connection = await _openLpClient.PrepareAsync();
            OpenLpStatus = $"Ready • plugin active • {connection.InstalledBibles.Count} Bibles";
            return true;
        }
        catch (HttpRequestException exception)
        {
            _logger?.Error("Unable to reach the ScriptureSync OpenLP plugin.", exception);
            OpenLpStatus = "Plugin unavailable • start OpenLP and activate ScriptureSync";
            return false;
        }
        catch (OpenLpException exception)
        {
            _logger?.Error("The ScriptureSync OpenLP plugin failed its status check.", exception);
            OpenLpStatus = $"Plugin error • {exception.Message}";
            return false;
        }
        catch (Exception exception)
        {
            _logger?.Error("Unexpected OpenLP plugin status-check failure.", exception);
            OpenLpStatus = "Plugin check failed • see the ScriptureSync log";
            return false;
        }
    }

    public async Task SyncToOpenLpAsync()
    {
        if (_openLpClient is null) return;
        IsSyncing = true;
        OpenLpStatus = "Connecting...";
        var addedTotal = 0;
        var failedRows = 0;
        ScriptureDraftItemViewModel? activeItem = null;

        try
        {
            if (!await CheckOpenLpPluginAsync()) return;

            foreach (var item in Items)
            {
                activeItem = item;
                var parsed = item.ParseResult;
                if (!parsed.IsValid) continue;

                var rowAdded = 0;
                var rowErrors = new List<string>();
                foreach (var translation in parsed.TranslationCodes)
                {
                    try
                    {
                        foreach (var passage in parsed.Passages)
                        {
                            item.SetSyncStatus($"Adding {passage} ({translation})...");
                            var result = await _openLpClient.AddScriptureAsync(
                                translation, passage.ToString());
                            if (result is null)
                            {
                                rowErrors.Add($"Not found: {passage} ({translation})");
                                continue;
                            }
                            rowAdded++;
                            addedTotal++;
                        }
                    }
                    catch (OpenLpBibleNotInstalledException)
                    {
                        rowErrors.Add($"Translation not installed: {translation}");
                    }
                    catch (HttpRequestException)
                    {
                        // A refused connection means OpenLP exited. Do not continue
                        // through later rows or resume against a restarted instance.
                        throw;
                    }
                    catch (Exception exception)
                    {
                        _logger?.Error($"Failed to sync '{item.RawText}' as {translation}.", exception);
                        rowErrors.Add($"{translation}: {exception.Message}");
                    }
                }

                if (rowErrors.Count == 0)
                {
                    item.SetSyncStatus($"Added {rowAdded} to OpenLP");
                }
                else
                {
                    failedRows++;
                    var prefix = rowAdded > 0 ? $"Added {rowAdded}; " : string.Empty;
                    item.SetSyncStatus(prefix + string.Join("; ", rowErrors));
                }
            }

            activeItem = null;

            OpenLpStatus = failedRows == 0
                ? $"Sync complete • {addedTotal} added"
                : $"Sync complete • {addedTotal} added • {failedRows} need attention";
        }
        catch (HttpRequestException exception)
        {
            activeItem?.SetSyncStatus("Stopped: ScriptureSync plugin unavailable");
            _logger?.Error("Unable to reach the ScriptureSync OpenLP plugin.", exception);
            OpenLpStatus = addedTotal == 0
                ? "Cannot reach the ScriptureSync plugin. Start OpenLP and activate the plugin."
                : $"Sync stopped • plugin unavailable after {addedTotal} added";
        }
        catch (OpenLpException exception)
        {
            activeItem?.SetSyncStatus($"OpenLP error: {exception.Message}");
            _logger?.Error("The ScriptureSync OpenLP plugin reported an error.", exception);
            OpenLpStatus = $"OpenLP error • {exception.Message}";
        }
        catch (Exception exception)
        {
            activeItem?.SetSyncStatus("Stopped: unexpected error");
            _logger?.Error("Unexpected ScriptureSync failure.", exception);
            OpenLpStatus = "Unexpected error. See the ScriptureSync log for details.";
        }
        finally
        {
            IsSyncing = false;
        }
    }

    private void AddEmpty()
    {
        var item = new ScriptureDraftItemViewModel(_parser, Guid.NewGuid(), string.Empty);
        AddItem(item);
        SelectedItem = item;
        SaveDraft();
    }

    private void RemoveSelected()
    {
        if (SelectedItem is null) return;
        var oldIndex = SelectedIndex;
        Items.Remove(SelectedItem);
        SelectedItem = Items.Count == 0 ? null : Items[Math.Min(oldIndex, Items.Count - 1)];
        SaveDraft();
    }

    private void DuplicateSelected()
    {
        if (SelectedItem is null) return;
        var duplicate = new ScriptureDraftItemViewModel(
            _parser, Guid.NewGuid(), SelectedItem.RawText, SelectedItem.Source);
        Items.Insert(SelectedIndex + 1, duplicate);
        SelectedItem = duplicate;
        SaveDraft();
    }

    private void MoveUp()
    {
        var index = SelectedIndex;
        if (index <= 0) return;
        Items.Move(index, index - 1);
        RaiseCommandStates();
        SaveDraft();
    }

    private void MoveDown()
    {
        var index = SelectedIndex;
        if (index < 0 || index >= Items.Count - 1) return;
        Items.Move(index, index + 1);
        RaiseCommandStates();
        SaveDraft();
    }

    private void AddItem(ScriptureDraftItemViewModel item)
    {
        item.PropertyChanged += ItemOnPropertyChanged;
        Items.Add(item);
    }

    private void ItemsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (ScriptureDraftItemViewModel item in e.OldItems)
                item.PropertyChanged -= ItemOnPropertyChanged;
        }
        RefreshSummary();
        RaiseCommandStates();
    }

    private void ItemOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ScriptureDraftItemViewModel.RawText) or
            nameof(ScriptureDraftItemViewModel.IsValid))
        {
            RefreshSummary();
            SaveDraft();
        }
    }

    private void RefreshSummary()
    {
        OnPropertyChanged(nameof(ReadyCount));
        OnPropertyChanged(nameof(AttentionCount));
        OnPropertyChanged(nameof(SummaryText));
        SyncCommand?.RaiseCanExecuteChanged();
    }

    private void RaiseCommandStates()
    {
        RemoveCommand.RaiseCanExecuteChanged();
        DuplicateCommand.RaiseCanExecuteChanged();
        MoveUpCommand.RaiseCanExecuteChanged();
        MoveDownCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(SelectedIndex));
    }

    private void SaveDraft() => _draftStore.Save(Items.Select(item =>
        new StoredDraftItem(item.Id, item.RawText, item.Source)));
}
