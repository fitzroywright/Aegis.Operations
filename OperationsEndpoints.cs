using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Common.Diagnostics;
using Common.Registration;
using Common.Security;

internal static class OperationsEndpoints
{
    internal static void MapOperationsEndpoints(
        this WebApplication app,
        string configurationUrl,
        string diagnosticsUrl,
        string site,
        string environment)
    {
        string? configuredRegistrationDirectory = app.Configuration["Operations:RegistrationDirectory"];
        string registrationDirectory;
        if (string.IsNullOrWhiteSpace(configuredRegistrationDirectory))
        {
            registrationDirectory = "/var/lib/aegis/operations/registration";
            app.Logger.LogWarning(
                "Configuration {ConfigurationKey} is not set; falling back to default value {FallbackValue}.",
                "Operations:RegistrationDirectory",
                registrationDirectory);
        }
        else
        {
            registrationDirectory = configuredRegistrationDirectory.Trim();
            app.Logger.LogInformation(
                "Configuration {ConfigurationKey} resolved from configured value {ConfiguredValue}.",
                "Operations:RegistrationDirectory",
                registrationDirectory);
        }

        Directory.CreateDirectory(registrationDirectory);
        var registrationStore = new ControlPlaneRegistrationStore(registrationDirectory);
        ILogger registrationLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Aegis.Operations.Registration");
        string operationsInstanceId = app.Configuration["Operations:InstanceId"]?.Trim() ?? Environment.MachineName;
        string operationsLocalUrl = (app.Configuration["Operations:LocalUrl"] ?? "http://127.0.0.1:5100").TrimEnd('/');
        string operationsCredentialFile = app.Configuration["Operations:RegistrationCredentialFile"]?.Trim()
            ?? Path.Combine(registrationDirectory, "operations.key");

        app.Lifetime.ApplicationStarted.Register(() =>
        {
            _ = Task.Run(async () =>
            {
                CancellationToken stoppingToken = app.Lifetime.ApplicationStopping;
                IHttpClientFactory factory = app.Services.GetRequiredService<IHttpClientFactory>();
                string? operationsCredential = null;

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        JsonObject contract = BuildOperationsContract(
                            operationsInstanceId,
                            site,
                            environment);

                        using HttpClient registrationHttp = factory.CreateClient("ops");
                        var registration = new ControlPlaneRegistrationClient(
                            registrationHttp,
                            new ControlPlaneRegistrationOptions(
                                new Uri(operationsLocalUrl + "/", UriKind.Absolute),
                                "Aegis.Operations",
                                operationsInstanceId,
                                operationsCredentialFile,
                                TimeSpan.FromSeconds(10)),
                            logger: registrationLogger);

                        ControlPlaneRegistrationStatus registrationStatus =
                            await registration.RegisterAsync(contract, stoppingToken);
                        if (!registrationStatus.IsRegistered)
                        {
                            registrationLogger.LogWarning(
                                "Aegis.Operations self-registration state is {RegistrationState}: {RegistrationError}.",
                                registrationStatus.State,
                                registrationStatus.Error ?? "No additional detail.");
                        }
                        else
                        {
                            operationsCredential = await ReadCredentialFileAsync(
                                operationsCredentialFile,
                                stoppingToken);
                            registrationLogger.LogInformation(
                                "Aegis.Operations control-plane registration is active for instance {InstanceId}.",
                                operationsInstanceId);
                        }

                        IReadOnlyList<string> activeApplicationIds =
                            await registrationStore.RegisteredApplicationIdsAsync(stoppingToken);
                        ConfigurationSyncResult sync = await ReconcileConfigurationAsync(
                            app,
                            factory,
                            configurationUrl,
                            operationsInstanceId,
                            operationsCredential,
                            activeApplicationIds,
                            stoppingToken);
                        if (!sync.Success)
                            registrationLogger.LogWarning("Configuration reconciliation failed: {Message}", sync.Message);
                        else
                            registrationLogger.LogInformation(
                                "Configuration reconciliation completed for {ApplicationCount} registered applications.",
                                activeApplicationIds.Count);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception exception)
                    {
                        registrationLogger.LogWarning(exception, "Unexpected failure during Operations self-registration or Configuration reconciliation.");
                    }

                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
            });
        });

