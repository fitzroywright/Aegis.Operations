using System.Collections.Concurrent;
using System.Text.Json;
using Common.Security;

internal enum FlowStageState
{
    Pending,
    Active,
    Completed,
    Warning,
    Failed,
    Skipped,
    TimedOut,
    Unknown
}

internal enum AlertSeverity
{
    Informational,
    Warning,
    High,
    Critical
}

internal enum AlertLifecycleState
{
    Raised,
    Acknowledged,
    Resolved
}

internal sealed record ApplicationOperationalRecord(
    string ApplicationId,
    string DisplayName,
    string InstanceId,
    string Environment,
    string Version,
    string RegistrationStatus,
    OperationalState State,
    DateTimeOffset ObservedAtUtc,
    string? ConfigurationStatus = null,
    string[]? Dependencies = null,
    string? Build = null,
    string? Runbook = null,
    string? Reason = null);

internal sealed record FlowStageRecord(
    string Name,
    FlowStageState State,
    DateTimeOffset? StartedAtUtc = null,
    DateTimeOffset? CompletedAtUtc = null,
    int? WarningAfterSeconds = null,
    int? CriticalAfterSeconds = null,
    string? Error = null,
    string? ParentStage = null,
    string? Branch = null);

internal sealed record FlowRecord(
    string CorrelationId,
    string ApplicationId,
    string FlowType,
    string InstanceId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset ObservedAtUtc,
    IReadOnlyList<FlowStageRecord> Stages,
    string? CurrentStage = null,
    string? RelatedBusinessId = null,
    string? Runbook = null);

internal sealed record AlertRecord(
    string AlertId,
    string ApplicationId,
    string? InstanceId,
    AlertSeverity Severity,
    string Title,
    string Detail,
    AlertLifecycleState State,
    DateTimeOffset RaisedAtUtc,
    DateTimeOffset? AcknowledgedAtUtc = null,
    string? AcknowledgedBy = null,
    DateTimeOffset? ResolvedAtUtc = null,
    string? ResolvedBy = null,
    string? CorrelationId = null,
    string? Runbook = null);

internal sealed record EngineerLogRecord(
    string EntryId,
    DateTimeOffset AtUtc,
    string Author,
    string Title,
    string Text,
    AlertSeverity Severity,
    string[] Tags,
    string? ApplicationId = null,
    string? Component = null,
    string? AlertId = null,
    string? DeploymentId = null,
    string? CorrelationId = null,
    JsonElement? DiagnosticSnapshot = null,
    string[]? Attachments = null,
    string? AudioReference = null,
    string? Transcription = null,
    string? TranscriptionStatus = null);

internal interface IRegistrationOperationsProvider { }
internal interface IConfigurationOperationsProvider { }
internal interface IDiagnosticsOperationsProvider { }
internal interface IMessagingOperationsProvider { }
internal interface IFlowOperationsProvider { }
internal interface IRunbookProvider { }
internal interface IOperationsEventProvider { }
internal interface IOperationsAdvisor { }

internal sealed class OperationsV1Store
{
    private readonly string _root;
    private readonly int _flowRetention;
    private readonly int _alertRetention;
    private readonly int _engineerLogRetention;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    internal OperationsV1Store(IConfiguration configuration)
    {
        _root = configuration["Operations:DataDirectory"]?.Trim()
            ?? "/var/lib/aegis/operations/v1";
        _flowRetention = Math.Clamp(configuration.GetValue("Operations:Retention:Flows", 1000), 100, 100000);
        _alertRetention = Math.Clamp(configuration.GetValue("Operations:Retention:Alerts", 5000), 100, 100000);
        _engineerLogRetention = Math.Clamp(configuration.GetValue("Operations:Retention:EngineerLog", 5000), 100, 100000);
        Directory.CreateDirectory(_root);
    }

    internal async Task<IReadOnlyList<T>> LoadAsync<T>(string bucket, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try { return await LoadUnsafeAsync<T>(bucket, ct); }
        finally { _gate.Release(); }
    }

