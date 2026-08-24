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
    }

    [Fact]
    public void Default_translation_is_saved_and_reloaded()
    {
        var store = CreateStore();
        store.Save(new AppConfiguration { DefaultBibleTranslation = "NLT" });

        Assert.Equal("NLT", CreateStore().Load().DefaultBibleTranslation);
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
