using System.Text.Encodings.Web;

internal static class OperationsSectionPage
{
    internal static string RenderApplications(string configurationUrl, string diagnosticsUrl, string? userName = null) =>
        Render("Applications", "applications", configurationUrl, diagnosticsUrl, userName, ApplicationsBody);

    internal static string RenderDeployments(string configurationUrl, string diagnosticsUrl, string? userName = null) =>
        Render("Deployments", "deployments", configurationUrl, diagnosticsUrl, userName, DeploymentsBody);

    private static string Render(string title, string active, string configurationUrl, string diagnosticsUrl, string? userName, string body)
    {
        string version = typeof(OperationsSectionPage).Assembly.GetName().Version?.ToString() ?? "unknown";
        string html = """
<!doctype html>
<html>
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>__TITLE__ · Operations</title>
<style>
:root{--alpha-blue:#5588ee;--arctic-ice:#66ccff;--arctic-snow:#99ccff;--radioactive:#88eeff;--starship-white:#f3f3fc;--sunset-red:#ff3500;--salmon:#7799dd;--title-gold:#ffcc66;--line:#455580;--line-soft:#344470;--muted:#99ccff;--rail-width:160px;--workspace-arm-height:48px;--elbow-radius:18px;--scroll-arrow-space:18px;--header-gap:18px}
*{box-sizing:border-box}html,body{width:100%;height:100%;margin:0;overflow:hidden;background:#000;color:var(--starship-white);font:14px/1.35 "Arial Narrow","Roboto Condensed","Segoe UI",Arial,sans-serif}a{color:inherit;text-decoration:none}button,input,select{font:inherit}
.lcars-top{position:fixed;left:8px;right:18px;top:8px;height:140px;z-index:20;background:#000}.left-vertical{position:absolute;left:0;top:0;width:var(--rail-width);height:140px;background:var(--alpha-blue);border-radius:28px 0 0 28px}.bottom-arm{position:absolute;left:var(--rail-width);right:0;top:126px;height:14px;background:var(--alpha-blue);border-radius:0 9px 9px 0}.lcars-top::after{content:"";position:absolute;left:var(--rail-width);top:108px;width:var(--elbow-radius);height:var(--elbow-radius);background:radial-gradient(circle at 100% 0,transparent 0 calc(var(--elbow-radius) - 1px),var(--alpha-blue) var(--elbow-radius));z-index:4}
.brand{position:absolute;left:calc(var(--rail-width) + 18px);top:8px;width:270px;height:96px}.brand .section{font-size:36px;line-height:1;color:var(--arctic-ice);font-weight:900;text-transform:uppercase;margin-top:10px}.brand .org{font-size:10px;color:var(--arctic-snow);font-weight:800;text-transform:uppercase;margin-top:5px;white-space:nowrap}.brand .datetime{margin-top:8px;font-weight:900;text-transform:uppercase;color:var(--arctic-ice);font-size:14px;white-space:nowrap}.brand .brand-user{margin-top:4px;color:var(--arctic-snow);font:800 10px/1 Arial,sans-serif;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;max-width:260px}
.live-log{position:absolute;left:470px;top:8px;right:318px;height:108px;overflow:hidden;font:13px/1.35 ui-monospace,SFMono-Regular,Consolas,monospace}.recent-title{height:18px;background:var(--title-gold);color:#071018;font:900 10px/18px Arial,sans-serif;letter-spacing:.12em;text-transform:uppercase;padding:0 8px;border-radius:3px 3px 0 0}.recent-rows{height:90px;overflow:hidden;padding:3px 10px}.row{white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.t{display:inline-block;min-width:176px;color:var(--arctic-snow)}.inf{color:var(--radioactive);font-weight:900}.wrn{color:var(--title-gold);font-weight:900}.src{color:var(--arctic-ice)}
.top-buttons{position:absolute;right:50px;top:18px;width:250px;height:72px;display:grid;grid-template-columns:1fr 1fr;gap:8px 10px;padding-left:12px}.top-buttons a{border:0;border-radius:8px;font-weight:900;text-transform:uppercase;color:#071018;font-size:12px;display:flex;align-items:center;justify-content:center}.top-buttons .blue{background:#5588ee}.top-buttons .salmon{background:#7799dd}.top-buttons .gold{background:#88eeff}.top-buttons .logout{background:var(--sunset-red);color:#fff}
.workspace-shell{position:fixed;left:8px;right:14px;top:166px;bottom:18px;overflow:hidden}.workspace-leg{position:absolute;left:0;top:0;bottom:0;width:var(--rail-width);background:var(--alpha-blue);border-radius:28px 0 0 28px}.workspace-arm{position:absolute;left:var(--rail-width);right:0;top:0;height:48px;background:var(--title-gold);display:flex;align-items:center;border-radius:0 9px 9px 0}.workspace-arm::before{content:"";position:absolute;left:0;top:0;width:18px;height:48px;background:var(--alpha-blue)}.workspace-title{position:relative;z-index:1;height:100%;display:flex;align-items:center;font-weight:900;text-transform:uppercase;color:#071018;letter-spacing:.055em;font-size:30px;margin-left:18px;padding:0 1ch;border-left:6px solid #000;border-right:6px solid #000}.last-scan{margin-left:auto;margin-right:14px;color:#3b2f16;font:700 9px/1 Arial,sans-serif;text-transform:uppercase}.workspace-shell::after{content:"";position:absolute;left:var(--rail-width);top:48px;width:18px;height:18px;background:radial-gradient(circle at 100% 100%,transparent 0 17px,var(--alpha-blue) 18px);z-index:5}
.workspace-nav{position:absolute;left:0;top:66px;width:var(--rail-width);z-index:2}.workspace-nav a{display:flex;align-items:center;gap:6px;height:48px;padding:0 6px 0 9px;color:#071018;font-weight:900;text-transform:uppercase;font-size:10px;white-space:normal;overflow-wrap:normal;word-break:normal;hyphens:none;border-bottom:3px solid #000}.workspace-nav a:not(.active)::before{content:"◆";flex:0 0 14px;width:14px;font-size:9px;text-align:center}.workspace-nav a.active{font-size:12px;font-weight:800;background:var(--title-gold)}.lcars-back{position:absolute;left:0;bottom:0;width:var(--rail-width);height:48px;border:0;border-top:3px solid #000;border-radius:0 0 0 28px;background:var(--alpha-blue);color:#071018;font:900 12px/1 Arial,sans-serif;text-align:left;padding-left:11px;text-transform:uppercase}.detail-scroll{position:absolute;left:var(--rail-width);right:0;top:66px;bottom:18px;overflow:auto;padding:12px 18px 20px 20px;background:#000}
.metrics{display:grid;grid-template-columns:repeat(5,minmax(110px,1fr));gap:8px;margin:0 0 10px}.metric{min-height:56px;border:1px solid var(--line-soft);border-top:2px solid var(--line);border-radius:8px;padding:7px 10px;background:#071020}.metric b{display:block;color:var(--arctic-ice);font-size:22px;line-height:1}.metric small{color:var(--muted);font-size:10px;text-transform:uppercase}
.panel{border:1px solid var(--line-soft);border-top:2px solid var(--line);border-radius:9px;background:linear-gradient(180deg,rgba(8,27,42,.5),rgba(1,7,12,.4));padding:10px 12px;margin-bottom:10px}.panel h2{margin:0 0 8px;color:var(--arctic-ice);font-size:17px}.muted{color:var(--muted)}table{width:100%;border-collapse:collapse}th{text-align:left;color:var(--arctic-ice);background:#071b29;padding:8px;font-size:10px;position:sticky;top:0}td{padding:8px;border-bottom:1px solid var(--line-soft);vertical-align:top}td small{display:block;color:var(--muted);margin-top:2px}.state{font-weight:900}.Healthy,.Running,.Ready{color:var(--radioactive)}.Degraded,.Warning,.Unknown,.Offline{color:var(--title-gold)}.Unhealthy,.Failed{color:var(--sunset-red)}.table-scroll{max-height:calc(100vh - 380px);overflow:auto}.two{display:grid;grid-template-columns:1fr 1fr;gap:10px}.chips{display:flex;flex-wrap:wrap;gap:5px}.chip{border:1px solid var(--line);border-radius:10px;padding:2px 6px;font-size:10px;color:var(--arctic-snow)}.activity-row{display:grid;grid-template-columns:145px 150px 1fr;gap:8px;padding:6px 0;border-bottom:1px solid var(--line-soft)}
@media(max-width:1250px){.live-log{left:410px;right:303px}.top-buttons{width:225px}.two{grid-template-columns:1fr}}@media(max-width:900px){.metrics{grid-template-columns:repeat(2,1fr)}}
__CONTROL_PLANE_HEADER_CSS__
</style>
</head>
<body>
<header class="lcars-top"><div class="left-vertical"></div><div class="bottom-arm"></div><div class="brand"><div class="section">Operations</div><div class="org">Food For The Poor Jamaica</div><div class="datetime" id="brandDateTime"></div><div class="brand-user" id="brandUser">__USER__</div></div><div class="live-log"><div class="recent-title">Recent Logs</div><div class="recent-rows" id="liveLog"><div class="row"><span class="t">--:--:--</span> <span class="inf">INF</span> <span class="src">[Operations]</span> Loading __TITLE__ telemetry</div></div></div><div class="top-buttons"><a class="blue" href="/">Operations</a><a class="salmon" href="__DIAG__">Diagnostics</a><a class="gold" href="__CFG__">Configuration</a><a class="logout" href="/auth/logout">Logout</a></div></header>
<section class="workspace-shell"><div class="workspace-arm"><div class="workspace-title">__TITLE__</div><div class="last-scan" id="lastScan">Last Scan: SCANNING</div></div><div class="workspace-leg"></div><nav class="workspace-nav"><a class="__OVERVIEW__" href="/">Overview</a><a class="__APPLICATIONS__" href="/applications">Applications</a><a class="__DEPLOYMENTS__" href="/deployments">Deployments</a><a href="/handoff/registration">Registration</a><a href="/logs">Logs</a></nav><button class="lcars-back" type="button" onclick="if(history.length>1){history.back()}else{location.href='/'}">Back</button><main class="detail-scroll">__BODY__</main></section>
<script>
const esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#039;'}[c]));
const fmt=v=>{const d=new Date(v);return Number.isNaN(d.getTime())?'—':d.toLocaleString()};
function updateBrandClock(){const d=new Date(),e=document.getElementById('brandDateTime');if(!e)return;e.textContent=d.toLocaleString(undefined,{month:'short',day:'2-digit',year:'numeric',hour:'2-digit',minute:'2-digit'}).toUpperCase()}updateBrandClock();setInterval(updateBrandClock,1000);
async function loadBrandUser(){try{const r=await fetch('/auth/me',{cache:'no-store'});if(!r.ok)return;const x=await r.json(),e=document.getElementById('brandUser');if(e)e.textContent='User: '+(x.displayName||x.userName||'Signed in')+' · IP: '+(x.clientIp||'unknown')}catch{}}loadBrandUser();
document.querySelectorAll('.top-buttons a,.workspace-nav a').forEach(a=>a.addEventListener('pointerdown',()=>{a.classList.add('nav-flash');setTimeout(()=>a.classList.remove('nav-flash'),180)}));
__SCRIPT__
</script>
</body>
</html>
""";
        return html
            .Replace("__TITLE__", HtmlEncoder.Default.Encode(title), StringComparison.Ordinal)
            .Replace("__USER__", HtmlEncoder.Default.Encode(userName ?? "SIGNED IN"), StringComparison.Ordinal)
            .Replace("__DIAG__", HtmlEncoder.Default.Encode(diagnosticsUrl), StringComparison.Ordinal)
            .Replace("__CFG__", HtmlEncoder.Default.Encode(configurationUrl), StringComparison.Ordinal)
            .Replace("__OVERVIEW__", active == "overview" ? "active" : "", StringComparison.Ordinal)
            .Replace("__APPLICATIONS__", active == "applications" ? "active" : "", StringComparison.Ordinal)
            .Replace("__DEPLOYMENTS__", active == "deployments" ? "active" : "", StringComparison.Ordinal)
            .Replace("__BODY__", body, StringComparison.Ordinal)
            .Replace("__SCRIPT__", active == "applications" ? ApplicationsScript : DeploymentsScript, StringComparison.Ordinal)
            .Replace("__VERSION__", HtmlEncoder.Default.Encode(version), StringComparison.Ordinal)
            .Replace("__CONTROL_PLANE_HEADER_CSS__", ControlPlaneHeader.Css, StringComparison.Ordinal);
    }

