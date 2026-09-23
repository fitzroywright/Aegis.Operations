using System.Net.Http.Headers;
using Common.Security;
using Common.Registration;

internal sealed class OperationsTelemetryAuthenticator
{
    private readonly IHttpClientFactory _factory;
    private readonly ILogger<OperationsTelemetryAuthenticator> _logger;
    private readonly string _configurationUrl;
    private readonly string _operationsUrl;
    private readonly TimeSpan _authorityGrace;
    private readonly TimeSpan _validationCache;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTimeOffset> _lastValidated = new(StringComparer.OrdinalIgnoreCase);

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

        _authorityGrace = TimeSpan.FromSeconds(Math.Clamp(
            configuration.GetValue("Operations:TelemetryAuthorityGraceSeconds", 900),
            30,
            3600));

        _validationCache = TimeSpan.FromSeconds(Math.Clamp(
            configuration.GetValue("Operations:TelemetryValidationCacheSeconds", 30),
            5,
            300));
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
        string correlation = context.Request.Headers["X-Aegis-Correlation-Id"].ToString().Trim();
        if (!string.IsNullOrWhiteSpace(correlation))
            request.Headers.TryAddWithoutValidation("X-Aegis-Correlation-Id", correlation);

        string cacheKey = suppliedApplicationId + "|" + instanceId + "|" + installationId + "|" + runtimeSessionId;

        if (_lastValidated.TryGetValue(cacheKey, out DateTimeOffset cachedAt) &&
            DateTimeOffset.UtcNow - cachedAt <= _validationCache)
        {
            return true;
        }

        try
        {
            using HttpResponseMessage response = await _factory.CreateClient("registration-authority").SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                _lastValidated[cacheKey] = DateTimeOffset.UtcNow;
                return true;
            }

            if ((int)response.StatusCode >= 500 && IsRecentlyValidated(cacheKey))
            {
                _logger.LogWarning(
                    "Configuration registration authority is unavailable (HTTP {StatusCode}); accepting recently validated telemetry identity {ApplicationId}/{InstanceId} within the configured grace period.",
                    (int)response.StatusCode,
                    suppliedApplicationId,
                    instanceId);
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
            if (IsRecentlyValidated(cacheKey))
            {
                _logger.LogWarning(
                    ex,
                    "Configuration registration authority is unavailable; accepting recently validated telemetry identity {ApplicationId}/{InstanceId} within the configured grace period.",
                    suppliedApplicationId,
                    instanceId);
                return true;
            }

            _logger.LogWarning(
                ex,
                "Telemetry authentication could not validate {ApplicationId}/{InstanceId} against Configuration and no recent successful validation is cached.",
                suppliedApplicationId,
                instanceId);
            return false;
        }
    }

    private bool IsRecentlyValidated(string cacheKey) =>
        _lastValidated.TryGetValue(cacheKey, out DateTimeOffset validatedAt) &&
        DateTimeOffset.UtcNow - validatedAt <= _authorityGrace;

    private static bool Has(HttpContext context, string permission) =>
        context.Items.TryGetValue("SuiteIdentity", out object? value) &&
        value is SuiteIdentity identity &&
        identity.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
}
