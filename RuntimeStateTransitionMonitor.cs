using Common.Diagnostics;

internal sealed class RuntimeStateTransitionMonitor(
    OperationsV1Store store,
    IConfiguration configuration,
    ILogger<RuntimeStateTransitionMonitor> logger) : BackgroundService
{
    private readonly Dictionary<string, OperationalState> _last = new(StringComparer.OrdinalIgnoreCase);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeSpan interval = TimeSpan.FromSeconds(Math.Clamp(
            configuration.GetValue("Operations:RuntimeTransitionScanSeconds", 5), 2, 60));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Runtime transition scan failed.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task ScanAsync(CancellationToken ct)
    {
        IReadOnlyList<ApplicationOperationalRecord> applications =
            await store.LoadAsync<ApplicationOperationalRecord>("applications", ct);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        TimeSpan staleAfter = TimeSpan.FromSeconds(
            configuration.GetValue("Operations:StaleAfterSeconds", 300));

        foreach (ApplicationOperationalRecord app in applications)
        {
            string key = app.ApplicationId.Trim() + "\u001f" + app.InstanceId.Trim();
            OperationalState effective = OperationsFreshness.IsStale(app.ObservedAtUtc, now, staleAfter)
                ? OperationalState.Offline
                : app.State;

            if (!_last.TryGetValue(key, out OperationalState previous))
            {
                _last[key] = effective;
                continue;
            }

            if (previous == effective)
                continue;

            _last[key] = effective;

            LifecycleEventOutcome outcome = effective switch
            {
                OperationalState.Healthy => LifecycleEventOutcome.Succeeded,
                OperationalState.Degraded or OperationalState.Warning or OperationalState.Unknown
                    => LifecycleEventOutcome.Warning,
                OperationalState.Offline or OperationalState.Failed
                    => LifecycleEventOutcome.Failed,
                _ => LifecycleEventOutcome.Warning
            };

            string detail = effective == OperationalState.Offline
                ? "Application heartbeat became stale; runtime is offline."
                : $"Application runtime changed from {previous} to {effective}.";

            LifecycleEvent evt = LifecycleEvent.Create(
                app.ApplicationId,
                app.InstanceId,
                "Runtime",
                "StateChanged",
                outcome,
                Guid.NewGuid().ToString("N"),
                code: "RUNTIME_STATE_CHANGED",
                detail: detail,
                properties: new Dictionary<string, string>
                {
                    ["previousState"] = previous.ToString(),
                    ["currentState"] = effective.ToString()
                },
                occurredAtUtc: now);

            int retention = Math.Clamp(
                configuration.GetValue("Operations:Retention:ActivityEvents", 10000),
                500,
                100000);

            await store.MutateAsync<LifecycleEvent>("activity-events", all =>
            {
                all.Add(evt);
                return [.. all
                    .OrderByDescending(x => x.OccurredAtUtc)
                    .Take(retention)
                    .OrderBy(x => x.OccurredAtUtc)];
            }, ct);

            logger.LogInformation(
                "Runtime state transition observed for {ApplicationId}/{InstanceId}: {PreviousState} -> {CurrentState}.",
                app.ApplicationId,
                app.InstanceId,
                previous,
                effective);
        }
    }
}
