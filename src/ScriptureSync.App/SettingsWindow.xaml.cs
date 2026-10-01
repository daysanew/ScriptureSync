using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Navigation;
using ScriptureSync.App.Services;
using ScriptureSync.Core.Configuration;
using System.Windows.Controls;

namespace ScriptureSync.App;

public partial class SettingsWindow : Window
{
    private readonly PlanningCenterCredentials _existingCredentials;
    private bool _removeCredentials;
    private readonly ProPresenterSettingsPanel _presentationPanel;

    public SettingsWindow(
        string defaultBibleTranslation,
        PlanningCenterCredentials credentials,
        IReadOnlyCollection<string> planningCenterItemNames, AppConfiguration? configuration = null)
    {
        InitializeComponent();
        _presentationPanel = new(configuration ?? new AppConfiguration());
        var general = (Grid)Content;
        Content = null;
        var oldFooter = general.Children.OfType<StackPanel>().Single(panel => Grid.GetRow(panel) == 15);
        general.Children.Remove(oldFooter);
        var root = new DockPanel();
        DockPanel.SetDock(oldFooter, Dock.Bottom);
        oldFooter.Margin = new Thickness(20, 12, 20, 16);
        root.Children.Add(oldFooter);
        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = "General / Planning Center", Content = new ScrollViewer { Content = general, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
        tabs.Items.Add(new TabItem { Header = "Presentation software", Content = new ScrollViewer { Content = _presentationPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
        root.Children.Add(tabs);
        Content = root;
        Width = 660; Height = 800; MaxHeight = SystemParameters.WorkArea.Height - 40;
        _existingCredentials = credentials;
        DefaultTranslationTextBox.Text = defaultBibleTranslation;
        PlanningCenterApplicationIdTextBox.Text = credentials.ApplicationId;
        SavedCredentialTextBlock.Text = credentials.IsComplete
            ? "Saved securely in Windows Credential Manager. Leave blank to keep it."
            : "No token is currently saved.";
        RemoveCredentialsButton.Visibility = credentials.IsComplete ? Visibility.Visible : Visibility.Collapsed;
        PlanningCenterItemNamesTextBox.Text = string.Join(Environment.NewLine, planningCenterItemNames);
        DefaultTranslationTextBox.SelectAll();
        DefaultTranslationTextBox.Focus();
    }

    public string DefaultBibleTranslation { get; private set; } = string.Empty;
    public PlanningCenterCredentials PlanningCenterCredentials { get; private set; } = new(string.Empty, string.Empty);
    public IReadOnlyList<string> PlanningCenterItemNames { get; private set; } = [];
    public bool RemovePlanningCenterCredentials => _removeCredentials;
    public string PresentationSoftware => _presentationPanel.Software;
    public ProPresenterConfiguration ProPresenterConfiguration => _presentationPanel.Configuration;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (PresentationSoftware == "ProPresenter" && (!Uri.TryCreate(ProPresenterConfiguration.Address, UriKind.Absolute, out var address) ||
            (address.Scheme != "http" && address.Scheme != "https")))
        {
            MessageBox.Show(this, "Enter the complete ProPresenter API address, including its port.");
            return;
        }
        var value = DefaultTranslationTextBox.Text.Trim().ToUpperInvariant();
        if (!Regex.IsMatch(value, "^[A-Z][A-Z0-9.-]{0,14}$"))
        {
            MessageBox.Show(this, "Enter a translation code such as KJV, NKJV, or NLT.",
                "Default Bible Translation", MessageBoxButton.OK, MessageBoxImage.Information);
            DefaultTranslationTextBox.Focus();
            return;
        }

        DefaultBibleTranslation = value;
        var itemNames = PlanningCenterItemNamesTextBox.Text.Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (itemNames.Length == 0)
        {
            MessageBox.Show(this, "Enter at least one Planning Center service item name.",
                "Planning Center", MessageBoxButton.OK, MessageBoxImage.Information);
            PlanningCenterItemNamesTextBox.Focus();
            return;
        }
        PlanningCenterItemNames = itemNames;
        var applicationId = PlanningCenterApplicationIdTextBox.Text.Trim();
        var enteredSecret = PlanningCenterSecretPasswordBox.Password.Trim();
        var secret = enteredSecret.Length > 0 ? enteredSecret : _existingCredentials.Secret;
        PlanningCenterCredentials = _removeCredentials
            ? new(string.Empty, string.Empty)
            : new(applicationId, secret);
        if ((applicationId.Length > 0 || enteredSecret.Length > 0) && !PlanningCenterCredentials.IsComplete)
        {
            MessageBox.Show(this, "Enter both the Application ID and Secret.",
                "Planning Center", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }

    private void RemoveCredentials_Click(object sender, RoutedEventArgs e)
    {
        _removeCredentials = true;
        PlanningCenterApplicationIdTextBox.Clear();
        PlanningCenterSecretPasswordBox.Clear();
        SavedCredentialTextBlock.Text = "The saved token will be removed when you choose Save.";
        RemoveCredentialsButton.Visibility = Visibility.Collapsed;
    }

    private void PlanningCenterDeveloperLink_RequestNavigate(
        object sender,
        RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
