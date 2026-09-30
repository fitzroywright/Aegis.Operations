using System.Text.Json;
using Common.Security;

internal enum DiagnosticCategory
{
    Health,
    Configuration,
    Dependency,
    Flow,
    Synchronization,
    Queue,
    Security,
    Performance,
    Error,
    BusinessProbe,
    Decision,
    OperationalEvent
}

internal sealed record DiagnosticRecord(
    string DiagnosticId,
    string ApplicationId,
    string Component,
    DiagnosticCategory Category,
    OperationalState State,
    string Summary,
    DateTimeOffset ObservedAtUtc,
    string? Detail = null,
    string? CorrelationId = null,
    string? Runbook = null,
    JsonElement? Data = null);

internal sealed record MessagingProviderRecord(
    string Provider,
    OperationalState State,
    DateTimeOffset ObservedAtUtc,
    string? Reason = null);

internal sealed record MessagingOperationalRecord(
    string ApplicationId,
    string InstanceId,
    int OutboundPending,
    int Dispatching,
    int Retrying,
    int DeadLetter,
    int Delivered,
    int Inbound,
    DateTimeOffset ObservedAtUtc,
    DateTimeOffset? OldestPendingAtUtc,
    IReadOnlyList<MessagingProviderRecord> Providers);

internal sealed record MessageTraceRecord(
    string MessageId,
    string CorrelationId,
    string ApplicationId,
    string Provider,
    int Attempts,
    string State,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ObservedAtUtc,
    IReadOnlyList<MessageTraceStage> Stages,
    string? Failure = null);

internal sealed record MessageTraceStage(
    string Name,
    string State,
    DateTimeOffset? AtUtc = null,
    string? Detail = null);

internal sealed record SynchronizationEndpointRecord(
    string EndpointId,
    string ApplicationId,
    string InstanceId,
    bool Online,
    OperationalState QueueState,
    int Pending,
    int Failed,
    DateTimeOffset ObservedAtUtc,
    DateTimeOffset? OldestPendingAtUtc = null,
    string? Reason = null);

internal sealed record RunbookRecord(
    string RunbookId,
    string ApplicationId,
    string Title,
    string[] Sections,
    string Content,
    DateTimeOffset UpdatedAtUtc);

internal sealed record OperationsAuditRecord(
    string AuditId,
    DateTimeOffset AtUtc,
    string Actor,
    string Action,
    string? ApplicationId,
    string? TargetId,
    string? CorrelationId,
    string? Detail);

