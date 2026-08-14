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
            new OpenLpClient(new AppConfiguration().OpenLpBaseAddress),
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
}
