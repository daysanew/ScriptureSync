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

    public MainWindow()
    {
        InitializeComponent();

        var paths = new LocalAppPaths();
        var logger = new FileAppLogger(paths.LogFile);
        _viewModel = new MainWindowViewModel(
            new ScriptureReferenceParser(),
            new ManualDraftStore(paths, logger),
            new OpenLpBridgeClient(new AppConfiguration().OpenLpBridgeAddress),
            logger);
        DataContext = _viewModel;
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