    internal async Task SaveAsync<T>(string bucket, IReadOnlyList<T> values, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try { await SaveUnsafeAsync(bucket, values, ct); }
        finally { _gate.Release(); }
    }

    internal async Task MutateAsync<T>(
        string bucket,
        Func<List<T>, List<T>> mutation,
        CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            List<T> all = [.. await LoadUnsafeAsync<T>(bucket, ct)];
            List<T> updated = mutation(all);
            await SaveUnsafeAsync(bucket, updated, ct);
        }
        finally { _gate.Release(); }
    }

    internal async Task UpsertApplicationAsync(ApplicationOperationalRecord incoming, CancellationToken ct)
    {
        await MutateAsync<ApplicationOperationalRecord>("applications", all =>
        {
            int index = all.FindIndex(x =>
                x.ApplicationId.Equals(incoming.ApplicationId, StringComparison.OrdinalIgnoreCase) &&
                x.InstanceId.Equals(incoming.InstanceId, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) all[index] = incoming; else all.Add(incoming);
            return all;
        }, ct);
    }

    internal async Task UpsertFlowAsync(FlowRecord incoming, CancellationToken ct)
    {
        await MutateAsync<FlowRecord>("flows", all =>
        {
            int index = all.FindIndex(x =>
                x.CorrelationId.Equals(incoming.CorrelationId, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) all[index] = incoming; else all.Add(incoming);
            return [.. all.OrderByDescending(x => x.ObservedAtUtc).Take(_flowRetention)];
        }, ct);
    }

    internal async Task UpsertAlertAsync(AlertRecord incoming, CancellationToken ct)
    {
        await MutateAsync<AlertRecord>("alerts", all =>
        {
            int index = all.FindIndex(x =>
                x.AlertId.Equals(incoming.AlertId, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) all[index] = incoming; else all.Add(incoming);
            return [.. all.OrderByDescending(x => x.RaisedAtUtc).Take(_alertRetention)];
        }, ct);
    }

    internal async Task AppendEngineerLogAsync(EngineerLogRecord incoming, CancellationToken ct)
    {
        await MutateAsync<EngineerLogRecord>("engineer-log", all =>
        {
            all.Add(incoming);
            return [.. all.OrderByDescending(x => x.AtUtc).Take(_engineerLogRetention)];
        }, ct);
    }

    private async Task<IReadOnlyList<T>> LoadUnsafeAsync<T>(string bucket, CancellationToken ct)
    {
        string path = Path.Combine(_root, bucket + ".json");
        if (!File.Exists(path)) return [];
        string json = await File.ReadAllTextAsync(path, ct);
        return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? [];
    }

    private async Task SaveUnsafeAsync<T>(string bucket, IReadOnlyList<T> values, CancellationToken ct)
    {
        string path = Path.Combine(_root, bucket + ".json");
        string temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(values, JsonOptions), ct);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(temp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.Move(temp, path, true);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}

internal static class OperationsV1Endpoints
{
    internal static void MapOperationsV1(this WebApplication app, string configurationUrl, string diagnosticsUrl, string site, string environment)
    {
        app.MapGet("/api/operations/applications", async (OperationsV1Store store, CancellationToken ct) =>
        {
            var all = await store.LoadAsync<ApplicationOperationalRecord>("applications", ct);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            int staleSeconds = app.Configuration.GetValue("Operations:StaleAfterSeconds", 90);
            TimeSpan staleAfter = TimeSpan.FromSeconds(staleSeconds);
            return Results.Ok(all.Select(x =>
            {
                bool stale = OperationsFreshness.IsStale(x.ObservedAtUtc, now, staleAfter);
                return new
                {
                    x.ApplicationId,
                    x.DisplayName,
                    x.InstanceId,
                    x.Environment,
                    x.Version,
                    x.RegistrationStatus,
                    state = stale ? OperationalState.Offline.ToString() : x.State.ToString(),
                    observedAtUtc = x.ObservedAtUtc,
                    stale,
                    reason = stale ? "No recent heartbeat has been received." : x.Reason,
                    x.ConfigurationStatus,
                    dependencies = x.Dependencies ?? [],
                    x.Build,
                    x.Runbook
                };
            }));
        });

        app.MapGet("/api/operations/applications/{applicationId}", async (
            string applicationId, OperationsV1Store store, CancellationToken ct) =>
        {
            var all = await store.LoadAsync<ApplicationOperationalRecord>("applications", ct);
            var matches = all.Where(x => x.ApplicationId.Equals(applicationId, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count == 0) return Results.NotFound();

            var diagnostics = (await store.LoadAsync<DiagnosticRecord>("diagnostics", ct))
                .Where(x => x.ApplicationId.Equals(applicationId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.ObservedAtUtc)
                .ToList();
            var flows = (await store.LoadAsync<FlowRecord>("flows", ct))
                .Where(x => x.ApplicationId.Equals(applicationId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.ObservedAtUtc)
                .Take(100)
                .ToList();
            var alerts = (await store.LoadAsync<AlertRecord>("alerts", ct))
                .Where(x => x.ApplicationId.Equals(applicationId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.RaisedAtUtc)
                .ToList();
            var runbooks = (await store.LoadAsync<RunbookRecord>("runbooks", ct))
                .Where(x => x.ApplicationId.Equals(applicationId, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var messaging = (await store.LoadAsync<MessagingOperationalRecord>("messaging", ct))
                .Where(x => x.ApplicationId.Equals(applicationId, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var synchronization = (await store.LoadAsync<SynchronizationEndpointRecord>("synchronization", ct))
                .Where(x => x.ApplicationId.Equals(applicationId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            DateTimeOffset now = DateTimeOffset.UtcNow;
            TimeSpan staleAfter = TimeSpan.FromSeconds(
                app.Configuration.GetValue("Operations:StaleAfterSeconds", 90));

            return Results.Ok(new
            {
                applicationId,
                instances = matches.Select(x => new
                {
                    x.ApplicationId,
                    x.DisplayName,
                    x.InstanceId,
                    x.Environment,
                    x.Version,
                    x.RegistrationStatus,
                    state = OperationsFreshness.IsStale(x.ObservedAtUtc, now, staleAfter) ? OperationalState.Offline.ToString() : OperationsFreshness.Apply(x.State, x.ObservedAtUtc, now, staleAfter).ToString(),
                    publishedState = x.State.ToString(),
                    x.ObservedAtUtc,
                    stale = OperationsFreshness.IsStale(x.ObservedAtUtc, now, staleAfter),
                    reason = OperationsFreshness.IsStale(x.ObservedAtUtc, now, staleAfter)
                        ? "No recent heartbeat has been received."
                        : x.Reason,
                    x.ConfigurationStatus,
                    dependencies = x.Dependencies ?? [],
                    x.Build,
                    x.Runbook
                }),
                diagnostics,
                flows,
                alerts,
                runbooks,
                messaging,
                synchronization
            });
        });

        app.MapPost("/api/operations/applications/observe", async (
            HttpContext context,
            ApplicationOperationalRecord record,
            OperationsV1Store store,
            OperationsTelemetryAuthenticator telemetryAuthenticator,
            CancellationToken ct) =>
        {
            if (!await telemetryAuthenticator.IsAuthorizedAsync(context, record.ApplicationId, ct)) return Results.Json(new { error = "Forbidden", detail = "The authenticated identity is not authorized for this operation." }, statusCode: StatusCodes.Status403Forbidden);
            var safe = record with
            {
                ObservedAtUtc = record.ObservedAtUtc == default ? DateTimeOffset.UtcNow : record.ObservedAtUtc,
                Dependencies = record.Dependencies ?? []
            };
            await store.UpsertApplicationAsync(safe, ct);
            return Results.Accepted();
        });

        app.MapGet("/api/operations/flows", async (
            string? applicationId,
            string? state,
            string? correlationId,
            OperationsV1Store store,
            CancellationToken ct) =>
        {
            IEnumerable<FlowRecord> query = await store.LoadAsync<FlowRecord>("flows", ct);
            if (!string.IsNullOrWhiteSpace(applicationId))
                query = query.Where(x => x.ApplicationId.Equals(applicationId, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(correlationId))
                query = query.Where(x => x.CorrelationId.Equals(correlationId, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(state))
                query = query.Where(x => x.Stages.Any(s => s.State.ToString().Equals(state, StringComparison.OrdinalIgnoreCase)));
            DateTimeOffset now = DateTimeOffset.UtcNow;
            return Results.Ok(query.OrderByDescending(x => x.ObservedAtUtc).Take(250).Select(x => new
            {
                x.CorrelationId,
                x.ApplicationId,
                x.FlowType,
                x.InstanceId,
                x.StartedAtUtc,
                x.ObservedAtUtc,
                x.CurrentStage,
                x.RelatedBusinessId,
                x.Runbook,
                stages = x.Stages.Select(stage => new
                {
                    stage.Name,
                    state = FlowStateEvaluator.EffectiveState(stage, now).ToString(),
                    publishedState = stage.State.ToString(),
                    stage.StartedAtUtc,
                    stage.CompletedAtUtc,
                    stage.WarningAfterSeconds,
                    stage.CriticalAfterSeconds,
                    stage.Error,
                    stage.ParentStage,
                    stage.Branch
                })
            }));
        });

        app.MapPost("/api/operations/flows/observe", async (
            HttpContext context,
            FlowRecord record,
            OperationsV1Store store,
            OperationsTelemetryAuthenticator telemetryAuthenticator,
            CancellationToken ct) =>
        {
            if (!await telemetryAuthenticator.IsAuthorizedAsync(context, record.ApplicationId, ct)) return Results.Json(new { error = "Forbidden", detail = "The authenticated identity is not authorized for this operation." }, statusCode: StatusCodes.Status403Forbidden);
            if (string.IsNullOrWhiteSpace(record.CorrelationId) ||
                string.IsNullOrWhiteSpace(record.ApplicationId) ||
                record.Stages is null ||
                record.Stages.Count == 0)
                return Results.BadRequest(new { error = "correlationId, applicationId and at least one stage are required." });

            var safe = record with
            {
                ObservedAtUtc = record.ObservedAtUtc == default ? DateTimeOffset.UtcNow : record.ObservedAtUtc
            };
            await store.UpsertFlowAsync(safe, ct);
            return Results.Accepted();
        });

        app.MapGet("/api/operations/alerts", async (OperationsV1Store store, CancellationToken ct) =>
            Results.Ok((await store.LoadAsync<AlertRecord>("alerts", ct)).OrderByDescending(x => x.RaisedAtUtc)));

        app.MapPost("/api/operations/alerts", async (
            HttpContext context,
            AlertRecord record,
            OperationsV1Store store,
            OperationsTelemetryAuthenticator telemetryAuthenticator,
            CancellationToken ct) =>
        {
            if (!await telemetryAuthenticator.IsAuthorizedAsync(context, record.ApplicationId, ct)) return Results.Json(new { error = "Forbidden", detail = "The authenticated identity is not authorized for this operation." }, statusCode: StatusCodes.Status403Forbidden);
            string id = string.IsNullOrWhiteSpace(record.AlertId)
                ? "ALT-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()
                : record.AlertId;
            var safe = record with
            {
                AlertId = id,
                RaisedAtUtc = record.RaisedAtUtc == default ? DateTimeOffset.UtcNow : record.RaisedAtUtc,
                State = AlertLifecycleState.Raised
            };
            await store.UpsertAlertAsync(safe, ct);
            return Results.Ok(safe);
        });

        app.MapPost("/api/operations/alerts/{alertId}/acknowledge", async (
            HttpContext context,
            string alertId,
            OperationsV1Store store,
            CancellationToken ct) =>
        {
            if (!Has(context, "Operations.Alerts.Acknowledge")) return Results.Json(new { error = "Forbidden", detail = "The authenticated identity is not authorized for this operation." }, statusCode: StatusCodes.Status403Forbidden);
            List<AlertRecord> all = [.. await store.LoadAsync<AlertRecord>("alerts", ct)];
            int index = all.FindIndex(x => x.AlertId.Equals(alertId, StringComparison.OrdinalIgnoreCase));
            if (index < 0) return Results.NotFound();
            if (all[index].State == AlertLifecycleState.Resolved)
                return Results.Conflict(new { error = "Resolved alerts cannot be acknowledged." });
            if (all[index].State == AlertLifecycleState.Acknowledged)
                return Results.Ok(all[index]);
            all[index] = all[index] with
            {
                State = AlertLifecycleState.Acknowledged,
                AcknowledgedAtUtc = DateTimeOffset.UtcNow,
                AcknowledgedBy = Actor(context)
            };
            await store.SaveAsync("alerts", all, ct);
            await OperationsSpecializedEndpoints.AuditAsync(store, context, "AlertAcknowledged", all[index].ApplicationId, alertId, all[index].CorrelationId, ct);
            return Results.Ok(all[index]);
        });

        app.MapPost("/api/operations/alerts/{alertId}/resolve", async (
            HttpContext context,
            string alertId,
            OperationsV1Store store,
            CancellationToken ct) =>
        {
            if (!Has(context, "Operations.Alerts.Resolve")) return Results.Json(new { error = "Forbidden", detail = "The authenticated identity is not authorized for this operation." }, statusCode: StatusCodes.Status403Forbidden);
            List<AlertRecord> all = [.. await store.LoadAsync<AlertRecord>("alerts", ct)];
            int index = all.FindIndex(x => x.AlertId.Equals(alertId, StringComparison.OrdinalIgnoreCase));
            if (index < 0) return Results.NotFound();
            if (all[index].State == AlertLifecycleState.Resolved)
                return Results.Ok(all[index]);
            all[index] = all[index] with
            {
                State = AlertLifecycleState.Resolved,
                ResolvedAtUtc = DateTimeOffset.UtcNow,
                ResolvedBy = Actor(context)
            };
            await store.SaveAsync("alerts", all, ct);
            await OperationsSpecializedEndpoints.AuditAsync(store, context, "AlertResolved", all[index].ApplicationId, alertId, all[index].CorrelationId, ct);
            return Results.Ok(all[index]);
        });

        app.MapGet("/api/operations/engineer-log", async (OperationsV1Store store, CancellationToken ct) =>
            Results.Ok((await store.LoadAsync<EngineerLogRecord>("engineer-log", ct)).OrderByDescending(x => x.AtUtc)));

        app.MapPost("/api/operations/engineer-log", async (
            HttpContext context,
            EngineerLogRecord record,
            OperationsV1Store store,
            CancellationToken ct) =>
        {
            if (!Has(context, "Operations.EngineerLog.Create")) return Results.Json(new { error = "Forbidden", detail = "The authenticated identity is not authorized for this operation." }, statusCode: StatusCodes.Status403Forbidden);
            string id = string.IsNullOrWhiteSpace(record.EntryId)
                ? "ENG-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()
                : record.EntryId;
            var safe = record with
            {
                EntryId = id,
                AtUtc = record.AtUtc == default ? DateTimeOffset.UtcNow : record.AtUtc,
                Author = Actor(context),
                Tags = record.Tags ?? [],
                DiagnosticSnapshot = OperationsSanitizer.Sanitize(record.DiagnosticSnapshot),
                TranscriptionStatus = string.IsNullOrWhiteSpace(record.AudioReference)
                    ? null
                    : record.TranscriptionStatus ?? "Pending"
            };
            await store.AppendEngineerLogAsync(safe, ct);
            await OperationsSpecializedEndpoints.AuditAsync(store, context, "EngineerLogCreated", safe.ApplicationId, safe.EntryId, safe.CorrelationId, ct);
            return Results.Ok(safe);
        });

        app.MapGet("/api/operations/history", async (
            OperationsV1Store store,
            CancellationToken ct) =>
        {
            var alerts = await store.LoadAsync<AlertRecord>("alerts", ct);
            var logs = await store.LoadAsync<EngineerLogRecord>("engineer-log", ct);
            var flows = await store.LoadAsync<FlowRecord>("flows", ct);
            var alertRaised = alerts.Select(x => new { at = x.RaisedAtUtc, type = "AlertRaised", id = x.AlertId, application = x.ApplicationId, text = x.Title });
            var alertAcknowledged = alerts
                .Where(x => x.AcknowledgedAtUtc.HasValue)
                .Select(x => new { at = x.AcknowledgedAtUtc!.Value, type = "AlertAcknowledged", id = x.AlertId, application = x.ApplicationId, text = x.Title });
            var alertResolved = alerts
                .Where(x => x.ResolvedAtUtc.HasValue)
                .Select(x => new { at = x.ResolvedAtUtc!.Value, type = "AlertResolved", id = x.AlertId, application = x.ApplicationId, text = x.Title });

            var events = alertRaised
                .Concat(alertAcknowledged)
                .Concat(alertResolved)
                .Concat(logs.Select(x => new { at = x.AtUtc, type = "EngineerLog", id = x.EntryId, application = x.ApplicationId ?? "", text = x.Title }))
                .Concat(flows.Select(x => new { at = x.ObservedAtUtc, type = "FlowObserved", id = x.CorrelationId, application = x.ApplicationId, text = x.FlowType }))
                .OrderByDescending(x => x.at)
                .Take(Math.Clamp(app.Configuration.GetValue("Operations:Retention:HistoryView", 500), 100, 5000));
            return Results.Ok(events);
        });

        app.MapGet("/api/operations/search", async (
            string q,
            OperationsV1Store store,
            CancellationToken ct) =>
        {
            q = q?.Trim() ?? "";
            if (q.Length < 2) return Results.BadRequest(new { error = "Search requires at least two characters." });
            var apps = (await store.LoadAsync<ApplicationOperationalRecord>("applications", ct))
                .Where(x => Contains(x.ApplicationId, q) || Contains(x.DisplayName, q) || Contains(x.InstanceId, q))
                .Select(x => new { type = "Application", id = x.ApplicationId, title = x.DisplayName, detail = x.InstanceId });
            var flows = (await store.LoadAsync<FlowRecord>("flows", ct))
                .Where(x => Contains(x.CorrelationId, q) || Contains(x.ApplicationId, q) || Contains(x.FlowType, q) || Contains(x.RelatedBusinessId, q))
                .Select(x => new { type = "Flow", id = x.CorrelationId, title = x.FlowType, detail = x.ApplicationId });
            var alerts = (await store.LoadAsync<AlertRecord>("alerts", ct))
                .Where(x => Contains(x.AlertId, q) || Contains(x.Title, q) || Contains(x.ApplicationId, q))
                .Select(x => new { type = "Alert", id = x.AlertId, title = x.Title, detail = x.ApplicationId });
            var logs = (await store.LoadAsync<EngineerLogRecord>("engineer-log", ct))
                .Where(x => Contains(x.EntryId, q) || Contains(x.Title, q) || Contains(x.Text, q) || Contains(x.CorrelationId, q))
                .Select(x => new { type = "EngineerLog", id = x.EntryId, title = x.Title, detail = x.ApplicationId ?? "" });
            var runbooks = (await store.LoadAsync<RunbookRecord>("runbooks", ct))
                .Where(x => Contains(x.RunbookId, q) || Contains(x.Title, q) || Contains(x.ApplicationId, q) || Contains(x.Content, q))
                .Select(x => new { type = "Runbook", id = x.RunbookId, title = x.Title, detail = x.ApplicationId });
            var messages = (await store.LoadAsync<MessageTraceRecord>("message-traces", ct))
                .Where(x => Contains(x.MessageId, q) || Contains(x.CorrelationId, q) || Contains(x.ApplicationId, q) || Contains(x.Provider, q))
                .Select(x => new { type = "Message", id = x.MessageId, title = x.Provider, detail = x.ApplicationId });
            return Results.Ok(apps.Concat(flows).Concat(alerts).Concat(logs).Concat(runbooks).Concat(messages).Take(100));
        });

        app.MapGet("/api/operations/flow-definitions", async (
            string? applicationId,
            OperationsV1Store store,
            CancellationToken ct) =>
        {
            IEnumerable<FlowDefinitionRecord> q = await store.LoadAsync<FlowDefinitionRecord>("flow-definitions", ct);
            if (!string.IsNullOrWhiteSpace(applicationId))
                q = q.Where(x => x.ApplicationId.Equals(applicationId, StringComparison.OrdinalIgnoreCase));
            return Results.Ok(q.OrderBy(x => x.ApplicationId).ThenBy(x => x.DisplayName));
        });

        app.MapPost("/api/operations/flow-definitions", async (
            HttpContext context,
            FlowDefinitionRecord record,
            OperationsV1Store store,
            OperationsTelemetryAuthenticator telemetryAuthenticator,
            CancellationToken ct) =>
        {
            if (!await telemetryAuthenticator.IsAuthorizedAsync(context, record.ApplicationId, ct)) return Results.Json(new { error = "Forbidden", detail = "The authenticated identity is not authorized for this operation." }, statusCode: StatusCodes.Status403Forbidden);
            if (string.IsNullOrWhiteSpace(record.ApplicationId) ||
                string.IsNullOrWhiteSpace(record.FlowType) ||
                record.Stages is null ||
                record.Stages.Count == 0)
                return Results.BadRequest(new { error = "applicationId, flowType and at least one stage are required." });

            List<FlowDefinitionRecord> all = [.. await store.LoadAsync<FlowDefinitionRecord>("flow-definitions", ct)];
            var safe = record with { UpdatedAtUtc = DateTimeOffset.UtcNow };
            int index = all.FindIndex(x =>
                x.ApplicationId.Equals(safe.ApplicationId, StringComparison.OrdinalIgnoreCase) &&
                x.FlowType.Equals(safe.FlowType, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) all[index] = safe; else all.Add(safe);
            await store.SaveAsync("flow-definitions", all, ct);
            return Results.Ok(safe);
        });

        app.MapGet("/", (HttpContext context) => Results.Content(OperationsPage.Render(configurationUrl, diagnosticsUrl, site, environment, ResolveDisplayUser(context)), "text/html; charset=utf-8"));
        app.MapGet("/operations-v1", (HttpContext context) => Results.Content(OperationsPage.Render(configurationUrl, diagnosticsUrl, site, environment, ResolveDisplayUser(context)), "text/html; charset=utf-8"));
    }

    private static string? ResolveDisplayUser(HttpContext context)
    {
        string? claimName = context.User?.Identity?.Name;
        if (!string.IsNullOrWhiteSpace(claimName))
            return claimName;

        string[] preferredClaimTypes =
        [
            "name",
            "display_name",
            "displayName",
            "preferred_username",
            "upn",
            "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"
        ];

        foreach (string claimType in preferredClaimTypes)
        {
            string? value = context.User?.Claims
                .FirstOrDefault(c => c.Type.Equals(claimType, StringComparison.OrdinalIgnoreCase))
                ?.Value;
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        if (context.Items.TryGetValue("SuiteIdentity", out object? identity) && identity is not null)
        {
            string[] propertyNames =
            [
                "DisplayName",
                "Name",
                "UserName",
                "Username",
                "SubjectId",
                "UserId"
            ];

            Type identityType = identity.GetType();
            foreach (string propertyName in propertyNames)
            {
                object? value = identityType.GetProperty(propertyName)?.GetValue(identity);
                if (value is string text && !string.IsNullOrWhiteSpace(text))
                    return text;
            }
        }

        return null;
    }

    private static bool Contains(string? value, string q) =>
        value?.Contains(q, StringComparison.OrdinalIgnoreCase) == true;

    private static bool Has(HttpContext context, string permission) =>
        context.Items.TryGetValue("SuiteIdentity", out object? value) &&
        value is SuiteIdentity identity &&
        identity.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    private static string Actor(HttpContext context) =>
        context.User?.Identity?.Name ?? "unknown";
}
