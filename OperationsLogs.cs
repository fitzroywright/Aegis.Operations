using Common.Diagnostics;

internal sealed record OperationsLogBatch(IReadOnlyList<OperationsLogRecord> Events);

internal static class OperationsLogEndpoints
{
    internal static void MapOperationsLogs(this WebApplication app)
    {
        app.MapPost("/api/operations/logs/observe", async (
            HttpContext context,
            OperationsLogBatch batch,
            OperationsV1Store store,
            OperationsTelemetryAuthenticator authenticator,
            CancellationToken ct) =>
        {
            if (batch.Events is null || batch.Events.Count == 0)
                return Results.BadRequest(new { error = "At least one log event is required." });
            if (batch.Events.Count > 1000)
                return Results.BadRequest(new { error = "A maximum of 1000 events may be published per batch." });

            string applicationId = batch.Events[0].ApplicationId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(applicationId))
                return Results.BadRequest(new { error = "applicationId is required." });

            if (batch.Events.Any(x =>
                !applicationId.Equals(x.ApplicationId, StringComparison.OrdinalIgnoreCase)))
                return Results.BadRequest(new { error = "All events in a batch must belong to the same application." });

            if (!await authenticator.IsAuthorizedAsync(context, applicationId, ct))
                return Results.Forbid();

            DateTimeOffset now = DateTimeOffset.UtcNow;
            OperationsLogRecord[] safe = batch.Events
                .Select(x => Sanitize(x, now))
                .Where(x => x is not null)
                .Cast<OperationsLogRecord>()
                .ToArray();

            int retention = Math.Clamp(
                app.Configuration.GetValue("Operations:Retention:ApplicationLogs", 50000),
                1000,
                500000);

            await store.MutateAsync<OperationsLogRecord>("application-logs", all =>
            {
                HashSet<Guid> ids = all.Select(x => x.EventId).ToHashSet();
                foreach (OperationsLogRecord item in safe)
                    if (ids.Add(item.EventId))
                        all.Add(item);

                return [.. all
                    .OrderByDescending(x => x.TimestampUtc)
                    .Take(retention)
                    .OrderBy(x => x.TimestampUtc)];
            }, ct);

            return Results.Accepted(value: new { accepted = safe.Length });
        });

        app.MapGet("/api/operations/logs", async (
            HttpContext context,
            string? applicationId,
            string? instanceId,
            string? level,
            string? category,
            string? correlationId,
            string? traceId,
            string? text,
            DateTimeOffset? since,
            int? take,
            OperationsV1Store store,
            OperationsTelemetryAuthenticator authenticator,
            CancellationToken ct) =>
        {
            if (!await CanReadAsync(context, authenticator, ct))
                return Results.Unauthorized();

            IEnumerable<OperationsLogRecord> query =
                await store.LoadAsync<OperationsLogRecord>("application-logs", ct);

            if (!string.IsNullOrWhiteSpace(applicationId))
                query = query.Where(x => Equal(x.ApplicationId, applicationId));
            if (!string.IsNullOrWhiteSpace(instanceId))
                query = query.Where(x => Equal(x.InstanceId, instanceId));
            if (!string.IsNullOrWhiteSpace(level))
            {
                int requested = LevelRank(level);
                query = query.Where(x => LevelRank(x.Level) >= requested);
            }
            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(x => Contains(x.Category, category));
            if (!string.IsNullOrWhiteSpace(correlationId))
                query = query.Where(x => Contains(x.CorrelationId, correlationId));
            if (!string.IsNullOrWhiteSpace(traceId))
                query = query.Where(x => Contains(x.TraceId, traceId));
            if (since.HasValue)
                query = query.Where(x => x.TimestampUtc >= since.Value);
            if (!string.IsNullOrWhiteSpace(text))
            {
                string q = text.Trim();
                query = query.Where(x =>
                    Contains(x.ApplicationId, q) ||
                    Contains(x.InstanceId, q) ||
                    Contains(x.Category, q) ||
                    Contains(x.Message, q) ||
                    Contains(x.ExceptionType, q) ||
                    Contains(x.ExceptionMessage, q) ||
                    Contains(x.CorrelationId, q) ||
                    Contains(x.TraceId, q) ||
                    Contains(x.OperationId, q) ||
                    (x.Properties?.Any(p => Contains(p.Key, q) || Contains(p.Value, q)) ?? false));
            }

            int limit = Math.Clamp(take ?? 1000, 1, 5000);
            OperationsLogRecord[] events = [.. query
                .OrderByDescending(x => x.TimestampUtc)
                .Take(limit)
                .OrderBy(x => x.TimestampUtc)];

            return Results.Ok(new
            {
                count = events.Length,
                observedAtUtc = DateTimeOffset.UtcNow,
                events
            });
        });