        app.MapGet("/api/summary", async (
            HttpContext context,
            SuiteSecurity suiteSecurity,
            IHttpClientFactory factory,
            CancellationToken ct) =>
        {
            HttpClient client = factory.CreateClient("ops");
            LifecycleSummaryProbe lifecycle =
                await ProbeLifecycleSummaryAsync(
                    context,
                    suiteSecurity,
                    client,
                    configurationUrl,
                    ct);

            return Results.Ok(new
            {
                site,
                environment,
                configuration = await ProbeAsync(client, configurationUrl, ct),
                diagnostics = await ProbeAsync(client, diagnosticsUrl, ct),
                registeredApplications = lifecycle.Registered,
                registration = lifecycle,
                controlPlaneRegistrations = await registrationStore.CountRegisteredAsync(ct),
                checkedAt = DateTimeOffset.UtcNow
            });
        });

        app.MapPost("/api/registration/control-plane/auto", async (
            HttpContext context,
            ControlPlaneBootstrapRequest request,
            CancellationToken ct) =>
        {
            if (context.Connection.RemoteIpAddress is null || !IPAddress.IsLoopback(context.Connection.RemoteIpAddress))
                return Results.Unauthorized();

            string[] controlPlaneApplications =
            [
                "Aegis.Operations",
                "Aegis.Configuration",
                "Aegis.Diagnostics"
            ];
            if (!controlPlaneApplications.Contains(request.ApplicationId, StringComparer.OrdinalIgnoreCase))
                return Results.Forbid();

            if (string.IsNullOrWhiteSpace(request.InstanceId))
                return Results.BadRequest(new { error = "instanceId is required." });

            ControlPlaneProvisionResult provisioned = await registrationStore.ProvisionControlPlaneAsync(
                request.ApplicationId.Trim(),
                string.IsNullOrWhiteSpace(request.DisplayName) ? request.ApplicationId.Trim() : request.DisplayName.Trim(),
                request.InstanceId.Trim(),
                ct);

            if (!provisioned.Succeeded || string.IsNullOrWhiteSpace(provisioned.Credential))
                return Results.Conflict(new { error = provisioned.Error ?? "Control-plane registration could not be provisioned." });

            context.Response.Headers.CacheControl = "no-store";
            registrationLogger.LogInformation(
                "Control-plane registration auto-provisioned for {ApplicationId}/{InstanceId}. RegistrationId={RegistrationId}.",
                request.ApplicationId,
                request.InstanceId,
                provisioned.RegistrationId);
            return Results.Ok(new
            {
                provisioned.RegistrationId,
                request.ApplicationId,
                request.InstanceId,
                credential = provisioned.Credential
            });
        }).AllowAnonymous();

        app.MapPost("/api/registration/validate", async (
            HttpContext context,
            CancellationToken ct) =>
        {
            string applicationId = context.Request.Headers["X-Aegis-Application-Id"].ToString().Trim();
            string instanceId = context.Request.Headers["X-Aegis-Instance-Id"].ToString().Trim();
            string credential = string.Empty;

            if (AuthenticationHeaderValue.TryParse(
                    context.Request.Headers.Authorization.ToString(),
                    out AuthenticationHeaderValue? authorization) &&
                string.Equals(
                    authorization.Scheme,
                    "Bearer",
                    StringComparison.OrdinalIgnoreCase))
            {
                credential = authorization.Parameter?.Trim() ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(applicationId) ||
                string.IsNullOrWhiteSpace(instanceId) ||
                string.IsNullOrWhiteSpace(credential))
            {
                return Results.Unauthorized();
            }

            RegistrationValidation validation = await registrationStore.ValidateAsync(
                applicationId,
                instanceId,
                credential,
                ct);

            return validation switch
            {
                RegistrationValidation.Valid => Results.Ok(new
                {
                    applicationId,
                    instanceId,
                    valid = true
                }),
                RegistrationValidation.Revoked => Results.Json(
                    new
                    {
                        applicationId,
                        instanceId,
                        valid = false,
                        revoked = true
                    },
                    statusCode: StatusCodes.Status410Gone),
                _ => Results.Unauthorized()
            };
        }).AllowAnonymous();

