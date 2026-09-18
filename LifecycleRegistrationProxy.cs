using Common.Security;

internal static class LifecycleRegistrationProxy
{
    internal static void MapLifecycleRegistrationProxy(this WebApplication app, string configurationUrl)
    {
        app.MapGet("/api/lifecycle-registrations", async (
            HttpContext context,
            SuiteSecurity suiteSecurity,
            IHttpClientFactory factory,
            CancellationToken ct) =>
        {
            if (!Has(context, "Configuration.Manage")) return Results.Forbid();
            return await ForwardAsync(context, suiteSecurity, factory, configurationUrl + "/api/registration/requests", HttpMethod.Get, null, ct);
        });

        app.MapPost("/api/lifecycle-registrations/{registrationId}/approve", async (
            HttpContext context,
            string registrationId,
            LifecycleDecision request,
            SuiteSecurity suiteSecurity,
            IHttpClientFactory factory,
            CancellationToken ct) =>
        {
            if (!Has(context, "Configuration.Manage")) return Results.Forbid();
            return await ForwardAsync(context, suiteSecurity, factory,
                configurationUrl + "/api/registration/" + Uri.EscapeDataString(registrationId) + "/approve",
                HttpMethod.Post, request, ct);
        });

        app.MapPost("/api/lifecycle-registrations/{registrationId}/reject", async (
            HttpContext context,
            string registrationId,
            LifecycleDecision request,
            IHttpClientFactory factory,
            CancellationToken ct) =>
        {
            if (!Has(context, "Configuration.Manage")) return Results.Forbid();
            return await ForwardAsync(context, suiteSecurity, factory,
                configurationUrl + "/api/registration/" + Uri.EscapeDataString(registrationId) + "/reject",
                HttpMethod.Post, request, ct);
        });

        app.MapGet("/registration-lifecycle", (HttpContext context) =>
        {
            if (!Has(context, "Configuration.Manage")) return Results.Forbid();
            return Results.Content(Page(), "text/html; charset=utf-8");
        });
    }

    private static async Task<IResult> ForwardAsync(
        HttpContext source,
        SuiteSecurity suiteSecurity,
        IHttpClientFactory factory,
        string url,
        HttpMethod method,
        object? body,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
            request.Content = System.Net.Http.Json.JsonContent.Create(body);

        if (!source.Items.TryGetValue("SuiteIdentity", out object? value) || value is not SuiteIdentity identity)
            return Results.Unauthorized();

        request.Headers.TryAddWithoutValidation(
            "X-Aegis-Suite-Handoff",
            suiteSecurity.CreateHandoff(identity, "Aegis.Configuration"));
        request.Headers.TryAddWithoutValidation("X-Aegis-Correlation-Id", Guid.NewGuid().ToString("N"));

        using HttpResponseMessage response = await factory.CreateClient("ops").SendAsync(request, ct);
        string content = await response.Content.ReadAsStringAsync(ct);
        return Results.Content(content,
            response.Content.Headers.ContentType?.ToString() ?? "application/json",
            statusCode: (int)response.StatusCode);
    }

    private static bool Has(HttpContext context, string permission)
        => context.Items.TryGetValue("SuiteIdentity", out object? value) &&
           value is SuiteIdentity identity &&
           identity.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    private static string Page() => """
<!doctype html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Aegis Registration Lifecycle</title><style>
body{font:14px Segoe UI,Arial,sans-serif;margin:0;background:#071522;color:#eef6ff}main{max-width:1250px;margin:30px auto;padding:0 20px}
a{color:#66bfff}.panel{background:#0d2030;border:1px solid #28597b;border-radius:8px;padding:18px;margin:14px 0}
table{width:100%;border-collapse:collapse}th,td{text-align:left;padding:9px;border-bottom:1px solid #29475b;vertical-align:top}
button{padding:8px 11px;background:#167ee6;color:#fff;border:0;border-radius:5px;cursor:pointer;margin-right:5px}.reject{background:#8d3838}
.badge{display:inline-block;padding:4px 8px;border-radius:999px;background:#34495a;font-weight:700}.pending,.recoverypending{background:#72531b}.registered{background:#145b40}.identityconflict,.revoked,.rejected{background:#7d2f2f}
small{color:#9fb7c8}.empty{padding:20px;color:#9fb7c8}.mono{font-family:Consolas,monospace;font-size:12px;word-break:break-all}
</style></head><body><main><p><a href="/">← Operations</a></p><h1>Application Registration</h1>
<p>Install → Request → Pending → Approve → Claim → Registered. Pending applications have zero trust.</p>
<section class="panel"><table><thead><tr><th>Application</th><th>Instance</th><th>Installation</th><th>State</th><th>Updated</th><th>Actions</th></tr></thead><tbody id="rows"></tbody></table></section>
<script>
const e=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
async function load(){const r=await fetch('/api/lifecycle-registrations');if(!r.ok){rows.innerHTML='<tr><td colspan="6">Unable to load registrations.</td></tr>';return}
const d=await r.json();rows.innerHTML=d.length?d.map(x=>{const actionable=x.state==='Pending'||x.state==='RecoveryPending';
return '<tr><td><b>'+e(x.applicationId)+'</b><br><small>'+e(x.registrationId)+'</small></td><td>'+e(x.instanceId)+'</td><td class="mono">'+e(x.installationId)+'</td><td><span class="badge '+e(x.state.toLowerCase())+'">'+e(x.state)+'</span></td><td>'+e(x.updatedAtUtc)+'</td><td>'+(actionable?'<button onclick="decide(\''+e(x.registrationId)+'\',\'approve\')">Approve</button><button class="reject" onclick="decide(\''+e(x.registrationId)+'\',\'reject\')">Reject</button>':'—')+'</td></tr>'}).join(''):'<tr><td colspan="6" class="empty">No lifecycle registrations.</td></tr>'}
async function decide(id,action){const reason=prompt('Reason/comment for '+action+':')||'';const r=await fetch('/api/lifecycle-registrations/'+encodeURIComponent(id)+'/'+action,{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({reason})});if(!r.ok)alert('Action failed');await load()}
load();setInterval(load,10000);
</script></main></body></html>
""";

    private sealed record LifecycleDecision(string? Reason);
}
