using Common.Diagnostics;
using Microsoft.Extensions.Configuration;
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

    [Fact]
    public async Task LifecycleEvent_ProjectsIntoExistingWorkflowVisualizerStore()
    {
        string root = Path.Combine(Path.GetTempPath(), "aegis-ops-lifecycle-" + Guid.NewGuid().ToString("N"));
        try
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Operations:DataDirectory"] = root
                })
                .Build();
            var operationalStore = new OperationsV1Store(configuration);
            DateTimeOffset started = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

            await OperationsLifecycleStore.ProjectIntoWorkflowVisualizerAsync(
                LifecycleEvent.Create(
                    "Aegis.Hello", "hello-01", "Registration", "Request",
                    LifecycleEventOutcome.Started, "corr-visual", occurredAtUtc: started),
                operationalStore,
                CancellationToken.None);

            await OperationsLifecycleStore.ProjectIntoWorkflowVisualizerAsync(
                LifecycleEvent.Create(
                    "Aegis.Hello", "hello-01", "Registration", "Request",
                    LifecycleEventOutcome.Succeeded, "corr-visual", occurredAtUtc: started.AddSeconds(1)),
                operationalStore,
                CancellationToken.None);

            FlowRecord flow = Assert.Single(await operationalStore.LoadAsync<FlowRecord>("flows", CancellationToken.None));
            FlowStageRecord stage = Assert.Single(flow.Stages);
            Assert.Equal("Registration", flow.FlowType);
            Assert.Equal("Request", flow.CurrentStage);
            Assert.Equal(FlowStageState.Completed, stage.State);
            Assert.Equal(started, stage.StartedAtUtc);
            Assert.Equal(started.AddSeconds(1), stage.CompletedAtUtc);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
