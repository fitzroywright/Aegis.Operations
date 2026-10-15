internal sealed record ApplicationCleanupMarker(string ApplicationFamily, DateTimeOffset AtUtc, bool IncludeChildren = true);

internal static class ApplicationControlPlaneCleanup
{
    internal static async Task ClearCurrentStateAsync(
        OperationsV1Store store, RegistrationCleanupScope scope, CancellationToken ct)
    {
        if (!scope.IsValid || scope.IsProtected)
            throw new ArgumentException("Invalid or protected cleanup scope.", nameof(scope));
        await store.MutateAsync<ApplicationCleanupMarker>("application-cleanups", all =>
        {
            all.RemoveAll(x => x.ApplicationFamily.Equals(scope.ApplicationId, StringComparison.OrdinalIgnoreCase) && x.IncludeChildren == scope.IncludeChildren);
            all.Add(new(scope.ApplicationId, DateTimeOffset.UtcNow, scope.IncludeChildren));
            return all;
        }, ct);
        await store.MutateAsync<ApplicationOperationalRecord>("applications",
            all => all.Where(x => !scope.Matches(x.ApplicationId)).ToList(), ct);
        // Retain flow and alert history, while closing obsolete active state.
        await store.MutateAsync<FlowRecord>("flows", all => all.Select(x =>
            !scope.Matches(x.ApplicationId) ? x : x with
            {
                Stages = x.Stages.Select(s => s.State is FlowStageState.Active or FlowStageState.Pending or FlowStageState.Warning
                    ? s with { State = FlowStageState.Skipped, CompletedAtUtc = DateTimeOffset.UtcNow }
                    : s).ToList(),
                CurrentStage = "Registration reset"
            }).ToList(), ct);
        await store.MutateAsync<AlertRecord>("alerts", all => all.Select(x =>
            !scope.Matches(x.ApplicationId) || x.State == AlertLifecycleState.Resolved ? x : x with
            {
                State = AlertLifecycleState.Resolved,
                ResolvedAtUtc = DateTimeOffset.UtcNow,
                ResolvedBy = "Registration cleanup"
            }).ToList(), ct);
    }
}
