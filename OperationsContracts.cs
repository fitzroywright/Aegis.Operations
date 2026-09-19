using System.Text.Json;

internal static class OperationsFreshness
{
    internal static bool IsStale(DateTimeOffset observedAtUtc, DateTimeOffset now, TimeSpan threshold)
        => threshold >= TimeSpan.Zero && now - observedAtUtc > threshold;

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
