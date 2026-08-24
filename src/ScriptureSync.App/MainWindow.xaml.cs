using System.Windows;
using ScriptureSync.App.Services;
using ScriptureSync.App.ViewModels;
using ScriptureSync.Core.Configuration;
using ScriptureSync.Core.Logging;
using ScriptureSync.Core.Parsing;
using ScriptureSync.OpenLP;

namespace ScriptureSync.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly ScriptureReferenceParser _parser;
    private readonly AppSettingsStore _settingsStore;
    private AppConfiguration _configuration;

    public MainWindow()
    {
        InitializeComponent();

        var paths = new LocalAppPaths();
        var logger = new FileAppLogger(paths.LogFile);
        _settingsStore = new AppSettingsStore(paths, logger);
        _configuration = _settingsStore.Load();
        _parser = new ScriptureReferenceParser(_configuration.DefaultBibleTranslation);
        _viewModel = new MainWindowViewModel(
            _parser,
            new ManualDraftStore(paths, logger),
            new OpenLpBridgeClient(_configuration.OpenLpBridgeAddress),
            logger);
        DataContext = _viewModel;
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(_configuration.DefaultBibleTranslation) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        _configuration = new AppConfiguration
        {
            DefaultBibleTranslation = dialog.DefaultBibleTranslation,
            PlanWindowDays = _configuration.PlanWindowDays,
            OpenLpBridgeAddress = _configuration.OpenLpBridgeAddress,
            IncludedServiceTypeIds = _configuration.IncludedServiceTypeIds,
            BibleMappings = _configuration.BibleMappings
        };
        _settingsStore.Save(_configuration);
        _parser.DefaultBibleTranslation = _configuration.DefaultBibleTranslation;
        _viewModel.RefreshValidation();
    }

    private void PasteScriptures_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PasteScripturesWindow { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            _viewModel.AddPastedText(dialog.ScriptureText);
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.CheckOpenLpPluginAsync();
    }

    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Items.Count == 0) return;
        if (_viewModel.IsSyncing)
        {
            MessageBox.Show(
                this,
                "Wait for the current OpenLP sync to finish before clearing the list.",
                "Scripture Sync",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var answer = MessageBox.Show(
            this,
            "Clear every scripture from this draft?\n\nThis will not remove anything already added to OpenLP.",
            "Clear All Scriptures",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (answer == MessageBoxResult.Yes)
        {
            _viewModel.ClearAll();
        }
    }
}
