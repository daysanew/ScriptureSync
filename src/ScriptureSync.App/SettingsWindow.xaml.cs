using System.Text.RegularExpressions;
using System.Windows;

namespace ScriptureSync.App;

public partial class SettingsWindow : Window
{
    public SettingsWindow(string defaultBibleTranslation)
    {
        InitializeComponent();
        DefaultTranslationTextBox.Text = defaultBibleTranslation;
        DefaultTranslationTextBox.SelectAll();
        DefaultTranslationTextBox.Focus();
    }

    public string DefaultBibleTranslation { get; private set; } = string.Empty;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var value = DefaultTranslationTextBox.Text.Trim().ToUpperInvariant();
        if (!Regex.IsMatch(value, "^[A-Z][A-Z0-9.-]{0,14}$"))
        {
            MessageBox.Show(this, "Enter a translation code such as KJV, NKJV, or NLT.",
                "Default Bible Translation", MessageBoxButton.OK, MessageBoxImage.Information);
            DefaultTranslationTextBox.Focus();
            return;
        }

        DefaultBibleTranslation = value;
        DialogResult = true;
    }
}
