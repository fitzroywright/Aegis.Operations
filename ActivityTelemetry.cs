using Common.Diagnostics;
using Common.Security;

internal static class ActivityTelemetryEndpoints
{
    internal static void MapActivityTelemetry(this WebApplication app)
    {
        app.MapPost("/api/operations/activity/observe", async (
            HttpContext context,
            LifecycleEvent incoming,
            OperationsV1Store store,
            OperationsTelemetryAuthenticator telemetryAuthenticator,
            CancellationToken ct) =>
        {
            if (!await telemetryAuthenticator.IsAuthorizedAsync(context, incoming.ApplicationId, ct))
                return Results.Json(new { error = "Forbidden", detail = "The authenticated identity is not authorized for this operation." }, statusCode: StatusCodes.Status403Forbidden);

            string? validationError = Validate(incoming);
            if (validationError is not null)
                return Results.BadRequest(new { error = validationError });

            LifecycleEvent safe = incoming with
            {
                ApplicationId = incoming.ApplicationId.Trim(),
                InstanceId = incoming.InstanceId.Trim(),
                Flow = incoming.Flow.Trim(),
                Stage = incoming.Stage.Trim(),
                CorrelationId = incoming.CorrelationId.Trim(),
                OperationId = Trim(incoming.OperationId),
                RelatedBusinessId = Trim(incoming.RelatedBusinessId),
                Code = Trim(incoming.Code),
                Detail = TrimTo(incoming.Detail, 4096),
                Properties = incoming.Properties is null
                    ? null
                    : incoming.Properties.ToDictionary(
                        x => x.Key.Trim(),
                        x => TrimTo(x.Value, 1024) ?? string.Empty,
                        StringComparer.OrdinalIgnoreCase),
                OccurredAtUtc = incoming.OccurredAtUtc == default ? DateTimeOffset.UtcNow : incoming.OccurredAtUtc
            };

            int retention = Math.Clamp(
                app.Configuration.GetValue("Operations:Retention:ActivityEvents", 10000),
                500,
                100000);

            await store.MutateAsync<LifecycleEvent>("activity-events", all =>
            {
                if (!all.Any(x => x.EventId == safe.EventId))
                    all.Add(safe);

                return [.. all
                    .OrderByDescending(x => x.OccurredAtUtc)
                    .Take(retention)
                    .OrderBy(x => x.OccurredAtUtc)];
            }, ct);

            return Results.Accepted(value: new { safe.EventId, safe.CorrelationId });
        });

        app.MapGet("/api/operations/activity", async (
            HttpContext context,
            string? applicationId,
            string? instanceId,
            string? flow,
            string? stage,
            string? outcome,
            string? correlationId,
            string? text,
            DateTimeOffset? since,
            int? take,
            OperationsV1Store store,
            OperationsTelemetryAuthenticator telemetryAuthenticator,
            CancellationToken ct) =>
        {
            if (!await CanReadAsync(context, telemetryAuthenticator, ct))
                return Results.Unauthorized();

            IEnumerable<LifecycleEvent> query =
                await store.LoadAsync<LifecycleEvent>("activity-events", ct);

            query = ApplyFilters(query, applicationId, instanceId, flow, stage, outcome, correlationId, text, since);

            int limit = Math.Clamp(take ?? 500, 1, 5000);
            LifecycleEvent[] events = [.. query
                .OrderByDescending(x => x.OccurredAtUtc)
                .Take(limit)
                .OrderBy(x => x.OccurredAtUtc)];

            return Results.Ok(new
            {
                count = events.Length,
                observedAtUtc = DateTimeOffset.UtcNow,
                events
            });
        });

        app.MapGet("/api/operations/activity/filters", async (
            HttpContext context,
            OperationsV1Store store,
            OperationsTelemetryAuthenticator telemetryAuthenticator,
            CancellationToken ct) =>
        {
            if (!await CanReadAsync(context, telemetryAuthenticator, ct))
                return Results.Unauthorized();

            IReadOnlyList<LifecycleEvent> all =
                await store.LoadAsync<LifecycleEvent>("activity-events", ct);

            return Results.Ok(new
            {
                applications = all.Select(x => x.ApplicationId).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x),
                instances = all.Select(x => x.InstanceId).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x),
                flows = all.Select(x => x.Flow).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x),
                stages = all.Select(x => x.Stage).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x),
                outcomes = Enum.GetNames<LifecycleEventOutcome>()
            });
        });

        app.MapGet("/api/operations/activity/flows", async (
            HttpContext context,
            string? applicationId,
            string? instanceId,
            string? flow,
            string? stage,
            string? outcome,
            string? correlationId,
            string? text,
            DateTimeOffset? since,
            int? take,
            OperationsV1Store store,
            OperationsTelemetryAuthenticator telemetryAuthenticator,
            CancellationToken ct) =>
        {
            if (!await CanReadAsync(context, telemetryAuthenticator, ct))
                return Results.Unauthorized();

            IEnumerable<LifecycleEvent> query =
                await store.LoadAsync<LifecycleEvent>("activity-events", ct);

            query = ApplyFilters(query, applicationId, instanceId, flow, stage, outcome, correlationId, text, since);
            int limit = Math.Clamp(take ?? 100, 1, 1000);

            var traces = query
                .GroupBy(x => x.CorrelationId, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    LifecycleEvent[] events = [.. group.OrderBy(x => x.OccurredAtUtc)];
                    DateTimeOffset started = events[0].OccurredAtUtc;
                    DateTimeOffset ended = events[^1].OccurredAtUtc;
                    LifecycleEventOutcome status = events.Any(x => x.Outcome == LifecycleEventOutcome.Failed)
                        ? LifecycleEventOutcome.Failed
                        : events.Any(x => x.Outcome == LifecycleEventOutcome.Warning)
                            ? LifecycleEventOutcome.Warning
                            : events.Any(x => x.Outcome == LifecycleEventOutcome.Started) &&
                              !events.Any(x => x.Outcome == LifecycleEventOutcome.Succeeded)
                                ? LifecycleEventOutcome.Started
                                : events[^1].Outcome;

                    return new
                    {
                        correlationId = group.Key,
                        applicationId = events[0].ApplicationId,
                        instanceId = events[0].InstanceId,
                        flow = events[0].Flow,
                        operationId = events.Select(x => x.OperationId).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                        relatedBusinessId = events.Select(x => x.RelatedBusinessId).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                        startedAtUtc = started,
                        lastEventAtUtc = ended,
                        durationMs = Math.Max(0, (long)(ended - started).TotalMilliseconds),
                        status = status.ToString(),
                        eventCount = events.Length,
                        stages = events.Select((x, index) => new
                        {
                            sequence = index + 1,
                            x.EventId,
                            x.Stage,
                            outcome = x.Outcome.ToString(),
                            x.OccurredAtUtc,
                            x.Code,
                            x.Detail,
                            x.Properties
                        })
                    };
                })
                .OrderByDescending(x => x.lastEventAtUtc)
                .Take(limit)
                .ToArray();

            return Results.Ok(new
            {
                count = traces.Length,
                observedAtUtc = DateTimeOffset.UtcNow,
                traces
            });
        });

        app.MapGet("/logs", () =>
            Results.Content(ActivityPage.Render(), "text/html; charset=utf-8"));
        app.MapGet("/activity", () => Results.Redirect("/logs"));
    }

    private static async Task<bool> CanReadAsync(
        HttpContext context,
        OperationsTelemetryAuthenticator telemetryAuthenticator,
        CancellationToken ct)
    {
        if (context.Items.TryGetValue("SuiteIdentity", out object? value) &&
            value is SuiteIdentity identity &&
            identity.Permissions.Contains("Operations.View", StringComparer.OrdinalIgnoreCase))
            return true;

        string applicationId = context.Request.Headers["X-Aegis-Application-Id"].ToString();
        return !string.IsNullOrWhiteSpace(applicationId) &&
               await telemetryAuthenticator.IsAuthorizedAsync(context, applicationId, ct);
    }

    private static IEnumerable<LifecycleEvent> ApplyFilters(
        IEnumerable<LifecycleEvent> query,
        string? applicationId,
        string? instanceId,
        string? flow,
        string? stage,
        string? outcome,
        string? correlationId,
        string? text,
        DateTimeOffset? since)
    {
        if (!string.IsNullOrWhiteSpace(applicationId))
            query = query.Where(x => EqualsIgnoreCase(x.ApplicationId, applicationId));
        if (!string.IsNullOrWhiteSpace(instanceId))
            query = query.Where(x => EqualsIgnoreCase(x.InstanceId, instanceId));
        if (!string.IsNullOrWhiteSpace(flow))
            query = query.Where(x => EqualsIgnoreCase(x.Flow, flow));
        if (!string.IsNullOrWhiteSpace(stage))
            query = query.Where(x => EqualsIgnoreCase(x.Stage, stage));
        if (!string.IsNullOrWhiteSpace(outcome) &&
            Enum.TryParse<LifecycleEventOutcome>(outcome, true, out LifecycleEventOutcome parsedOutcome))
            query = query.Where(x => x.Outcome == parsedOutcome);
        if (!string.IsNullOrWhiteSpace(correlationId))
            query = query.Where(x => x.CorrelationId.Contains(correlationId, StringComparison.OrdinalIgnoreCase));
        if (since.HasValue)
            query = query.Where(x => x.OccurredAtUtc >= since.Value);
        if (!string.IsNullOrWhiteSpace(text))
        {
            string q = text.Trim();
            query = query.Where(x =>
                Contains(x.ApplicationId, q) ||
                Contains(x.InstanceId, q) ||
                Contains(x.Flow, q) ||
                Contains(x.Stage, q) ||
                Contains(x.CorrelationId, q) ||
                Contains(x.OperationId, q) ||
                Contains(x.RelatedBusinessId, q) ||
                Contains(x.Code, q) ||
                Contains(x.Detail, q) ||
                (x.Properties?.Any(p => Contains(p.Key, q) || Contains(p.Value, q)) ?? false));
        }

        return query;
    }

    private static string? Validate(LifecycleEvent value)
    {
        if (string.IsNullOrWhiteSpace(value.ApplicationId)) return "applicationId is required.";
        if (string.IsNullOrWhiteSpace(value.InstanceId)) return "instanceId is required.";
        if (string.IsNullOrWhiteSpace(value.Flow)) return "flow is required.";
        if (string.IsNullOrWhiteSpace(value.Stage)) return "stage is required.";
        if (string.IsNullOrWhiteSpace(value.CorrelationId)) return "correlationId is required.";

        if (value.Properties is not null)
        {
            foreach ((string key, string propertyValue) in value.Properties)
            {
                if (string.IsNullOrWhiteSpace(key))
                    return "Lifecycle property names cannot be blank.";
                if (LifecycleEvent.LooksSensitive(key))
                    return $"Lifecycle property '{key}' is not permitted because it may contain sensitive data.";
                if (propertyValue?.Length > 1024)
                    return $"Lifecycle property '{key}' exceeds 1024 characters.";
            }
        }

        return null;
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? TrimTo(string? value, int maxLength)
    {
        string? trimmed = Trim(value);
        if (trimmed is null) return null;
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static bool EqualsIgnoreCase(string value, string other) =>
        value.Equals(other.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool Contains(string? value, string q) =>
        value?.Contains(q, StringComparison.OrdinalIgnoreCase) == true;
}
