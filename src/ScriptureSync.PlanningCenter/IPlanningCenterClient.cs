using ScriptureSync.Core.Models;

namespace ScriptureSync.PlanningCenter;

public interface IPlanningCenterClient
{
    Task<IReadOnlyList<ServicePlan>> GetUpcomingPlansAsync(
        int windowDays,
        IReadOnlyCollection<string>? includedServiceTypeIds = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlanningCenterScriptureItem>> GetScriptureItemsAsync(
        ServicePlan plan,
        IReadOnlyCollection<string> itemNames,
        CancellationToken cancellationToken = default);
}

public sealed record PlanningCenterScriptureItem(string Id, int Sequence, string Title, string Details);
