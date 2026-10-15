using Microsoft.Extensions.Configuration;
using Xunit;

public sealed class CafeteriaControlPlaneCleanupTests
{
    [Theory]
    [InlineData("Aegis.Cafeteria", true)]
    [InlineData("Aegis.Cafeteria.POS", true)]
    [InlineData("Aegis.Cafeteria.POS.UI", true)]
    [InlineData("Aegis.Cafeteria.Services", true)]
    [InlineData("Cafeteria.Services", true)]
    [InlineData("aegis.cafeteria.services", true)]
    [InlineData("Aegis.CafeteriaBackup", false)]
    [InlineData("Aegis.Hello", false)]
    public void Cleanup_is_scoped_to_cafeteria(string applicationId, bool expected)
        => Assert.Equal(expected, CafeteriaControlPlaneCleanup.Matches(applicationId));

    [Fact]
    public async Task Cleanup_removes_only_cafeteria_current_entries_and_retains_history()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var store = new OperationsV1Store(new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["Operations:DataDirectory"] = root }).Build());
            DateTimeOffset now = DateTimeOffset.UtcNow;
            await store.UpsertApplicationAsync(new("Aegis.Cafeteria.Services", "Cafeteria", "kratos",
                "Test", "1", "Registered", OperationalState.Failed, now), default);
            await store.UpsertApplicationAsync(new("Aegis.CafeteriaBackup", "Unrelated", "kratos",
                "Test", "1", "Registered", OperationalState.Healthy, now), default);
            await store.UpsertFlowAsync(new("flow-1", "Aegis.Cafeteria.Services", "Registration", "kratos",
                now, now, [new("Start", FlowStageState.Active)]), default);
            await store.UpsertAlertAsync(new("alert-1", "Aegis.Cafeteria.Services", "kratos",
                AlertSeverity.Warning, "Old registration", "Test", AlertLifecycleState.Raised, now), default);

            await CafeteriaControlPlaneCleanup.ClearCurrentStateAsync(store, default);
            await CafeteriaControlPlaneCleanup.ClearCurrentStateAsync(store, default);
            Assert.Equal("Aegis.CafeteriaBackup",
                Assert.Single(await store.LoadAsync<ApplicationOperationalRecord>("applications", default)).ApplicationId);
            var flow = Assert.Single(await store.LoadAsync<FlowRecord>("flows", default));
            Assert.Equal(FlowStageState.Skipped, Assert.Single(flow.Stages).State);
            Assert.Equal(AlertLifecycleState.Resolved,
                Assert.Single(await store.LoadAsync<AlertRecord>("alerts", default)).State);
            Assert.Single(await store.LoadAsync<ApplicationCleanupMarker>("application-cleanups", default));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
