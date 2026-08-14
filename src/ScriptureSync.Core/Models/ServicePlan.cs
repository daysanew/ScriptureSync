namespace ScriptureSync.Core.Models;

public sealed record ServicePlan(
    string Id,
    string ServiceTypeId,
    string ServiceTypeName,
    string Title,
    DateTimeOffset StartsAt)
{
    public string DisplayName => $"{ServiceTypeName} — {StartsAt:ddd, MMM d h:mm tt}";
}
