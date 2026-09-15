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
        if (string.IsNullOrWhiteSpace(url))
            return new { available = false, status = "Not configured", url = string.Empty };

        string trimmedUrl = url.Trim();
        if (!Uri.TryCreate(trimmedUrl, UriKind.Absolute, out Uri? baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            return new { available = false, status = "Invalid configuration", url = trimmedUrl };
        }

        Uri healthUri = new($"{baseUri.AbsoluteUri.TrimEnd('/')}/health", UriKind.Absolute);

        try
        {
            using HttpResponseMessage response = await client.GetAsync(healthUri, ct);
            if (!response.IsSuccessStatusCode)
                return new { available = false, status = "Unavailable", url = trimmedUrl };

            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            string status = document.RootElement.TryGetProperty("status", out JsonElement value)
                ? value.GetString() ?? "Available"
                : "Available";

            return new { available = true, status, url = trimmedUrl };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new { available = false, status = "Unavailable", url = trimmedUrl };
        }
    }
}
