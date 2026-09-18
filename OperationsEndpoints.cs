using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Common.Diagnostics;
using Common.Security;

internal static class OperationsEndpoints
{
    private const string RegistrationEnvironmentVariable = "AEGIS_CONFIGURATION_REGISTRATION_KEY";

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

        app.MapGet("/registrations", (HttpContext context) =>
        {
            if (!Has(context, "Configuration.Manage"))
                return Results.Forbid();
            return Results.Content(RenderRegistrationsPage(), "text/html; charset=utf-8");
        });

        app.MapPost("/api/registration/pending", async (
            HttpContext context,
            PendingRegistrationRequest request,
            CancellationToken ct) =>
        {
            if (!Has(context, "Configuration.Manage"))
                return Results.Forbid();

            if (string.IsNullOrWhiteSpace(request.ApplicationId) ||
                string.IsNullOrWhiteSpace(request.DisplayName) ||
                string.IsNullOrWhiteSpace(request.InstanceId))
                return Results.BadRequest(new { error = "applicationId, displayName and instanceId are required." });

            CreatePendingResult result = await registrationStore.CreatePendingAsync(
                request.ApplicationId.Trim(),
                request.DisplayName.Trim(),
                request.InstanceId.Trim(),
                Actor(context),
                ct);

            if (result.Conflict)
            {
                return Results.Conflict(new
                {
                    error = "An active registration already exists for this application instance. Revoke or invalidate it before creating a replacement.",
                    result.ExistingState
                });
            }

            PendingRegistration record = result.Record!;
            string retrieveUrl =
                $"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}/api/registration/pending/{Uri.EscapeDataString(record.RegistrationId)}/key?pin={Uri.EscapeDataString(record.Pin)}";
            string curl = $"curl -fsS '{retrieveUrl}'";

            return Results.Ok(new
            {
                registrationId = record.RegistrationId,
                applicationId = record.ApplicationId,
                displayName = record.DisplayName,
                instanceId = record.InstanceId,
                pin = record.Pin,
                state = record.State.ToString(),
                environmentVariable = RegistrationEnvironmentVariable,
                curlCommand = curl,
                createdAtUtc = record.CreatedAtUtc
            });
        });

        app.MapGet("/api/registration/pending/{registrationId}/key", async (
            HttpContext context,
            string registrationId,
            string pin,
            CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            CredentialRetrieval result = await registrationStore.RetrieveKeyAsync(registrationId, pin, ct);
            return result.State switch
            {
                RegistrationRecordState.Pending when result.Credential is not null =>
                    Results.Text(result.Credential, "text/plain; charset=utf-8"),
                RegistrationRecordState.KeyRetrieved or RegistrationRecordState.Registered =>
                    Results.Conflict(new { error = "Registration key has already been retrieved. It is not displayed again." }),
                RegistrationRecordState.Revoked =>
                    Results.StatusCode(StatusCodes.Status410Gone),
                _ => Results.Unauthorized()
            };
        }).AllowAnonymous();

