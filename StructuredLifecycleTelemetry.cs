using Common.Diagnostics;

public sealed class OperationsLifecycleStore
{
    private readonly object gate = new();
    private readonly LinkedList<LifecycleEvent> events = new();
    private const int Capacity = 5000;

    public void Append(LifecycleEvent item)
    {
        ArgumentNullException.ThrowIfNull(item);
        lock (gate)
        {
            events.AddFirst(item);
            while (events.Count > Capacity) events.RemoveLast();
        }
    }

    public IReadOnlyList<LifecycleEvent> Recent(int take = 200)
    {
        lock (gate) return events.Take(Math.Clamp(take, 1, 1000)).ToArray();
    }

    public IReadOnlyList<LifecycleJourneySnapshot> Journeys(int take = 100)
    {
        lock (gate)
        {
            return events
                .GroupBy(x => new { x.ApplicationId, x.InstanceId, x.Flow, x.CorrelationId })
                .Select(group =>
                {
                    LifecycleEvent[] ordered = group.OrderBy(x => x.OccurredAtUtc).ToArray();
                    LifecycleEvent latest = ordered[^1];
                    string health = latest.Outcome switch
                    {
                        LifecycleEventOutcome.Failed => "Failed",
                        LifecycleEventOutcome.Warning => "Warning",
                        LifecycleEventOutcome.Started => "Running",
                        LifecycleEventOutcome.Skipped => "Skipped",
                        _ => "Healthy"
                    };
                    return new LifecycleJourneySnapshot(
                        latest.ApplicationId,
                        latest.InstanceId,
                        latest.Flow,
                        latest.CorrelationId,
                        latest.OperationId,
                        latest.RelatedBusinessId,
                        latest.Stage,
                        latest.Outcome,
                        health,
                        ordered[0].OccurredAtUtc,
                        latest.OccurredAtUtc,
                        ordered.Select(x => new LifecycleJourneyStage(
                            x.Stage,
                            x.Outcome,
                            x.OccurredAtUtc,
                            x.Code,
                            x.Detail)).ToArray());
                })
                .OrderByDescending(x => x.LastEventAtUtc)
                .Take(Math.Clamp(take, 1, 500))
                .ToArray();
        }
    }
}

public sealed record LifecycleJourneySnapshot(
    string ApplicationId,
    string InstanceId,
    string Flow,
    string CorrelationId,
    string? OperationId,
    string? RelatedBusinessId,
    string CurrentStage,
    LifecycleEventOutcome Outcome,
    string Health,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset LastEventAtUtc,
    IReadOnlyList<LifecycleJourneyStage> Stages);

public sealed record LifecycleJourneyStage(
    string Stage,
    LifecycleEventOutcome Outcome,
    DateTimeOffset OccurredAtUtc,
    string? Code,
    string? Detail);

public static class OperationsLifecycleEndpoints
{
    public static void MapStructuredLifecycle(this WebApplication app)
    {
        app.MapPost("/api/operations/lifecycle/events", async (
            HttpContext context,
            LifecycleEvent item,
            OperationsTelemetryAuthenticator authenticator,
            OperationsLifecycleStore store,
            CancellationToken ct) =>
        {
            if (!await authenticator.IsAuthorizedAsync(context, item.ApplicationId, ct)) return Results.Unauthorized();
            if (LifecycleEvent.LooksSensitive(item.Code ?? string.Empty))
                return Results.BadRequest(new { error = "Sensitive telemetry field rejected." });
            store.Append(item);
            return Results.Accepted();
        });

        app.MapGet("/api/operations/lifecycle/events", (
            HttpContext context,
            int? take,
            OperationsLifecycleStore store) =>
        {
            if (!(context.User?.Identity?.IsAuthenticated ?? false)) return Results.Unauthorized();
            return Results.Ok(store.Recent(take ?? 200));
        });

        app.MapGet("/api/operations/lifecycle/journeys", (
            HttpContext context,
            int? take,
            OperationsLifecycleStore store) =>
        {
            if (!(context.User?.Identity?.IsAuthenticated ?? false)) return Results.Unauthorized();
            return Results.Ok(new
            {
                generatedAtUtc = DateTimeOffset.UtcNow,
                journeys = store.Journeys(take ?? 100)
            });
        });
    }
}

