using System.Windows;
using System.Windows.Controls;
using ScriptureSync.Core.Models;
using ScriptureSync.PlanningCenter;

namespace ScriptureSync.App;

public partial class PlanningCenterImportWindow : Window
{
    private readonly IPlanningCenterClient _client;
    private readonly int _windowDays;
    private readonly IReadOnlyCollection<string> _serviceTypeIds;
    private readonly IReadOnlyCollection<string> _itemNames;

    public PlanningCenterImportWindow(
        IPlanningCenterClient client,
        int windowDays,
        IReadOnlyCollection<string> serviceTypeIds,
        IReadOnlyCollection<string> itemNames)
    {
        InitializeComponent();
        _client = client;
        _windowDays = windowDays;
        _serviceTypeIds = serviceTypeIds;
        _itemNames = itemNames;
    }

    public ServicePlan? SelectedPlan { get; private set; }
    public IReadOnlyList<PlanningCenterScriptureItem> ImportedItems { get; private set; } = [];

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        StatusTextBlock.Text = "Loading upcoming plans...";
        try
        {
            var plans = await _client.GetUpcomingPlansAsync(_windowDays, _serviceTypeIds);
            PlansComboBox.ItemsSource = plans;
            if (plans.Count > 0)
            {
                PlansComboBox.SelectedIndex = 0;
                StatusTextBlock.Text = $"{plans.Count} upcoming plan{(plans.Count == 1 ? string.Empty : "s")} found.";
            }
            else StatusTextBlock.Text = $"No service plans were found in the next {_windowDays} days.";
        }
        catch (Exception exception)
        {
            StatusTextBlock.Text = exception.Message;
        }
    }

    private void PlansComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ImportButton.IsEnabled = PlansComboBox.SelectedItem is ServicePlan;

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        if (PlansComboBox.SelectedItem is not ServicePlan plan) return;
        ImportButton.IsEnabled = false;
        StatusTextBlock.Text = "Reading scripture items...";
        try
        {
            var items = await _client.GetScriptureItemsAsync(plan, _itemNames);
            if (items.Count == 0)
            {
                StatusTextBlock.Text = $"No matching items were found. Looking for: {string.Join(", ", _itemNames)}.";
                ImportButton.IsEnabled = true;
                return;
            }
            SelectedPlan = plan;
            ImportedItems = items;
            DialogResult = true;
        }
        catch (Exception exception)
        {
            StatusTextBlock.Text = exception.Message;
            ImportButton.IsEnabled = true;
        }
    }
}
