using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class CafeteriaAcceptanceOperationsTests
{
    [Fact]
    public void Ordinary_application_assemblies_cannot_be_launched_as_acceptance_runners()
    {
        string path = typeof(CafeteriaAcceptanceOperations).Assembly.Location;
        string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
        Assert.False(CafeteriaAcceptanceOperations.IsAcceptanceAssembly(path, hash));
        Assert.False(CafeteriaAcceptanceOperations.IsAcceptanceAssembly(path, "incorrect"));
        Assert.False(CafeteriaAcceptanceOperations.IsAcceptanceAssembly(path, null));
    }
    [Fact]
    public void Viewing_operations_does_not_authorize_financial_acceptance_execution()
    {
        Assert.False(CafeteriaAcceptanceOperations.CanRun(new SuiteIdentity("reader", "Reader", "User", ["Operations.View"])));
        Assert.False(CafeteriaAcceptanceOperations.CanRun(new SuiteIdentity("runner", "Runner", "User", ["Diagnostics.Run"])));
        Assert.True(CafeteriaAcceptanceOperations.CanRun(new SuiteIdentity("engineer", "Engineer", "Admin", ["Diagnostics.Run", "Diagnostics.Critical"])));
    }
    [Fact]
    public async Task Missing_configuration_has_no_invented_acceptance_history_or_run_action()
    {
        var service = new CafeteriaAcceptanceOperations(new ConfigurationBuilder().Build(), NullLogger<CafeteriaAcceptanceOperations>.Instance);
        Assert.False(service.Configured); Assert.Empty(await service.ReadHistoryAsync(default));
        Assert.Null(await service.StartAsync("Engineer", "Qualification", default));
    }
    [Fact]
    public void Acceptance_page_uses_existing_operations_shell_and_safe_text_rendering()
    {
        string html = OperationsSectionPage.RenderCafeteriaAcceptance("http://configuration", "http://diagnostics", "Engineer");
        Assert.Contains("lcars-top", html); Assert.Contains("Cafeteria Acceptance", html);
        Assert.Contains("Diagnostics", html); Assert.Contains("textContent=value", html);
        Assert.Contains("X-Aegis-Action", html); Assert.Contains("disabled", html);
    }
}
