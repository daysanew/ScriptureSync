using ScriptureSync.App.Services;
using ScriptureSync.Core.Configuration;
using ScriptureSync.Core.Logging;

namespace ScriptureSync.Tests;

public sealed class AppSettingsStoreTests : IDisposable
{
    private readonly string _temporaryRoot = Path.Combine(
        Path.GetTempPath(), $"ScriptureSyncSettingsTests-{Guid.NewGuid():N}");

    [Fact]
    public void Fresh_settings_use_KJV_as_the_default_translation()
    {
        var store = CreateStore();

        Assert.Equal("KJV", store.Load().DefaultBibleTranslation);
        Assert.Equal("OpenLP", store.Load().PresentationSoftware);
    }

    [Fact]
    public void ProPresenter_configuration_is_saved_and_reloaded()
    {
        var config = new ProPresenterConfiguration { Address = "http://127.0.0.1:49627", LibraryId = Guid.NewGuid().ToString(), TemplatePath = "template.pro" };
        CreateStore().Save(new AppConfiguration { PresentationSoftware = "ProPresenter", ProPresenter = config });
        var saved = CreateStore().Load();
        Assert.Equal("ProPresenter", saved.PresentationSoftware);
        Assert.Equal(config, saved.ProPresenter);
    }

    [Fact]
    public void Default_translation_is_saved_and_reloaded()
    {
        var store = CreateStore();
        store.Save(new AppConfiguration { DefaultBibleTranslation = "NLT" });

        Assert.Equal("NLT", CreateStore().Load().DefaultBibleTranslation);
    }

    [Fact]
    public void Planning_Center_item_names_are_saved_and_reloaded()
    {
        var store = CreateStore();
        store.Save(new AppConfiguration { PlanningCenterItemNames = ["Scripture", "Message Text"] });

        Assert.Equal(["Scripture", "Message Text"], CreateStore().Load().PlanningCenterItemNames);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryRoot)) Directory.Delete(_temporaryRoot, recursive: true);
    }

    private AppSettingsStore CreateStore() =>
        new(new LocalAppPaths(_temporaryRoot), new SilentLogger());

    private sealed class SilentLogger : IAppLogger
    {
        public void Info(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }
}
