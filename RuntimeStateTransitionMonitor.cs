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
            configuration.GetValue("Operations:StaleAfterSeconds", 90));

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

            string correlationId = Guid.NewGuid().ToString("N");
            var properties = new Dictionary<string, string>
            {
                ["previousState"] = previous.ToString(),
                ["currentState"] = effective.ToString()
            };

            LifecycleEvent evt = LifecycleEvent.Create(
                app.ApplicationId,
                app.InstanceId,
                "Application State",
                "StateChanged",
                outcome,
                correlationId,
                code: "APPLICATION_STATE_CHANGED",
                detail: detail,
                properties: properties,
                occurredAtUtc: now);

            int activityRetention = Math.Clamp(
                configuration.GetValue("Operations:Retention:ActivityEvents", 10000),
                500,
                100000);

            await store.MutateAsync<LifecycleEvent>("activity-events", all =>
            {
                all.Add(evt);
                return [.. all
                    .OrderByDescending(x => x.OccurredAtUtc)
                    .Take(activityRetention)
                    .OrderBy(x => x.OccurredAtUtc)];
            }, ct);

            string level = outcome == LifecycleEventOutcome.Failed
                ? "Error"
                : outcome == LifecycleEventOutcome.Warning
                    ? "Warning"
                    : "Information";

            var logRecord = new OperationsLogRecord(
                Guid.NewGuid(),
                now,
                app.ApplicationId,
                app.InstanceId,
                app.Environment ?? "Unknown",
                level,
                "Application State",
                detail,
                CorrelationId: correlationId,
                Host: Environment.MachineName,
                Properties: properties);

            int logRetention = Math.Clamp(
                configuration.GetValue("Operations:Retention:ApplicationLogs", 50000),
                1000,
                500000);

            await store.MutateAsync<OperationsLogRecord>("application-logs", all =>
            {
                all.Add(logRecord);
                return [.. all
                    .OrderByDescending(x => x.TimestampUtc)
                    .Take(logRetention)
                    .OrderBy(x => x.TimestampUtc)];
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
