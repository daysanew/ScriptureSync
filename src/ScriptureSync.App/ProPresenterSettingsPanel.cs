using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ScriptureSync.Core.Configuration;
using ScriptureSync.ProPresenter;
using ScriptureSync.OpenLP;
using System.IO;

namespace ScriptureSync.App;

public sealed class ProPresenterSettingsPanel : StackPanel
{
    private readonly ComboBox _software = new() { ItemsSource = new[] { "OpenLP", "ProPresenter" }, Height = 30 };
    private readonly TextBox _address = new();
    private readonly TextBox _template = new();
    private readonly TextBox _bibles = new();
    private readonly TextBox _directory = new();
    private readonly ComboBox _library = new() { DisplayMemberPath = "Name", Height = 30 };
    private readonly ComboBox _playlist = new() { DisplayMemberPath = "Name", Height = 30 };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 12, 0, 0) };

    public ProPresenterSettingsPanel(AppConfiguration configuration, string? defaultLibraryRoot = null)
    {
        Margin = new Thickness(24);
        _software.SelectedItem = configuration.PresentationSoftware;
        var saved = configuration.ProPresenter;
        _address.Text = saved.Address;
        _template.Text = saved.TemplatePath;
        _bibles.Text = saved.BibleDirectory;
        _directory.Text = saved.LibraryDirectory;
        _library.Items.Add(new ProPresenterItem(saved.LibraryId, saved.LibraryName)); _library.SelectedIndex = 0;
        string? automaticFolder = ProPresenterFolderDefaults.FindLibrary(saved.LibraryName, saved.Address, defaultLibraryRoot);
        if (!string.Equals(_directory.Text, automaticFolder, StringComparison.OrdinalIgnoreCase)) automaticFolder = null;
        void FillDefaultLibrary()
        {
            if (_library.SelectedItem is not ProPresenterItem library || string.IsNullOrEmpty(library.Name)) return;
            if (!string.IsNullOrWhiteSpace(_directory.Text) &&
                !string.Equals(_directory.Text, automaticFolder, StringComparison.OrdinalIgnoreCase)) return;
            automaticFolder = ProPresenterFolderDefaults.FindLibrary(library.Name, _address.Text.Trim(), defaultLibraryRoot);
            _directory.Text = automaticFolder ?? "";
        }
        _library.SelectionChanged += (_, _) => FillDefaultLibrary();
        _address.TextChanged += (_, _) => FillDefaultLibrary();
        FillDefaultLibrary();
        if (string.IsNullOrWhiteSpace(_bibles.Text) && Directory.Exists(ProPresenterFolderDefaults.BibleDirectory))
            _bibles.Text = ProPresenterFolderDefaults.BibleDirectory;
        _playlist.Items.Add(new ProPresenterItem(saved.PlaylistId, string.IsNullOrEmpty(saved.PlaylistName) ? "Library only" : saved.PlaylistName)); _playlist.SelectedIndex = 0;
        AddField("Presentation software", _software);
        AddField("ProPresenter API address (from Network settings)", _address);
        var connect = new Button { Content = "Test connection / load destinations", Height = 34, Margin = new(0, 8, 0, 4) };
        connect.Click += async (_, _) =>
        {
            connect.IsEnabled = false;
            try
            {
                using var http = NewHttp();
                var api = new ProPresenterApiClient(http, new Uri(_address.Text.Trim()));
                var version = await api.GetVersionAsync();
                var selectedLibrary = (_library.SelectedItem as ProPresenterItem)?.Id;
                var selectedPlaylist = (_playlist.SelectedItem as ProPresenterItem)?.Id;
                var libraries = await api.GetLibrariesAsync();
                var playlists = await api.GetPlaylistsAsync();
                _library.Items.Clear(); foreach (var item in libraries) _library.Items.Add(item);
                _library.SelectedItem = libraries.FirstOrDefault(l => l.Id == selectedLibrary) ?? libraries.FirstOrDefault();
                _playlist.Items.Clear(); _playlist.Items.Add(new ProPresenterItem("", "Library only"));
                foreach (var item in playlists) _playlist.Items.Add(item);
                _playlist.SelectedItem = playlists.FirstOrDefault(p => p.Id == selectedPlaylist) ?? _playlist.Items[0];
                var bibles = new ProPresenterBibleCatalog(_bibles.Text.Trim()).Discover();
                _status.Text = $"Connected: {version.Description}. Bibles: {string.Join(", ", bibles.Select(b => b.Code))}. Preview will validate the selected template and library folder.";
            }
            catch (Exception e) { _status.Text = e.Message; }
            finally { connect.IsEnabled = true; }
        };
        Children.Add(connect);
        AddField("Destination library", _library);
        AddField("Playlist (optional; item mapping is reviewed before sync)", _playlist);
        AddPath("Library folder on this computer (must match the selected library)", _directory, false);
        Children.Add(new TextBlock
        {
            Text = "The library folder fills automatically when a matching folder exists in ProPresenter’s default local workspace. Custom folders can still be selected with Browse.",
            TextWrapping = TextWrapping.Wrap, Margin = new(0, 4, 0, 0)
        });
        AddPath("Exported scripture template (.pro)", _template, true);
        AddPath("Installed Bible folder", _bibles, false);
        var defaults = new Button { Content = "Use default folders", HorizontalAlignment = HorizontalAlignment.Left, Padding = new(12, 5, 12, 5), Margin = new(0, 10, 0, 0) };
        defaults.Click += (_, _) =>
        {
            automaticFolder = (_library.SelectedItem is ProPresenterItem library)
                ? ProPresenterFolderDefaults.FindLibrary(library.Name, _address.Text.Trim(), defaultLibraryRoot) : null;
            if (automaticFolder is not null) _directory.Text = automaticFolder;
            if (Directory.Exists(ProPresenterFolderDefaults.BibleDirectory)) _bibles.Text = ProPresenterFolderDefaults.BibleDirectory;
            _status.Text = automaticFolder is null
                ? "No matching default local library folder was found. Select your library folder with Browse."
                : "Default folders selected. Your exported scripture template remains a separate file selection.";
        };
        Children.Add(defaults);
        Children.Add(_status);
        // Keep both sets of controls alive so switching destinations retains unsaved values.
        var proPresenter = new StackPanel();
        while (Children.Count > 2)
        {
            var child = Children[2];
            Children.RemoveAt(2);
            proPresenter.Children.Add(child);
        }
        var openLp = new StackPanel();
        openLp.Children.Add(new TextBlock
        {
            Text = "OpenLP connection", FontWeight = FontWeights.SemiBold, Margin = new(0, 18, 0, 8)
        });
        openLp.Children.Add(new TextBlock
        {
            Text = "Run OpenLP on this computer and enable ScriptureSync under Settings > Manage Plugins. ScriptureSync adds passages to the current OpenLP service.",
            TextWrapping = TextWrapping.Wrap
        });
        openLp.Children.Add(new TextBlock { Text = $"Plugin address: {configuration.OpenLpBridgeAddress}", Margin = new(0, 12, 0, 8), TextWrapping = TextWrapping.Wrap });
        openLp.Children.Add(new TextBlock
        {
            Text = "Set the default Bible translation on the General / Planning Center tab. Translation codes must match the short names of Bibles installed in OpenLP.",
            TextWrapping = TextWrapping.Wrap
        });
        var openLpStatus = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new(0, 10, 0, 0) };
        var checkOpenLp = new Button { Content = "Test OpenLP connection", Height = 34, Margin = new(0, 12, 0, 0) };
        checkOpenLp.Click += async (_, _) =>
        {
            checkOpenLp.IsEnabled = false;
            try
            {
                using var client = new OpenLpBridgeClient(configuration.OpenLpBridgeAddress);
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var info = await client.GetConnectionInfoAsync(timeout.Token);
                openLpStatus.Text = $"Connected. Installed Bibles: {string.Join(", ", info.InstalledBibles.Keys)}.";
            }
            catch (Exception e) { openLpStatus.Text = e.Message; }
            finally { checkOpenLp.IsEnabled = true; }
        };
        openLp.Children.Add(checkOpenLp);
        openLp.Children.Add(openLpStatus);
        Children.Add(openLp);
        Children.Add(proPresenter);
        void ShowSelectedSoftware()
        {
            openLp.Visibility = Software == "OpenLP" ? Visibility.Visible : Visibility.Collapsed;
            proPresenter.Visibility = Software == "ProPresenter" ? Visibility.Visible : Visibility.Collapsed;
        }
        _software.SelectionChanged += (_, _) => ShowSelectedSoftware();
        ShowSelectedSoftware();
    }

    public string Software => _software.SelectedItem as string ?? "OpenLP";
    public ProPresenterConfiguration Configuration => new()
    {
        Address = _address.Text.Trim(), TemplatePath = _template.Text.Trim(), BibleDirectory = _bibles.Text.Trim(),
        LibraryDirectory = _directory.Text.Trim(), LibraryId = (_library.SelectedItem as ProPresenterItem)?.Id ?? "",
        LibraryName = (_library.SelectedItem as ProPresenterItem)?.Name ?? "",
        PlaylistId = (_playlist.SelectedItem as ProPresenterItem)?.Id ?? "", PlaylistName = (_playlist.SelectedItem as ProPresenterItem)?.Name ?? ""
    };
    public static HttpClient NewHttp() => new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(15) };
    private void AddField(string label, Control control)
    {
        Children.Add(new TextBlock { Text = label, Margin = new(0, 12, 0, 4), TextWrapping = TextWrapping.Wrap });
        control.MinHeight = 30;
        Children.Add(control);
    }
    private void AddPath(string label, TextBox box, bool file)
    {
        AddField(label, box);
        var browse = new Button { Content = "Browse…", HorizontalAlignment = HorizontalAlignment.Right, Margin = new(0, 4, 0, 0), Padding = new(12, 3, 12, 3) };
        browse.Click += (_, _) =>
        {
            if (file)
            {
                var dialog = new OpenFileDialog { Filter = "ProPresenter presentation|*.pro" };
                if (dialog.ShowDialog() == true) box.Text = dialog.FileName;
            }
            else
            {
                var dialog = new OpenFolderDialog();
                if (dialog.ShowDialog() == true) box.Text = dialog.FolderName;
            }
        };
        Children.Add(browse);
    }
}
