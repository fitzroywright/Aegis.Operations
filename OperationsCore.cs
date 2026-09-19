using System.Text.Json;

internal enum OperationalState
{
    Healthy,
    Degraded,
    Warning,
    Failed,
    Offline,
    Unknown,
    Maintenance,
    NotSupported
}

internal sealed record OperationalObservation(
    OperationalState State,
    string Status,
    DateTimeOffset ObservedAt,
    string? Reason = null,
    string? Source = null,
    string? Url = null)
{
    // Backward-compatible hint for existing consumers. New UI/code must use State.
    public bool Available => State is OperationalState.Healthy
        or OperationalState.Degraded
        or OperationalState.Warning
        or OperationalState.Maintenance;

    public string StateName => State.ToString();
}

internal static class OperationalStateSemantics
{
    internal static OperationalState FromHealthStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return OperationalState.Unknown;

        string normalized = status.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();

        return normalized switch
        {
            "healthy" or "ok" or "ready" or "available" => OperationalState.Healthy,
            "degraded" => OperationalState.Degraded,
            "warning" or "warn" => OperationalState.Warning,
            "failed" or "failure" or "unhealthy" or "critical" => OperationalState.Failed,
            "offline" or "unreachable" => OperationalState.Offline,
            "maintenance" => OperationalState.Maintenance,
            "notsupported" or "unsupported" => OperationalState.NotSupported,
            "unknown" => OperationalState.Unknown,
            _ => OperationalState.Unknown
        };
    }

    internal static OperationalObservation Unknown(
        string reason,
        string? source = null,
        string? url = null) =>
        new(
            OperationalState.Unknown,
            "Unknown",
            DateTimeOffset.UtcNow,
            reason,
            source,
            url);

    internal static OperationalObservation FromHealthDocument(
        JsonElement root,
        string source,
        string url)
    {
        if (!root.TryGetProperty("status", out JsonElement value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return Unknown(
                "Health endpoint responded but did not publish a recognized status.",
                source,
                url);
        }

        string? status = value.GetString();
        OperationalState state = FromHealthStatus(status);

        return new(
            state,
            state == OperationalState.Unknown ? "Unknown" : (status?.Trim() ?? "Unknown"),
            DateTimeOffset.UtcNow,
            state == OperationalState.Unknown
                ? $"Health endpoint returned unrecognized status '{status}'."
                : null,
            source,
            url);
    }
}
