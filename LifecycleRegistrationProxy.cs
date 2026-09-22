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
            if (!Has(context, "Configuration.Manage")) return Results.Json(new { error = "Forbidden", detail = "The authenticated identity is not authorized for this operation." }, statusCode: StatusCodes.Status403Forbidden);
            return await ForwardAsync(
                context,
                suiteSecurity,
                factory,
                configurationUrl + "/api/registration/requests",
                HttpMethod.Get,
                null,
                ct);
        });

        app.MapGet("/api/lifecycle-registrations/flow", async (
            HttpContext context,
            SuiteSecurity suiteSecurity,
            IHttpClientFactory factory,
            CancellationToken ct) =>
        {
            if (!Has(context, "Configuration.View")) return Results.Json(new { error = "Forbidden", detail = "The authenticated identity is not authorized for this operation." }, statusCode: StatusCodes.Status403Forbidden);
            return await ForwardAsync(
                context,
                suiteSecurity,
                factory,
                configurationUrl + "/api/registration/flow?limit=200",
                HttpMethod.Get,
                null,
                ct);
        });

        app.MapGet("/api/lifecycle-registrations/reconciliation", async (
            HttpContext context,
            SuiteSecurity suiteSecurity,
            IHttpClientFactory factory,
            CancellationToken ct) =>
        {
            if (!Has(context, "Configuration.Manage")) return Results.Json(new { error = "Forbidden", detail = "The authenticated identity is not authorized for this operation." }, statusCode: StatusCodes.Status403Forbidden);
            return await ForwardAsync(
                context,
                suiteSecurity,
                factory,
                configurationUrl + "/api/registration/reconciliation",
                HttpMethod.Get,
                null,
                ct);
        });

        app.MapPost("/api/lifecycle-registrations/{registrationId}/approve", async (
            HttpContext context,
            string registrationId,
            [Microsoft.AspNetCore.Mvc.FromBody] LifecycleDecision request,
            SuiteSecurity suiteSecurity,
            IHttpClientFactory factory,
            CancellationToken ct) =>
        {
            if (!Has(context, "Configuration.Manage")) return Results.Json(new { error = "Forbidden", detail = "The authenticated identity is not authorized for this operation." }, statusCode: StatusCodes.Status403Forbidden);
            return await ForwardAsync(
                context,
                suiteSecurity,
                factory,
                configurationUrl + "/api/registration/" + Uri.EscapeDataString(registrationId) + "/approve",
                HttpMethod.Post,
                request,
                ct);
        });

        app.MapPost("/api/lifecycle-registrations/{registrationId}/reject", async (
            HttpContext context,
            string registrationId,
            [Microsoft.AspNetCore.Mvc.FromBody] LifecycleDecision request,
            SuiteSecurity suiteSecurity,
            IHttpClientFactory factory,
            CancellationToken ct) =>
        {
            if (!Has(context, "Configuration.Manage")) return Results.Json(new { error = "Forbidden", detail = "The authenticated identity is not authorized for this operation." }, statusCode: StatusCodes.Status403Forbidden);
            return await ForwardAsync(
                context,
                suiteSecurity,
                factory,
                configurationUrl + "/api/registration/" + Uri.EscapeDataString(registrationId) + "/reject",
                HttpMethod.Post,
                request,
                ct);
        });

        app.MapPost("/api/lifecycle-registrations/{registrationId}/revoke", async (
            HttpContext context,
            string registrationId,
            [Microsoft.AspNetCore.Mvc.FromBody] LifecycleDecision request,
            SuiteSecurity suiteSecurity,
            IHttpClientFactory factory,
            CancellationToken ct) =>
        {
            if (!Has(context, "Configuration.Manage")) return Results.Json(new { error = "Forbidden", detail = "The authenticated identity is not authorized for this operation." }, statusCode: StatusCodes.Status403Forbidden);
            return await ForwardAsync(
                context,
                suiteSecurity,
                factory,
                configurationUrl + "/api/registration/" + Uri.EscapeDataString(registrationId) + "/revoke",
                HttpMethod.Post,
                request,
                ct);
        });

        app.MapDelete("/api/lifecycle-registrations/{registrationId}", async (
            HttpContext context,
            string registrationId,
            [Microsoft.AspNetCore.Mvc.FromBody] LifecycleDecision request,
            SuiteSecurity suiteSecurity,
            IHttpClientFactory factory,
            CancellationToken ct) =>
        {
            if (!Has(context, "Configuration.Manage")) return Results.Json(new { error = "Forbidden", detail = "The authenticated identity is not authorized for this operation." }, statusCode: StatusCodes.Status403Forbidden);
            return await ForwardAsync(
                context,
                suiteSecurity,
                factory,
                configurationUrl + "/api/registration/" + Uri.EscapeDataString(registrationId),
                HttpMethod.Delete,
                request,
                ct);
        });

        app.MapPost("/api/lifecycle-registrations/{registrationId}/conflict/retain-existing", async (
            HttpContext context,
            string registrationId,
            [Microsoft.AspNetCore.Mvc.FromBody] LifecycleDecision request,
            SuiteSecurity suiteSecurity,
            IHttpClientFactory factory,
            CancellationToken ct) =>
        {
            if (!Has(context, "Configuration.Manage")) return Results.Json(new { error = "Forbidden", detail = "The authenticated identity is not authorized for this operation." }, statusCode: StatusCodes.Status403Forbidden);
            return await ForwardAsync(
                context,
                suiteSecurity,
                factory,
                configurationUrl + "/api/registration/" + Uri.EscapeDataString(registrationId) + "/conflict/retain-existing",
                HttpMethod.Post,
                request,
                ct);
        });


        app.MapPost("/api/lifecycle-registrations/{registrationId}/conflict/replace-with-incoming", async (
            HttpContext context,
            string registrationId,
            [Microsoft.AspNetCore.Mvc.FromBody] LifecycleDecision request,
            SuiteSecurity suiteSecurity,
            IHttpClientFactory factory,
            CancellationToken ct) =>
        {
            if (!Has(context, "Configuration.Manage")) return Results.Json(new { error = "Forbidden", detail = "The authenticated identity is not authorized for this operation." }, statusCode: StatusCodes.Status403Forbidden);
            return await ForwardAsync(
                context,
                suiteSecurity,
                factory,
                configurationUrl + "/api/registration/" +
                Uri.EscapeDataString(registrationId) +
                "/conflict/replace-with-incoming",
                HttpMethod.Post,
                request,
                ct);
        });

        app.MapGet("/registration-lifecycle", (HttpContext context) =>
        {
            if (!Has(context, "Configuration.Manage")) return Results.Json(new { error = "Forbidden", detail = "The authenticated identity is not authorized for this operation." }, statusCode: StatusCodes.Status403Forbidden);
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
        request.Headers.TryAddWithoutValidation(
            "X-Aegis-Correlation-Id",
            Guid.NewGuid().ToString("N"));

        using HttpResponseMessage response = await factory
            .CreateClient("ops")
            .SendAsync(request, ct);

        string content = await response.Content.ReadAsStringAsync(ct);

        return Results.Content(
            content,
            response.Content.Headers.ContentType?.ToString() ?? "application/json",
            statusCode: (int)response.StatusCode);
    }

    private static bool Has(HttpContext context, string permission)
        => context.Items.TryGetValue("SuiteIdentity", out object? value) &&
           value is SuiteIdentity identity &&
           identity.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    private static string Page() => """
<!doctype html>
<html>
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Aegis Registration Lifecycle</title>
<style>
body{font:14px Segoe UI,Arial,sans-serif;margin:0;background:#071522;color:#eef6ff}
main{max-width:1250px;margin:30px auto;padding:0 20px}
a{color:#66bfff}
.panel{background:#0d2030;border:1px solid #28597b;border-radius:8px;padding:18px;margin:14px 0}
table{width:100%;border-collapse:collapse}
th,td{text-align:left;padding:9px;border-bottom:1px solid #29475b;vertical-align:top}
button{padding:8px 11px;background:#167ee6;color:#fff;border:0;border-radius:5px;cursor:pointer;margin:2px 5px 2px 0}
button.danger{background:#8d3838}
button.warn{background:#9a6a14}
.badge{display:inline-block;padding:4px 8px;border-radius:999px;background:#34495a;font-weight:700}
.pending,.recoverypending{background:#72531b}
.registered{background:#145b40}
.identityconflict,.revoked,.rejected{background:#7d2f2f}
small{color:#9fb7c8}
.empty{padding:20px;color:#9fb7c8}
.mono{font-family:Consolas,monospace;font-size:12px;word-break:break-all}.audit{padding:7px 0;border-top:1px solid #29475b}.audit:first-of-type{border-top:0}details{margin:4px 0}summary{cursor:pointer;color:#8fd0ff}pre{white-space:pre-wrap;max-width:360px}
.flow-shell{position:relative;padding:24px 18px 14px;margin-top:12px;background:#071a28;border:1px solid #245a7a;border-radius:9px;overflow:hidden}
.flow-track{position:relative;height:110px;margin:0 34px}
.flow-rail{position:absolute;left:2%;right:2%;top:34px;height:4px;background:#244b64;border-radius:999px}
.flow-light{position:absolute;top:24px;width:24px;height:24px;margin-left:-12px;border-radius:50%;background:#20d789;box-shadow:0 0 8px #20d789,0 0 24px #20d789;transition:left .9s ease,background .3s,box-shadow .3s;z-index:4}
.flow-light.slow{background:#ffad31;box-shadow:0 0 8px #ffad31,0 0 24px #ffad31}.flow-light.stalled,.flow-light.failed{background:#ff6b6b;box-shadow:0 0 10px #ff6b6b,0 0 28px #ff6b6b}
.flow-light.healthy{animation:pulse 1.4s infinite}@keyframes pulse{50%{transform:scale(1.28)}}
.flow-nodes{position:absolute;inset:0;display:grid;grid-template-columns:repeat(6,1fr);align-items:start}
.flow-node{text-align:center;position:relative;z-index:2}.flow-node i{display:block;width:17px;height:17px;margin:27px auto 9px;border-radius:50%;background:#587386;border:3px solid #071a28}.flow-node.active i{background:#50c8ff;box-shadow:0 0 14px #50c8ff}.flow-node.done i{background:#20d789}.flow-node b{display:block;font-size:12px}.flow-node small{display:block;color:#8faabd;font-size:10px}
.flow-meta{display:grid;grid-template-columns:repeat(5,1fr);gap:8px;margin-top:4px}.flow-meta div{background:#0a2233;padding:8px;border-radius:6px}.flow-meta small{display:block;color:#8faabd}.health{font-weight:800}.health.healthy{color:#20d789}.health.slow{color:#ffad31}.health.stalled,.health.failed{color:#ff6b6b}
.journey-list{display:flex;gap:8px;flex-wrap:wrap;margin:10px 0}.journey-chip{background:#102c40;border:1px solid #35617d;color:#eef6ff;border-radius:999px;padding:7px 10px;cursor:pointer}.journey-chip.selected{border-color:#50c8ff;box-shadow:0 0 0 1px #50c8ff}.journey-chip.failed,.journey-chip.stalled{border-color:#c84c4c}.journey-chip.slow{border-color:#c58a2c}
.event-strip{max-height:260px;overflow:auto}.flow-event{display:grid;grid-template-columns:85px 120px 100px 1fr 220px;gap:8px;padding:7px 0;border-bottom:1px solid #29475b}.flow-event small{color:#9fb7c8}.outcome-failed{color:#ff8080}.outcome-succeeded{color:#56dda2}
@media(max-width:900px){.flow-meta{grid-template-columns:1fr 1fr}.flow-event{grid-template-columns:75px 90px 1fr}.flow-event .corr,.flow-event .app{display:none}}

</style>
</head>
<body>
<main>
<p><a href="/">← Operations</a></p>
<h1>Application Registration</h1>
<p>Install → Request → Pending → Approve → Claim → Registered. Pending applications have zero trust.</p>
<section class="panel">
<h2>Live Registration Flow</h2>
<p class="muted">Real lifecycle telemetry from Configuration. The light moves as the selected registration advances; amber means slow, red means stalled or failed.</p>
<div id="journeys" class="journey-list"></div>
<div class="flow-shell">
  <div class="flow-track">
    <div class="flow-rail"></div>
    <div id="flowLight" class="flow-light healthy" style="left:0%"></div>
    <div class="flow-nodes">
      <div class="flow-node" data-stage="Request"><i></i><b>Request</b><small>zero trust</small></div>
      <div class="flow-node" data-stage="Approval"><i></i><b>Approval</b><small>human gate</small></div>
      <div class="flow-node" data-stage="Claim"><i></i><b>Claim</b><small>one time</small></div>
      <div class="flow-node" data-stage="Authenticate"><i></i><b>Authenticate</b><small>durable ID</small></div>
      <div class="flow-node" data-stage="Publish"><i></i><b>Publish</b><small>contract</small></div>
      <div class="flow-node" data-stage="Running"><i></i><b id="runtimeFlowParent">Registered</b><small id="runtimeFlowQualifier">(RUNNING)</small></div>
    </div>
  </div>
  <div id="flowMeta" class="flow-meta"></div>
</div>
<h3>Recent Flow Events</h3>
<div id="flowEvents" class="event-strip"></div>
</section>
<section class="panel"><div id="reconciliation" class="empty">Checking reconciliation…</div></section>
<section class="panel">
<table>
<thead><tr><th>Application</th><th>Instance</th><th>Installation</th><th>State</th><th>Updated</th><th>Audit / Request</th><th>Actions</th></tr></thead>
<tbody id="rows"></tbody>
</table>
</section>
<script>
const e=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const localTime=v=>{if(!v)return '—';const d=new Date(v);return Number.isNaN(d.getTime())?String(v):d.toLocaleString(undefined,{dateStyle:'medium',timeStyle:'medium'});};
const localTimeOnly=v=>{if(!v)return '—';const d=new Date(v);return Number.isNaN(d.getTime())?String(v):d.toLocaleTimeString();};
const journeys=document.getElementById('journeys');
const flowLight=document.getElementById('flowLight');
const flowMeta=document.getElementById('flowMeta');
const flowEvents=document.getElementById('flowEvents');
const reconciliation=document.getElementById('reconciliation');
const rows=document.getElementById('rows');
let flowSnapshot={journeys:[],events:[]};
let selectedRegistrationId=null;
const stageOrder=['Request','Approval','Claim','Authenticate','Publish','Running'];
function stageIndex(stage){
 if(stage==='Recovery Approval'||stage==='Rejected')return 1;
 if(stage==='Recovery Claim')return 2;
 if(stage==='Identity Conflict')return 3;
 if(stage==='Revoked')return 5;
 const i=stageOrder.indexOf(stage);
 return i<0?0:i;
}
function fmtAge(seconds){
 const s=Number(seconds||0); if(s<60)return s+'s';
 if(s<3600)return Math.floor(s/60)+'m '+(s%60)+'s';
 return Math.floor(s/3600)+'h '+Math.floor((s%3600)/60)+'m';
}
function selectJourney(id){selectedRegistrationId=id;renderFlow()}
function formatLifecycleState(state){
 const s=String(state||'');
 if(s==='Pending')return 'Pending Approval';
 if(s==='RecoveryPending')return 'Recovery Pending';
 if(s==='IdentityConflict')return 'Identity Conflict';
 return s||'Unknown Authority';
}
function runtimeQualifier(x){
 const h=String(x?.health||'').toLowerCase();
 if(h==='healthy'||h==='running'||h==='ready')return 'RUNNING/HEALTHY';
 if(h==='failed'||h==='stalled'||h==='degraded'||h==='warning'||h==='slow'||h==='unknown'||h==='offline'||!h)return 'STOPPED/UNHEALTHY';
 return 'STOPPED/UNHEALTHY';
}
function lifecycleDisplayState(registration){
 const state=String(registration?.state||'');
 if(state!=='Registered')return formatLifecycleState(state);
 const journey=(flowSnapshot.journeys||[]).find(j=>j.registrationId===registration.registrationId);
 return 'Registered ('+runtimeQualifier(journey)+')';
}
function renderFlow(){
 const js=flowSnapshot.journeys||[];
 if(!selectedRegistrationId||!js.some(x=>x.registrationId===selectedRegistrationId))
   selectedRegistrationId=js.length?js[0].registrationId:null;
 journeys.innerHTML=js.length?js.map(x=>'<button class="journey-chip '+e(String(x.health||'').toLowerCase())+(x.registrationId===selectedRegistrationId?' selected':'')+'" onclick="selectJourney(\''+e(x.registrationId)+'\')">'+e(x.applicationId)+' · '+e(x.instanceId)+' · '+e(x.health)+'</button>').join(''):'<span class="empty">No active registration journeys.</span>';
 const x=js.find(y=>y.registrationId===selectedRegistrationId);
 if(!x){flowMeta.innerHTML='<div><small>STATE</small><b>No active journey</b></div>';flowLight.style.left='0%';return}
 const idx=stageIndex(x.currentStage);
 const registered=String(x.state||'').toLowerCase()==='registered';
 const parent=document.getElementById('runtimeFlowParent'),qualifier=document.getElementById('runtimeFlowQualifier');
 if(parent)parent.textContent=registered?'Registered':'Runtime';
 if(qualifier)qualifier.textContent=registered?'('+runtimeQualifier(x)+')':'';
 const pct=(idx/(stageOrder.length-1))*100;
 flowLight.style.left=pct+'%';
 flowLight.className='flow-light '+String(x.health||'Healthy').toLowerCase();
 document.querySelectorAll('.flow-node').forEach((n,i)=>{n.classList.toggle('active',i===idx);n.classList.toggle('done',i<idx)});
 flowMeta.innerHTML=
  '<div><small>APPLICATION</small><b>'+e(x.applicationId)+'</b></div>'+
  '<div><small>STATE / STAGE</small><b>'+e(registered?('Registered ('+runtimeQualifier(x)+')'):formatLifecycleState(x.state))+' / '+e(x.currentStage)+'</b></div>'+
  '<div><small>HEALTH</small><b class="health '+e(String(x.health).toLowerCase())+'">'+e(x.health)+'</b></div>'+
  '<div><small>STAGE AGE</small><b>'+e(fmtAge(x.stageAgeSeconds))+'</b></div>'+
  '<div><small>CORRELATION</small><b class="mono">'+e(x.correlationId||'—')+'</b></div>';
 const ev=(flowSnapshot.events||[]).filter(v=>!selectedRegistrationId||v.registrationId===selectedRegistrationId).slice(0,30);
 flowEvents.innerHTML=ev.length?ev.map(v=>'<div class="flow-event"><span>'+e(localTimeOnly(v.atUtc))+'</span><b>'+e(v.stage)+'</b><span class="outcome-'+e(String(v.outcome).toLowerCase())+'">'+e(v.outcome)+'</span><span class="app">'+e(v.action)+(v.reason?' · '+e(v.reason):'')+'</span><code class="corr">'+e(v.correlationId||'—')+'</code></div>').join(''):'<div class="empty">No flow events for this journey.</div>';
}
async function loadFlow(){
 try{
  const r=await fetch('/api/lifecycle-registrations/flow',{cache:'no-store'});
  if(!r.ok)throw new Error('HTTP '+r.status);
  flowSnapshot=await r.json();
  renderFlow();
  load();
 }catch(err){
  flowMeta.innerHTML='<div><small>FLOW TELEMETRY</small><b class="health failed">Unavailable</b></div>';
  flowEvents.innerHTML='<div class="empty">'+e(err.message)+'</div>';
 }
}


async function load(){
 const rr=await fetch('/api/lifecycle-registrations/reconciliation');
 if(rr.ok){
   const s=await rr.json();
   const issues=Array.isArray(s.issues)?s.issues:[];
   reconciliation.innerHTML=s.healthy
     ? '<b>Reconciliation: Healthy</b> · '+e(s.registrationCount)+' registration(s) checked'
     : '<b>Reconciliation: Attention required</b> · '+e(issues.length)+' issue(s)<br><small>'+issues.map(i=>e(i.code)+': '+e(i.applicationId)+'/'+e(i.instanceId)).join(' · ')+'</small>';
 }
 const r=await fetch('/api/lifecycle-registrations');
 if(!r.ok){rows.innerHTML='<tr><td colspan="7">Unable to load registrations.</td></tr>';return}
 const d=await r.json();
 rows.innerHTML=d.length?d.map(x=>{
   const actionable=x.state==='Pending'||x.state==='RecoveryPending';
   const conflict=x.state==='IdentityConflict';
   const canRevoke=x.state==='Registered'||x.state==='IdentityConflict';
   const canPurge=x.state==='Revoked'||x.state==='Rejected';
   let actions='';
   if(actionable){
     actions+='<button onclick="decide(\''+e(x.registrationId)+'\',\'approve\')">Approve</button>';
     actions+='<button class="danger" onclick="decide(\''+e(x.registrationId)+'\',\'reject\')">Reject</button>';
   }
   if(conflict){
     actions+='<button class="warn" onclick="decide(\''+e(x.registrationId)+'\',\'conflict/retain-existing\')">Retain Existing</button>';
     actions+='<button class="danger" onclick="decide(\''+e(x.registrationId)+'\',\'conflict/replace-with-incoming\')">Replace with Incoming</button>';
   }
   if(canRevoke){
     actions+='<button class="danger" onclick="decide(\''+e(x.registrationId)+'\',\'revoke\')">Revoke</button>';
   }
   if(canPurge){
     actions+='<button class="danger" onclick="purge(\''+e(x.registrationId)+'\')">Purge</button>';
   }
   const audit=Array.isArray(x.audit)?x.audit:[];
   const history=audit.length
     ? '<details><summary>'+e(audit.length)+' event(s)</summary>'+audit.slice().reverse().map(a=>'<div class="audit"><b>'+e(a.action)+'</b> · '+e(localTime(a.atUtc))+'<br><small>Actor: '+e(a.actor)+' · IP: '+e(a.sourceIp||'—')+' · '+e(a.previousState||'—')+' → '+e(a.resultingState||'—')+(a.reason?'<br>Reason: '+e(a.reason):'')+(a.correlationId?'<br>Correlation: <span class="mono">'+e(a.correlationId)+'</span>':'')+'</small></div>').join('')+'</details>'
     : '—';
   const metadata=x.metadata?'<details><summary>Request metadata</summary><pre class="mono">'+e(JSON.stringify(x.metadata,null,2))+'</pre></details>':'';
   return '<tr><td><b>'+e(x.applicationId)+'</b><br><small>'+e(x.registrationId)+'</small></td>'+
     '<td>'+e(x.instanceId)+'</td>'+
     '<td class="mono">'+e(x.installationId)+'</td>'+
     '<td><span class="badge '+e(x.state.toLowerCase())+'">'+e(lifecycleDisplayState(x))+'</span></td>'+
     '<td>'+e(localTime(x.updatedAtUtc))+'</td>'+
     '<td>'+history+metadata+'</td>'+
     '<td>'+(actions||'—')+'</td></tr>';
 }).join(''):'<tr><td colspan="7" class="empty">No lifecycle registrations.</td></tr>';
}

async function decide(id,action){
 const reason=prompt('Reason/comment for '+action+':')||'';
 const r=await fetch('/api/lifecycle-registrations/'+encodeURIComponent(id)+'/'+action,{
   method:'POST',
   headers:{'content-type':'application/json'},
   body:JSON.stringify({reason})
 });
 if(!r.ok){
   let d={}; try{d=await r.json()}catch{}
   alert(d.error||d.detail||'Action failed');
 }
 await load();
}

async function purge(id){
 if(!confirm('Purge this revoked/rejected registration? Audit history will be retained.'))return;
 const reason=prompt('Reason/comment for purge:')||'';
 const r=await fetch('/api/lifecycle-registrations/'+encodeURIComponent(id),{
   method:'DELETE',
   headers:{'content-type':'application/json'},
   body:JSON.stringify({reason})
 });
 if(!r.ok){
   let d={}; try{d=await r.json()}catch{}
   alert(d.error||d.detail||'Purge failed');
 }
 await load();
}

load();
loadFlow();
setInterval(load,10000);
setInterval(loadFlow,3000);
</script>
</main>
</body>
</html>
""";

    private sealed record LifecycleDecision(string? Reason);
}