        app.MapPost("/api/registration/contracts", async (
            HttpContext context,
            IHttpClientFactory factory,
            CancellationToken ct) =>
        {
            string attemptId = context.Request.Headers["X-Aegis-Registration-Attempt-Id"].ToString();
            if (string.IsNullOrWhiteSpace(attemptId)) attemptId = Guid.NewGuid().ToString("N");
            string applicationId = context.Request.Headers["X-Aegis-Application-Id"].ToString();
            string instanceId = context.Request.Headers["X-Aegis-Instance-Id"].ToString();
            string credential = Bearer(context.Request.Headers.Authorization.ToString());
            using IDisposable? logScope = registrationLogger.BeginScope(new Dictionary<string, object?>
            {
                ["Activity"] = "Registration",
                ["Application"] = applicationId,
                ["RegistrationAttemptId"] = attemptId,
                ["Stage"] = "ContractReceived"
            });
            registrationLogger.LogInformation("Registration contract request received for instance {InstanceId}.", instanceId);

            if (string.IsNullOrWhiteSpace(applicationId) ||
                string.IsNullOrWhiteSpace(instanceId) ||
                string.IsNullOrWhiteSpace(credential))
                return Results.Unauthorized();

            RegistrationValidation validation = await registrationStore.ValidateAsync(
                applicationId,
                instanceId,
                credential,
                ct);

            if (validation == RegistrationValidation.Revoked)
            {
                registrationLogger.LogWarning("Registration contract rejected because registration is revoked.");
                return Results.StatusCode(StatusCodes.Status410Gone);
            }
            if (validation != RegistrationValidation.Valid)
            {
                registrationLogger.LogWarning("Registration contract rejected because registration key validation failed.");
                return Results.Unauthorized();
            }
            registrationLogger.LogInformation("Registration key validation succeeded.");

            JsonNode? contract = await JsonNode.ParseAsync(context.Request.Body, cancellationToken: ct);
            if (contract is null)
                return Results.BadRequest(new { error = "Configuration contract body is required." });

            string? operationsCredential = await ReadCredentialFileAsync(operationsCredentialFile, ct);
            if (string.IsNullOrWhiteSpace(operationsCredential))
            {
                return Results.Problem(
                    "Operations cannot forward registered application contracts because its self-registration credential is unavailable.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            HttpClient client = factory.CreateClient("ops");
            using var forward = new HttpRequestMessage(
                HttpMethod.Post,
                configurationUrl.TrimEnd('/') + "/api/contracts/register")
            {
                Content = JsonContent.Create(contract)
            };
            forward.Headers.TryAddWithoutValidation("X-Aegis-Registration-Key", operationsCredential);
            forward.Headers.TryAddWithoutValidation("X-Aegis-Application-Id", "Aegis.Operations");
            forward.Headers.TryAddWithoutValidation("X-Aegis-Instance-Id", operationsInstanceId);
            forward.Headers.TryAddWithoutValidation("X-Aegis-Registration-Attempt-Id", attemptId);

            registrationLogger.LogInformation("Forwarding validated configuration contract to Aegis.Configuration.");
            using HttpResponseMessage response = await client.SendAsync(forward, ct);
            registrationLogger.LogInformation("Aegis.Configuration returned HTTP {StatusCode}.", (int)response.StatusCode);
            string body = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                bool markedRegistered = await registrationStore.MarkRegisteredAsync(
                    applicationId,
                    instanceId,
                    credential,
                    ct);
                if (!markedRegistered)
                {
                    registrationLogger.LogError(
                        "Configuration accepted the contract, but Operations could not finalize the registration state.");
                    return Results.Problem(
                        "Configuration accepted the contract, but Operations could not finalize registration state.",
                        statusCode: StatusCodes.Status500InternalServerError);
                }

                registrationLogger.LogInformation(
                    "Registration finalized after Configuration accepted the contract.");
            }
            else
            {
                registrationLogger.LogWarning(
                    "Registration remains retryable because Configuration rejected the contract with HTTP {StatusCode}.",
                    (int)response.StatusCode);
            }

            return Results.Content(
                body,
                response.Content.Headers.ContentType?.ToString() ?? "application/json",
                statusCode: (int)response.StatusCode);
        }).AllowAnonymous();

        app.MapGet("/", () => Results.Content(
            OperationsPage.Render(configurationUrl, diagnosticsUrl, site, environment),
            "text/html"));

        app.MapAegisHealth("Aegis.Operations");
    }

    private sealed record ConfigurationSyncResult(bool Success, string Message);

