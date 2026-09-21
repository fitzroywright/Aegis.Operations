using System.Text.Json;

internal static class OperationsFreshness
{
    internal static bool IsStale(DateTimeOffset observedAtUtc, DateTimeOffset now, TimeSpan threshold)
        => threshold >= TimeSpan.Zero && now - observedAtUtc > threshold;

    internal static bool IsAging(DateTimeOffset observedAtUtc, DateTimeOffset now, TimeSpan offlineThreshold)
    {
        if (offlineThreshold <= TimeSpan.Zero)
            return false;

        TimeSpan age = now - observedAtUtc;
        TimeSpan agingThreshold = TimeSpan.FromTicks(offlineThreshold.Ticks / 2);
        return age > agingThreshold && age <= offlineThreshold;
    }

    internal static OperationalState Apply(
        OperationalState publishedState,
        DateTimeOffset observedAtUtc,
        DateTimeOffset now,
        TimeSpan threshold)
        => IsStale(observedAtUtc, now, threshold)
            ? OperationalState.Unknown
            : publishedState;
}

internal interface IAudioTranscriptionProvider
{
    Task<AudioTranscriptionResult> TranscribeAsync(
        string audioReference,
        CancellationToken cancellationToken = default);
}

internal sealed record AudioTranscriptionResult(
    bool Succeeded,
    string? Text,
    string? Provider,
    string? Error);

internal sealed record FlowDefinitionRecord(
    string ApplicationId,
    string FlowType,
    string DisplayName,
    IReadOnlyList<FlowDefinitionStage> Stages,
    DateTimeOffset UpdatedAtUtc);

internal sealed record FlowDefinitionStage(
    string Name,
    string? ParentStage = null,
    string? Branch = null,
    int? WarningAfterSeconds = null,
    int? CriticalAfterSeconds = null);


internal static class RegistrationFlowStateFormatter
{
    internal static string Format(
        string? registrationState,
        OperationalState runtimeState,
        bool telemetryAvailable,
        bool aging,
        bool offline,
        bool authorityAvailable)
    {
        if (!authorityAvailable)
            return "Unknown Authority";

        string state = (registrationState ?? string.Empty).Trim();

        if (state.Equals("IdentityConflict", StringComparison.OrdinalIgnoreCase) ||
            state.Equals("Identity Conflict", StringComparison.OrdinalIgnoreCase))
            return "Identity Conflict";

        if (state.Equals("Pending", StringComparison.OrdinalIgnoreCase))
            return "Pending Approval";

        if (state.Equals("RecoveryPending", StringComparison.OrdinalIgnoreCase) ||
            state.Equals("Recovery Pending", StringComparison.OrdinalIgnoreCase))
            return "Recovery Pending";

        if (state.Equals("Revoked", StringComparison.OrdinalIgnoreCase))
            return "Revoked";

        if (state.Equals("Purged", StringComparison.OrdinalIgnoreCase))
            return "Purged";

        if (!state.Equals("Registered", StringComparison.OrdinalIgnoreCase))
            return string.IsNullOrWhiteSpace(state) ? "Unknown Authority" : state;

        if (offline || runtimeState == OperationalState.Offline || !telemetryAvailable)
            return "Registered (Offline)";

        if (runtimeState is OperationalState.Degraded
            or OperationalState.Warning
            or OperationalState.Failed
            or OperationalState.Maintenance
            or OperationalState.NotSupported
            or OperationalState.Unknown)
            return "Registered (Degraded)";

        if (aging)
            return "Registered (Stale)";

        return "Registered (Running)";
    }

    internal static string Tone(string label)
        => label switch
        {
            "Registered (Running)" => "good",
            "Registered (Stale)" or "Registered (Degraded)" or "Recovery Pending" or "Pending Approval" => "warn",
            "Registered (Offline)" => "offline",
            "Revoked" or "Identity Conflict" => "bad",
            "Unknown Authority" => "warn",
            _ => "neutral"
        };
}