internal static class OperationsSpecializedEndpoints
{
    internal static void MapOperationsSpecialized(this WebApplication app)
    {
        app.MapGet("/api/operations/diagnostics", async (
            string? applicationId,
            string? category,
            OperationsV1Store store,
            CancellationToken ct) =>
        {
            IEnumerable<DiagnosticRecord> q = await store.LoadAsync<DiagnosticRecord>("diagnostics", ct);
            if (!string.IsNullOrWhiteSpace(applicationId))
                q = q.Where(x => x.ApplicationId.Equals(applicationId, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(category))
                q = q.Where(x => x.Category.ToString().Equals(category, StringComparison.OrdinalIgnoreCase));
            DateTimeOffset now = DateTimeOffset.UtcNow;
            TimeSpan staleAfter = TimeSpan.FromSeconds(app.Configuration.GetValue("Operations:StaleAfterSeconds", 300));
            return Results.Ok(q.OrderByDescending(x => x.ObservedAtUtc).Take(500).Select(x => new
            {
                x.DiagnosticId,
                x.ApplicationId,
                x.Component,
                x.Category,
                state = OperationsFreshness.Apply(x.State, x.ObservedAtUtc, now, staleAfter).ToString(),
                publishedState = x.State.ToString(),
                x.Summary,
                x.ObservedAtUtc,
                stale = OperationsFreshness.IsStale(x.ObservedAtUtc, now, staleAfter),
                x.Detail,
                x.CorrelationId,
                x.Runbook,
                x.Data
            }));
        });

        app.MapPost("/api/operations/diagnostics/observe", async (
            HttpContext context,
            DiagnosticRecord record,
            OperationsV1Store store,
            OperationsTelemetryAuthenticator telemetryAuthenticator,
            CancellationToken ct) =>
        {
            if (!await telemetryAuthenticator.IsAuthorizedAsync(context, record.ApplicationId, ct)) return Results.Forbid();
            string id = string.IsNullOrWhiteSpace(record.DiagnosticId)
                ? $"{record.ApplicationId}:{record.Component}:{record.Category}"
                : record.DiagnosticId;
            var safe = record with
            {
                DiagnosticId = id,
                ObservedAtUtc = record.ObservedAtUtc == default ? DateTimeOffset.UtcNow : record.ObservedAtUtc
            };
            await store.MutateAsync<DiagnosticRecord>("diagnostics", all =>
            {
                int index = all.FindIndex(x => x.DiagnosticId.Equals(id, StringComparison.OrdinalIgnoreCase));
                if (index >= 0) all[index] = safe; else all.Add(safe);
                return all;
            }, ct);
            return Results.Accepted();
        });

        app.MapGet("/api/operations/messaging", async (OperationsV1Store store, CancellationToken ct) =>
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            TimeSpan staleAfter = TimeSpan.FromSeconds(app.Configuration.GetValue("Operations:StaleAfterSeconds", 300));
            return Results.Ok((await store.LoadAsync<MessagingOperationalRecord>("messaging", ct))
                .OrderByDescending(x => x.ObservedAtUtc)
                .Select(x => new
                {
                    x.ApplicationId,
                    x.InstanceId,
                    x.OutboundPending,
                    x.Dispatching,
                    x.Retrying,
                    x.DeadLetter,
                    x.Delivered,
                    x.Inbound,
                    x.ObservedAtUtc,
                    x.OldestPendingAtUtc,
                    stale = OperationsFreshness.IsStale(x.ObservedAtUtc, now, staleAfter),
                    providers = x.Providers.Select(p => new
                    {
                        p.Provider,
                        state = OperationsFreshness.Apply(p.State, p.ObservedAtUtc, now, staleAfter).ToString(),
                        publishedState = p.State.ToString(),
                        p.ObservedAtUtc,
                        p.Reason
                    })
                }));
        });

        app.MapPost("/api/operations/messaging/observe", async (
            HttpContext context,
            MessagingOperationalRecord record,
            OperationsV1Store store,
            OperationsTelemetryAuthenticator telemetryAuthenticator,
            CancellationToken ct) =>
        {
            if (!await telemetryAuthenticator.IsAuthorizedAsync(context, record.ApplicationId, ct)) return Results.Forbid();
            var safe = record with
            {
                ObservedAtUtc = record.ObservedAtUtc == default ? DateTimeOffset.UtcNow : record.ObservedAtUtc,
                Providers = record.Providers ?? []
            };
            await store.MutateAsync<MessagingOperationalRecord>("messaging", all =>
            {
                int index = all.FindIndex(x =>
                    x.ApplicationId.Equals(safe.ApplicationId, StringComparison.OrdinalIgnoreCase) &&
                    x.InstanceId.Equals(safe.InstanceId, StringComparison.OrdinalIgnoreCase));
                if (index >= 0) all[index] = safe; else all.Add(safe);
                return all;
            }, ct);
            return Results.Accepted();
        });

        app.MapGet("/api/operations/messages/{messageId}", async (
            string messageId,
            OperationsV1Store store,
            CancellationToken ct) =>
        {
            var traces = await store.LoadAsync<MessageTraceRecord>("message-traces", ct);
            var trace = traces.FirstOrDefault(x => x.MessageId.Equals(messageId, StringComparison.OrdinalIgnoreCase));
            return trace is null ? Results.NotFound() : Results.Ok(trace);
        });

        app.MapPost("/api/operations/messages/trace", async (
            HttpContext context,
            MessageTraceRecord record,
            OperationsV1Store store,
            OperationsTelemetryAuthenticator telemetryAuthenticator,
            CancellationToken ct) =>
        {
            if (!await telemetryAuthenticator.IsAuthorizedAsync(context, record.ApplicationId, ct)) return Results.Forbid();
            var safe = record with
            {
                ObservedAtUtc = record.ObservedAtUtc == default ? DateTimeOffset.UtcNow : record.ObservedAtUtc,
                Stages = record.Stages ?? []
            };
            await store.MutateAsync<MessageTraceRecord>("message-traces", all =>
            {
                int index = all.FindIndex(x => x.MessageId.Equals(record.MessageId, StringComparison.OrdinalIgnoreCase));
                if (index >= 0) all[index] = safe; else all.Add(safe);
                return [.. all.OrderByDescending(x => x.ObservedAtUtc).Take(2000)];
            }, ct);
            return Results.Accepted();
        });

        app.MapGet("/api/operations/synchronization", async (
            string? applicationId,
            OperationsV1Store store,
            CancellationToken ct) =>
        {
            IEnumerable<SynchronizationEndpointRecord> q =
                await store.LoadAsync<SynchronizationEndpointRecord>("synchronization", ct);
            if (!string.IsNullOrWhiteSpace(applicationId))
                q = q.Where(x => x.ApplicationId.Equals(applicationId, StringComparison.OrdinalIgnoreCase));
            DateTimeOffset now = DateTimeOffset.UtcNow;
            TimeSpan staleAfter = TimeSpan.FromSeconds(app.Configuration.GetValue("Operations:StaleAfterSeconds", 300));
            return Results.Ok(q.OrderBy(x => x.ApplicationId).ThenBy(x => x.EndpointId).Select(x => new
            {
                x.EndpointId,
                x.ApplicationId,
                x.InstanceId,
                x.Online,
                queueState = OperationsFreshness.Apply(x.QueueState, x.ObservedAtUtc, now, staleAfter).ToString(),
                publishedQueueState = x.QueueState.ToString(),
                x.Pending,
                x.Failed,
                x.ObservedAtUtc,
                x.OldestPendingAtUtc,
                stale = OperationsFreshness.IsStale(x.ObservedAtUtc, now, staleAfter),
                x.Reason
            }));
        });

        app.MapPost("/api/operations/synchronization/observe", async (
            HttpContext context,
            SynchronizationEndpointRecord record,
            OperationsV1Store store,
            OperationsTelemetryAuthenticator telemetryAuthenticator,
            CancellationToken ct) =>
        {
            if (!await telemetryAuthenticator.IsAuthorizedAsync(context, record.ApplicationId, ct)) return Results.Forbid();
            var safe = record with
            {
                ObservedAtUtc = record.ObservedAtUtc == default ? DateTimeOffset.UtcNow : record.ObservedAtUtc
            };
            await store.MutateAsync<SynchronizationEndpointRecord>("synchronization", all =>
            {
                int index = all.FindIndex(x =>
                    x.ApplicationId.Equals(safe.ApplicationId, StringComparison.OrdinalIgnoreCase) &&
                    x.EndpointId.Equals(safe.EndpointId, StringComparison.OrdinalIgnoreCase));
                if (index >= 0) all[index] = safe; else all.Add(safe);
                return all;
            }, ct);
            return Results.Accepted();
        });

        app.MapGet("/api/operations/runbooks", async (
            string? applicationId,
            OperationsV1Store store,
            CancellationToken ct) =>
        {
            IEnumerable<RunbookRecord> q = await store.LoadAsync<RunbookRecord>("runbooks", ct);
            if (!string.IsNullOrWhiteSpace(applicationId))
                q = q.Where(x => x.ApplicationId.Equals(applicationId, StringComparison.OrdinalIgnoreCase));
            return Results.Ok(q.OrderBy(x => x.ApplicationId).ThenBy(x => x.Title));
        });

        app.MapPost("/api/operations/runbooks", async (
            HttpContext context,
            RunbookRecord record,
            OperationsV1Store store,
            CancellationToken ct) =>
        {
            if (!Has(context, "Operations.Runbooks.Manage")) return Results.Forbid();
            string id = string.IsNullOrWhiteSpace(record.RunbookId)
                ? "RB-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()
                : record.RunbookId;
            var safe = record with
            {
                RunbookId = id,
                Sections = record.Sections ?? [],
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };
            await store.MutateAsync<RunbookRecord>("runbooks", all =>
            {
                int index = all.FindIndex(x => x.RunbookId.Equals(id, StringComparison.OrdinalIgnoreCase));
                if (index >= 0) all[index] = safe; else all.Add(safe);
                return all;
            }, ct);
            await AuditAsync(store, context, "RunbookChanged", record.ApplicationId, id, null, ct);
            return Results.Ok(safe);
        });

        app.MapGet("/api/operations/audit", async (
            HttpContext context,
            OperationsV1Store store,
            CancellationToken ct) =>
        {
            if (!Has(context, "Operations.Audit.View")) return Results.Forbid();
            return Results.Ok((await store.LoadAsync<OperationsAuditRecord>("audit", ct))
                .OrderByDescending(x => x.AtUtc).Take(1000));
        });

        app.MapGet("/api/operations/self", async (
            OperationsV1Store store,
            CancellationToken ct) =>
        {
            try
            {
                _ = await store.LoadAsync<ApplicationOperationalRecord>("applications", ct);
                (long? memoryTotalBytes, long? memoryFreeBytes) = ReadMemoryCapacity();
                (long? diskTotalBytes, long? diskFreeBytes) = ReadDiskCapacity();

                return Results.Ok(new
                {
                    state = OperationalState.Healthy.ToString(),
                    observedAtUtc = DateTimeOffset.UtcNow,
                    storage = "Healthy",
                    memoryTotalBytes,
                    memoryFreeBytes,
                    diskTotalBytes,
                    diskFreeBytes,
                    advisor = "Not Supported",
                    audioTranscription = "Not Supported"
                });
            }
            catch (Exception ex)
            {
                return Results.Ok(new
                {
                    state = OperationalState.Failed.ToString(),
                    observedAtUtc = DateTimeOffset.UtcNow,
                    storage = "Failed",
                    reason = ex.Message
                });
            }
        });
    }

