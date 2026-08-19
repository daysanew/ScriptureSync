namespace ScriptureSync.Core.Configuration;

public sealed class AppConfiguration
{
    public int PlanWindowDays { get; init; } = 7;

    public Uri OpenLpBridgeAddress { get; init; } = new("http://127.0.0.1:4317/v1/");

    public List<string> IncludedServiceTypeIds { get; init; } = [];

    public Dictionary<string, string> BibleMappings { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
}
