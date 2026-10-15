using System.Text.Encodings.Web;

internal static class OperationsIssueEndpoints
{
    private static string E(string? value) => HtmlEncoder.Default.Encode(value ?? "Not published");
    internal static void Map(WebApplication app, string configurationUrl, string diagnosticsUrl)
    {
        app.MapGet("/issues", async (string applicationId, string? alertId, string? instanceId, OperationsV1Store store, CancellationToken ct) =>
        {
            alertId = string.IsNullOrWhiteSpace(alertId) ? null : alertId;
            instanceId = string.IsNullOrWhiteSpace(instanceId) ? null : instanceId;
            var alerts = (await store.LoadAsync<AlertRecord>("alerts", ct)).Where(x => x.ApplicationId.Equals(applicationId, StringComparison.OrdinalIgnoreCase) && (alertId is null || x.AlertId == alertId));
            var records = (await store.LoadAsync<ApplicationOperationalRecord>("applications", ct)).Where(x => x.ApplicationId.Equals(applicationId, StringComparison.OrdinalIgnoreCase) && (instanceId is null || x.InstanceId == instanceId));
            string detail = string.Join("", alerts.Select(x => $"<section class='panel' style='padding:16px'><h2>{E(x.Title)}</h2><p>Check / issue: {E(x.AlertId)} · {E(x.State.ToString())}</p><p>{E(x.Detail)}</p><p>Component: {E(x.InstanceId)} · Last reported: {E(x.RaisedAtUtc.ToString("u"))}</p></section>"));
            detail += string.Join("", records.Select(x => $"<section class='panel' style='padding:16px'><h2>{E(x.InstanceId)}</h2><p>Registration: {E(x.RegistrationStatus)} · Runtime state: {E(x.State.ToString())} · Configuration: {E(x.ConfigurationStatus)}</p><p>{E(x.Reason)}</p><p>Last observation: {E(x.ObservedAtUtc.ToString("u"))}</p></section>"));
            string path = "/applications/" + Uri.EscapeDataString(applicationId) + "/issues";
            detail += $"<section class='panel' style='padding:16px'><h2>Corrective guidance</h2><p>Inspect the specific failure above. Open this application's configuration issues for missing requirements or publication failures. Use the published diagnostic capability for provider, database, messaging or credential checks. Runtime and registration state are independent.</p><a href='/handoff/configuration?returnUrl={Uri.EscapeDataString(path)}'>Open this application's configuration failures</a><p><a href='/issues?applicationId={Uri.EscapeDataString(applicationId)}&amp;alertId={Uri.EscapeDataString(alertId ?? "")}&amp;instanceId={Uri.EscapeDataString(instanceId ?? "")}'>Refresh reported evidence</a></p></section>";
            return Results.Content(OperationsSectionPage.RenderIssue(applicationId, detail, configurationUrl, diagnosticsUrl), "text/html");
        });
    }
}
