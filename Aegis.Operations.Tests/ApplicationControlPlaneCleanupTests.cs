using Microsoft.Extensions.Configuration;
using Xunit;

public sealed class ApplicationControlPlaneCleanupTests
{
    [Theory]
    [InlineData("Aegis.Studio", true, "Aegis.Studio.Web", true)]
    [InlineData("Aegis.Studio", false, "Aegis.Studio.Web", false)]
    [InlineData("Aegis.Studio", true, "Aegis.StudioBackup", false)]
    [InlineData("Aegis.Studio", false, "aegis.studio", true)]
    [InlineData("Aegis.Cafeteria", true, "Aegis.Cafeteria.POS.UI", true)]
    [InlineData("RequestPortal", false, "RequestPortal", true)]
    public void Exact_and_family_scopes_respect_application_id_boundaries(
        string id, bool children, string candidate, bool expected)
        => Assert.Equal(expected, new RegistrationCleanupScope(id, children).Matches(candidate));

    [Fact]
    public void Previous_cleanup_markers_keep_family_semantics()
    {
        var marker = System.Text.Json.JsonSerializer.Deserialize<ApplicationCleanupMarker>(
            """{"ApplicationFamily":"Aegis.Cafeteria","AtUtc":"2026-10-15T00:00:00Z"}""");
        Assert.True(marker!.IncludeChildren);
    }

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

            await ApplicationControlPlaneCleanup.ClearCurrentStateAsync(store, new("Aegis.Cafeteria", true), default);
            await ApplicationControlPlaneCleanup.ClearCurrentStateAsync(store, new("Aegis.Cafeteria", true), default);
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
