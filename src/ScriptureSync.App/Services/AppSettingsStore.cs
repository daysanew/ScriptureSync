using System.IO;
using System.Text.Json;
using ScriptureSync.Core.Configuration;
using ScriptureSync.Core.Logging;

namespace ScriptureSync.App.Services;

public sealed class AppSettingsStore(LocalAppPaths paths, IAppLogger logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public AppConfiguration Load()
    {
        try
        {
            if (!File.Exists(paths.ConfigurationFile)) return new AppConfiguration();
            return JsonSerializer.Deserialize<AppConfiguration>(
                File.ReadAllText(paths.ConfigurationFile), _jsonOptions) ?? new AppConfiguration();
        }
        catch (Exception exception)
        {
            logger.Error("The application settings could not be loaded.", exception);
            return new AppConfiguration();
        }
    }

    public void Save(AppConfiguration configuration)
    {
        try
        {
            Directory.CreateDirectory(paths.DataDirectory);
            File.WriteAllText(paths.ConfigurationFile,
                JsonSerializer.Serialize(configuration, _jsonOptions));
        }
        catch (Exception exception)
        {
            logger.Error("The application settings could not be saved.", exception);
        }
    }
}
