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
}

public static class OperationsLifecycleEndpoints
{
    public static void MapStructuredLifecycle(this WebApplication app)
    {
        app.MapPost("/api/operations/lifecycle/events", (
            HttpContext context,
            LifecycleEvent item,
            OperationsTelemetryAuthenticator authenticator,
            OperationsLifecycleStore store) =>
        {
            if (!authenticator.TryAuthenticate(context, out _)) return Results.Unauthorized();
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
    }
}
