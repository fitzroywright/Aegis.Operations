using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Common.Diagnostics;

internal static class OperationsEndpoints
{
    internal static void MapOperationsEndpoints(
        this WebApplication app,
        string configurationUrl,
        string diagnosticsUrl,
        string site,
        string environment)
    {
        string registrationDirectory = app.Configuration["Operations:RegistrationDirectory"]
            ?? Path.Combine(app.Environment.ContentRootPath, "registration");
        Directory.CreateDirectory(registrationDirectory);
        var registrationStore = new RegistrationStore(registrationDirectory);

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

        app.MapPost("/api/registration/pending", async (PendingRegistrationRequest request, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.ApplicationId) ||
                string.IsNullOrWhiteSpace(request.DisplayName) ||
                string.IsNullOrWhiteSpace(request.InstanceId))
                return Results.BadRequest(new { error = "applicationId, displayName and instanceId are required." });

            PendingRegistration record = await registrationStore.EnsurePendingAsync(
                request.ApplicationId.Trim(),
                request.DisplayName.Trim(),
                request.InstanceId.Trim(),
                ct);

            return Results.Ok(new
            {
                registrationId = record.RegistrationId,
                pin = record.Pin,
                state = record.State,
                createdAtUtc = record.CreatedAtUtc
            });
        }).AllowAnonymous();

        app.MapGet("/api/registration/pending/{registrationId}/credential", async (
            string registrationId,
            string pin,
            CancellationToken ct) =>
        {
            CredentialRetrieval result = await registrationStore.RetrieveCredentialAsync(registrationId, pin, ct);
            return result.State switch
            {
                RegistrationRecordState.Pending => Results.Accepted(value: new { state = "Pending" }),
                RegistrationRecordState.Revoked => Results.StatusCode(StatusCodes.Status410Gone),
                RegistrationRecordState.Purged => Results.NotFound(),
                RegistrationRecordState.Approved when result.Credential is not null => Results.Ok(new { credential = result.Credential }),
                _ => Results.Unauthorized()
            };
        }).AllowAnonymous();

        app.MapGet("/api/registration/pending", async (HttpContext context, CancellationToken ct) =>
        {
            if (!Has(context, "Configuration.Manage"))
                return Results.Forbid();

            return Results.Ok(await registrationStore.ListAsync(ct));
        });

        app.MapPost("/api/registration/pending/{registrationId}/approve", async (
            HttpContext context,
            string registrationId,
            CancellationToken ct) =>
        {
            if (!Has(context, "Configuration.Manage"))
                return Results.Forbid();

            PendingRegistration? approved = await registrationStore.ApproveAsync(
                registrationId,
                Actor(context),
                ct);
            return approved is null ? Results.NotFound() : Results.Ok(new
            {
                approved = true,
                approved.RegistrationId,
                approved.ApplicationId,
                approved.InstanceId,
                approved.State,
                approved.ApprovedAtUtc
            });
        });

        app.MapPost("/api/registration/{registrationId}/revoke", async (
            HttpContext context,
            string registrationId,
            CancellationToken ct) =>
        {
            if (!Has(context, "Configuration.Manage"))
                return Results.Forbid();

            PendingRegistration? revoked = await registrationStore.RevokeAsync(
                registrationId,
                Actor(context),
                ct);
            return revoked is null ? Results.NotFound() : Results.Ok(new
            {
                revoked = true,
                revoked.RegistrationId,
                revoked.ApplicationId,
                revoked.State,
                revoked.RevokedAtUtc
            });
        });

        app.MapDelete("/api/registration/{registrationId}", async (
            HttpContext context,
            string registrationId,
            CancellationToken ct) =>
        {
            if (!Has(context, "Configuration.Manage"))
                return Results.Forbid();

            PurgeResult result = await registrationStore.PurgeAsync(
                registrationId,
                Actor(context),
                ct);
            return result switch
            {
                PurgeResult.NotFound => Results.NotFound(),
                PurgeResult.MustRevokeFirst => Results.Conflict(new { error = "Registration must be revoked before purge." }),
                _ => Results.NoContent()
            };
        });

        app.MapPost("/api/registration/contracts", async (
            HttpContext context,
            IHttpClientFactory factory,
            CancellationToken ct) =>
        {
            string applicationId = context.Request.Headers["X-Aegis-Application-Id"].ToString();
            string instanceId = context.Request.Headers["X-Aegis-Instance-Id"].ToString();
            string credential = Bearer(context.Request.Headers.Authorization.ToString());
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
                return Results.StatusCode(StatusCodes.Status410Gone);
            if (validation != RegistrationValidation.Valid)
                return Results.Unauthorized();

            JsonNode? contract = await JsonNode.ParseAsync(context.Request.Body, cancellationToken: ct);
            if (contract is null)
                return Results.BadRequest(new { error = "Configuration contract body is required." });

            HttpClient client = factory.CreateClient("ops");
            using var forward = new HttpRequestMessage(
                HttpMethod.Post,
                configurationUrl.TrimEnd('/') + "/api/contracts/register")
            {
                Content = JsonContent.Create(contract)
            };

            string? configurationRegistrationKey = app.Configuration["Operations:ConfigurationRegistrationKey"];
            if (string.IsNullOrWhiteSpace(configurationRegistrationKey))
                return Results.Problem(
                    "Operations cannot forward approved application contracts because its Configuration registration credential is not configured.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);

            forward.Headers.TryAddWithoutValidation("X-Aegis-Registration-Key", configurationRegistrationKey);
            using HttpResponseMessage response = await client.SendAsync(forward, ct);
            string body = await response.Content.ReadAsStringAsync(ct);
            return Results.Content(body, response.Content.Headers.ContentType?.ToString() ?? "application/json", statusCode: (int)response.StatusCode);
        }).AllowAnonymous();

        app.MapGet("/", () => Results.Content(
            OperationsPage.Render(configurationUrl, diagnosticsUrl, site, environment),
            "text/html"));

        app.MapAegisHealth("Aegis.Operations");
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
        context.Items.TryGetValue("SuiteIdentity", out object? value) && value is SuiteIdentity identity
            ? identity.UserName ?? "unknown"
            : "unknown";

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

    private sealed record PendingRegistrationRequest(string ApplicationId, string DisplayName, string InstanceId);

    private enum RegistrationRecordState
    {
        Pending,
        Approved,
        Revoked,
        Purged
    }

    private enum RegistrationValidation
    {
        Valid,
        Invalid,
        Revoked
    }

    private enum PurgeResult
    {
        Purged,
        NotFound,
        MustRevokeFirst
    }

    private sealed record CredentialRetrieval(RegistrationRecordState State, string? Credential);

    private sealed record RegistrationAudit(
        DateTimeOffset AtUtc,
        string Action,
        string Actor);

    private sealed record PendingRegistration(
        string RegistrationId,
        string ApplicationId,
        string DisplayName,
        string InstanceId,
        string Pin,
        RegistrationRecordState State,
        string? CredentialHash,
        string? CredentialForRetrieval,
        bool CredentialRetrieved,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? ApprovedAtUtc,
        DateTimeOffset? RevokedAtUtc,
        List<RegistrationAudit> Audit);

    private sealed class RegistrationStore
    {
        private readonly string _directory;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        public RegistrationStore(string directory) => _directory = directory;

        public async Task<PendingRegistration> EnsurePendingAsync(
            string applicationId,
            string displayName,
            string instanceId,
            CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try
            {
                foreach (PendingRegistration item in await LoadAllUnsafeAsync(ct))
                {
                    if (string.Equals(item.ApplicationId, applicationId, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(item.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase) &&
                        item.State is RegistrationRecordState.Pending or RegistrationRecordState.Approved)
                        return item;
                }

                var record = new PendingRegistration(
                    Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant(),
                    applicationId,
                    displayName,
                    instanceId,
                    RandomNumberGenerator.GetInt32(100000, 1000000).ToString(),
                    RegistrationRecordState.Pending,
                    null,
                    null,
                    false,
                    DateTimeOffset.UtcNow,
                    null,
                    null,
                    [new(DateTimeOffset.UtcNow, "PendingCreated", applicationId)]);

                await SaveUnsafeAsync(record, ct);
                return record;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<IReadOnlyList<object>> ListAsync(CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try
            {
                return (await LoadAllUnsafeAsync(ct))
                    .Where(x => x.State != RegistrationRecordState.Purged)
                    .OrderByDescending(x => x.CreatedAtUtc)
                    .Select(x => (object)new
                    {
                        x.RegistrationId,
                        x.ApplicationId,
                        x.DisplayName,
                        x.InstanceId,
                        x.Pin,
                        state = x.State.ToString(),
                        x.CreatedAtUtc,
                        x.ApprovedAtUtc,
                        x.RevokedAtUtc,
                        credentialStored = x.CredentialHash is not null,
                        x.CredentialRetrieved,
                        x.Audit
                    })
                    .ToList();
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<PendingRegistration?> ApproveAsync(string id, string actor, CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try
            {
                PendingRegistration? record = await LoadUnsafeAsync(id, ct);
                if (record is null || record.State != RegistrationRecordState.Pending)
                    return record;

                string credential = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
                List<RegistrationAudit> audit = [.. record.Audit, new(DateTimeOffset.UtcNow, "Approved", actor)];
                record = record with
                {
                    State = RegistrationRecordState.Approved,
                    CredentialHash = Hash(credential),
                    CredentialForRetrieval = credential,
                    CredentialRetrieved = false,
                    ApprovedAtUtc = DateTimeOffset.UtcNow,
                    Audit = audit
                };
                await SaveUnsafeAsync(record, ct);
                return record with { CredentialForRetrieval = null };
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<CredentialRetrieval> RetrieveCredentialAsync(string id, string pin, CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try
            {
                PendingRegistration? record = await LoadUnsafeAsync(id, ct);
                if (record is null)
                    return new(RegistrationRecordState.Purged, null);
                if (!FixedEquals(record.Pin, pin))
                    return new(RegistrationRecordState.Purged, null);
                if (record.State != RegistrationRecordState.Approved)
                    return new(record.State, null);
                if (record.CredentialRetrieved || string.IsNullOrWhiteSpace(record.CredentialForRetrieval))
                    return new(RegistrationRecordState.Approved, null);

                string credential = record.CredentialForRetrieval;
                record = record with
                {
                    CredentialForRetrieval = null,
                    CredentialRetrieved = true,
                    Audit = [.. record.Audit, new(DateTimeOffset.UtcNow, "CredentialRetrieved", record.ApplicationId)]
                };
                await SaveUnsafeAsync(record, ct);
                return new(RegistrationRecordState.Approved, credential);
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
                string hash = Hash(credential);
                foreach (PendingRegistration record in await LoadAllUnsafeAsync(ct))
                {
                    if (!string.Equals(record.ApplicationId, applicationId, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(record.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (record.State == RegistrationRecordState.Revoked)
                        return RegistrationValidation.Revoked;
                    if (record.State == RegistrationRecordState.Approved &&
                        record.CredentialHash is not null &&
                        FixedEquals(record.CredentialHash, hash))
                        return RegistrationValidation.Valid;
                }
                return RegistrationValidation.Invalid;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<PendingRegistration?> RevokeAsync(string id, string actor, CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try
            {
                PendingRegistration? record = await LoadUnsafeAsync(id, ct);
                if (record is null)
                    return null;
                if (record.State == RegistrationRecordState.Revoked)
                    return record with { CredentialForRetrieval = null };

                record = record with
                {
                    State = RegistrationRecordState.Revoked,
                    CredentialHash = null,
                    CredentialForRetrieval = null,
                    RevokedAtUtc = DateTimeOffset.UtcNow,
                    Audit = [.. record.Audit, new(DateTimeOffset.UtcNow, "Revoked", actor)]
                };
                await SaveUnsafeAsync(record, ct);
                return record;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<PurgeResult> PurgeAsync(string id, string actor, CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try
            {
                PendingRegistration? record = await LoadUnsafeAsync(id, ct);
                if (record is null)
                    return PurgeResult.NotFound;
                if (record.State != RegistrationRecordState.Revoked)
                    return PurgeResult.MustRevokeFirst;

                string auditPath = Path.Combine(_directory, "audit.log");
                string auditLine = JsonSerializer.Serialize(
                    new
                    {
                        atUtc = DateTimeOffset.UtcNow,
                        action = "Purged",
                        actor,
                        record.RegistrationId,
                        record.ApplicationId,
                        record.InstanceId
                    },
                    JsonOptions);
                await File.AppendAllTextAsync(auditPath, auditLine + Environment.NewLine, ct);
                SecureFile(auditPath);
                File.Delete(PathFor(id));
                return PurgeResult.Purged;
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task<List<PendingRegistration>> LoadAllUnsafeAsync(CancellationToken ct)
        {
            var result = new List<PendingRegistration>();
            foreach (string file in Directory.EnumerateFiles(_directory, "*.json"))
            {
                PendingRegistration? item = JsonSerializer.Deserialize<PendingRegistration>(
                    await File.ReadAllTextAsync(file, ct),
                    JsonOptions);
                if (item is not null)
                    result.Add(item);
            }
            return result;
        }

        private async Task<PendingRegistration?> LoadUnsafeAsync(string id, CancellationToken ct)
        {
            string path = PathFor(id);
            if (!File.Exists(path))
                return null;
            return JsonSerializer.Deserialize<PendingRegistration>(
                await File.ReadAllTextAsync(path, ct),
                JsonOptions);
        }

        private async Task SaveUnsafeAsync(PendingRegistration record, CancellationToken ct)
        {
            string path = PathFor(record.RegistrationId);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(record, JsonOptions), ct);
            SecureFile(path);
        }

        private string PathFor(string id)
        {
            if (id.Any(ch => !char.IsLetterOrDigit(ch)))
                throw new ArgumentException("Invalid registration id.", nameof(id));
            return Path.Combine(_directory, id + ".json");
        }

        private static string Hash(string value) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

        private static bool FixedEquals(string expected, string actual)
        {
            byte[] a = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
            byte[] b = SHA256.HashData(Encoding.UTF8.GetBytes(actual));
            return CryptographicOperations.FixedTimeEquals(a, b);
        }

        private static void SecureFile(string path)
        {
            if (OperatingSystem.IsWindows())
                return;
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
