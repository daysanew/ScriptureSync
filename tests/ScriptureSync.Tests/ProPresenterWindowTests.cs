using System.Windows;
using System.Windows.Controls;
using ScriptureSync.App;
using ScriptureSync.App.Services;
using ScriptureSync.Core.Configuration;

namespace ScriptureSync.Tests;

public sealed class ProPresenterWindowTests
{
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
