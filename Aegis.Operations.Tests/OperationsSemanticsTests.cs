using System.Text.Json;
using Xunit;

public sealed class OperationsSemanticsTests
{
    [Theory]
    [InlineData("Healthy", OperationalState.Healthy)]
    [InlineData("Degraded", OperationalState.Degraded)]
    [InlineData("Warning", OperationalState.Warning)]
    [InlineData("Failed", OperationalState.Failed)]
    [InlineData("Offline", OperationalState.Offline)]
    [InlineData("Unknown", OperationalState.Unknown)]
    [InlineData("Maintenance", OperationalState.Maintenance)]
    [InlineData("Not Supported", OperationalState.NotSupported)]
    [InlineData("something-new", OperationalState.Unknown)]
    public void Health_statuses_do_not_manufacture_healthy(string input, OperationalState expected)
        => Assert.Equal(expected, OperationalStateSemantics.FromHealthStatus(input));

    [Fact]
    public void Missing_health_status_is_unknown()
        => Assert.Equal(OperationalState.Unknown, OperationalStateSemantics.FromHealthStatus(null));

    [Fact]
    public void Active_stage_crossing_warning_threshold_becomes_warning()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var stage = new FlowStageRecord(
            "Services",
            FlowStageState.Active,
            StartedAtUtc: now.AddSeconds(-10),
            WarningAfterSeconds: 5,
            CriticalAfterSeconds: 30);

        Assert.Equal(FlowStageState.Warning, FlowStateEvaluator.EffectiveState(stage, now));
    }

    [Fact]
    public void Active_stage_crossing_critical_threshold_times_out()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var stage = new FlowStageRecord(
            "Services",
            FlowStageState.Active,
            StartedAtUtc: now.AddSeconds(-45),
            WarningAfterSeconds: 5,
            CriticalAfterSeconds: 30);

        Assert.Equal(FlowStageState.TimedOut, FlowStateEvaluator.EffectiveState(stage, now));
    }

    [Fact]
    public void Completed_stage_is_not_reinterpreted_by_elapsed_time()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var stage = new FlowStageRecord(
            "Database",
            FlowStageState.Completed,
            StartedAtUtc: now.AddMinutes(-10),
            CompletedAtUtc: now.AddMinutes(-9),
            WarningAfterSeconds: 5,
            CriticalAfterSeconds: 30);

        Assert.Equal(FlowStageState.Completed, FlowStateEvaluator.EffectiveState(stage, now));
    }

    [Fact]
    public void Diagnostic_snapshot_redacts_secret_like_fields_recursively()
    {
        using JsonDocument source = JsonDocument.Parse("""
        {
          "database": {
            "connectionString": "Server=db;Password=secret",
            "status": "Healthy"
          },
          "token": "abc",
          "nested": [{"apiKey":"xyz","safe":"value"}]
        }
        """);

        JsonElement sanitized = OperationsSanitizer.Sanitize(source.RootElement)!.Value;

        Assert.Equal("[REDACTED]", sanitized.GetProperty("database").GetProperty("connectionString").GetString());
        Assert.Equal("Healthy", sanitized.GetProperty("database").GetProperty("status").GetString());
        Assert.Equal("[REDACTED]", sanitized.GetProperty("token").GetString());
        Assert.Equal("[REDACTED]", sanitized.GetProperty("nested")[0].GetProperty("apiKey").GetString());
        Assert.Equal("value", sanitized.GetProperty("nested")[0].GetProperty("safe").GetString());
    }
}
