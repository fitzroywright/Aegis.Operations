using System.Text.Json;

internal static class OperationsEndpoints
{
    internal static void MapOperationsEndpoints(
        this WebApplication app,
        string configurationUrl,
        string diagnosticsUrl,
        string site,
        string environment)
    {
        app.MapGet("/api/summary", async (IHttpClientFactory factory, CancellationToken ct) =>
        {
            HttpClient client = factory.CreateClient("ops");
            return Results.Ok(new
            {
                site,
                environment,
                configuration = await ProbeAsync(client, configurationUrl, ct),
                diagnostics = await ProbeAsync(client, diagnosticsUrl, ct),
                checkedAt = DateTimeOffset.UtcNow
            });
        });

        app.MapGet("/", () => Results.Content(
            OperationsPage.Render(configurationUrl, diagnosticsUrl, site, environment),
            "text/html"));

        app.MapGet("/health", () => Results.Ok(new
        {
            status = "healthy",
            service = "Aegis.Operations",
            displayName = "System Operations"
        }));
    }

    private static async Task<object> ProbeAsync(HttpClient client, string url, CancellationToken ct)
    {
        try
        {
            using HttpResponseMessage response = await client.GetAsync(url + "/health", ct);
            if (!response.IsSuccessStatusCode)
                return new { available = false, status = "Unavailable", url };

            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            string status = document.RootElement.TryGetProperty("status", out JsonElement value)
                ? value.GetString() ?? "Available"
                : "Available";

            return new { available = true, status, url };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new { available = false, status = "Unavailable", url };
        }
    }
}
