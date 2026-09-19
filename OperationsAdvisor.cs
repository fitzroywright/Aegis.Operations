internal sealed record OperationsAdvisorFinding(
    string Severity,
    string Area,
    string Summary,
    string Evidence,
    string SuggestedNextCheck,
    string? ApplicationId = null,
    string? CorrelationId = null,
    string? Runbook = null);

internal static class OperationsAdvisorEndpoints
{
    internal static void MapOperationsAdvisor(this WebApplication app)
    {
        app.MapGet("/api/operations/advisor", async (
            string? applicationId,
            OperationsV1Store store,
            CancellationToken ct) =>
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            TimeSpan staleAfter = TimeSpan.FromSeconds(
                app.Configuration.GetValue("Operations:StaleAfterSeconds", 300));

            var applications = await store.LoadAsync<ApplicationOperationalRecord>("applications", ct);
            var diagnostics = await store.LoadAsync<DiagnosticRecord>("diagnostics", ct);
            var flows = await store.LoadAsync<FlowRecord>("flows", ct);
            var alerts = await store.LoadAsync<AlertRecord>("alerts", ct);
            var synchronization = await store.LoadAsync<SynchronizationEndpointRecord>("synchronization", ct);

            if (!string.IsNullOrWhiteSpace(applicationId))
            {
                applications = applications
                    .Where(x => x.ApplicationId.Equals(applicationId, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                diagnostics = diagnostics
                    .Where(x => x.ApplicationId.Equals(applicationId, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                flows = flows
                    .Where(x => x.ApplicationId.Equals(applicationId, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                alerts = alerts
                    .Where(x => x.ApplicationId.Equals(applicationId, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                synchronization = synchronization
                    .Where(x => x.ApplicationId.Equals(applicationId, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
            }

            var findings = new List<OperationsAdvisorFinding>();

            foreach (var item in applications)
            {
                OperationalState effective = OperationsFreshness.Apply(item.State, item.ObservedAtUtc, now, staleAfter);
                if (effective == OperationalState.Unknown)
                {
                    findings.Add(new(
                        "Warning",
                        "Application",
                        $"{item.DisplayName} has no fresh trustworthy aggregate state.",
                        $"Last application observation: {item.ObservedAtUtc:O}; published state: {item.State}.",
                        "Check the application's latest diagnostic publication and registration connectivity.",
                        item.ApplicationId,
                        Runbook: item.Runbook));
                }
                else if (effective is OperationalState.Failed or OperationalState.Offline)
                {
                    findings.Add(new(
                        "Critical",
                        "Application",
                        $"{item.DisplayName} is {effective}.",
                        item.Reason ?? $"Observed state {effective} at {item.ObservedAtUtc:O}.",
                        "Open the newest failing diagnostic and follow the linked runbook before attempting repair.",
                        item.ApplicationId,
                        Runbook: item.Runbook));
                }
                else if (effective is OperationalState.Degraded or OperationalState.Warning)
                {
                    findings.Add(new(
                        "Warning",
                        "Application",
                        $"{item.DisplayName} is {effective}.",
                        item.Reason ?? $"Observed state {effective} at {item.ObservedAtUtc:O}.",
                        "Review component diagnostics and active alerts to identify the degraded dependency.",
                        item.ApplicationId,
                        Runbook: item.Runbook));
                }
            }

            foreach (var diagnostic in diagnostics.OrderByDescending(x => x.ObservedAtUtc))
            {
                OperationalState effective = OperationsFreshness.Apply(
                    diagnostic.State,
                    diagnostic.ObservedAtUtc,
                    now,
                    staleAfter);

                if (effective is OperationalState.Failed or OperationalState.Offline)
                {
                    findings.Add(new(
                        "Critical",
                        diagnostic.Category.ToString(),
                        diagnostic.Summary,
                        diagnostic.Detail ?? $"{diagnostic.Component} reported {effective}.",
                        "Verify the component directly and inspect correlated flow/alert evidence before intervention.",
                        diagnostic.ApplicationId,
                        diagnostic.CorrelationId,
                        diagnostic.Runbook));
                }
                else if (effective is OperationalState.Warning or OperationalState.Degraded or OperationalState.Unknown)
                {
                    findings.Add(new(
                        "Warning",
                        diagnostic.Category.ToString(),
                        diagnostic.Summary,
                        diagnostic.Detail ?? $"{diagnostic.Component} reported {effective}.",
                        effective == OperationalState.Unknown
                            ? "Refresh or re-run the diagnostic; do not infer health from missing or stale evidence."
                            : "Inspect the component's dependency and recent history for the first abnormal observation.",
                        diagnostic.ApplicationId,
                        diagnostic.CorrelationId,
                        diagnostic.Runbook));
                }
            }

            foreach (var flow in flows.OrderByDescending(x => x.ObservedAtUtc).Take(100))
            {
                var abnormal = flow.Stages
                    .Select(stage => new { Stage = stage, Effective = FlowStateEvaluator.EffectiveState(stage, now) })
                    .FirstOrDefault(x => x.Effective is FlowStageState.Failed or FlowStageState.TimedOut or FlowStageState.Warning);

                if (abnormal is not null)
                {
                    findings.Add(new(
                        abnormal.Effective is FlowStageState.Failed or FlowStageState.TimedOut ? "Critical" : "Warning",
                        "Flow",
                        $"{flow.FlowType} is {abnormal.Effective} at {abnormal.Stage.Name}.",
                        abnormal.Stage.Error ?? $"Correlation {flow.CorrelationId}; last observed {flow.ObservedAtUtc:O}.",
                        "Inspect the failing stage and its immediate upstream/downstream dependency; use the correlation ID to trace related records.",
                        flow.ApplicationId,
                        flow.CorrelationId,
                        flow.Runbook));
                }
            }

            foreach (var sync in synchronization)
            {
                OperationalState queueState = OperationsFreshness.Apply(sync.QueueState, sync.ObservedAtUtc, now, staleAfter);
                if (!sync.Online || queueState is OperationalState.Failed or OperationalState.Warning or OperationalState.Unknown)
                {
                    findings.Add(new(
                        !sync.Online || queueState == OperationalState.Failed ? "Critical" : "Warning",
                        "Synchronization",
                        $"{sync.EndpointId} sync requires attention.",
                        $"Online={sync.Online}; queue={queueState}; pending={sync.Pending}; failed={sync.Failed}; observed={sync.ObservedAtUtc:O}.",
                        sync.Online
                            ? "Inspect queue age, failed items, and retry history before replaying anything."
                            : "Confirm connectivity first; preserve queued work and avoid destructive retry/reset actions.",
                        sync.ApplicationId));
                }
            }

            foreach (var alert in alerts.Where(x => x.State != AlertLifecycleState.Resolved))
            {
                findings.Add(new(
                    alert.Severity is AlertSeverity.Critical or AlertSeverity.High ? "Critical" : "Warning",
                    "Alert",
                    alert.Title,
                    alert.Detail,
                    "Review the alert evidence and linked runbook; acknowledge only after ownership is clear.",
                    alert.ApplicationId,
                    alert.CorrelationId,
                    alert.Runbook));
            }

            findings = findings
                .GroupBy(x => new { x.Severity, x.Area, x.Summary, x.ApplicationId, x.CorrelationId })
                .Select(g => g.First())
                .OrderBy(x => x.Severity == "Critical" ? 0 : 1)
                .ThenBy(x => x.ApplicationId)
                .ThenBy(x => x.Area)
                .Take(100)
                .ToList();

            return Results.Ok(new
            {
                generatedAtUtc = now,
                mode = "ReadOnlyEvidenceBased",
                destructiveActionsAllowed = false,
                scope = string.IsNullOrWhiteSpace(applicationId) ? "Estate" : applicationId,
                summary = findings.Count == 0
                    ? "No actionable abnormal evidence is currently available. This is not proof that the estate is healthy."
                    : $"{findings.Count} evidence-backed finding(s) require review.",
                findings
            });
        });
    }
}