        app.MapGet("/api/operations/logs/filters", async (
            HttpContext context,
            OperationsV1Store store,
            OperationsTelemetryAuthenticator authenticator,
            CancellationToken ct) =>
        {
            if (!await CanReadAsync(context, authenticator, ct))
                return Results.Unauthorized();

            IReadOnlyList<OperationsLogRecord> all =
                await store.LoadAsync<OperationsLogRecord>("application-logs", ct);

            return Results.Ok(new
            {
                applications = all.Select(x => x.ApplicationId).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x),
                instances = all.Select(x => x.InstanceId).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x),
                levels = new[] { "Trace", "Debug", "Information", "Warning", "Error", "Critical" },
                categories = all.Select(x => x.Category).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).Take(500)
            });
        });
    }

    private static async Task<bool> CanReadAsync(
        HttpContext context,
        OperationsTelemetryAuthenticator authenticator,
        CancellationToken ct)
    {
        if (context.Items.TryGetValue("SuiteIdentity", out object? value) &&
            value is SuiteIdentity identity &&
            identity.Permissions.Contains("Operations.View", StringComparer.OrdinalIgnoreCase))
            return true;

        string applicationId = context.Request.Headers["X-Aegis-Application-Id"].ToString();
        return !string.IsNullOrWhiteSpace(applicationId) &&
               await authenticator.IsAuthorizedAsync(context, applicationId, ct);
    }

    private static OperationsLogRecord? Sanitize(OperationsLogRecord item, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(item.ApplicationId) ||
            string.IsNullOrWhiteSpace(item.InstanceId) ||
            string.IsNullOrWhiteSpace(item.Level) ||
            string.IsNullOrWhiteSpace(item.Category))
            return null;

        IReadOnlyDictionary<string, string>? properties = item.Properties is null
            ? null
            : item.Properties
                .Where(x => !string.IsNullOrWhiteSpace(x.Key) && !LifecycleEvent.LooksSensitive(x.Key))
                .Take(64)
                .ToDictionary(
                    x => Trim(x.Key, 128) ?? string.Empty,
                    x => Trim(x.Value, 1024) ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase);

        DateTimeOffset timestamp = item.TimestampUtc == default ? now : item.TimestampUtc;
        if (timestamp > now.AddMinutes(5) || timestamp < now.AddDays(-365))
            timestamp = now;

        return item with
        {
            EventId = item.EventId == Guid.Empty ? Guid.NewGuid() : item.EventId,
            TimestampUtc = timestamp,
            ApplicationId = item.ApplicationId.Trim(),
            InstanceId = item.InstanceId.Trim(),
            Environment = Trim(item.Environment, 128) ?? "Unknown",
            Level = NormalizeLevel(item.Level),
            Category = Trim(item.Category, 512) ?? "Unknown",
            Message = Trim(item.Message, 8192) ?? string.Empty,
            ExceptionType = Trim(item.ExceptionType, 512),
            ExceptionMessage = Trim(item.ExceptionMessage, 4096),
            CorrelationId = Trim(item.CorrelationId, 256),
            TraceId = Trim(item.TraceId, 256),
            SpanId = Trim(item.SpanId, 128),
            OperationId = Trim(item.OperationId, 256),
            Host = Trim(item.Host, 256),
            Properties = properties
        };
    }

    private static string NormalizeLevel(string value) =>
        value.Trim().ToUpperInvariant() switch
        {
            "TRACE" => "Trace",
            "DEBUG" => "Debug",
            "WARNING" or "WARN" => "Warning",
            "ERROR" => "Error",
            "CRITICAL" or "FATAL" => "Critical",
            _ => "Information"
        };

    private static int LevelRank(string value) =>
        NormalizeLevel(value) switch
        {
            "Trace" => 0,
            "Debug" => 1,
            "Information" => 2,
            "Warning" => 3,
            "Error" => 4,
            "Critical" => 5,
            _ => 2
        };

    private static bool Equal(string value, string other) =>
        value.Equals(other.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool Contains(string? value, string query) =>
        value?.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase) == true;

    private static string? Trim(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string text = value.Trim();
        return text.Length <= max ? text : text[..max];
    }
}
