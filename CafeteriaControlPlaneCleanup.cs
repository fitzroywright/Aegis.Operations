internal sealed record ApplicationCleanupMarker(string ApplicationFamily, DateTimeOffset AtUtc);

internal static class CafeteriaControlPlaneCleanup
{
    internal static bool Matches(string applicationId) =>
        applicationId.Equals("Cafeteria.Services", StringComparison.OrdinalIgnoreCase) ||
        applicationId.Equals("Aegis.Cafeteria", StringComparison.OrdinalIgnoreCase) ||
        applicationId.StartsWith("Aegis.Cafeteria.", StringComparison.OrdinalIgnoreCase);

    internal static async Task ClearCurrentStateAsync(OperationsV1Store store, CancellationToken ct)
    {
        await store.MutateAsync<ApplicationCleanupMarker>("application-cleanups", all =>
        {
            all.RemoveAll(x => x.ApplicationFamily == "Aegis.Cafeteria");
            all.Add(new("Aegis.Cafeteria", DateTimeOffset.UtcNow));
            return all;
        }, ct);
        await store.MutateAsync<ApplicationOperationalRecord>("applications",
            all => all.Where(x => !Matches(x.ApplicationId)).ToList(), ct);
        // Retain flow and alert history, while closing obsolete active state.
        await store.MutateAsync<FlowRecord>("flows", all => all.Select(x =>
            !Matches(x.ApplicationId) ? x : x with
            {
                Stages = x.Stages.Select(s => s.State is FlowStageState.Active or FlowStageState.Pending or FlowStageState.Warning
                    ? s with { State = FlowStageState.Skipped, CompletedAtUtc = DateTimeOffset.UtcNow }
                    : s).ToList(),
                CurrentStage = "Registration reset"
            }).ToList(), ct);
        await store.MutateAsync<AlertRecord>("alerts", all => all.Select(x =>
            !Matches(x.ApplicationId) || x.State == AlertLifecycleState.Resolved ? x : x with
            {
                State = AlertLifecycleState.Resolved,
                ResolvedAtUtc = DateTimeOffset.UtcNow,
                ResolvedBy = "Registration cleanup"
            }).ToList(), ct);
    }
}
