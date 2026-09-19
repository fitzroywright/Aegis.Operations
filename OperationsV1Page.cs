internal static class OperationsV1Page
{
    internal static string Render() => """
<!doctype html>
<html>
<head>
<meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Aegis Operations V1</title>
<style>
:root{--bg:#071522;--panel:#0d2030;--line:#28597b;--text:#eef6ff;--muted:#9fb7c8;--green:#20d789;--amber:#ffad31;--red:#ff6b6b;--blue:#50c8ff}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font:14px Segoe UI,Arial,sans-serif}
header{padding:18px 24px;border-bottom:1px solid var(--line);display:flex;align-items:center;gap:16px}header h1{margin:0;font-size:26px}header small{color:var(--muted)}
nav{display:flex;gap:8px;flex-wrap:wrap;padding:12px 24px;border-bottom:1px solid #18394f}nav button{background:#102c40;color:var(--text);border:1px solid #35617d;border-radius:6px;padding:9px 12px;cursor:pointer}
nav button.active{border-color:var(--blue);box-shadow:0 0 0 1px var(--blue)}
main{padding:20px 24px}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(260px,1fr));gap:12px}.card,.panel{background:var(--panel);border:1px solid var(--line);border-radius:8px;padding:14px}.card h3{margin:0 0 7px}.muted{color:var(--muted)}
.state{font-weight:800}.healthy{color:var(--green)}.degraded,.warning,.maintenance{color:var(--amber)}.failed,.offline{color:var(--red)}.unknown,.notsupported{color:var(--muted)}
.metrics{display:grid;grid-template-columns:repeat(auto-fit,minmax(120px,1fr));gap:10px;margin-bottom:16px}.metric{background:#0a2233;border:1px solid var(--line);border-radius:8px;padding:13px}.metric b{display:block;font-size:25px}
table{width:100%;border-collapse:collapse}th,td{text-align:left;padding:9px;border-bottom:1px solid #29475b;vertical-align:top}
input,textarea,select{width:100%;background:#081b28;color:var(--text);border:1px solid #35617d;border-radius:5px;padding:8px}button.action{background:#167ee6;color:white;border:0;border-radius:5px;padding:8px 11px;cursor:pointer}
.flow{display:flex;gap:5px;align-items:center;flex-wrap:wrap}.stage{padding:6px 8px;border:1px solid #35617d;border-radius:999px}.stage.completed{border-color:var(--green)}.stage.active{border-color:var(--blue);box-shadow:0 0 8px #50c8ff55}.stage.failed,.stage.timedout{border-color:var(--red)}.stage.warning{border-color:var(--amber)}
.hidden{display:none}.sectionhead{display:flex;justify-content:space-between;align-items:center;margin:4px 0 12px}
@media(max-width:700px){main{padding:14px}header{padding:14px}nav{padding:10px 14px}}
</style>
</head>
<body>
<header><div><h1>Aegis Operations</h1><small>See the state. Know where to act.</small></div></header>
<nav id="nav">
<button data-view="overview" class="active">Overview</button>
<button data-view="applications">Applications</button>
<button data-view="flows">Flows</button>
<button data-view="diagnostics">Diagnostics</button>
<button data-view="messaging">Messaging</button>
<button data-view="sync">Synchronization</button>
<button data-view="alerts">Alerts</button>
<button data-view="runbooks">Runbooks</button>
<button data-view="log">Engineer's Log</button>
<button data-view="history">History</button>
</nav>
<main>
<section id="overview"><div class="metrics" id="metrics"></div><div class="grid" id="estate"></div></section>
<section id="applications" class="hidden"><div class="sectionhead"><h2>Applications</h2></div><div id="appsTable"></div></section>
<section id="flows" class="hidden"><div class="sectionhead"><h2>Flows</h2></div><div id="flowsList"></div></section>
<section id="diagnostics" class="hidden"><div class="sectionhead"><h2>Diagnostics</h2></div><div id="diagnosticsList"></div></section>
<section id="messaging" class="hidden"><div class="sectionhead"><h2>Messaging</h2></div><div id="messagingList"></div></section>
<section id="sync" class="hidden"><div class="sectionhead"><h2>Synchronization</h2></div><div id="syncList"></div></section>
<section id="alerts" class="hidden"><div class="sectionhead"><h2>Alerts</h2></div><div id="alertsTable"></div></section>
<section id="runbooks" class="hidden"><div class="sectionhead"><h2>Runbooks</h2></div><div id="runbooksList"></div></section>
<section id="log" class="hidden"><div class="sectionhead"><h2>Engineer's Log</h2></div><div class="panel"><input id="logTitle" placeholder="Title"><br><br><textarea id="logText" rows="5" placeholder="What happened?"></textarea><br><br><select id="logSeverity"><option>Informational</option><option>Warning</option><option>High</option><option>Critical</option></select><br><br><button class="action" onclick="createLog()">Record Entry</button></div><div id="logList"></div></section>
<section id="history" class="hidden"><div class="sectionhead"><h2>Operational History</h2></div><div id="historyList"></div></section>
</main>
<script>
const e=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const time=v=>v?new Date(v).toLocaleString():'—';
const cls=v=>String(v??'unknown').toLowerCase();
document.querySelectorAll('#nav button').forEach(b=>b.onclick=()=>{document.querySelectorAll('main>section').forEach(s=>s.classList.add('hidden'));document.getElementById(b.dataset.view).classList.remove('hidden');document.querySelectorAll('#nav button').forEach(x=>x.classList.remove('active'));b.classList.add('active')});
async function json(url,opts){const r=await fetch(url,{cache:'no-store',...opts});if(!r.ok)throw new Error('HTTP '+r.status);return await r.json()}
async function load(){
 let apps=[],flows=[],diagnostics=[],messaging=[],sync=[],alerts=[],runbooks=[],logs=[],history=[];
 try{[apps,flows,diagnostics,messaging,sync,alerts,runbooks,logs,history]=await Promise.all([
  json('/api/operations/applications'),
  json('/api/operations/flows'),
  json('/api/operations/diagnostics'),
  json('/api/operations/messaging'),
  json('/api/operations/synchronization'),
  json('/api/operations/alerts'),
  json('/api/operations/runbooks'),
  json('/api/operations/engineer-log'),
  json('/api/operations/history')
 ])}catch(err){console.error(err)}
 const count=s=>apps.filter(x=>x.state===s).length;
 metrics.innerHTML=[['Applications',apps.length],['Healthy',count('Healthy')],['Degraded',count('Degraded')+count('Warning')],['Failed',count('Failed')+count('Offline')],['Unknown',count('Unknown')],['Active Alerts',alerts.filter(x=>x.state!=='Resolved').length]].map(x=>'<div class="metric"><b>'+e(x[1])+'</b><span>'+e(x[0])+'</span></div>').join('');
 estate.innerHTML=apps.length?apps.map(x=>'<article class="card"><h3>'+e(x.displayName)+'</h3><div class="state '+cls(x.state)+'">'+e(x.state)+'</div><p>'+e(x.instanceId)+' · '+e(x.environment)+'</p><small class="muted">Observed '+e(time(x.observedAtUtc))+(x.stale?' · STALE':'')+'</small></article>').join(''):'<div class="panel muted">No application telemetry has been published yet.</div>';
 appsTable.innerHTML='<div class="panel"><table><thead><tr><th>Application</th><th>Instance</th><th>State</th><th>Registration</th><th>Configuration</th><th>Observed</th></tr></thead><tbody>'+apps.map(x=>'<tr><td><b>'+e(x.displayName)+'</b><br><small>'+e(x.applicationId)+'</small></td><td>'+e(x.instanceId)+'</td><td class="state '+cls(x.state)+'">'+e(x.state)+'</td><td>'+e(x.registrationStatus)+'</td><td>'+e(x.configurationStatus??'—')+'</td><td>'+e(time(x.observedAtUtc))+'</td></tr>').join('')+'</tbody></table></div>';
 flowsList.innerHTML=flows.length?flows.map(x=>'<article class="panel"><b>'+e(x.flowType)+'</b> · <code>'+e(x.correlationId)+'</code><div class="muted">'+e(x.applicationId)+' · '+e(time(x.observedAtUtc))+'</div><div class="flow">'+x.stages.map(s=>'<span class="stage '+cls(s.state)+'">'+e(s.name)+' · '+e(s.state)+'</span>').join(' → ')+'</div></article>').join(''):'<div class="panel muted">No flow telemetry available.</div>';
 diagnosticsList.innerHTML=diagnostics.length?diagnostics.map(x=>'<article class="panel"><b>'+e(x.component)+'</b> · '+e(x.category)+'<div class="state '+cls(x.state)+'">'+e(x.state)+'</div><p>'+e(x.summary)+'</p><small class="muted">'+e(x.applicationId)+' · '+e(time(x.observedAtUtc))+'</small></article>').join(''):'<div class="panel muted">No specialized diagnostics published.</div>';
 messagingList.innerHTML=messaging.length?messaging.map(x=>'<article class="panel"><b>'+e(x.applicationId)+'</b><p>Pending '+e(x.outboundPending)+' · Dispatching '+e(x.dispatching)+' · Retrying '+e(x.retrying)+' · Dead Letter '+e(x.deadLetter)+'</p><div class="flow">'+(x.providers||[]).map(p=>'<span class="stage '+cls(p.state)+'">'+e(p.provider)+' · '+e(p.state)+'</span>').join('')+'</div><small class="muted">Observed '+e(time(x.observedAtUtc))+'</small></article>').join(''):'<div class="panel muted">Messaging diagnostics not supported or not yet published.</div>';
 syncList.innerHTML=sync.length?'<div class="panel"><table><thead><tr><th>Application</th><th>Endpoint</th><th>Connectivity</th><th>Queue</th><th>Pending</th><th>Failed</th><th>Observed</th></tr></thead><tbody>'+sync.map(x=>'<tr><td>'+e(x.applicationId)+'</td><td>'+e(x.endpointId)+'</td><td>'+e(x.online?'Online':'Offline')+'</td><td class="state '+cls(x.queueState)+'">'+e(x.queueState)+'</td><td>'+e(x.pending)+'</td><td>'+e(x.failed)+'</td><td>'+e(time(x.observedAtUtc))+'</td></tr>').join('')+'</tbody></table></div>':'<div class="panel muted">No synchronization telemetry available.</div>';
 runbooksList.innerHTML=runbooks.length?runbooks.map(x=>'<article class="panel"><b>'+e(x.title)+'</b><div class="muted">'+e(x.applicationId)+' · '+e(x.runbookId)+'</div><p>'+e((x.sections||[]).join(' · '))+'</p><details><summary>Open runbook</summary><pre>'+e(x.content)+'</pre></details></article>').join(''):'<div class="panel muted">No runbooks registered.</div>';
 alertsTable.innerHTML='<div class="panel"><table><thead><tr><th>Severity</th><th>Alert</th><th>Application</th><th>State</th><th>Raised</th></tr></thead><tbody>'+alerts.map(x=>'<tr><td>'+e(x.severity)+'</td><td><b>'+e(x.title)+'</b><br><small>'+e(x.alertId)+'</small></td><td>'+e(x.applicationId)+'</td><td>'+e(x.state)+'</td><td>'+e(time(x.raisedAtUtc))+'</td></tr>').join('')+'</tbody></table></div>';
 logList.innerHTML=logs.map(x=>'<article class="panel"><b>'+e(x.title)+'</b> · '+e(x.severity)+'<p>'+e(x.text)+'</p><small class="muted">'+e(x.author)+' · '+e(time(x.atUtc))+' · '+e(x.entryId)+'</small></article>').join('');
 historyList.innerHTML=history.map(x=>'<div class="panel"><b>'+e(x.type)+'</b> · '+e(x.text)+'<br><small class="muted">'+e(x.application)+' · '+e(time(x.at))+' · '+e(x.id)+'</small></div>').join('');
}
async function createLog(){
 const body={entryId:'',atUtc:new Date().toISOString(),author:'',title:logTitle.value,text:logText.value,severity:logSeverity.value,tags:[]};
 try{await json('/api/operations/engineer-log',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify(body)});logTitle.value='';logText.value='';await load()}catch(err){alert('Unable to create Engineer\'s Log entry: '+err.message)}
}
load();setInterval(load,15000);
</script>
</body></html>
""";
}
