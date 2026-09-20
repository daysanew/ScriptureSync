using System.Windows;
using System.IO;
using System.Windows.Data;
using ScriptureSync.App.Services;
using ScriptureSync.App.ViewModels;
using ScriptureSync.Core.Configuration;
using ScriptureSync.Core.Logging;
using ScriptureSync.Core.Parsing;
using ScriptureSync.OpenLP;
using ScriptureSync.PlanningCenter;

namespace ScriptureSync.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly ScriptureReferenceParser _parser;
    private readonly AppSettingsStore _settingsStore;
    private readonly PlanningCenterCredentialsStore _credentialsStore;
    private PlanningCenterCredentials _planningCenterCredentials;
    private AppConfiguration _configuration;
    private readonly LocalAppPaths _paths = new();
    private bool IsProPresenter => _configuration.PresentationSoftware == "ProPresenter";
    private bool IsPcoAttachmentDraft => _viewModel.Items.Count > 0 && _viewModel.Items.All(i => i.SourceKey?.StartsWith("PCO:", StringComparison.Ordinal) == true);

    public MainWindow()
    {
        InitializeComponent();

        var paths = _paths;
        var logger = new FileAppLogger(paths.LogFile);
        _settingsStore = new AppSettingsStore(paths, logger);
        _credentialsStore = new PlanningCenterCredentialsStore(paths, logger);
        _configuration = _settingsStore.Load();
        _planningCenterCredentials = _credentialsStore.Load();
        _parser = new ScriptureReferenceParser(_configuration.DefaultBibleTranslation);
        _viewModel = new MainWindowViewModel(
            _parser,
            new ManualDraftStore(paths, logger),
            new OpenLpSyncDestination(new OpenLpBridgeClient(_configuration.OpenLpBridgeAddress)),
            logger);
        DataContext = _viewModel;
        _viewModel.PropertyChanged += (_, _) => UpdateDestination();
        UpdateDestination();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsSyncing) return;
        var dialog = new SettingsWindow(
            _configuration.DefaultBibleTranslation,
            _planningCenterCredentials,
            _configuration.PlanningCenterItemNames, _configuration) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        _configuration = new AppConfiguration
        {
            DefaultBibleTranslation = dialog.DefaultBibleTranslation,
            PresentationSoftware = dialog.PresentationSoftware,
            ProPresenter = dialog.ProPresenterConfiguration,
            PlanWindowDays = _configuration.PlanWindowDays,
            OpenLpBridgeAddress = _configuration.OpenLpBridgeAddress,
            IncludedServiceTypeIds = _configuration.IncludedServiceTypeIds,
            PlanningCenterItemNames = dialog.PlanningCenterItemNames.ToList(),
            BibleMappings = _configuration.BibleMappings
        };
        _settingsStore.Save(_configuration);
        _planningCenterCredentials = dialog.PlanningCenterCredentials;
        if (dialog.RemovePlanningCenterCredentials)
        {
            _credentialsStore.Delete();
        }
        else if (_planningCenterCredentials.IsComplete)
        {
            _credentialsStore.Save(_planningCenterCredentials);
        }
        _parser.DefaultBibleTranslation = _configuration.DefaultBibleTranslation;
        _viewModel.RefreshValidation();
        UpdateDestination();
    }

    private void ImportPlanningCenter_Click(object sender, RoutedEventArgs e)
    {
        if (!_planningCenterCredentials.IsComplete)
        {
            MessageBox.Show(this,
                "Add a Planning Center Personal Access Token in Settings first.",
                "Planning Center", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var client = new PlanningCenterClient(
            _planningCenterCredentials.ApplicationId, _planningCenterCredentials.Secret);
        var dialog = new PlanningCenterImportWindow(
            client,
            _configuration.PlanWindowDays,
            _configuration.IncludedServiceTypeIds,
            _configuration.PlanningCenterItemNames) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.SelectedPlan is null) return;

        var count = _viewModel.AddPlanningCenterItems(
            dialog.ImportedItems, dialog.SelectedPlan.DisplayName,
            $"{dialog.SelectedPlan.ServiceTypeId}:{dialog.SelectedPlan.Id}", updateExisting: IsProPresenter);
        MessageBox.Show(this,
            $"Imported {count} scripture row{(count == 1 ? string.Empty : "s")} from {dialog.SelectedPlan.DisplayName}.\n\nReview the rows before syncing.",
            "Planning Center Import", MessageBoxButton.OK, MessageBoxImage.Information);
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
        if (!IsProPresenter) await _viewModel.CheckOpenLpPluginAsync();
    }

    private void UpdateDestination()
    {
        IntroductionText.Text = $"Review and prepare scripture before sending it to {(IsProPresenter ? "ProPresenter" : "OpenLP")}.";
        DestinationLabel.Text = IsProPresenter ? "PROPRESENTER" : "OPENLP";
        if (IsProPresenter)
            DestinationStatus.Text = "Preview Bible text and changes before syncing. Configure the library and template in Settings.";
        else
            DestinationStatus.SetBinding(System.Windows.Controls.TextBlock.TextProperty, new Binding(nameof(MainWindowViewModel.OpenLpStatus)));
        DestinationHint.Text = IsProPresenter ? IsPcoAttachmentDraft
            ? "Send scripture attachments to PCO, then import or refresh the plan in ProPresenter. ProPresenter does not need to be open to send."
            : "ProPresenter must be running before previewing and syncing." : "OpenLP must be running on this computer before syncing.";
        SyncButton.Content = IsProPresenter ? IsPcoAttachmentDraft ? "Preview PCO attachments" : "Preview ProPresenter" : _viewModel.SyncButtonText;
        SyncButton.IsEnabled = !_viewModel.IsSyncing && (IsProPresenter ? _viewModel.Items.Count > 0 : _viewModel.ReadyCount > 0);
    }

    private async void Sync_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsSyncing) return;
        if (!IsProPresenter)
        {
            await _viewModel.SyncToOpenLpAsync();
            return;
        }
        try
        {
            if (IsPcoAttachmentDraft)
            {
                new PlanningCenterPresentationWindow(_configuration.ProPresenter, _viewModel.Items, _planningCenterCredentials,
                    Path.Combine(_paths.DataDirectory, "ProPresenter")) { Owner = this }.ShowDialog();
                return;
            }
            new ProPresenterSyncWindow(_configuration.ProPresenter, _viewModel.Items,
                Path.Combine(_paths.DataDirectory, "ProPresenter")) { Owner = this }.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message + "\n\nCheck Presentation software in Settings.", "ProPresenter", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Items.Count == 0) return;
        if (_viewModel.IsSyncing)
        {
            MessageBox.Show(
                this,
                "Wait for the current sync to finish before clearing the list.",
                "Scripture Sync",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var answer = MessageBox.Show(
            this,
            "Clear every scripture from this draft?\n\nThis will not remove anything already published.",
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
