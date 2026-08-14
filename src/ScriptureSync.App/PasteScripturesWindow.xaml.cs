using System.Windows;

namespace ScriptureSync.App;

public partial class PasteScripturesWindow : Window
{
    public PasteScripturesWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => ScriptureTextBox.Focus();
    }

    public string ScriptureText => ScriptureTextBox.Text;

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ScriptureTextBox.Text))
        {
            MessageBox.Show(
                this,
                "Paste at least one scripture reference.",
                "Nothing to add",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }
}
