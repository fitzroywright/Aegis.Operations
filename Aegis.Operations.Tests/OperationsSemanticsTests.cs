using System.Text.Json;
using Xunit;

public sealed class OperationsSemanticsTests
{
    [Theory]
    [InlineData("Healthy", "Healthy")]
    [InlineData("Degraded", "Degraded")]
    [InlineData("Warning", "Warning")]
    [InlineData("Failed", "Failed")]
    [InlineData("Offline", "Offline")]
    [InlineData("Unknown", "Unknown")]
    [InlineData("Maintenance", "Maintenance")]
    [InlineData("Not Supported", "NotSupported")]
    [InlineData("something-new", "Unknown")]
    public void Health_statuses_do_not_manufacture_healthy(string input, string expected)
        => Assert.Equal(expected, OperationalStateSemantics.FromHealthStatus(input).ToString());

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
    public void Stale_healthy_observation_becomes_unknown()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Assert.Equal(
            OperationalState.Unknown,
            OperationsFreshness.Apply(
                OperationalState.Healthy,
                now.AddMinutes(-10),
                now,
                TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void Fresh_offline_state_remains_offline()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Assert.Equal(
            OperationalState.Offline,
            OperationsFreshness.Apply(
                OperationalState.Offline,
                now.AddSeconds(-20),
                now,
                TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void Branch_states_are_independent()
    {
        var email = new FlowStageRecord("Email", FlowStageState.Completed, Branch: "Email");
        var whatsapp = new FlowStageRecord("WhatsApp", FlowStageState.Failed, Branch: "WhatsApp");

        Assert.Equal(FlowStageState.Completed, email.State);
        Assert.Equal(FlowStageState.Failed, whatsapp.State);
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
    [Theory]
    [InlineData("Healthy", true, false, false, "Registered (Running)")]
    [InlineData("Healthy", true, true, false, "Registered (Stale)")]
    [InlineData("Healthy", true, false, true, "Registered (Offline)")]
    [InlineData("Degraded", true, false, false, "Registered (Degraded)")]
    public void Registered_flow_label_combines_authoritative_and_runtime_state(
        string runtimeStateName,
        bool telemetryAvailable,
        bool aging,
        bool offline,
        string expected)
    {
        OperationalState runtimeState = Enum.Parse<OperationalState>(runtimeStateName);

        Assert.Equal(
            expected,
            RegistrationFlowStateFormatter.Format(
                "Registered",
                runtimeState,
                telemetryAvailable,
                aging,
                offline,
                authorityAvailable: true));

        const string authoritativeRegistrationState = "Registered";
        _ = RegistrationFlowStateFormatter.Format(
            authoritativeRegistrationState,
            runtimeState,
            telemetryAvailable,
            aging,
            offline,
            authorityAvailable: true);
        Assert.Equal("Registered", authoritativeRegistrationState);
    }

    [Theory]
    [InlineData("Pending", "Pending Approval")]
    [InlineData("RecoveryPending", "Recovery Pending")]
    [InlineData("Revoked", "Revoked")]
    [InlineData("Purged", "Purged")]
    [InlineData("IdentityConflict", "Identity Conflict")]
    public void Non_registered_lifecycle_labels_are_preserved(string state, string expected)
        => Assert.Equal(
            expected,
            RegistrationFlowStateFormatter.Format(
                state,
                OperationalState.Unknown,
                telemetryAvailable: false,
                aging: false,
                offline: false,
                authorityAvailable: true));

    [Fact]
    public void Unavailable_registration_authority_is_unknown_authority()
        => Assert.Equal(
            "Unknown Authority",
            RegistrationFlowStateFormatter.Format(
                "Registered",
                OperationalState.Healthy,
                telemetryAvailable: true,
                aging: false,
                offline: false,
                authorityAvailable: false));

    [Fact]
    public void Missing_telemetry_is_offline_not_stopped()
        => Assert.Equal(
            "Registered (Offline)",
            RegistrationFlowStateFormatter.Format(
                "Registered",
                OperationalState.Unknown,
                telemetryAvailable: false,
                aging: false,
                offline: false,
                authorityAvailable: true));

    [Fact]
    public void Aging_classification_does_not_change_existing_offline_threshold()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        TimeSpan threshold = TimeSpan.FromMinutes(5);

        Assert.False(OperationsFreshness.IsAging(now.AddMinutes(-2), now, threshold));
        Assert.True(OperationsFreshness.IsAging(now.AddMinutes(-3), now, threshold));
        Assert.False(OperationsFreshness.IsStale(now.AddMinutes(-3), now, threshold));
        Assert.True(OperationsFreshness.IsStale(now.AddMinutes(-6), now, threshold));
    }

}
