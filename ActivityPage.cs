using System.Text.Encodings.Web;

internal static class ActivityPage
{
    internal static string Render()
    {
        return """
<!doctype html>
<html>
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Activity · Aegis Operations</title>
<style>
:root{--blue:#5899c6;--gold:#e7b94f;--salmon:#d9796b;--ink:#08090b;--panel:#11151b;--line:#27313b;--text:#edf3f7;--muted:#93a2ae;--good:#75d69b;--warn:#e7b94f;--bad:#f16f69;--skip:#7f8b96}
*{box-sizing:border-box}body{margin:0;background:var(--ink);color:var(--text);font:14px/1.4 ui-monospace,SFMono-Regular,Consolas,monospace}
header{height:94px;display:flex;align-items:flex-end;gap:10px;padding:16px 50px 12px 44px;border-bottom:10px solid var(--blue)}
.brand{font-family:Arial,sans-serif;margin-right:auto}.brand b{display:block;font-size:24px;letter-spacing:2px;text-transform:uppercase}.brand span{color:var(--muted);font-size:12px}
.top a,.tabs button,.action{border:0;border-radius:18px;padding:9px 16px;text-decoration:none;color:#050505;font-weight:800;cursor:pointer}.top a{background:var(--blue);margin-left:6px}.top a.active{background:var(--gold)}
main{padding:26px 50px 44px}.head{display:flex;align-items:flex-end;justify-content:space-between;margin-bottom:18px}.head h1{margin:0;font:700 28px Arial,sans-serif}.head p{margin:5px 0 0;color:var(--muted)}
.tabs{display:flex;gap:8px}.tabs button{background:#28333e;color:var(--text)}.tabs button.active{background:var(--gold);color:#050505}
.filters{display:grid;grid-template-columns:repeat(6,minmax(120px,1fr));gap:9px;padding:14px;background:var(--panel);border:1px solid var(--line);border-radius:12px;margin-bottom:12px}
.filters input,.filters select{width:100%;background:#090c10;color:var(--text);border:1px solid #36434f;border-radius:7px;padding:9px}.filters .wide{grid-column:span 2}
.toolbar{display:flex;align-items:center;gap:8px;margin-bottom:10px}.action{background:var(--blue);font-size:12px}.action.secondary{background:#35414d;color:var(--text)}.state{margin-left:auto;color:var(--muted)}
.panel{border:1px solid var(--line);border-radius:12px;background:#090c10;overflow:hidden}.log{height:calc(100vh - 330px);min-height:390px;overflow:auto;padding:8px 0}
.row{display:grid;grid-template-columns:125px 170px 135px 110px 1fr 110px;gap:10px;padding:7px 12px;border-bottom:1px solid #151b21;align-items:center}.row:hover{background:#111922}.row button{background:none;border:0;color:inherit;text-align:left;font:inherit;cursor:pointer;padding:0}.time{color:#8fa0ad}.app{color:#8cc8ef}.flow{color:#d7a8ff}.outcome{font-weight:800}.Succeeded{color:var(--good)}.Warning{color:var(--warn)}.Failed{color:var(--bad)}.Skipped{color:var(--skip)}.Started{color:#8cc8ef}.detail{white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.analyze{color:var(--gold)!important;text-align:right!important}
.empty{padding:40px;text-align:center;color:var(--muted)}
.traces{height:calc(100vh - 330px);min-height:390px;overflow:auto;padding:12px}.trace{border:1px solid var(--line);border-radius:10px;margin-bottom:12px;background:#0d1116;overflow:hidden}.trace-head{display:flex;gap:14px;align-items:center;padding:12px 14px;background:#121820}.trace-head b{font-size:15px}.trace-head .grow{flex:1}.trace-meta{color:var(--muted);font-size:12px}.stages{display:flex;align-items:flex-start;gap:0;padding:20px 18px;overflow-x:auto}.stage{min-width:150px;max-width:210px;position:relative;text-align:center}.stage:not(:last-child):after{content:"";position:absolute;top:12px;left:60%;width:80%;height:3px;background:#34404b}.lamp{position:relative;z-index:1;width:24px;height:24px;border-radius:50%;margin:0 auto 8px;background:#63707c;box-shadow:0 0 0 4px #161c22}.lamp.Succeeded{background:var(--good)}.lamp.Warning{background:var(--warn)}.lamp.Failed{background:var(--bad)}.lamp.Started{background:var(--blue);animation:pulse 1.2s infinite}.lamp.Skipped{background:var(--skip)}@keyframes pulse{50%{box-shadow:0 0 0 8px rgba(88,153,198,.18)}}.stage b{display:block;font-size:12px}.stage small{display:block;color:var(--muted);margin-top:4px}.trace-detail{padding:0 16px 15px;color:#b8c4cd}
@media(max-width:1000px){.filters{grid-template-columns:repeat(2,1fr)}.row{grid-template-columns:100px 1fr 100px}.row .flow,.row .stagecol{display:none}header{padding-right:20px}main{padding:20px}.top{display:none}}
</style>
</head>
<body>
<header>
 <div class="brand"><b>Aegis Operations</b><span id="clock">ACTIVITY TELEMETRY</span></div>
 <nav class="top"><a href="/">Operations</a><a class="active" href="/activity">Activity</a><a href="https://diagnostics.ffpja.org">Diagnostics</a><a href="https://config.ffpja.org">Configuration</a></nav>
</header>
<main>
 <section class="head"><div><h1>Activity & Flow Analyzer</h1><p>Live lifecycle telemetry. Select any event to reconstruct its correlated flow.</p></div><div class="tabs"><button id="logTab" class="active">Activity Log</button><button id="flowTab">Flow Analyzer</button></div></section>
 <section class="filters">
   <select id="application"><option value="">All applications</option></select>
   <select id="instance"><option value="">All instances</option></select>
   <select id="flow"><option value="">All flows</option></select>
   <select id="outcome"><option value="">All outcomes</option></select>
   <input id="correlation" placeholder="Correlation ID">
   <input id="sinceMinutes" type="number" min="1" max="10080" value="60" placeholder="Minutes">
   <input class="wide" id="text" placeholder="Search stage, code, detail, business ID…">
   <input id="stage" placeholder="Stage">
   <button class="action" id="apply">Apply Filter</button>
   <button class="action secondary" id="reset">Reset</button>
 </section>
 <div class="toolbar"><button class="action secondary" id="pause">Pause</button><button class="action secondary" id="follow">Follow Tail: On</button><button class="action secondary" id="clear">Clear View</button><span class="state" id="state">CONNECTING…</span></div>
 <section class="panel"><div id="logView" class="log"></div><div id="flowView" class="traces" hidden></div></section>
</main>
<script>
const $=id=>document.getElementById(id), esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#039;'}[c]));
let paused=false,followTail=true,mode='log',hiddenBefore=null,lastCount=0;
function clock(){const d=new Date();$('clock').textContent=d.toLocaleString(undefined,{month:'short',day:'2-digit',year:'numeric',hour:'2-digit',minute:'2-digit',second:'2-digit'}).toUpperCase()}clock();setInterval(clock,1000);
function params(){
 const p=new URLSearchParams(), map={applicationId:'application',instanceId:'instance',flow:'flow',outcome:'outcome',correlationId:'correlation',stage:'stage',text:'text'};
 for(const [q,id] of Object.entries(map)){const v=$(id).value.trim();if(v)p.set(q,v)}
 const m=parseInt($('sinceMinutes').value||'0',10);if(m>0)p.set('since',new Date(Date.now()-m*60000).toISOString());
 p.set('take',mode==='log'?'1000':'250');return p;
}
function setOptions(id,values,label){const el=$(id),current=el.value;el.innerHTML='<option value="">All '+label+'</option>'+values.map(v=>'<option>'+esc(v)+'</option>').join('');el.value=current}
async function loadFilters(){try{const r=await fetch('/api/operations/activity/filters',{cache:'no-store'});if(!r.ok)return;const x=await r.json();setOptions('application',x.applications||[],'applications');setOptions('instance',x.instances||[],'instances');setOptions('flow',x.flows||[],'flows');setOptions('outcome',x.outcomes||[],'outcomes')}catch{}}
function fmt(v){const d=new Date(v);return d.toLocaleTimeString(undefined,{hour12:false})+'.'+String(d.getMilliseconds()).padStart(3,'0')}
function detail(e){return [e.stage,e.code,e.detail].filter(Boolean).join(' · ')}
function analyze(c){$('correlation').value=c;mode='flow';tabs();refresh()}
window.analyze=analyze;
async function loadLog(){
 const r=await fetch('/api/operations/activity?'+params(),{cache:'no-store'});if(!r.ok)throw new Error('HTTP '+r.status);const x=await r.json();let events=x.events||[];
 if(hiddenBefore)events=events.filter(e=>new Date(e.occurredAtUtc)>hiddenBefore);
 $('logView').innerHTML=events.length?events.map(e=>`<div class="row"><span class="time">${fmt(e.occurredAtUtc)}</span><span class="app">${esc(e.applicationId)}</span><span class="flow">${esc(e.flow)}</span><span class="outcome ${esc(e.outcome)}">${esc(e.outcome)}</span><button class="detail" title="${esc(detail(e))}" onclick="analyze('${esc(e.correlationId)}')">${esc(detail(e))}</button><button class="analyze" onclick="analyze('${esc(e.correlationId)}')">Analyze →</button></div>`).join(''):'<div class="empty">No lifecycle events match this filter.</div>';
 lastCount=events.length;$('state').textContent=events.length+' EVENTS · '+new Date().toLocaleTimeString();
 if(followTail){const el=$('logView');el.scrollTop=el.scrollHeight}
}
async function loadFlows(){
 const r=await fetch('/api/operations/activity/flows?'+params(),{cache:'no-store'});if(!r.ok)throw new Error('HTTP '+r.status);const x=await r.json(),traces=x.traces||[];
 $('flowView').innerHTML=traces.length?traces.map(t=>`<article class="trace"><div class="trace-head"><b class="${esc(t.status)}">${esc(t.status)}</b><div class="grow"><b>${esc(t.flow)}</b><div class="trace-meta">${esc(t.applicationId)} / ${esc(t.instanceId)} · ${esc(t.correlationId)}</div></div><div class="trace-meta">${t.durationMs} ms · ${t.eventCount} events</div></div><div class="stages">${(t.stages||[]).map(s=>`<div class="stage"><div class="lamp ${esc(s.outcome)}"></div><b>${esc(s.stage)}</b><small>${esc(s.outcome)} · ${fmt(s.occurredAtUtc)}</small></div>`).join('')}</div>${t.relatedBusinessId?'<div class="trace-detail">Business ID: '+esc(t.relatedBusinessId)+'</div>':''}</article>`).join(''):'<div class="empty">No correlated flows match this filter.</div>';
 $('state').textContent=traces.length+' FLOWS · '+new Date().toLocaleTimeString()
}
async function refresh(){if(paused)return;try{mode==='log'?await loadLog():await loadFlows()}catch(e){$('state').textContent='TELEMETRY UNAVAILABLE · '+e.message}}
function tabs(){$('logTab').classList.toggle('active',mode==='log');$('flowTab').classList.toggle('active',mode==='flow');$('logView').hidden=mode!=='log';$('flowView').hidden=mode!=='flow'}
$('logTab').onclick=()=>{mode='log';tabs();refresh()};$('flowTab').onclick=()=>{mode='flow';tabs();refresh()};
$('apply').onclick=refresh;$('reset').onclick=()=>{for(const id of ['application','instance','flow','outcome','correlation','stage','text'])$(id).value='';$('sinceMinutes').value='60';hiddenBefore=null;refresh()};
$('pause').onclick=()=>{paused=!paused;$('pause').textContent=paused?'Resume':'Pause';$('state').textContent=paused?'PAUSED · INGESTION CONTINUES':'RESUMED';if(!paused)refresh()};
$('follow').onclick=()=>{followTail=!followTail;$('follow').textContent='Follow Tail: '+(followTail?'On':'Off')};
$('clear').onclick=()=>{hiddenBefore=new Date();$('logView').innerHTML='<div class="empty">View cleared. New events will appear here.</div>';$('flowView').innerHTML='<div class="empty">View cleared. Adjust filters or wait for new flows.</div>';lastCount=0};
loadFilters();refresh();setInterval(()=>{loadFilters();refresh()},2000);
</script>
</body>
</html>
""";
    }
}