        app.MapGet("/api/registration/pending", async (HttpContext context, CancellationToken ct) =>
        {
            if (!Has(context, "Configuration.Manage"))
                return Results.Forbid();
            return Results.Ok(await registrationStore.ListAsync(ct));
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
                "Revoked",
                ct);

            return revoked is null
                ? Results.NotFound()
                : Results.Ok(new
                {
                    revoked = true,
                    revoked.RegistrationId,
                    revoked.ApplicationId,
                    state = revoked.State.ToString(),
                    revoked.RevokedAtUtc
                });
        });

        app.MapPost("/api/registration/{registrationId}/invalidate", async (
            HttpContext context,
            string registrationId,
            CancellationToken ct) =>
        {
            if (!Has(context, "Configuration.Manage"))
                return Results.Forbid();

            PendingRegistration? revoked = await registrationStore.RevokeAsync(
                registrationId,
                Actor(context),
                "Invalidated",
                ct);

            return revoked is null
                ? Results.NotFound()
                : Results.Ok(new
                {
                    invalidated = true,
                    revoked.RegistrationId,
                    revoked.ApplicationId,
                    state = revoked.State.ToString(),
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
                PurgeResult.MustRevokeFirst => Results.Conflict(new { error = "Registration must be revoked or invalidated before purge." }),
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

            RegistrationValidation validation = await registrationStore.ValidateAndMarkRegisteredAsync(
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

            string? configurationRegistrationKey = app.Configuration["Operations:ConfigurationRegistrationKey"];
            if (string.IsNullOrWhiteSpace(configurationRegistrationKey))
            {
                return Results.Problem(
                    "Operations cannot forward registered application contracts because its Configuration registration credential is not configured.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            HttpClient client = factory.CreateClient("ops");
            using var forward = new HttpRequestMessage(
                HttpMethod.Post,
                configurationUrl.TrimEnd('/') + "/api/contracts/register")
            {
                Content = JsonContent.Create(contract)
            };
            forward.Headers.TryAddWithoutValidation("X-Aegis-Registration-Key", configurationRegistrationKey);

            using HttpResponseMessage response = await client.SendAsync(forward, ct);
            string body = await response.Content.ReadAsStringAsync(ct);
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

    private static string RenderRegistrationsPage() => """
<!doctype html>
<html>
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Application Registration</title>
<style>
body{font:14px Segoe UI,Arial,sans-serif;margin:0;background:#071522;color:#eef6ff}main{max-width:1100px;margin:30px auto;padding:0 20px}
a{color:#66bfff}.panel{background:#0d2030;border:1px solid #28597b;border-radius:8px;padding:18px;margin:14px 0}
input{width:100%;box-sizing:border-box;padding:10px;margin:5px 0 12px;background:#081621;color:#fff;border:1px solid #486b83;border-radius:5px}
button{padding:10px 14px;background:#167ee6;color:#fff;border:0;border-radius:5px;cursor:pointer}
pre{white-space:pre-wrap;word-break:break-all;background:#06111b;padding:12px;border-radius:5px;border:1px solid #29475b}
table{width:100%;border-collapse:collapse}th,td{text-align:left;padding:9px;border-bottom:1px solid #29475b}.muted{color:#a9c0d1}
.actions button{margin-right:6px}.danger{background:#a63d3d}
</style>
</head>
<body><main>
<p><a href="/">← Operations</a></p>
<h1>Application Registration</h1>
<p class="muted">Create the pending registration before installing the application. The application never creates its own pending registration.</p>
<section class="panel">
<h2>Create Pending Registration</h2>
<label>Application ID<input id="applicationId" placeholder="Aegis.Cafeteria.Services"></label>
<label>Display Name<input id="displayName" placeholder="Aegis Cafeteria Services"></label>
<label>Instance ID<input id="instanceId" placeholder="kratos"></label>
<button onclick="createRegistration()">Create Pending Registration</button>
<div id="created"></div>
</section>
<section class="panel">
<h2>Registrations</h2>
<table><thead><tr><th>Application</th><th>Instance</th><th>State</th><th>Created</th><th>Actions</th></tr></thead><tbody id="rows"></tbody></table>
</section>
<script>
const e=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
async function createRegistration(){
 const body={applicationId:applicationId.value,displayName:displayName.value,instanceId:instanceId.value};
 const r=await fetch('/api/registration/pending',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify(body)});
 const d=await r.json();
 if(!r.ok){created.innerHTML='<p>'+e(d.error||'Registration could not be created.')+'</p>';return}
 created.innerHTML='<h3>Pending Registration Created</h3>'+
 '<p><b>PIN:</b> <code>'+e(d.pin)+'</code></p>'+
 '<p><b>Retrieve registration key:</b></p><pre>'+e(d.curlCommand)+'</pre>'+
 '<p><b>Designated environment variable:</b> <code>'+e(d.environmentVariable)+'</code></p>'+
 '<p class="muted">Run the curl command on the target machine, save the returned key in the environment variable, then install/start the application. The key is returned only once.</p>';
 load();
}
async function action(id,kind){await fetch('/api/registration/'+encodeURIComponent(id)+'/'+kind,{method:'POST'});load()}
async function purge(id){await fetch('/api/registration/'+encodeURIComponent(id),{method:'DELETE'});load()}
async function load(){
 const r=await fetch('/api/registration/pending',{cache:'no-store'}); if(!r.ok)return;
 const d=await r.json();
 rows.innerHTML=d.map(x=>'<tr><td><b>'+e(x.displayName)+'</b><br><span class="muted">'+e(x.applicationId)+'</span></td>'+
 '<td>'+e(x.instanceId)+'</td><td>'+e(x.state)+'</td><td>'+e(new Date(x.createdAtUtc).toLocaleString())+'</td>'+
 '<td class="actions"><button class="danger" onclick="action(\''+e(x.registrationId)+'\',\'revoke\')">Revoke</button>'+
 '<button onclick="action(\''+e(x.registrationId)+'\',\'invalidate\')">Invalidate</button>'+
 '<button onclick="purge(\''+e(x.registrationId)+'\')">Purge</button></td></tr>').join('');
}
load();
</script>
</main></body></html>
""";

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

    private sealed record PendingRegistrationRequest(string ApplicationId, string DisplayName, string InstanceId);

    private enum RegistrationRecordState
    {
        Pending,
        KeyRetrieved,
        Registered,
        Revoked
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

    private sealed record RegistrationAudit(DateTimeOffset AtUtc, string Action, string Actor);

    private sealed record PendingRegistration(
        string RegistrationId,
        string ApplicationId,
        string DisplayName,
        string InstanceId,
        string Pin,
        RegistrationRecordState State,
        string CredentialHash,
        string? CredentialForRetrieval,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? KeyRetrievedAtUtc,
        DateTimeOffset? RegisteredAtUtc,
        DateTimeOffset? RevokedAtUtc,
        List<RegistrationAudit> Audit);

    private sealed record CreatePendingResult(
        PendingRegistration? Record,
        bool Conflict,
        RegistrationRecordState? ExistingState);

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

        public async Task<CreatePendingResult> CreatePendingAsync(
            string applicationId,
            string displayName,
            string instanceId,
            string actor,
            CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try
            {
                List<PendingRegistration> all = await LoadAllUnsafeAsync(ct);
                PendingRegistration? existing = all
                    .OrderByDescending(x => x.CreatedAtUtc)
                    .FirstOrDefault(x =>
                        string.Equals(x.ApplicationId, applicationId, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(x.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase) &&
                        x.State != RegistrationRecordState.Revoked);

                if (existing is not null)
                {
                    if (existing.State == RegistrationRecordState.Pending)
                        return new(existing, false, existing.State);
                    return new(null, true, existing.State);
                }

                string credential = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
                var record = new PendingRegistration(
                    Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant(),
                    applicationId,
                    displayName,
                    instanceId,
                    RandomNumberGenerator.GetInt32(100000, 1000000).ToString(),
                    RegistrationRecordState.Pending,
                    Hash(credential),
                    credential,
                    DateTimeOffset.UtcNow,
                    null,
                    null,
                    null,
                    [new(DateTimeOffset.UtcNow, "PendingCreated", actor)]);

                await SaveUnsafeAsync(record, ct);
                return new(record, false, null);
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
                    .OrderByDescending(x => x.CreatedAtUtc)
                    .Select(x => (object)new
                    {
                        x.RegistrationId,
                        x.ApplicationId,
                        x.DisplayName,
                        x.InstanceId,
                        state = x.State.ToString(),
                        x.CreatedAtUtc,
                        x.KeyRetrievedAtUtc,
                        x.RegisteredAtUtc,
                        x.RevokedAtUtc,
                        x.Audit
                    })
                    .ToList();
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<CredentialRetrieval> RetrieveKeyAsync(
            string id,
            string pin,
            CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try
            {
                PendingRegistration? record = await LoadUnsafeAsync(id, ct);
                if (record is null)
                    return new(RegistrationRecordState.Revoked, null);
                if (!FixedEquals(record.Pin, pin))
                    return new(RegistrationRecordState.Revoked, null);
                if (record.State != RegistrationRecordState.Pending)
                    return new(record.State, null);
                if (string.IsNullOrWhiteSpace(record.CredentialForRetrieval))
                    return new(RegistrationRecordState.KeyRetrieved, null);

                string credential = record.CredentialForRetrieval;
                record = record with
                {
                    State = RegistrationRecordState.KeyRetrieved,
                    CredentialForRetrieval = null,
                    KeyRetrievedAtUtc = DateTimeOffset.UtcNow,
                    Audit = [.. record.Audit, new(DateTimeOffset.UtcNow, "KeyRetrieved", record.ApplicationId)]
                };
                await SaveUnsafeAsync(record, ct);
                return new(RegistrationRecordState.Pending, credential);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<RegistrationValidation> ValidateAndMarkRegisteredAsync(
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
                        !string.Equals(record.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase) ||
                        !FixedEquals(record.CredentialHash, hash))
                        continue;

                    if (record.State == RegistrationRecordState.Revoked)
                        return RegistrationValidation.Revoked;

                    if (record.State is RegistrationRecordState.KeyRetrieved or RegistrationRecordState.Registered)
                    {
                        if (record.State != RegistrationRecordState.Registered)
                        {
                            PendingRegistration registered = record with
                            {
                                State = RegistrationRecordState.Registered,
                                RegisteredAtUtc = DateTimeOffset.UtcNow,
                                Audit = [.. record.Audit, new(DateTimeOffset.UtcNow, "Registered", applicationId)]
                            };
                            await SaveUnsafeAsync(registered, ct);
                        }
                        return RegistrationValidation.Valid;
                    }
                }
                return RegistrationValidation.Invalid;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<PendingRegistration?> RevokeAsync(
            string id,
            string actor,
            string action,
            CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try
            {
                PendingRegistration? record = await LoadUnsafeAsync(id, ct);
                if (record is null)
                    return null;
                if (record.State == RegistrationRecordState.Revoked)
                    return record;

                record = record with
                {
                    State = RegistrationRecordState.Revoked,
                    CredentialForRetrieval = null,
                    RevokedAtUtc = DateTimeOffset.UtcNow,
                    Audit = [.. record.Audit, new(DateTimeOffset.UtcNow, action, actor)]
                };
                await SaveUnsafeAsync(record, ct);
                return record;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<PurgeResult> PurgeAsync(
            string id,
            string actor,
            CancellationToken ct)
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
                string auditLine = JsonSerializer.Serialize(new
                {
                    atUtc = DateTimeOffset.UtcNow,
                    action = "Purged",
                    actor,
                    record.RegistrationId,
                    record.ApplicationId,
                    record.InstanceId
                }, JsonOptions);
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
