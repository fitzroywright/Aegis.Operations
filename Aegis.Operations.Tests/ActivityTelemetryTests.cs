using Xunit;
using Common.Diagnostics;

public sealed class ActivityTelemetryTests
{
    [Fact]
    public void Lifecycle_event_rejects_sensitive_property_names()
    {
        Assert.Throws<ArgumentException>(() => LifecycleEvent.Create(
            "Aegis.Hello",
            "hello-01",
            "Registration",
            "ResolveSecret",
            LifecycleEventOutcome.Started,
            "corr-1",
            properties: new Dictionary<string, string> { ["access_token"] = "do-not-log" }));
    }

    [Fact]
    public void Lifecycle_event_keeps_flow_identity_for_analysis()
    {
        DateTimeOffset at = new(2026, 9, 20, 6, 0, 0, TimeSpan.Zero);
        LifecycleEvent value = LifecycleEvent.Create(
            "Aegis.Cafeteria",
            "services-01",
            "EmployeeRefresh",
            "EnsureCurrentPeriodAllocation",
            LifecycleEventOutcome.Succeeded,
            "employee-refresh-42",
            operationId: "refresh-42",
            relatedBusinessId: "employee-42",
            code: "AllocationEnsured",
            detail: "Current-period allocation is present.",
            occurredAtUtc: at);

        Assert.Equal("EmployeeRefresh", value.Flow);
        Assert.Equal("EnsureCurrentPeriodAllocation", value.Stage);
        Assert.Equal("employee-refresh-42", value.CorrelationId);
        Assert.Equal("employee-42", value.RelatedBusinessId);
        Assert.Equal(at, value.OccurredAtUtc);
    }
}
