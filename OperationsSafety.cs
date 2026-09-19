using System.Text.Json;
using System.Text.Json.Nodes;

internal static class OperationsSanitizer
{
    private static readonly string[] SensitiveFragments =
    [
        "password",
        "passwd",
        "secret",
        "token",
        "apikey",
        "api_key",
        "authorization",
        "credential",
        "connectionstring",
        "registrationkey",
        "signingkey",
        "privatekey"
    ];

    internal static JsonElement? Sanitize(JsonElement? input)
    {
        if (input is null) return null;
        JsonNode? node = JsonNode.Parse(input.Value.GetRawText());
        if (node is null) return null;
        SanitizeNode(node);
        using JsonDocument document = JsonDocument.Parse(node.ToJsonString());
        return document.RootElement.Clone();
    }

    private static void SanitizeNode(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (string key in obj.Select(x => x.Key).ToList())
            {
                if (IsSensitive(key))
                {
                    obj[key] = "[REDACTED]";
                    continue;
                }

                JsonNode? child = obj[key];
                if (child is not null) SanitizeNode(child);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (JsonNode? child in array)
                if (child is not null) SanitizeNode(child);
        }
    }

    private static bool IsSensitive(string key)
    {
        string normalized = key.Replace("-", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal)
            .Replace(":", "", StringComparison.Ordinal)
            .ToLowerInvariant();

        return SensitiveFragments.Any(fragment =>
            normalized.Contains(fragment.Replace("_", "", StringComparison.Ordinal), StringComparison.Ordinal));
    }
}

internal static class FlowStateEvaluator
{
    internal static FlowStageState EffectiveState(FlowStageRecord stage, DateTimeOffset now)
    {
        if (stage.State != FlowStageState.Active || stage.StartedAtUtc is null)
            return stage.State;

        TimeSpan elapsed = now - stage.StartedAtUtc.Value;

        if (stage.CriticalAfterSeconds is int critical &&
            critical >= 0 &&
            elapsed > TimeSpan.FromSeconds(critical))
            return FlowStageState.TimedOut;

        if (stage.WarningAfterSeconds is int warning &&
            warning >= 0 &&
            elapsed > TimeSpan.FromSeconds(warning))
            return FlowStageState.Warning;

        return FlowStageState.Active;
    }
}
