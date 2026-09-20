using System.Net.Http.Headers;
using Common.Security;

internal sealed class OperationsTelemetryAuthenticator
{
    private readonly IHttpClientFactory _factory;
    private readonly string _configurationUrl;
    private readonly string _operationsLocalUrl;

    internal OperationsTelemetryAuthenticator(IHttpClientFactory factory, IConfiguration configuration)
    {
        _factory = factory;
        _configurationUrl = (configuration["Operations:ConfigurationUrl"] ?? "http://localhost:5101").TrimEnd('/');
        _operationsLocalUrl = (configuration["Operations:LocalUrl"] ?? "http://127.0.0.1:5100").TrimEnd('/');
    }

    internal async Task<bool> IsAuthorizedAsync(
        HttpContext context,
        string applicationId,
        CancellationToken ct)
    {
        if (Has(context, "Operations.Telemetry.Publish"))
            return true;

        string suppliedApplicationId = context.Request.Headers["X-Aegis-Application-Id"].ToString().Trim();
        string instanceId = context.Request.Headers["X-Aegis-Instance-Id"].ToString().Trim();
        string installationId = context.Request.Headers["X-Aegis-Installation-Id"].ToString().Trim();
        string authorization = context.Request.Headers.Authorization.ToString();

        if (string.IsNullOrWhiteSpace(applicationId) ||
            !applicationId.Equals(suppliedApplicationId, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(instanceId) ||
            !AuthenticationHeaderValue.TryParse(authorization, out AuthenticationHeaderValue? parsed) ||
            !parsed.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(parsed.Parameter))
        {
            return false;
        }

        string correlation = context.Request.Headers["X-Aegis-Correlation-Id"].ToString().Trim();

        try
        {
            if (!string.IsNullOrWhiteSpace(installationId))
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    _configurationUrl + "/api/registration/authenticate");
                request.Headers.TryAddWithoutValidation("X-Aegis-Application-Id", suppliedApplicationId);
                request.Headers.TryAddWithoutValidation("X-Aegis-Instance-Id", instanceId);
                request.Headers.TryAddWithoutValidation("X-Aegis-Installation-Id", installationId);
                request.Headers.TryAddWithoutValidation("Authorization", authorization);
                if (!string.IsNullOrWhiteSpace(correlation))
                    request.Headers.TryAddWithoutValidation("X-Aegis-Correlation-Id", correlation);

                using HttpResponseMessage response = await _factory.CreateClient("ops").SendAsync(request, ct);
                if (response.IsSuccessStatusCode)
                    return true;
            }

            using var controlPlaneRequest = new HttpRequestMessage(
                HttpMethod.Post,
                _operationsLocalUrl + "/api/registration/validate");
            controlPlaneRequest.Headers.TryAddWithoutValidation("X-Aegis-Application-Id", suppliedApplicationId);
            controlPlaneRequest.Headers.TryAddWithoutValidation("X-Aegis-Instance-Id", instanceId);
            controlPlaneRequest.Headers.TryAddWithoutValidation("Authorization", authorization);
            if (!string.IsNullOrWhiteSpace(correlation))
                controlPlaneRequest.Headers.TryAddWithoutValidation("X-Aegis-Correlation-Id", correlation);

            using HttpResponseMessage controlPlaneResponse =
                await _factory.CreateClient("ops").SendAsync(controlPlaneRequest, ct);
            return controlPlaneResponse.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    private static bool Has(HttpContext context, string permission) =>
        context.Items.TryGetValue("SuiteIdentity", out object? value) &&
        value is SuiteIdentity identity &&
        identity.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
}
