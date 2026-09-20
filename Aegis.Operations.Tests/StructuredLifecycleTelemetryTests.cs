using Common.Diagnostics;
using Xunit;

public sealed class StructuredLifecycleTelemetryTests
{
    [Fact]
    public void Journeys_GroupsByCorrelationAndOrdersStages()
    {
        var store = new OperationsLifecycleStore();
        DateTimeOffset t0 = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

        store.Append(LifecycleEvent.Create(
            "Aegis.Hello", "hello-01", "Registration", "Request",
            LifecycleEventOutcome.Started, "corr-1", occurredAtUtc: t0));
        store.Append(LifecycleEvent.Create(
            "Aegis.Hello", "hello-01", "Registration", "Registered",
            LifecycleEventOutcome.Succeeded, "corr-1", relatedBusinessId: "reg-1", occurredAtUtc: t0.AddSeconds(2)));

        LifecycleJourneySnapshot journey = Assert.Single(store.Journeys());

        Assert.Equal("Aegis.Hello", journey.ApplicationId);
        Assert.Equal("Registration", journey.Flow);
        Assert.Equal("Registered", journey.CurrentStage);
        Assert.Equal("Healthy", journey.Health);
        Assert.Equal(2, journey.Stages.Count);
        Assert.Equal("Request", journey.Stages[0].Stage);
        Assert.Equal("Registered", journey.Stages[1].Stage);
    }

    [Fact]
    public void Journeys_UsesLatestFailureAsHealth()
    {
        var store = new OperationsLifecycleStore();
        store.Append(LifecycleEvent.Create(
            "app", "instance", "flow", "Start", LifecycleEventOutcome.Started, "corr"));
        store.Append(LifecycleEvent.Create(
            "app", "instance", "flow", "Database", LifecycleEventOutcome.Failed, "corr", code: "DatabaseUnavailable"));

        LifecycleJourneySnapshot journey = Assert.Single(store.Journeys());
        Assert.Equal("Failed", journey.Health);
        Assert.Equal(LifecycleEventOutcome.Failed, journey.Outcome);
    }
}