    private const string ApplicationsBody = """
<section class="metrics"><div class="metric"><b id="appTotal">—</b><small>Applications</small></div><div class="metric"><b id="instanceTotal">—</b><small>Instances</small></div><div class="metric"><b id="healthyTotal">—</b><small>Healthy</small></div><div class="metric"><b id="attentionTotal">—</b><small>Attention</small></div><div class="metric"><b id="environmentTotal">—</b><small>Environments</small></div></section>
<section class="panel"><h2>Application Inventory</h2><div class="muted" id="authority">Loading authoritative registration and operational telemetry…</div><div class="table-scroll"><table><thead><tr><th>Application</th><th>Instance</th><th>Environment</th><th>Version / Build</th><th>Registration</th><th>State</th><th>Configuration</th><th>Observed</th></tr></thead><tbody id="appsBody"><tr><td colspan="8">Loading…</td></tr></tbody></table></div></section>
<section class="two"><section class="panel"><h2>Application Attention</h2><div id="attentionList">Loading…</div></section><section class="panel"><h2>Dependencies</h2><div id="dependencyList">Loading…</div></section></section>
""";

    private const string ApplicationsScript = """
async function load(){
 try{
  const [registered,telemetry]=await Promise.all([fetch('/api/operations/registered-applications',{cache:'no-store'}).then(r=>r.json()),fetch('/api/operations/applications',{cache:'no-store'}).then(r=>r.json())]);
  const apps=registered.applications||[], raw=telemetry||[], byKey=new Map(raw.map(x=>[(x.applicationId+'|'+x.instanceId).toLowerCase(),x]));
  const uniqueApps=new Set(apps.map(x=>x.applicationId)), envs=new Set(apps.map(x=>x.environment).filter(x=>x&&x!=='—'));
  const healthy=apps.filter(x=>String(x.state).toLowerCase()==='healthy'&&!x.stale).length, attention=apps.length-healthy;
  appTotal.textContent=uniqueApps.size;instanceTotal.textContent=apps.length;healthyTotal.textContent=healthy;attentionTotal.textContent=attention;environmentTotal.textContent=envs.size;
  authority.textContent=registered.authorityAvailable?'Configuration registration authority online · operational state merged by instance':'Registration authority unavailable'+(registered.authorityError?' · '+registered.authorityError:'');
  appsBody.innerHTML=apps.length?apps.map(a=>{const op=byKey.get((a.applicationId+'|'+a.instanceId).toLowerCase())||{};const deps=op.dependencies||[];return '<tr><td><b>'+esc(a.displayName||a.applicationId)+'</b><small>'+esc(a.applicationId)+(deps.length?' · '+deps.length+' dependencies':'')+'</small></td><td>'+esc(a.instanceId)+'</td><td>'+esc(a.environment)+'</td><td>'+esc(a.version||'—')+'<small>'+esc(a.build||'No build published')+'</small></td><td><span class="state '+esc(a.registrationFlowTone||'')+'">'+esc(a.registrationFlowLabel||a.registrationStatus||'Unknown')+'</span><small>'+esc(a.registrationHealth||'')+'</small></td><td><span class="state '+esc(a.state)+'">'+esc(a.stale?'Offline':a.state)+'</span><small>'+esc(a.reason||'')+'</small></td><td>'+esc(a.configurationStatus||'—')+'</td><td>'+fmt(a.observedAtUtc||a.registrationObservedAtUtc)+'</td></tr>'}).join(''):'<tr><td colspan="8">No applications registered.</td></tr>';
  const issues=apps.filter(a=>String(a.state).toLowerCase()!=='healthy'||a.stale||String(a.registrationHealth).toLowerCase()==='attention');
  attentionList.innerHTML=issues.length?issues.map(a=>'<div class="activity-row"><b>'+esc(a.applicationId)+'</b><span class="state '+esc(a.state)+'">'+esc(a.stale?'Offline':a.state)+'</span><span>'+esc(a.reason||a.registrationHealth||'Attention required')+'</span></div>').join(''):'<div class="muted">No application conditions require attention.</div>';
  const depRows=raw.filter(x=>(x.dependencies||[]).length).map(x=>'<div class="activity-row"><b>'+esc(x.applicationId)+'</b><span>'+esc(x.instanceId)+'</span><span class="chips">'+x.dependencies.map(d=>'<span class="chip">'+esc(d)+'</span>').join('')+'</span></div>');
  dependencyList.innerHTML=depRows.length?depRows.join(''):'<div class="muted">No dependency telemetry has been published.</div>';
  lastScan.textContent='Last Scan: '+new Date().toLocaleString();
 }catch(e){appsBody.innerHTML='<tr><td colspan="8">Application inventory unavailable · '+esc(e.message)+'</td></tr>';lastScan.textContent='Last Scan: UNAVAILABLE'}
}
load();setInterval(load,15000);
""";

