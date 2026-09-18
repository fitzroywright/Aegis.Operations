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

        int pendingLifetimeMinutes = app.Configuration.GetValue<int?>("Operations:RegistrationPendingLifetimeMinutes") ?? 30;
        int retrievedLifetimeMinutes = app.Configuration.GetValue<int?>("Operations:RegistrationRetrievedLifetimeMinutes") ?? 60;
        if (pendingLifetimeMinutes <= 0) pendingLifetimeMinutes = 30;
        if (retrievedLifetimeMinutes <= 0) retrievedLifetimeMinutes = 60;

        app.Logger.LogInformation(
            "Registration key lifetimes: pending={PendingLifetimeMinutes} minutes, retrieved-unused={RetrievedLifetimeMinutes} minutes.",
            pendingLifetimeMinutes,
            retrievedLifetimeMinutes);

        Directory.CreateDirectory(registrationDirectory);
        var registrationStore = new RegistrationStore(
            registrationDirectory,
            TimeSpan.FromMinutes(pendingLifetimeMinutes),
            TimeSpan.FromMinutes(retrievedLifetimeMinutes));
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
                try
                {
                    operationsCredential = await registrationStore.EnsureControlPlaneCredentialAsync(
                        "Aegis.Operations",
                        "Aegis Operations",
                        operationsInstanceId,
                        operationsCredentialFile,
                        stoppingToken);
                    registrationLogger.LogInformation(
                        "Aegis.Operations control-plane registration is active for instance {InstanceId}.",
                        operationsInstanceId);

                    await PublishOperationsContractAsync(
                        factory,
                        operationsLocalUrl,
                        operationsInstanceId,
                        operationsCredential,
                        site,
                        environment,
                        stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    registrationLogger.LogError(exception, "Aegis.Operations self-registration failed.");
                }

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
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
                        registrationLogger.LogWarning(exception, "Unexpected failure during Configuration reconciliation.");
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

        app.MapGet("/api/summary", async (IHttpClientFactory factory, CancellationToken ct) =>
        {
            HttpClient client = factory.CreateClient("ops");
            return Results.Ok(new
            {
                site,
                environment,
                configuration = await ProbeAsync(client, configurationUrl, ct),
                diagnostics = await ProbeAsync(client, diagnosticsUrl, ct),
                registeredApplications = await registrationStore.CountRegisteredAsync(ct),
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
            string attemptId = Guid.NewGuid().ToString("N");
            using IDisposable? logScope = registrationLogger.BeginScope(new Dictionary<string, object?>
            {
                ["Activity"] = "Registration",
                ["Application"] = request.ApplicationId,
                ["RegistrationAttemptId"] = attemptId,
                ["Stage"] = "PendingRegistration"
            });
            registrationLogger.LogInformation("Pending registration creation requested for instance {InstanceId}.", request.InstanceId);
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
            registrationLogger.LogInformation("Pending registration exists or was created. RegistrationId={RegistrationId}.", record.RegistrationId);
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
                keyRetrievalUrl = retrieveUrl,
                curlCommand = curl,
                createdAtUtc = record.CreatedAtUtc,
                expiresAtUtc = record.CreatedAtUtc.AddMinutes(pendingLifetimeMinutes)
            });
        });

        app.MapGet("/api/registration/pending/{registrationId}/key", async (
            HttpContext context,
            string registrationId,
            string pin,
            CancellationToken ct) =>
        {
            using IDisposable? logScope = registrationLogger.BeginScope(new Dictionary<string, object?>
            {
                ["Activity"] = "Registration",
                ["Application"] = "Aegis.Operations",
                ["RegistrationAttemptId"] = registrationId,
                ["Stage"] = "KeyRetrieval"
            });
            registrationLogger.LogInformation("Registration key retrieval requested.");
            context.Response.Headers.CacheControl = "no-store";
            CredentialRetrieval result = await registrationStore.RetrieveKeyAsync(registrationId, pin, ct);
            return result.State switch
            {
                RegistrationRecordState.Pending when result.Credential is not null =>
                    Results.Text(result.Credential, "text/plain; charset=utf-8"),
                RegistrationRecordState.KeyRetrieved or RegistrationRecordState.Registered =>
                    Results.Conflict(new { error = "Registration key has already been retrieved. It is not displayed again." }),
                RegistrationRecordState.Revoked or RegistrationRecordState.Expired =>
                    Results.StatusCode(StatusCodes.Status410Gone),
                _ => Results.Unauthorized()
            };
        }).AllowAnonymous();

        app.MapPost("/api/registration/validate", async (
            HttpContext context,
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
                applicationId.Trim(),
                instanceId.Trim(),
                credential,
                ct);

            return validation switch
            {
                RegistrationValidation.Valid => Results.Ok(new
                {
                    valid = true,
                    applicationId = applicationId.Trim(),
                    instanceId = instanceId.Trim()
                }),
                RegistrationValidation.Revoked => Results.StatusCode(StatusCodes.Status410Gone),
                _ => Results.Unauthorized()
            };
        }).AllowAnonymous();

        app.MapPost("/api/registration/control-plane/auto", async (
            HttpContext context,
            PendingRegistrationRequest request,
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

        app.MapGet("/api/registration/pending", async (HttpContext context, CancellationToken ct) =>
        {
            if (!Has(context, "Configuration.Manage"))
                return Results.Forbid();
            return Results.Ok(await registrationStore.ListAsync(ct));
        });

        app.MapPost("/api/registration/{registrationId}/revoke", async (
            HttpContext context,
            string registrationId,
            IHttpClientFactory factory,
            CancellationToken ct) =>
        {
            if (!Has(context, "Configuration.Manage"))
                return Results.Forbid();

            PendingRegistration? revoked = await registrationStore.RevokeAsync(
                registrationId,
                Actor(context),
                "Revoked",
                ct);

            if (revoked is null) return Results.NotFound();

            bool hasOtherRegisteredInstance = await registrationStore.HasRegisteredApplicationAsync(revoked.ApplicationId, ct);
            if (!hasOtherRegisteredInstance)
            {
                ConfigurationSyncResult sync = await SyncConfigurationContractAsync(
                    app, factory, configurationUrl, operationsInstanceId, operationsCredentialFile, revoked.ApplicationId, "retire", "revoked", ct);
                if (!sync.Success)
                    return Results.Problem(
                        $"Registration was revoked, but Configuration synchronization failed: {sync.Message}",
                        statusCode: StatusCodes.Status502BadGateway);
            }

            return Results.Ok(new
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
            IHttpClientFactory factory,
            CancellationToken ct) =>
        {
            if (!Has(context, "Configuration.Manage"))
                return Results.Forbid();

            PendingRegistration? revoked = await registrationStore.RevokeAsync(
                registrationId,
                Actor(context),
                "Invalidated",
                ct);

            if (revoked is null) return Results.NotFound();

            bool hasOtherRegisteredInstance = await registrationStore.HasRegisteredApplicationAsync(revoked.ApplicationId, ct);
            if (!hasOtherRegisteredInstance)
            {
                ConfigurationSyncResult sync = await SyncConfigurationContractAsync(
                    app, factory, configurationUrl, operationsInstanceId, operationsCredentialFile, revoked.ApplicationId, "retire", "invalidated", ct);
                if (!sync.Success)
                    return Results.Problem(
                        $"Registration was invalidated, but Configuration synchronization failed: {sync.Message}",
                        statusCode: StatusCodes.Status502BadGateway);
            }

            return Results.Ok(new
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
            IHttpClientFactory factory,
            CancellationToken ct) =>
        {
            if (!Has(context, "Configuration.Manage"))
                return Results.Forbid();

            PendingRegistration? record = await registrationStore.GetAsync(registrationId, ct);
            if (record is null) return Results.NotFound();
            if (record.State is not (RegistrationRecordState.Revoked or RegistrationRecordState.Expired))
                return Results.Conflict(new { error = "Registration must be revoked, invalidated, or expired before purge." });

            bool hasOtherRegisteredInstance = await registrationStore.HasRegisteredApplicationAsync(record.ApplicationId, ct);

            PurgeResult result = await registrationStore.PurgeAsync(
                registrationId,
                Actor(context),
                ct);

            if (result == PurgeResult.NotFound)
                return Results.NotFound();
            if (result == PurgeResult.MustRevokeFirst)
                return Results.Conflict(new { error = "Registration must be revoked or invalidated before purge." });

            bool configurationSynchronized = true;
            string? synchronizationWarning = null;
            if (!hasOtherRegisteredInstance)
            {
                ConfigurationSyncResult sync = await SyncConfigurationContractAsync(
                    app, factory, configurationUrl, operationsInstanceId, operationsCredentialFile, record.ApplicationId, "purge", null, ct);
                configurationSynchronized = sync.Success;
                if (!sync.Success)
                {
                    synchronizationWarning =
                        $"Registration was purged, but Configuration cleanup failed and will be retried by reconciliation: {sync.Message}";
                    registrationLogger.LogWarning(
                        "Registration {RegistrationId} was purged, but Configuration cleanup failed for {ApplicationId}: {Message}",
                        record.RegistrationId,
                        record.ApplicationId,
                        sync.Message);
                }
            }

            return Results.Ok(new
            {
                purged = true,
                record.RegistrationId,
                record.ApplicationId,
                configurationSynchronized,
                warning = synchronizationWarning
            });
        });

        app.MapPost("/api/registration/reconcile", async (
            HttpContext context,
            IHttpClientFactory factory,
            CancellationToken ct) =>
        {
            if (!Has(context, "Configuration.Manage"))
                return Results.Forbid();

            IReadOnlyList<string> activeApplicationIds = await registrationStore.RegisteredApplicationIdsAsync(ct);
            ConfigurationSyncResult sync = await ReconcileConfigurationAsync(
                app, factory, configurationUrl, operationsInstanceId, await ReadCredentialFileAsync(operationsCredentialFile, ct), activeApplicationIds, ct);

            return sync.Success
                ? Results.Ok(new { activeApplicationIds, synchronized = true })
                : Results.Problem(sync.Message, statusCode: StatusCodes.Status502BadGateway);
        });

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
button:disabled{opacity:.45;cursor:not-allowed}
pre{white-space:pre-wrap;word-break:break-all;background:#06111b;padding:12px;border-radius:5px;border:1px solid #29475b}
table{width:100%;border-collapse:collapse}th,td{text-align:left;padding:9px;border-bottom:1px solid #29475b}.muted{color:#a9c0d1}
.actions{display:flex;gap:6px;flex-wrap:wrap}.danger{background:#a63d3d}.warn{background:#9a6a14}.purge{background:#6f2c2c}.ok{background:#1b7a50}
.badge{display:inline-block;padding:4px 8px;border-radius:999px;font-size:12px;font-weight:700;background:#24394a}
.badge.registered{background:#145b40}.badge.pending{background:#4b5563}.badge.keyretrieved{background:#4b3f72}.badge.revoked{background:#7d2f2f}.badge.expired{background:#6b4b17}
#message{margin-top:10px}.error{color:#ffb4b4}.success{color:#9be7bd}
</style>
</head>
<body><main>
<p><a href="/">← Operations</a></p>
<h1>Application Registration</h1>
<p class="muted">Create the pending registration before installing the application. The application never creates its own pending registration.</p>
<section class="panel">
<h2>Create Pending Registration</h2>
<label>Application ID<input id="applicationId" placeholder=""></label>
<label>Display Name<input id="displayName" placeholder=""></label>
<label>Instance ID<input id="instanceId" placeholder=""></label>
<button onclick="createRegistration()">Create Pending Registration</button>
<div id="created"></div>
</section>
<section class="panel">
<h2>Registrations</h2>
<p class="muted">Revoke or invalidate an active registration before purging it. Purge permanently removes the registration record.</p>
<button onclick="reconcile()">Reconcile Configuration</button>
<div id="message"></div>
<table><thead><tr><th>Application</th><th>Instance</th><th>State</th><th>Created</th><th>Expires</th><th>Actions</th></tr></thead><tbody id="rows"></tbody></table>
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
 '<p><b>Retrieve registration key:</b></p><pre><a href="'+e(d.keyRetrievalUrl)+'" target="_blank" rel="noopener noreferrer" title="Open key retrieval in a new window">'+e(d.curlCommand)+'</a></pre>'+
 '<p><b>Designated environment variable:</b> <code>'+e(d.environmentVariable)+'</code></p>'+
 '<p class="muted">Run the curl command on the target machine, save the returned key in the environment variable, then install/start the application. The key is returned only once.</p>';
 load();
}
function notice(text,ok=false){message.innerHTML='<p class="'+(ok?'success':'error')+'">'+e(text)+'</p>'}
async function action(id,kind,label){
 if(!confirm(label+' this registration?')) return;
 try{
   const r=await fetch('/api/registration/'+encodeURIComponent(id)+'/'+kind,{method:'POST'});
   if(!r.ok){
     let d={}; try{d=await r.json()}catch{}
     notice(d.detail||d.error||label+' failed.');
     return;
   }
   notice('Registration '+label.toLowerCase()+'d successfully.',true);
 } finally {
   await load();
 }
}
async function reconcile(){
 const r=await fetch('/api/registration/reconcile',{method:'POST'});
 if(!r.ok){
   let d={}; try{d=await r.json()}catch{}
   notice(d.detail||d.error||'Configuration reconciliation failed.');
   return;
 }
 notice('Configuration contracts reconciled with registered applications.',true);
 await load();
}
async function purge(id){
 if(!confirm('Permanently purge this revoked registration? This cannot be undone.')) return;
 try{
   const r=await fetch('/api/registration/'+encodeURIComponent(id),{method:'DELETE'});
   let d={}; try{d=await r.json()}catch{}
   if(!r.ok){
     notice(d.detail||d.error||'Purge failed.');
     return;
   }
   notice(d.warning||'Registration purged permanently.',true);
 } finally {
   await load();
 }
}
function actionsFor(x){
 const id=e(x.registrationId);
 const state=String(x.state||'').toLowerCase();
 if(state==='revoked'||state==='expired'){
   return '<button class="purge" onclick="purge(\''+id+'\')">Purge</button>';
 }
 return '<button class="danger" onclick="action(\''+id+'\',\'revoke\',\'Revoke\')">Revoke</button>'+
        '<button class="warn" onclick="action(\''+id+'\',\'invalidate\',\'Invalidate\')">Invalidate</button>'+
        '<button class="purge" disabled title="Revoke or invalidate before purge">Purge</button>';
}
async function load(){
 const r=await fetch('/api/registration/pending',{cache:'no-store'});
 if(!r.ok){notice('Unable to load registrations.');return;}
 const d=await r.json();
 rows.innerHTML=d.map(x=>{
   const state=String(x.state||'');
   const css=state.toLowerCase();
   return '<tr><td><b>'+e(x.displayName)+'</b><br><span class="muted">'+e(x.applicationId)+'</span></td>'+
   '<td>'+e(x.instanceId)+'</td><td><span class="badge '+e(css)+'">'+e(state)+'</span></td><td>'+e(new Date(x.createdAtUtc).toLocaleString())+'</td>'+
   '<td>'+(x.expiresAtUtc?e(new Date(x.expiresAtUtc).toLocaleString()):'—')+'</td>'+
   '<td class="actions">'+actionsFor(x)+'</td></tr>';
 }).join('');
}
load();
</script>
</main></body></html>
""";

    private sealed record ConfigurationSyncResult(bool Success, string Message);

    private static async Task<ConfigurationSyncResult> SyncConfigurationContractAsync(
        WebApplication app,
        IHttpClientFactory factory,
        string configurationUrl,
        string operationsInstanceId,
        string operationsCredentialFile,
        string applicationId,
        string action,
        string? reason,
        CancellationToken ct)
    {
        string? key = await ReadCredentialFileAsync(operationsCredentialFile, ct);
        if (string.IsNullOrWhiteSpace(key))
            return new(false, "Aegis.Operations self-registration credential is unavailable.");

        string url = configurationUrl.TrimEnd('/') + "/api/contracts/" + Uri.EscapeDataString(applicationId);
        HttpMethod method;
        if (action == "retire")
        {
            method = HttpMethod.Post;
            url += "/retire";
            if (!string.IsNullOrWhiteSpace(reason))
                url += "?reason=" + Uri.EscapeDataString(reason);
        }
        else
        {
            method = HttpMethod.Delete;
        }

        using var request = new HttpRequestMessage(method, url);
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
                return new(false, "Configuration redirected the lifecycle API request to interactive login.");

            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
                return new(true, response.StatusCode == HttpStatusCode.NotFound ? "Contract was already absent." : "Configuration synchronized.");

            return new(false, $"Configuration returned HTTP {(int)response.StatusCode}.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new(false, ex.Message);
        }
    }

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

    private static async Task PublishOperationsContractAsync(
        IHttpClientFactory factory,
        string operationsLocalUrl,
        string instanceId,
        string credential,
        string site,
        string environment,
        CancellationToken ct)
    {
        JsonObject contract = new()
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
                    ["sensitive"] = false
                },
                new JsonObject
                {
                    ["id"] = "diagnostics-url",
                    ["displayName"] = "Diagnostics URL",
                    ["kind"] = 0,
                    ["required"] = true,
                    ["purpose"] = "Aegis.Diagnostics endpoint used by Operations.",
                    ["configurationKey"] = "Operations:DiagnosticsUrl",
                    ["sensitive"] = false
                },
                new JsonObject
                {
                    ["id"] = "registration-store",
                    ["displayName"] = "Registration Store",
                    ["kind"] = 0,
                    ["required"] = true,
                    ["purpose"] = "Persistent Operations registration state.",
                    ["configurationKey"] = "Operations:RegistrationDirectory",
                    ["sensitive"] = false
                }
            }
        };

        using HttpClient client = factory.CreateClient("ops");
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            operationsLocalUrl.TrimEnd('/') + "/api/registration/contracts")
        {
            Content = JsonContent.Create(contract)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential);
        request.Headers.TryAddWithoutValidation("X-Aegis-Application-Id", "Aegis.Operations");
        request.Headers.TryAddWithoutValidation("X-Aegis-Instance-Id", instanceId);
        request.Headers.TryAddWithoutValidation("X-Aegis-Registration-Attempt-Id", Guid.NewGuid().ToString("N"));
        using HttpResponseMessage response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
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
        Revoked,
        Expired
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

    private sealed record ControlPlaneProvisionResult(bool Succeeded, string? RegistrationId, string? Credential, string? Error);

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
        DateTimeOffset? ExpiredAtUtc,
        List<RegistrationAudit> Audit);

    private sealed record CreatePendingResult(
        PendingRegistration? Record,
        bool Conflict,
        RegistrationRecordState? ExistingState);

    private sealed class RegistrationStore
    {
        private readonly string _directory;
        private readonly TimeSpan _pendingLifetime;
        private readonly TimeSpan _retrievedLifetime;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        public RegistrationStore(string directory, TimeSpan pendingLifetime, TimeSpan retrievedLifetime)
        {
            _directory = directory;
            _pendingLifetime = pendingLifetime;
            _retrievedLifetime = retrievedLifetime;
        }

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
                        x.State is not (RegistrationRecordState.Revoked or RegistrationRecordState.Expired));

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

        public async Task<PendingRegistration?> GetAsync(string id, CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try
            {
                return await LoadUnsafeAsync(id, ct);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<bool> HasRegisteredApplicationAsync(string applicationId, CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try
            {
                return (await LoadAllUnsafeAsync(ct)).Any(x =>
                    x.State == RegistrationRecordState.Registered &&
                    string.Equals(x.ApplicationId, applicationId, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<IReadOnlyList<string>> RegisteredApplicationIdsAsync(CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try
            {
                return (await LoadAllUnsafeAsync(ct))
                    .Where(x => x.State == RegistrationRecordState.Registered)
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
                return (await LoadAllUnsafeAsync(ct))
                    .Count(x => x.State == RegistrationRecordState.Registered);
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
                        x.ExpiredAtUtc,
                        expiresAtUtc = x.State switch
                        {
                            RegistrationRecordState.Pending => x.CreatedAtUtc + _pendingLifetime,
                            RegistrationRecordState.KeyRetrieved when x.KeyRetrievedAtUtc.HasValue => x.KeyRetrievedAtUtc.Value + _retrievedLifetime,
                            _ => (DateTimeOffset?)null
                        },
                        x.Audit
                    })
                    .ToList();
            }
            finally
            {
                _gate.Release();
            }
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
                List<PendingRegistration> all = await LoadAllUnsafeAsync(ct);
                foreach (PendingRegistration existing in all.Where(x =>
                    string.Equals(x.ApplicationId, applicationId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(x.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase) &&
                    x.State is not (RegistrationRecordState.Revoked or RegistrationRecordState.Expired)))
                {
                    PendingRegistration revoked = existing with
                    {
                        State = RegistrationRecordState.Revoked,
                        CredentialForRetrieval = null,
                        RevokedAtUtc = DateTimeOffset.UtcNow,
                        Audit = [.. existing.Audit, new(DateTimeOffset.UtcNow, "ControlPlaneAutoRotated", applicationId)]
                    };
                    await SaveUnsafeAsync(revoked, ct);
                }

                string credential = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
                DateTimeOffset now = DateTimeOffset.UtcNow;
                PendingRegistration record = new(
                    Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant(),
                    applicationId,
                    displayName,
                    instanceId,
                    string.Empty,
                    RegistrationRecordState.Registered,
                    Hash(credential),
                    null,
                    now,
                    now,
                    now,
                    null,
                    null,
                    [new(now, "ControlPlaneAutoRegistered", applicationId)]);
                await SaveUnsafeAsync(record, ct);
                return new(true, record.RegistrationId, credential, null);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<string> EnsureControlPlaneCredentialAsync(
            string applicationId,
            string displayName,
            string instanceId,
            string credentialFile,
            CancellationToken ct)
        {
            if (File.Exists(credentialFile))
            {
                string existingCredential = (await File.ReadAllTextAsync(credentialFile, ct)).Trim();
                if (!string.IsNullOrWhiteSpace(existingCredential))
                {
                    RegistrationValidation validation = await ValidateAsync(applicationId, instanceId, existingCredential, ct);
                    if (validation == RegistrationValidation.Valid)
                        return existingCredential;
                }
            }

            ControlPlaneProvisionResult result = await ProvisionControlPlaneAsync(
                applicationId,
                displayName,
                instanceId,
                ct);
            if (!result.Succeeded || string.IsNullOrWhiteSpace(result.Credential))
                throw new InvalidOperationException(result.Error ?? "Control-plane credential provisioning failed.");

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(credentialFile))!);
            await File.WriteAllTextAsync(credentialFile, result.Credential + Environment.NewLine, ct);
            SecureFile(credentialFile);
            return result.Credential;
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
                        !string.Equals(record.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase) ||
                        !FixedEquals(record.CredentialHash, hash))
                        continue;

                    if (record.State == RegistrationRecordState.Revoked)
                        return RegistrationValidation.Revoked;

                    if (record.State is RegistrationRecordState.KeyRetrieved or RegistrationRecordState.Registered)
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

                    if (record.State == RegistrationRecordState.Registered)
                        return true;

                    if (record.State != RegistrationRecordState.KeyRetrieved)
                        return false;

                    PendingRegistration registered = record with
                    {
                        State = RegistrationRecordState.Registered,
                        RegisteredAtUtc = DateTimeOffset.UtcNow,
                        Audit = [.. record.Audit, new(DateTimeOffset.UtcNow, "Registered", applicationId)]
                    };
                    await SaveUnsafeAsync(registered, ct);
                    return true;
                }

                return false;
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
                if (record.State is not (RegistrationRecordState.Revoked or RegistrationRecordState.Expired))
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
                if (item is null)
                    continue;

                item = await ApplyExpirationUnsafeAsync(item, ct);
                result.Add(item);
            }
            return result;
        }

        private async Task<PendingRegistration?> LoadUnsafeAsync(string id, CancellationToken ct)
        {
            string path = PathFor(id);
            if (!File.Exists(path))
                return null;

            PendingRegistration? record = JsonSerializer.Deserialize<PendingRegistration>(
                await File.ReadAllTextAsync(path, ct),
                JsonOptions);
            return record is null ? null : await ApplyExpirationUnsafeAsync(record, ct);
        }

        private async Task<PendingRegistration> ApplyExpirationUnsafeAsync(
            PendingRegistration record,
            CancellationToken ct)
        {
            if (record.State is RegistrationRecordState.Registered or RegistrationRecordState.Revoked or RegistrationRecordState.Expired)
                return record;

            DateTimeOffset now = DateTimeOffset.UtcNow;
            DateTimeOffset? expiresAt = record.State switch
            {
                RegistrationRecordState.Pending => record.CreatedAtUtc + _pendingLifetime,
                RegistrationRecordState.KeyRetrieved when record.KeyRetrievedAtUtc.HasValue =>
                    record.KeyRetrievedAtUtc.Value + _retrievedLifetime,
                _ => null
            };

            if (!expiresAt.HasValue || now < expiresAt.Value)
                return record;

            PendingRegistration expired = record with
            {
                State = RegistrationRecordState.Expired,
                CredentialForRetrieval = null,
                ExpiredAtUtc = now,
                Audit = [.. record.Audit, new(now, "Expired", "Aegis.Operations")]
            };
            await SaveUnsafeAsync(expired, ct);
            return expired;
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
