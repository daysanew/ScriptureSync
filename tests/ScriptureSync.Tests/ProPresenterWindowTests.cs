using System.Windows;
using System.Windows.Controls;
using ScriptureSync.App;
using ScriptureSync.App.Services;
using ScriptureSync.Core.Configuration;
using ScriptureSync.ProPresenter;

namespace ScriptureSync.Tests;

public sealed class ProPresenterWindowTests
{
    [Fact]
    public void Folder_defaults_follow_library_selection_but_preserve_custom_paths()
    {
        Exception? failure = null;
        var folder = Path.Combine(Path.GetTempPath(), "ScriptureSync-folders-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(folder, "Default"));
        Directory.CreateDirectory(Path.Combine(folder, "Sermons"));
        var thread = new Thread(() =>
        {
            try
            {
                var panel = new ProPresenterSettingsPanel(new AppConfiguration
                {
                    ProPresenter = new() { LibraryName = "Default", Address = "http://127.0.0.1:49627" }
                }, folder);
                Assert.Equal(Path.Combine(folder, "Default"), panel.Configuration.LibraryDirectory);
                var section = panel.Children.OfType<StackPanel>().Last();
                var library = section.Children.OfType<ComboBox>().First();
                library.Items.Add(new ProPresenterItem("second", "Sermons"));
                library.SelectedIndex = 1;
                Assert.Equal(Path.Combine(folder, "Sermons"), panel.Configuration.LibraryDirectory);
                var fields = section.Children.OfType<TextBox>().ToArray();
                fields[0].Text = "http://192.0.2.1:49627";
                Assert.Equal("", panel.Configuration.LibraryDirectory);
                fields[0].Text = "http://localhost:49627";
                Assert.Equal(Path.Combine(folder, "Sermons"), panel.Configuration.LibraryDirectory);
                fields[1].Text = "D:\\Custom Library";
                library.SelectedIndex = 0;
                Assert.Equal("D:\\Custom Library", panel.Configuration.LibraryDirectory);
                Assert.Null(ProPresenterFolderDefaults.FindLibrary("../Default", "http://localhost", folder));
                Assert.Null(ProPresenterFolderDefaults.FindLibrary("Missing", "http://localhost", folder));
            }
            catch (Exception e) { failure = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)));
        Directory.Delete(folder, true);
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [Fact]
    public void Settings_constructs_both_tabs_and_shared_footer_on_sta_thread()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var window = new SettingsWindow("KJV", new PlanningCenterCredentials("", ""), ["Scripture"], new AppConfiguration());
                window.Measure(new Size(660, 800));
                window.Arrange(new Rect(0, 0, 660, 800));
                var root = Assert.IsType<DockPanel>(window.Content);
                var tabs = Assert.Single(root.Children.OfType<TabControl>());
                Assert.Equal(2, tabs.Items.Count);
                Assert.Equal("OpenLP", window.PresentationSoftware);
                Assert.Single(root.Children.OfType<StackPanel>());
                var softwareTab = (TabItem)tabs.Items[1];
                var panel = Assert.IsType<ProPresenterSettingsPanel>(((ScrollViewer)softwareTab.Content).Content);
                var selector = Assert.Single(panel.Children.OfType<ComboBox>());
                var sections = panel.Children.OfType<StackPanel>().ToArray();
                Assert.Equal(Visibility.Visible, sections[0].Visibility);
                Assert.Equal(Visibility.Collapsed, sections[1].Visibility);
                selector.SelectedItem = "ProPresenter";
                Assert.Equal(Visibility.Collapsed, sections[0].Visibility);
                Assert.Equal(Visibility.Visible, sections[1].Visibility);
                var address = sections[1].Children.OfType<TextBox>().First();
                address.Text = "http://127.0.0.1:49627";
                selector.SelectedItem = "OpenLP";
                Assert.Equal(Visibility.Visible, sections[0].Visibility);
                Assert.Equal(Visibility.Collapsed, sections[1].Visibility);
                selector.SelectedItem = "ProPresenter";
                Assert.Equal("http://127.0.0.1:49627", panel.Configuration.Address);
                window.Close();
            }
            catch (Exception e) { failure = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)));
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