    private static async Task<ConfigurationSyncResult> ReconcileConfigurationAsync(
        WebApplication app,
        IHttpClientFactory factory,
        string configurationUrl,
        string operationsInstanceId,
        string? operationsCredential,
        IReadOnlyList<string> activeApplicationIds,
        CancellationToken ct)
    {
        string? key = operationsCredential;
        if (string.IsNullOrWhiteSpace(key))
            return new(false, "Aegis.Operations self-registration credential is unavailable.");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            configurationUrl.TrimEnd('/') + "/api/contracts/reconcile")
        {
            Content = JsonContent.Create(activeApplicationIds)
        };
        request.Headers.TryAddWithoutValidation("X-Aegis-Registration-Key", key);
        request.Headers.TryAddWithoutValidation("X-Aegis-Application-Id", "Aegis.Operations");
        request.Headers.TryAddWithoutValidation("X-Aegis-Instance-Id", operationsInstanceId);

        try
        {
            HttpClient client = factory.CreateClient("ops");
            using HttpResponseMessage response = await client.SendAsync(request, ct);
            Uri? finalUri = response.RequestMessage?.RequestUri;
            bool redirectedToLogin = finalUri is not null &&
                finalUri.AbsolutePath.StartsWith("/login", StringComparison.OrdinalIgnoreCase);
            bool htmlResponse = response.Content.Headers.ContentType?.MediaType?.Equals(
                "text/html",
                StringComparison.OrdinalIgnoreCase) == true;
            if (redirectedToLogin || htmlResponse)
                return new(false, "Configuration redirected the reconciliation API request to interactive login.");

            return response.IsSuccessStatusCode
                ? new(true, "Configuration reconciliation completed.")
                : new(false, $"Configuration reconciliation returned HTTP {(int)response.StatusCode}.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new(false, ex.Message);
        }
    }