    private static (long? TotalBytes, long? FreeBytes) ReadMemoryCapacity()
    {
        try
        {
            if (OperatingSystem.IsLinux() && File.Exists("/proc/meminfo"))
            {
                long? totalKb = null;
                long? availableKb = null;
                foreach (string line in File.ReadLines("/proc/meminfo"))
                {
                    if (line.StartsWith("MemTotal:", StringComparison.Ordinal))
                        totalKb = ParseMemInfoKb(line);
                    else if (line.StartsWith("MemAvailable:", StringComparison.Ordinal))
                        availableKb = ParseMemInfoKb(line);

                    if (totalKb.HasValue && availableKb.HasValue)
                        break;
                }

                return (
                    totalKb.HasValue ? totalKb.Value * 1024L : null,
                    availableKb.HasValue ? availableKb.Value * 1024L : null);
            }

            GCMemoryInfo memoryInfo = GC.GetGCMemoryInfo();
            long total = memoryInfo.TotalAvailableMemoryBytes;
            return (total > 0 ? total : null, null);
        }
        catch
        {
            return (null, null);
        }
    }

    private static long? ParseMemInfoKb(string line)
    {
        string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && long.TryParse(parts[1], out long kb) ? kb : null;
    }

    private static (long? TotalBytes, long? FreeBytes) ReadDiskCapacity()
    {
        try
        {
            string root = Path.GetPathRoot(AppContext.BaseDirectory) ?? "/";
            DriveInfo drive = new(root);
            return (drive.TotalSize, drive.AvailableFreeSpace);
        }
        catch
        {
            return (null, null);
        }
    }

    internal static async Task AuditAsync(
        OperationsV1Store store,
        HttpContext context,
        string action,
        string? applicationId,
        string? targetId,
        string? correlationId,
        CancellationToken ct,
        string? detail = null)
    {
        var audit = new OperationsAuditRecord(
            "AUD-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            DateTimeOffset.UtcNow,
            context.User?.Identity?.Name ?? "unknown",
            action,
            applicationId,
            targetId,
            correlationId,
            detail);
        await store.MutateAsync<OperationsAuditRecord>("audit", all =>
        {
            all.Add(audit);
            return [.. all.OrderByDescending(x => x.AtUtc).Take(10000)];
        }, ct);
    }

    private static bool Has(HttpContext context, string permission) =>
        context.Items.TryGetValue("SuiteIdentity", out object? value) &&
        value is SuiteIdentity identity &&
        identity.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
}
