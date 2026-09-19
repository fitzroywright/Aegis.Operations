internal static class OperationsV1Page
{
    internal static string Render() => """
<!doctype html>
<html>
<head>
<meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Aegis Operations</title>
<style>
:root{--bg:#00101d;--panel:#001827;--line:#28597b;--text:#eef6ff;--muted:#9fb7c8;--green:#20d789;--amber:#ffad31;--red:#ff6b6b;--blue:#50c8ff;--purple:#8e61ff;--orange:#ffad31}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font:14px Segoe UI,Arial,sans-serif}
header{min-height:84px;padding:14px 24px;border-bottom:2px solid #4180aa;display:flex;align-items:center;gap:16px;background:#00101d}header h1{margin:0;font-size:30px}header h1 em{font-style:normal;color:var(--orange)}header small{color:#42bdff;font-weight:800;letter-spacing:.04em}.shell{display:grid;grid-template-columns:190px minmax(0,1fr);min-height:calc(100vh - 84px)}nav{background:#001b2e;border-right:1px solid #174f73;padding:20px 10px;display:flex;flex-direction:column;gap:4px}nav .navlabel{color:#6ea8c8;font-size:10px;font-weight:800;letter-spacing:.12em;padding:14px 10px 5px}nav button,nav a{background:transparent;color:var(--text);border:0;border-left:3px solid transparent;border-radius:5px;padding:10px 11px;cursor:pointer;text-align:left;font:inherit;font-weight:700;text-decoration:none}nav button:hover,nav a:hover{background:#0b2a40}nav button.active{background:var(--orange);color:#071522;border-left-color:var(--purple)}nav a.external{border:1px solid #28597b;margin-top:3px}main{padding:20px 24px;min-width:0}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(260px,1fr));gap:12px}.card,.panel{background:var(--panel);border:1px solid var(--line);border-radius:8px;padding:14px}.card h3{margin:0 0 7px}.muted{color:var(--muted)}
.state{font-weight:800}.healthy{color:var(--green)}.degraded,.warning,.maintenance{color:var(--amber)}.failed,.offline{color:var(--red)}.unknown,.notsupported{color:var(--muted)}
.metrics{display:grid;grid-template-columns:repeat(auto-fit,minmax(120px,1fr));gap:10px;margin-bottom:16px}.metric{background:#0a2233;border:1px solid var(--line);border-radius:8px;padding:13px}.metric b{display:block;font-size:25px}
table{width:100%;border-collapse:collapse}th,td{text-align:left;padding:9px;border-bottom:1px solid #29475b;vertical-align:top}
input,textarea,select{width:100%;background:#081b28;color:var(--text);border:1px solid #35617d;border-radius:5px;padding:8px}button.action{background:#167ee6;color:white;border:0;border-radius:5px;padding:8px 11px;cursor:pointer}
.flow{display:flex;gap:5px;align-items:center;flex-wrap:wrap}.stage{padding:6px 8px;border:1px solid #35617d;border-radius:999px}.stage.completed{border-color:var(--green)}.stage.active{border-color:var(--blue);box-shadow:0 0 8px #50c8ff55}.stage.failed,.stage.timedout{border-color:var(--red)}.stage.warning{border-color:var(--amber)}
.flowmap{position:relative;margin-top:14px;padding-top:28px}.flowrail{position:absolute;left:2%;right:2%;top:12px;height:3px;background:#29475b}.flowlight{position:absolute;top:3px;width:20px;height:20px;margin-left:-10px;border-radius:50%;transition:left .9s ease,background .25s,box-shadow .25s}.flowlight.active{background:var(--blue);box-shadow:0 0 8px var(--blue),0 0 20px #50c8ff88}.flowlight.warning{background:var(--amber);box-shadow:0 0 8px var(--amber),0 0 20px #ffad3188}.flowlight.failed,.flowlight.timedout{background:var(--red);box-shadow:0 0 8px var(--red),0 0 22px #ff6b6b88}.flowlight.completed{background:var(--green);box-shadow:0 0 8px var(--green),0 0 18px #20d78966}.flownodes{display:grid;grid-template-columns:repeat(var(--stage-count),minmax(90px,1fr));gap:6px}.flownode{text-align:center;font-size:12px}.flownode i{display:block;width:14px;height:14px;border-radius:50%;margin:-23px auto 9px;background:#587386;border:3px solid var(--bg)}.flownode.completed i{background:var(--green)}.flownode.active i{background:var(--blue)}.flownode.warning i{background:var(--amber)}.flownode.failed i,.flownode.timedout i{background:var(--red)}.flowdetail{margin-top:9px;color:var(--muted);font-size:12px}
.hidden{display:none}.sectionhead{display:flex;justify-content:space-between;align-items:center;margin:4px 0 12px}.sectionactions{display:flex;gap:8px;align-items:center}.handoff{display:inline-block;background:#102c40;color:var(--text);border:1px solid #50c8ff;border-radius:5px;padding:8px 11px;text-decoration:none;font-weight:700}.notice{border-left:4px solid var(--amber)}.telemetry-missing{color:var(--amber);font-weight:700}
@media(max-width:800px){.shell{grid-template-columns:1fr}nav{border-right:0;border-bottom:1px solid #174f73;display:flex;flex-direction:row;flex-wrap:wrap}.navlabel{width:100%}main{padding:14px}header{padding:14px}}
</style>
</head>
<body>
<header><div><h1>System <em>Operations</em></h1><small>SEE THE STATE. KNOW WHERE TO ACT.</small></div><div style="margin-left:auto;min-width:280px"><input id="searchBox" placeholder="Search application, correlation, alert, log…" onkeydown="if(event.key==='Enter')runSearch()"></div></header>
<div class="shell"><nav id="nav">
<div class="navlabel">COMMAND</div>
<button data-view="overview" class="active">▦ Overview</button>
<button data-view="applications">◇ Applications</button>
<button data-view="flows">⌁ Flows</button>
<button data-view="alerts">▲ Alerts</button>
<div class="navlabel">SYSTEMS</div>
<a class="external" href="/handoff/configuration">⚙ Configuration ↗</a>
<button data-view="diagnostics">◉ Diagnostics</button>
<a class="external" href="/handoff/diagnostics">Open Diagnostics ↗</a>
<a class="external" href="/registration-lifecycle">🔑 Registration</a>
<button data-view="messaging">✉ Messaging</button>
<div class="navlabel">ENGINEERING</div>
<button data-view="sync">⇄ Synchronization</button>
<button data-view="runbooks">▤ Runbooks</button>
<button data-view="log">✎ Engineer's Log</button>
<button data-view="history">◷ History</button>
<button data-view="advisor">◇ Advisor</button>
</nav>
<main>
<section id="overview"><div class="metrics" id="metrics"></div><div class="grid" id="estate"></div></section>
<section id="appdetail" class="hidden"><div class="sectionhead"><h2 id="appDetailTitle">Application</h2><button class="action" onclick="showView('applications')">← Applications</button></div><div id="appDetailBody"></div></section>
<section id="search" class="hidden"><div class="sectionhead"><h2>Search Results</h2><button class="action" onclick="showView('overview')">Close</button></div><div id="searchResults"></div></section>
<section id="applications" class="hidden"><div class="sectionhead"><div><h2>Registered Applications</h2><small class="muted">Configuration registration is authoritative; Operations telemetry is overlaid when available.</small></div><div class="sectionactions"><a class="handoff" href="/registration-lifecycle">Registration Lifecycle →</a><a class="handoff" href="/handoff/configuration">Open Configuration ↗</a></div></div><div id="registrationNotice"></div><div id="appsTable"></div></section>
<section id="flows" class="hidden"><div class="sectionhead"><h2>Flows</h2></div><div id="flowsList"></div></section>
<section id="diagnostics" class="hidden"><div class="sectionhead"><div><h2>Integrated Diagnostics</h2><small class="muted">Operational diagnostic observations published into Operations.</small></div><a class="handoff" href="/handoff/diagnostics">Open Aegis.Diagnostics ↗</a></div><div id="diagnosticsList"></div></section>
<section id="messaging" class="hidden"><div class="sectionhead"><h2>Messaging</h2></div><div id="messagingList"></div></section>
<section id="sync" class="hidden"><div class="sectionhead"><h2>Synchronization</h2></div><div id="syncList"></div></section>
<section id="alerts" class="hidden"><div class="sectionhead"><h2>Alerts</h2></div><div id="alertsTable"></div></section>
<section id="runbooks" class="hidden"><div class="sectionhead"><h2>Runbooks</h2></div><div id="runbooksList"></div></section>
<section id="log" class="hidden"><div class="sectionhead"><h2>Engineer's Log</h2></div><div class="panel"><input id="logTitle" placeholder="Title"><br><br><textarea id="logText" rows="5" placeholder="What happened?"></textarea><br><br><select id="logSeverity"><option>Informational</option><option>Warning</option><option>High</option><option>Critical</option></select><br><br><button class="action" onclick="createLog()">Record Entry</button></div><div id="logList"></div></section>
<section id="history" class="hidden"><div class="sectionhead"><h2>Operational History</h2></div><div id="historyList"></div></section>
<section id="advisor" class="hidden"><div class="sectionhead"><h2>Operations Advisor</h2><button class="action" onclick="loadAdvisor()">Refresh Analysis</button></div><div class="panel muted">Read-only, evidence-based guidance. It does not execute repairs or infer health from missing telemetry.</div><div id="advisorList"></div></section>
</main></div>
<script>
const e=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const time=v=>v?new Date(v).toLocaleString():'—';
const cls=v=>String(v??'unknown').toLowerCase();
function flowViz(x){
 const stages=x.stages||[];
 if(!stages.length)return '<div class="muted">No stage telemetry.</div>';
 let idx=stages.findIndex(s=>s.name===x.currentStage);
 if(idx<0)idx=stages.findIndex(s=>['Active','Warning','Failed','TimedOut'].includes(s.state));
 if(idx<0)idx=Math.max(0,stages.map(s=>s.state).lastIndexOf('Completed'));
 const active=stages[idx]||stages[0];
 const pct=stages.length===1?50:(idx/(stages.length-1))*96+2;
 const nodes=stages.map(s=>'<div class="flownode '+cls(s.state)+'"><i></i><b>'+e(s.name)+'</b><br><span>'+e(s.state)+'</span></div>').join('');
 const err=active?.error?'<br><span class="state failed">'+e(active.error)+'</span>':'';
 return '<div class="flowmap" style="--stage-count:'+stages.length+'"><div class="flowrail"></div><div class="flowlight '+cls(active?.state)+'" style="left:'+pct+'%"></div><div class="flownodes">'+nodes+'</div></div><div class="flowdetail">Current: <b>'+e(active?.name??'Unknown')+'</b> · '+e(active?.state??'Unknown')+err+'</div>';
}
function showView(id){document.querySelectorAll('main>section').forEach(s=>s.classList.add('hidden'));document.getElementById(id).classList.remove('hidden');document.querySelectorAll('#nav button').forEach(x=>x.classList.toggle('active',x.dataset.view===id))}
document.querySelectorAll('#nav button').forEach(b=>b.onclick=()=>{showView(b.dataset.view);if(b.dataset.view==='advisor')loadAdvisor()});
async function json(url,opts){const r=await fetch(url,{cache:'no-store',...opts});if(!r.ok)throw new Error('HTTP '+r.status);return await r.json()}
async function load(){
 let apps=[],flows=[],diagnostics=[],messaging=[],sync=[],alerts=[],runbooks=[],logs=[],history=[],inventory={authorityAvailable:false,applications:[]};
 try{[inventory,flows,diagnostics,messaging,sync,alerts,runbooks,logs,history]=await Promise.all([
  json('/api/operations/registered-applications'),
  json('/api/operations/flows'),
  json('/api/operations/diagnostics'),
  json('/api/operations/messaging'),
  json('/api/operations/synchronization'),
  json('/api/operations/alerts'),
  json('/api/operations/runbooks'),
  json('/api/operations/engineer-log'),
  json('/api/operations/history')
 ])}catch(err){console.error(err)}
 apps=inventory.applications||[];
 registrationNotice.innerHTML=inventory.authorityAvailable?'':('<div class="panel notice"><b>Registration authority unavailable.</b><p>'+e(inventory.authorityError||'Operations could not retrieve the authoritative Configuration registration inventory.')+'</p><small class="muted">Telemetry may still be shown, but it is not authoritative registration proof.</small></div>');
 const count=s=>apps.filter(x=>x.state===s).length;
 metrics.innerHTML=[['Applications',apps.length],['Healthy',count('Healthy')],['Degraded',count('Degraded')+count('Warning')],['Failed',count('Failed')+count('Offline')],['Unknown',count('Unknown')],['Active Alerts',alerts.filter(x=>x.state!=='Resolved').length]].map(x=>'<div class="metric"><b>'+e(x[1])+'</b><span>'+e(x[0])+'</span></div>').join('');
 estate.innerHTML=apps.length?apps.map(x=>'<article class="card" style="cursor:pointer" onclick="openApp(\''+e(x.applicationId)+'\')"><h3>'+e(x.displayName)+'</h3><div class="state '+cls(x.state)+'">'+e(x.state)+'</div><p>'+e(x.instanceId)+' · '+e(x.environment)+'</p><small>'+e(x.registrationStatus)+'</small><br><small class="'+(x.telemetryAvailable?'muted':'telemetry-missing')+'">'+(x.telemetryAvailable?('Observed '+e(time(x.observedAtUtc))+(x.stale?' · STALE':'')):'REGISTERED · NO OPERATIONAL TELEMETRY')+'</small></article>').join(''):'<div class="panel muted">No registered applications or telemetry records are available.</div>';
 appsTable.innerHTML='<div class="panel"><table><thead><tr><th>Application</th><th>Instance</th><th>Operational State</th><th>Registration</th><th>Configuration</th><th>Telemetry</th></tr></thead><tbody>'+apps.map(x=>'<tr style="cursor:pointer" onclick="openApp(\''+e(x.applicationId)+'\')"><td><b>'+e(x.displayName)+'</b><br><small>'+e(x.applicationId)+'</small></td><td>'+e(x.instanceId)+'</td><td class="state '+cls(x.state)+'">'+e(x.state)+'</td><td><b>'+e(x.registrationStatus)+'</b><br><small>'+e(x.registrationHealth||'—')+'</small></td><td>'+e(x.configurationStatus??'—')+'</td><td>'+(x.telemetryAvailable?e(time(x.observedAtUtc)):'<span class="telemetry-missing">No telemetry</span>')+'</td></tr>').join('')+'</tbody></table></div>';
 flowsList.innerHTML=flows.length?flows.map(x=>'<article class="panel"><b>'+e(x.flowType)+'</b> · <code>'+e(x.correlationId)+'</code><div class="muted">'+e(x.applicationId)+' · '+e(time(x.observedAtUtc))+'</div>'+flowViz(x)+'</article>').join(''):'<div class="panel muted">No flow telemetry available.</div>';
 diagnosticsList.innerHTML=diagnostics.length?diagnostics.map(x=>'<article class="panel"><b>'+e(x.component)+'</b> · '+e(x.category)+'<div class="state '+cls(x.state)+'">'+e(x.state)+'</div><p>'+e(x.summary)+'</p><small class="muted">'+e(x.applicationId)+' · '+e(time(x.observedAtUtc))+'</small></article>').join(''):'<div class="panel muted">No specialized diagnostics published.</div>';
 messagingList.innerHTML=messaging.length?messaging.map(x=>'<article class="panel"><b>'+e(x.applicationId)+'</b><p>Pending '+e(x.outboundPending)+' · Dispatching '+e(x.dispatching)+' · Retrying '+e(x.retrying)+' · Dead Letter '+e(x.deadLetter)+'</p><div class="flow">'+(x.providers||[]).map(p=>'<span class="stage '+cls(p.state)+'">'+e(p.provider)+' · '+e(p.state)+'</span>').join('')+'</div><small class="muted">Observed '+e(time(x.observedAtUtc))+'</small></article>').join(''):'<div class="panel muted">Messaging diagnostics not supported or not yet published.</div>';
 syncList.innerHTML=sync.length?'<div class="panel"><table><thead><tr><th>Application</th><th>Endpoint</th><th>Connectivity</th><th>Queue</th><th>Pending</th><th>Failed</th><th>Observed</th></tr></thead><tbody>'+sync.map(x=>'<tr><td>'+e(x.applicationId)+'</td><td>'+e(x.endpointId)+'</td><td>'+e(x.online?'Online':'Offline')+'</td><td class="state '+cls(x.queueState)+'">'+e(x.queueState)+'</td><td>'+e(x.pending)+'</td><td>'+e(x.failed)+'</td><td>'+e(time(x.observedAtUtc))+'</td></tr>').join('')+'</tbody></table></div>':'<div class="panel muted">No synchronization telemetry available.</div>';
 runbooksList.innerHTML=runbooks.length?runbooks.map(x=>'<article class="panel"><b>'+e(x.title)+'</b><div class="muted">'+e(x.applicationId)+' · '+e(x.runbookId)+'</div><p>'+e((x.sections||[]).join(' · '))+'</p><details><summary>Open runbook</summary><pre>'+e(x.content)+'</pre></details></article>').join(''):'<div class="panel muted">No runbooks registered.</div>';
 alertsTable.innerHTML='<div class="panel"><table><thead><tr><th>Severity</th><th>Alert</th><th>Application</th><th>State</th><th>Raised</th><th>Actions</th></tr></thead><tbody>'+alerts.map(x=>'<tr><td>'+e(x.severity)+'</td><td><b>'+e(x.title)+'</b><br><small>'+e(x.alertId)+'</small></td><td>'+e(x.applicationId)+'</td><td>'+e(x.state)+'</td><td>'+e(time(x.raisedAtUtc))+'</td><td>'+(x.state==='Raised'?'<button class="action" onclick="alertAction(\''+e(x.alertId)+'\',\'acknowledge\')">Acknowledge</button> ':'')+(x.state!=='Resolved'?'<button class="action" onclick="alertAction(\''+e(x.alertId)+'\',\'resolve\')">Resolve</button>':'')+'</td></tr>').join('')+'</tbody></table></div>';
 logList.innerHTML=logs.map(x=>'<article class="panel"><b>'+e(x.title)+'</b> · '+e(x.severity)+'<p>'+e(x.text)+'</p><small class="muted">'+e(x.author)+' · '+e(time(x.atUtc))+' · '+e(x.entryId)+'</small></article>').join('');
 historyList.innerHTML=history.map(x=>'<div class="panel"><b>'+e(x.type)+'</b> · '+e(x.text)+'<br><small class="muted">'+e(x.application)+' · '+e(time(x.at))+' · '+e(x.id)+'</small></div>').join('');
}
async function openApp(id){
 try{
  const d=await json('/api/operations/applications/'+encodeURIComponent(id));
  appDetailTitle.textContent=id;
  const inst=(d.instances||[]).map(x=>'<article class="card"><b>'+e(x.instanceId)+'</b><div class="state '+cls(x.state)+'">'+e(x.state)+'</div><small>'+e(time(x.observedAtUtc))+'</small></article>').join('');
  const di=(d.diagnostics||[]).map(x=>'<div class="panel"><b>'+e(x.component)+'</b> · '+e(x.category)+' · <span class="state '+cls(x.state)+'">'+e(x.state)+'</span><br>'+e(x.summary)+'</div>').join('');
  const fl=(d.flows||[]).map(x=>'<div class="panel"><b>'+e(x.flowType)+'</b> · <code>'+e(x.correlationId)+'</code>'+flowViz(x)+'</div>').join('');
  const al=(d.alerts||[]).map(x=>'<div class="panel"><b>'+e(x.severity)+' · '+e(x.title)+'</b><br><small>'+e(x.state)+' · '+e(x.alertId)+'</small></div>').join('');
  const rb=(d.runbooks||[]).map(x=>'<div class="panel"><b>'+e(x.title)+'</b><br><small>'+e(x.runbookId)+'</small></div>').join('');
  appDetailBody.innerHTML='<h3>Instances</h3><div class="grid">'+(inst||'<div class="panel muted">None</div>')+'</div><h3>Diagnostics</h3>'+(di||'<div class="panel muted">Not Supported</div>')+'<h3>Recent Flows</h3>'+(fl||'<div class="panel muted">Not Supported</div>')+'<h3>Alerts</h3>'+(al||'<div class="panel muted">None</div>')+'<h3>Runbooks</h3>'+(rb||'<div class="panel muted">Not Supported</div>');
  showView('appdetail');
 }catch(err){alert('Unable to load application detail: '+err.message)}
}
async function alertAction(id,action){
 try{await json('/api/operations/alerts/'+encodeURIComponent(id)+'/'+action,{method:'POST'});await load()}catch(err){alert('Alert action failed: '+err.message)}
}
async function runSearch(){
 const q=searchBox.value.trim();if(q.length<2)return;
 try{
  const results=await json('/api/operations/search?q='+encodeURIComponent(q));
  searchResults.innerHTML=results.length?results.map(x=>'<div class="panel"><b>'+e(x.type)+' · '+e(x.title)+'</b><br><small>'+e(x.id)+' · '+e(x.detail)+'</small></div>').join(''):'<div class="panel muted">No matches.</div>';
  showView('search');
 }catch(err){alert('Search failed: '+err.message)}
}
async function loadAdvisor(){
 try{
  const d=await json('/api/operations/advisor');
  const findings=d.findings||[];
  advisorList.innerHTML='<div class="panel"><b>'+e(d.summary)+'</b><br><small class="muted">Generated '+e(time(d.generatedAtUtc))+' · '+e(d.mode)+'</small></div>'+
   (findings.length?findings.map(x=>'<article class="panel"><b>'+e(x.severity)+' · '+e(x.area)+'</b><h3>'+e(x.summary)+'</h3><p>'+e(x.evidence)+'</p><p><b>Next check:</b> '+e(x.suggestedNextCheck)+'</p><small class="muted">'+e(x.applicationId??'Estate')+(x.correlationId?' · '+e(x.correlationId):'')+(x.runbook?' · Runbook: '+e(x.runbook):'')+'</small></article>').join(''):'');
 }catch(err){advisorList.innerHTML='<div class="panel state failed">Advisor unavailable: '+e(err.message)+'</div>'}
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
