using System.Net.Http.Headers;
using Common.Security;
using Common.Registration;

internal sealed class OperationsTelemetryAuthenticator
{
    private readonly IHttpClientFactory _factory;
    private readonly ILogger<OperationsTelemetryAuthenticator> _logger;
    private readonly string _configurationUrl;
    private readonly string _operationsUrl;

    internal OperationsTelemetryAuthenticator(
        IHttpClientFactory factory,
        IConfiguration configuration,
        ILogger<OperationsTelemetryAuthenticator> logger)
    {
        _factory = factory;
        _logger = logger;

        _configurationUrl = AegisControlPlaneEndpoints.ResolveInternal(
            configuration,
            AegisControlPlaneService.Configuration,
            _logger);

        _operationsUrl = AegisControlPlaneEndpoints.ResolveInternal(
            configuration,
            AegisControlPlaneService.Operations,
            _logger,
            configurationKey: "Operations:SelfUrl");
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

        bool controlPlane = applicationId.Equals("Aegis.Operations", StringComparison.OrdinalIgnoreCase) ||
            applicationId.Equals("Aegis.Configuration", StringComparison.OrdinalIgnoreCase) ||
            applicationId.Equals("Aegis.Diagnostics", StringComparison.OrdinalIgnoreCase);

        if (controlPlane)
        {
            using var validation = new HttpRequestMessage(
                HttpMethod.Post,
                _operationsUrl + "/api/registration/validate");
            validation.Headers.TryAddWithoutValidation("X-Aegis-Application-Id", suppliedApplicationId);
            validation.Headers.TryAddWithoutValidation("X-Aegis-Instance-Id", instanceId);
            validation.Headers.TryAddWithoutValidation("Authorization", authorization);
            try
            {
                using HttpResponseMessage response =
                    await _factory.CreateClient("ops").SendAsync(validation, ct);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(installationId))
            return false;

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            _configurationUrl + "/api/registration/authenticate");
        request.Headers.TryAddWithoutValidation("X-Aegis-Application-Id", suppliedApplicationId);
        request.Headers.TryAddWithoutValidation("X-Aegis-Instance-Id", instanceId);
        request.Headers.TryAddWithoutValidation("X-Aegis-Installation-Id", installationId);
        request.Headers.TryAddWithoutValidation("Authorization", authorization);
        string correlation = context.Request.Headers["X-Aegis-Correlation-Id"].ToString().Trim();
        if (!string.IsNullOrWhiteSpace(correlation))
            request.Headers.TryAddWithoutValidation("X-Aegis-Correlation-Id", correlation);

        try
        {
            using HttpResponseMessage response = await _factory.CreateClient("ops").SendAsync(request, ct);
            return response.IsSuccessStatusCode;
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