    private const string DeploymentsBody = """
<section class="metrics"><div class="metric"><b id="deployApps">—</b><small>Observed Apps</small></div><div class="metric"><b id="builds">—</b><small>Builds Published</small></div><div class="metric"><b id="ready">—</b><small>Ready / Healthy</small></div><div class="metric"><b id="blocked">—</b><small>Attention</small></div><div class="metric"><b id="syncCount">—</b><small>Sync Endpoints</small></div></section>
<section class="panel"><h2>Release / Deployment Inventory</h2><div class="muted">Observed deployment readiness based on application version, build, registration, configuration and runtime telemetry.</div><div class="table-scroll"><table><thead><tr><th>Application</th><th>Instance</th><th>Environment</th><th>Version</th><th>Build</th><th>Runtime State</th><th>Configuration</th><th>Readiness</th><th>Observed</th></tr></thead><tbody id="deployBody"><tr><td colspan="9">Loading…</td></tr></tbody></table></div></section>
<section class="two"><section class="panel"><h2>Synchronization</h2><div id="syncList">Loading…</div></section><section class="panel"><h2>Recent Release / Operations Activity</h2><div id="historyList">Loading…</div></section></section>
<section class="two"><section class="panel"><h2>Runbooks</h2><div id="runbookList">Loading…</div></section><section class="panel"><h2>Deployment Notes</h2><div class="muted">This page reports observed release/deployment state. Deployment execution controls remain disabled until applications publish an explicit deployment contract.</div></section></section>
""";