    private static async Task<string?> ReadCredentialFileAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
            return null;
        string value = (await File.ReadAllTextAsync(path, ct)).Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static JsonObject BuildOperationsContract(
        string instanceId,
        string site,
        string environment)
    {
        return new JsonObject
        {
            ["applicationId"] = "Aegis.Operations",
            ["displayName"] = "Aegis Operations",
            ["version"] = typeof(OperationsEndpoints).Assembly.GetName().Version?.ToString() ?? "unknown",
            ["siteId"] = site == "—" ? null : site,
            ["instanceId"] = instanceId,
            ["requirements"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "configuration-url",
                    ["displayName"] = "Configuration URL",
                    ["kind"] = 0,
                    ["required"] = true,
                    ["purpose"] = "Aegis.Configuration endpoint used for contract publication and lifecycle reconciliation.",
                    ["configurationKey"] = "Operations:ConfigurationUrl",
                    ["sensitive"] = false,
                    ["isConfigured"] = true,
                    ["effectiveValueAvailable"] = true,
                    ["configurationState"] = "Configured",
                    ["effectiveSource"] = "Resolved IConfiguration"
                },
                new JsonObject
                {
                    ["id"] = "diagnostics-url",
                    ["displayName"] = "Diagnostics URL",
                    ["kind"] = 0,
                    ["required"] = true,
                    ["purpose"] = "Aegis.Diagnostics endpoint used by Operations.",
                    ["configurationKey"] = "Operations:DiagnosticsUrl",
                    ["sensitive"] = false,
                    ["isConfigured"] = true,
                    ["effectiveValueAvailable"] = true,
                    ["configurationState"] = "Configured",
                    ["effectiveSource"] = "Resolved IConfiguration"
                },
                new JsonObject
                {
                    ["id"] = "registration-store",
                    ["displayName"] = "Registration Store",
                    ["kind"] = 0,
                    ["required"] = true,
                    ["purpose"] = "Persistent Operations registration state.",
                    ["configurationKey"] = "Operations:RegistrationDirectory",
                    ["sensitive"] = false,
                    ["isConfigured"] = true,
                    ["effectiveValueAvailable"] = true,
                    ["configurationState"] = "Configured",
                    ["effectiveSource"] = "Resolved IConfiguration"
                }
            }
        };
    }

    private static string Bearer(string authorization)
    {
        if (!AuthenticationHeaderValue.TryParse(authorization, out AuthenticationHeaderValue? header) ||
            !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
            return string.Empty;
        return header.Parameter?.Trim() ?? string.Empty;
    }

    private static bool Has(HttpContext context, string permission) =>
        context.Items.TryGetValue("SuiteIdentity", out object? value) &&
        value is SuiteIdentity identity &&
        identity.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    private static string Actor(HttpContext context) =>
        context.User?.Identity?.Name ?? "unknown";

    private static async Task<LifecycleSummaryProbe> ProbeLifecycleSummaryAsync(
        HttpContext context,
        SuiteSecurity suiteSecurity,
        HttpClient client,
        string configurationUrl,
        CancellationToken ct)
    {
        if (!context.Items.TryGetValue("SuiteIdentity", out object? value) ||
            value is not SuiteIdentity identity)
        {
            return LifecycleSummaryProbe.Unavailable("No authenticated Operations identity.");
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                configurationUrl.TrimEnd('/') + "/api/registration/summary");
            request.Headers.TryAddWithoutValidation(
                "X-Aegis-Suite-Handoff",
                suiteSecurity.CreateHandoff(identity, "Aegis.Configuration"));

            using HttpResponseMessage response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return LifecycleSummaryProbe.Unavailable(
                    $"Configuration registration summary returned HTTP {(int)response.StatusCode}.");

            using JsonDocument document =
                JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            JsonElement root = document.RootElement;

            static int Int(JsonElement root, string name) =>
                root.TryGetProperty(name, out JsonElement value) &&
                value.TryGetInt32(out int number)
                    ? number
                    : 0;

            return new LifecycleSummaryProbe(
                true,
                Int(root, "total"),
                Int(root, "pending"),
                Int(root, "registered"),
                Int(root, "recoveryPending"),
                Int(root, "conflicts"),
                Int(root, "revoked"),
                Int(root, "rejected"),
                Int(root, "attention"),
                null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return LifecycleSummaryProbe.Unavailable(ex.Message);
        }
    }

    private sealed record LifecycleSummaryProbe(
        bool Available,
        int Total,
        int Pending,
        int Registered,
        int RecoveryPending,
        int Conflicts,
        int Revoked,
        int Rejected,
        int Attention,
        string? Error)
    {
        public static LifecycleSummaryProbe Unavailable(string error) =>
            new(false, 0, 0, 0, 0, 0, 0, 0, 0, error);
    }

    private static async Task<object> ProbeAsync(HttpClient client, string url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url))
            return new { available = false, status = "Not configured", url = string.Empty };

        string trimmedUrl = url.Trim();
        if (!Uri.TryCreate(trimmedUrl, UriKind.Absolute, out Uri? baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
            return new { available = false, status = "Invalid configuration", url = trimmedUrl };

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

    private sealed record ControlPlaneBootstrapRequest(
        string ApplicationId,
        string DisplayName,
        string InstanceId);

    private enum RegistrationValidation
    {
        Valid,
        Invalid,
        Revoked
    }

    private sealed record ControlPlaneProvisionResult(
        bool Succeeded,
        string? RegistrationId,
        string? Credential,
        string? Error);

    private sealed record ControlPlaneRegistrationAudit(
        DateTimeOffset AtUtc,
        string Action,
        string Actor);

    private sealed record ControlPlaneRegistrationRecord(
        string RegistrationId,
        string ApplicationId,
        string DisplayName,
        string InstanceId,
        bool Revoked,
        string CredentialHash,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? RevokedAtUtc,
        DateTimeOffset? LastAuthenticatedAtUtc,
        List<ControlPlaneRegistrationAudit> Audit);

    private sealed class ControlPlaneRegistrationStore
    {
        private readonly string _directory;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

        public ControlPlaneRegistrationStore(string directory)
        {
            _directory = directory;
            Directory.CreateDirectory(_directory);
        }

        public async Task<ControlPlaneProvisionResult> ProvisionControlPlaneAsync(
            string applicationId,
            string displayName,
            string instanceId,
            CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try
            {
                List<ControlPlaneRegistrationRecord> all = await LoadAllUnsafeAsync(ct);
                DateTimeOffset now = DateTimeOffset.UtcNow;

                foreach (ControlPlaneRegistrationRecord existing in all.Where(x =>
                    string.Equals(x.ApplicationId, applicationId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(x.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase) &&
                    !x.Revoked))
                {
                    ControlPlaneRegistrationRecord revoked = existing with
                    {
                        Revoked = true,
                        RevokedAtUtc = now,
                        Audit = [.. existing.Audit, new(now, "ControlPlaneCredentialRotated", applicationId)]
                    };
                    await SaveUnsafeAsync(revoked, ct);
                }

                string credential = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
                ControlPlaneRegistrationRecord record = new(
                    Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant(),
                    applicationId,
                    displayName,
                    instanceId,
                    false,
                    HashCredential(credential),
                    now,
                    null,
                    null,
                    [new(now, "ControlPlaneAutoRegistered", applicationId)]);

                await SaveUnsafeAsync(record, ct);

                return new(
                    true,
                    record.RegistrationId,
                    credential,
                    null);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<RegistrationValidation> ValidateAsync(
            string applicationId,
            string instanceId,
            string credential,
            CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try
            {
                foreach (ControlPlaneRegistrationRecord record in await LoadAllUnsafeAsync(ct))
                {
                    if (!string.Equals(record.ApplicationId, applicationId, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(record.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase) ||
                        !VerifyCredentialHash(record.CredentialHash, credential))
                    {
                        continue;
                    }

                    if (record.Revoked)
                        return RegistrationValidation.Revoked;

                    ControlPlaneRegistrationRecord authenticated = record with
                    {
                        LastAuthenticatedAtUtc = DateTimeOffset.UtcNow
                    };
                    await SaveUnsafeAsync(authenticated, ct);

                    return RegistrationValidation.Valid;
                }

                return RegistrationValidation.Invalid;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<bool> MarkRegisteredAsync(
            string applicationId,
            string instanceId,
            string credential,
            CancellationToken ct)
        {
            RegistrationValidation validation = await ValidateAsync(
                applicationId,
                instanceId,
                credential,
                ct);

            return validation == RegistrationValidation.Valid;
        }

        public async Task<IReadOnlyList<string>> RegisteredApplicationIdsAsync(CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try
            {
                return (await LoadAllUnsafeAsync(ct))
                    .Where(x => !x.Revoked)
                    .Select(x => x.ApplicationId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<int> CountRegisteredAsync(CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try
            {
                return (await LoadAllUnsafeAsync(ct)).Count(x => !x.Revoked);
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task<List<ControlPlaneRegistrationRecord>> LoadAllUnsafeAsync(CancellationToken ct)
        {
            var result = new List<ControlPlaneRegistrationRecord>();

            foreach (string file in Directory.EnumerateFiles(_directory, "control-plane-*.json"))
            {
                string json = await File.ReadAllTextAsync(file, ct);
                ControlPlaneRegistrationRecord? record =
                    JsonSerializer.Deserialize<ControlPlaneRegistrationRecord>(json, JsonOptions);

                if (record is not null)
                    result.Add(record);
            }

            return result;
        }

        private async Task SaveUnsafeAsync(
            ControlPlaneRegistrationRecord record,
            CancellationToken ct)
        {
            string path = Path.Combine(
                _directory,
                "control-plane-" + record.RegistrationId + ".json");

            string temp = path + ".tmp-" + Guid.NewGuid().ToString("N");

            await File.WriteAllTextAsync(
                temp,
                JsonSerializer.Serialize(record, JsonOptions),
                ct);

            SecureFile(temp);
            File.Move(temp, path, true);
            SecureFile(path);
        }

        private static string HashCredential(string credential)
        {
            const int iterations = 210_000;
            byte[] salt = RandomNumberGenerator.GetBytes(32);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                credential,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                32);

            return "PBKDF2-SHA256$" + iterations + "$" +
                   Convert.ToBase64String(salt) + "$" +
                   Convert.ToBase64String(hash);
        }

        private static bool VerifyCredentialHash(
            string encoded,
            string credential)
        {
            try
            {
                string[] parts = encoded.Split('$');
                if (parts.Length != 4 ||
                    !string.Equals(parts[0], "PBKDF2-SHA256", StringComparison.Ordinal))
                {
                    return false;
                }

                int iterations = int.Parse(
                    parts[1],
                    System.Globalization.CultureInfo.InvariantCulture);

                byte[] salt = Convert.FromBase64String(parts[2]);
                byte[] expected = Convert.FromBase64String(parts[3]);
                byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
                    credential,
                    salt,
                    iterations,
                    HashAlgorithmName.SHA256,
                    expected.Length);

                return CryptographicOperations.FixedTimeEquals(
                    actual,
                    expected);
            }
            catch
            {
                return false;
            }
        }

        private static void SecureFile(string path)
        {
            if (OperatingSystem.IsWindows())
                return;

            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
