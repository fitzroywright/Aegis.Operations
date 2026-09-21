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


public sealed class ActivityPageInteractionContractTests
{
    private static readonly string Page = ActivityPage.Render();

    [Fact]
    public void Activity_page_preserves_lcars_shell_and_uses_compact_collapsible_filters()
    {
        Assert.Contains("workspace-shell", Page);
        Assert.Contains("workspace-arm", Page);
        Assert.Contains("workspace-leg", Page);
        Assert.Contains("id=\"filterToggle\"", Page);
        Assert.Contains("filters collapsed", Page);
        Assert.Contains("Show Filters", Page);
        Assert.Contains("setFiltersOpen(false)", Page);
    }

    [Fact]
    public void Activity_page_has_sticky_summary_zero_state_and_keyboard_selection()
    {
        Assert.Contains("id=\"filterSummary\"", Page);
        Assert.Contains("Reset Filters", Page);
        Assert.Contains("ArrowDown", Page);
        Assert.Contains("ArrowUp", Page);
        Assert.Contains("telemetry-row.active", Page);
        Assert.Contains("restoreSelection", Page);
    }

    [Fact]
    public void Flow_analyzer_is_a_non_destructive_floating_overlay()
    {
        Assert.Contains("id=\"flowOverlay\"", Page);
        Assert.Contains("position:absolute;z-index:40", Page);
        Assert.Contains("refreshFlowOverlay", Page);
        Assert.Contains("lastFocusedList", Page);
        Assert.Contains("focus({preventScroll:true})", Page);
    }
}
