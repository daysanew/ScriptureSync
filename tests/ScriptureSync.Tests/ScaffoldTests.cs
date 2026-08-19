using ScriptureSync.Core.Configuration;
using ScriptureSync.Core.Models;

namespace ScriptureSync.Tests;

public class ScaffoldTests
{
    [Fact]
    public void Default_configuration_is_local_and_scoped_to_one_week()
    {
        var configuration = new AppConfiguration();

        Assert.Equal(7, configuration.PlanWindowDays);
        Assert.Equal("127.0.0.1", configuration.OpenLpBridgeAddress.Host);
        Assert.Equal(4317, configuration.OpenLpBridgeAddress.Port);
    }

    [Fact]
    public void Service_plan_display_name_includes_type_and_date()
    {
        var plan = new ServicePlan(
            "plan-1",
            "type-1",
            "Sunday",
            "Sunday Morning",
            new DateTimeOffset(2026, 8, 16, 10, 30, 0, TimeSpan.FromHours(-5)));

        Assert.Contains("Sunday", plan.DisplayName);
        Assert.Contains("Aug 16", plan.DisplayName);
    }

    [Fact]
    public void Local_paths_keep_all_application_data_together()
    {
        var paths = new LocalAppPaths("C:\\LocalData");

        Assert.Equal("C:\\LocalData\\ScriptureSync", paths.DataDirectory);
        Assert.StartsWith(paths.DataDirectory, paths.ConfigurationFile);
        Assert.StartsWith(paths.DataDirectory, paths.SyncStateFile);
        Assert.StartsWith(paths.DataDirectory, paths.LogFile);
    }
}
