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
        string runtimeSessionId = context.Request.Headers["X-Aegis-Runtime-Session-Id"].ToString().Trim();
        string authorization = context.Request.Headers.Authorization.ToString();

        if (string.IsNullOrWhiteSpace(applicationId) ||
            string.IsNullOrWhiteSpace(suppliedApplicationId) ||
            string.IsNullOrWhiteSpace(instanceId) ||
            !AuthenticationHeaderValue.TryParse(authorization, out AuthenticationHeaderValue? parsed) ||
            !parsed.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(parsed.Parameter))
        {
            return false;
        }

        bool delegatedConfigurationTelemetry =
            suppliedApplicationId.Equals("Aegis.Configuration", StringComparison.OrdinalIgnoreCase) &&
            !applicationId.Equals(suppliedApplicationId, StringComparison.OrdinalIgnoreCase);

        if (!delegatedConfigurationTelemetry &&
            !applicationId.Equals(suppliedApplicationId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        bool controlPlane = suppliedApplicationId.Equals("Aegis.Operations", StringComparison.OrdinalIgnoreCase) ||
            suppliedApplicationId.Equals("Aegis.Configuration", StringComparison.OrdinalIgnoreCase) ||
            suppliedApplicationId.Equals("Aegis.Diagnostics", StringComparison.OrdinalIgnoreCase);

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
                    await _factory.CreateClient("registration-authority").SendAsync(validation, ct);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(installationId) ||
            string.IsNullOrWhiteSpace(runtimeSessionId))
            return false;

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            _configurationUrl + "/api/registration/authenticate");
        request.Headers.TryAddWithoutValidation("X-Aegis-Application-Id", suppliedApplicationId);
        request.Headers.TryAddWithoutValidation("X-Aegis-Instance-Id", instanceId);
        request.Headers.TryAddWithoutValidation("X-Aegis-Installation-Id", installationId);
        request.Headers.TryAddWithoutValidation("X-Aegis-Runtime-Session-Id", runtimeSessionId);
        request.Headers.TryAddWithoutValidation("Authorization", authorization);
        request.Headers.TryAddWithoutValidation("X-Aegis-Required-Scope", "operations.telemetry.publish");
        string correlation = context.Request.Headers["X-Aegis-Correlation-Id"].ToString().Trim();
        if (!string.IsNullOrWhiteSpace(correlation))
            request.Headers.TryAddWithoutValidation("X-Aegis-Correlation-Id", correlation);

        // Credentials are checked against the current authority on every request.
        // An outage cannot restore a revoked credential; the client retains its retry/outbox behavior.
        try
        {
            using HttpResponseMessage response = await _factory.CreateClient("registration-authority").SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            _logger.LogWarning(
                "Telemetry authentication for {ApplicationId}/{InstanceId} was rejected by Configuration with HTTP {StatusCode}.",
                suppliedApplicationId,
                instanceId,
                (int)response.StatusCode);
            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Telemetry authentication could not validate {ApplicationId}/{InstanceId} against Configuration and authentication failed closed.",
                suppliedApplicationId,
                instanceId);
            return false;
        }
    }

    private static bool Has(HttpContext context, string permission) =>
        context.Items.TryGetValue("SuiteIdentity", out object? value) &&
        value is SuiteIdentity identity &&
        identity.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
}