    private const string DeploymentsScript = """
async function safeJson(url,fallback){try{const r=await fetch(url,{cache:'no-store'});return r.ok?await r.json():fallback}catch{return fallback}}
async function load(){
 const [apps,sync,history,runbooks]=await Promise.all([safeJson('/api/operations/applications',[]),safeJson('/api/operations/synchronization',[]),safeJson('/api/operations/history',[]),safeJson('/api/operations/runbooks',[])]);
 const rows=apps||[], isReady=a=>String(a.state).toLowerCase()==='healthy'&&!a.stale&&(!a.configurationStatus||['configured','healthy','ready'].includes(String(a.configurationStatus).toLowerCase()));
 deployApps.textContent=new Set(rows.map(x=>x.applicationId)).size;builds.textContent=rows.filter(x=>x.build).length;ready.textContent=rows.filter(isReady).length;blocked.textContent=rows.filter(x=>!isReady(x)).length;syncCount.textContent=(sync||[]).length;
 deployBody.innerHTML=rows.length?rows.map(a=>'<tr><td><b>'+esc(a.displayName||a.applicationId)+'</b><small>'+esc(a.applicationId)+'</small></td><td>'+esc(a.instanceId)+'</td><td>'+esc(a.environment)+'</td><td>'+esc(a.version||'—')+'</td><td>'+esc(a.build||'—')+'</td><td><span class="state '+esc(a.state)+'">'+esc(a.state)+'</span><small>'+esc(a.reason||'')+'</small></td><td>'+esc(a.configurationStatus||'—')+'</td><td><span class="state '+(isReady(a)?'Ready':'Warning')+'">'+(isReady(a)?'READY':'ATTENTION')+'</span></td><td>'+fmt(a.observedAtUtc)+'</td></tr>').join(''):'<tr><td colspan="9">No deployment telemetry has been observed.</td></tr>';
 syncList.innerHTML=(sync||[]).length?(sync||[]).slice(0,12).map(x=>'<div class="activity-row"><b>'+esc(x.applicationId||'Application')+'</b><span>'+esc(x.name||x.endpointId||x.direction||'Sync')+'</span><span>'+esc(x.state||x.status||x.detail||'Observed')+'</span></div>').join(''):'<div class="muted">No synchronization endpoints reported.</div>';
 historyList.innerHTML=(history||[]).length?(history||[]).slice(0,12).map(x=>'<div class="activity-row"><b>'+fmt(x.at)+'</b><span>'+esc(x.application||x.type||'Operations')+'</span><span>'+esc(x.text||x.message||'Operational event')+'</span></div>').join(''):'<div class="muted">No recent operational history.</div>';
 runbookList.innerHTML=(runbooks||[]).length?(runbooks||[]).slice(0,12).map(x=>'<div class="activity-row"><b>'+esc(x.applicationId||'Application')+'</b><span>'+esc(x.title||x.runbookId||'Runbook')+'</span><span>'+fmt(x.updatedAtUtc||x.createdAtUtc)+'</span></div>').join(''):'<div class="muted">No deployment-related runbooks published.</div>';
 liveLog.innerHTML=(history||[]).slice(0,5).map(x=>'<div class="row"><span class="t">'+fmt(x.at)+'</span> <span class="inf">INF</span> <span class="src">['+esc(x.application||x.type||'Operations')+']</span> '+esc(x.text||'Operational event')+'</div>').join('')||'<div class="row"><span class="t">--</span> <span class="inf">INF</span> <span class="src">[Deployments]</span> No recent deployment activity</div>';
 lastScan.textContent='Last Scan: '+new Date().toLocaleString();
}
load();setInterval(load,15000);
""";
}
