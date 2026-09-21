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
<title>Activity & Flow Analyzer · Operations</title>
<style>
:root{
  --black:#000;--alpha-blue:#5588ee;--arctic-ice:#66ccff;--arctic-snow:#99ccff;
  --beta-blue:#7799dd;--night-cloud:#344470;--night-rain:#455580;--radioactive:#88eeff;
  --starship-white:#f3f3fc;--sunset-red:#ff3500;--peach:#99ccff;--salmon:#7799dd;
  --gold:#88eeff;--blue:#5588ee;--cyan:#66ccff;--green:#88eeff;--yellow:#99ccff;
  --red:#ff3500;--white:#f3f3fc;--muted:#99ccff;--line:#455580;--line-soft:#344470;
  --panel:#071020;--title-gold:#ffcc66;--rail-width:96px;--top-arm-height:14px;
  --workspace-arm-height:48px;--elbow-radius:18px;--scroll-arrow-space:18px;
  --top-right-inset:18px;--header-gap:18px
}
*{box-sizing:border-box}
html,body{width:100%;height:100%;margin:0;overflow:hidden;background:#000;color:var(--white);font-family:"Arial Narrow","Roboto Condensed","Segoe UI",Arial,sans-serif}
body{position:relative}a{color:inherit;text-decoration:none}

/* LCARS top shell */
.lcars-top{position:fixed;left:8px;right:var(--top-right-inset);top:8px;height:140px;z-index:20;background:#000}
.lcars-top .left-vertical{position:absolute;left:0;top:0;width:var(--rail-width);height:140px;background:var(--alpha-blue);border-radius:28px 0 0 28px}
.lcars-top .bottom-arm{position:absolute;left:var(--rail-width);right:0;top:126px;height:var(--top-arm-height);background:var(--alpha-blue);z-index:1;border-radius:0 9px 9px 0}
.lcars-top::after{content:"";position:absolute;left:var(--rail-width);top:calc(126px - var(--elbow-radius));width:var(--elbow-radius);height:var(--elbow-radius);background:radial-gradient(circle at 100% 0,transparent 0 calc(var(--elbow-radius) - 1px),var(--alpha-blue) var(--elbow-radius));z-index:4;pointer-events:none}
.brand{position:absolute;left:calc(var(--rail-width) + 18px);top:8px;width:320px;height:96px;z-index:3}
.brand .section{font-size:36px;line-height:1;color:var(--arctic-ice);font-weight:900;text-transform:uppercase;margin-top:10px}
.brand .org{font-size:10px;color:var(--arctic-snow);font-weight:800;text-transform:uppercase;margin-top:5px;max-width:220px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.brand .datetime{margin-top:8px;font-weight:900;text-transform:uppercase;color:var(--arctic-ice);letter-spacing:.04em;font-size:14px;white-space:nowrap}.brand .brand-user{margin-top:4px;color:var(--arctic-snow);font:800 10px/1 Arial,sans-serif;letter-spacing:.04em;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;max-width:260px}

.live-log{position:absolute;left:470px;top:8px;right:calc(250px + 50px + var(--header-gap));height:108px;overflow:hidden;font:13px/1.35 ui-monospace,SFMono-Regular,Consolas,monospace;padding:0}
.live-log .recent-title{display:flex;align-items:center;width:100%;height:18px;background:var(--title-gold);color:#071018;font:900 10px/18px Arial,sans-serif;letter-spacing:.12em;text-transform:uppercase;padding:0 8px;border-radius:3px 3px 0 0}
.live-log .recent-rows{height:90px;overflow:hidden;padding:3px 10px 0;scrollbar-width:none;-ms-overflow-style:none}
.live-log .recent-rows::-webkit-scrollbar{display:none;width:0;height:0}
.live-log .row{white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.live-log .t{display:inline-block;min-width:176px;color:var(--arctic-snow)}.live-log .inf{color:var(--green);font-weight:900}.live-log .wrn{color:var(--yellow);font-weight:900}.live-log .err{color:var(--red);font-weight:900}.live-log .src{color:var(--cyan)}

.top-buttons{position:absolute;right:50px;top:18px;width:250px;height:72px;display:grid;grid-template-columns:1fr 1fr;gap:8px 10px;padding-left:12px}
.top-buttons a{border:0;border-radius:8px;font-weight:900;text-transform:uppercase;color:#071018;cursor:pointer;font-size:12px;display:flex;align-items:center;justify-content:center;line-height:1;padding:0 8px;box-shadow:inset 0 1px 0 rgba(255,255,255,.28),0 1px 0 rgba(0,0,0,.55)}
.top-buttons .gold{background:var(--gold)}.top-buttons .blue{background:var(--blue)}.top-buttons .salmon{background:var(--salmon)}.top-buttons .gray{background:var(--peach)}
.top-buttons a,.workspace-nav a{transition:background .12s ease,color .12s ease,filter .12s ease}
.top-buttons a.nav-flash,.workspace-nav a.nav-flash{background:var(--title-gold)!important;color:#071018!important;filter:brightness(1.08)}

/* LCARS workspace */
.workspace-shell{position:fixed;left:8px;right:14px;top:calc(148px + var(--scroll-arrow-space));bottom:var(--scroll-arrow-space);min-height:0;overflow:hidden;z-index:10}
.workspace-leg{position:absolute;left:0;top:0;bottom:0;width:var(--rail-width);background:var(--alpha-blue);border-radius:28px 0 0 28px}
.workspace-arm{position:absolute;left:var(--rail-width);right:0;top:0;height:var(--workspace-arm-height);background:var(--title-gold);display:flex;align-items:center;padding:0;z-index:3;border-radius:0 9px 9px 0}
.workspace-arm::before{content:"";position:absolute;left:0;top:0;width:var(--elbow-radius);height:100%;background:var(--alpha-blue)}
.workspace-arm .workspace-title{position:relative;z-index:1;height:100%;display:flex;align-items:center;font-weight:900;text-transform:uppercase;color:#071018;letter-spacing:.055em;font-size:30px;line-height:1;margin-left:var(--elbow-radius);padding:0 1ch;border-left:6px solid #000;border-right:6px solid #000}.workspace-arm .last-scan{margin-left:auto;margin-right:12px;color:#3b2f16;font:700 9px/1 Arial,sans-serif;letter-spacing:.06em;text-transform:uppercase;white-space:nowrap;opacity:.9}
.workspace-shell::after{content:"";position:absolute;left:var(--rail-width);top:var(--workspace-arm-height);width:var(--elbow-radius);height:var(--elbow-radius);background:radial-gradient(circle at 100% 100%,transparent 0 calc(var(--elbow-radius) - 1px),var(--alpha-blue) var(--elbow-radius));z-index:5;pointer-events:none}
.workspace-nav{position:absolute;left:0;top:calc(var(--workspace-arm-height) + var(--scroll-arrow-space));width:var(--rail-width);z-index:2}
.workspace-nav a{display:flex;align-items:center;height:48px;padding-left:11px;color:#071018;font-weight:900;text-transform:uppercase;font-size:11px;border-bottom:3px solid #000;cursor:pointer}
.workspace-nav a.active{font-size:12px;background:var(--title-gold);color:#071018}.lcars-back{position:absolute;left:0;bottom:0;width:var(--rail-width);height:48px;z-index:7;border:0;border-top:3px solid #000;border-radius:0 0 0 28px;background:var(--alpha-blue);color:#071018;font:900 12px/1 "Arial Narrow","Roboto Condensed","Segoe UI",Arial,sans-serif;text-transform:uppercase;text-align:left;padding:0 0 0 11px;cursor:pointer}.lcars-back:hover,.lcars-back:focus-visible{background:var(--title-gold);outline:none}.lcars-back:active{background:var(--title-gold);filter:brightness(1.08)}
.detail-scroll{position:absolute;left:var(--rail-width);right:0;top:calc(var(--workspace-arm-height) + var(--scroll-arrow-space));bottom:var(--scroll-arrow-space);min-height:0;overflow-y:auto;overflow-x:hidden;overscroll-behavior:contain;background:#000;padding:18px 22px 24px 24px;scrollbar-color:var(--salmon) #111}
.detail-scroll::-webkit-scrollbar{width:10px}.detail-scroll::-webkit-scrollbar-track{background:#090909}.detail-scroll::-webkit-scrollbar-thumb{background:var(--salmon);border-radius:10px}

.control-plane-health-banner{display:none;margin:0 0 14px;padding:11px 13px;border:1px solid #c88a27;border-top:3px solid #e5a83f;border-radius:9px;background:#6d4911;color:#ffe1a3}.control-plane-health-banner strong{display:block;color:#ffd27a;font-size:12px;letter-spacing:.08em;text-transform:uppercase;margin-bottom:3px}.control-plane-health-banner.critical{background:#4a1711;border-color:#b94832;border-top-color:#e25b40;color:#ffd0c7}.control-plane-health-banner.critical strong{color:#ffb0a1}.control-plane-health-banner.health-unavailable{background:#30280e;border-color:#8f7620;color:#f0dfa0}
.page-head{display:flex;align-items:flex-start;justify-content:space-between;gap:20px;margin-bottom:14px}.page-head p{margin:0;color:var(--starship-white)}
.filters{display:grid;grid-template-columns:repeat(6,minmax(120px,1fr));gap:9px;padding:14px;background:linear-gradient(180deg,rgba(8,27,42,.50),rgba(1,7,12,.40));border:1px solid var(--line-soft);border-top:2px solid var(--line);border-radius:10px;margin-bottom:12px}
.filters input,.filters select{width:100%;background:#02080d;color:var(--white);border:1px solid var(--line);border-radius:6px;padding:9px;font:13px "Arial Narrow","Segoe UI",Arial,sans-serif}
.filters .wide{grid-column:span 2}
.toolbar{display:flex;align-items:center;justify-content:flex-end;gap:8px;margin-bottom:10px}.toolbar .state{margin-left:0;margin-right:auto;order:-1}.action{border:0;border-radius:8px;padding:8px 13px;background:var(--blue);color:#071018;font-weight:900;text-transform:uppercase;cursor:pointer}.action.secondary{background:var(--salmon)}.state{color:var(--muted);font-weight:800}
.telemetry-panel{border:1px solid var(--line-soft);border-top:2px solid var(--line);border-radius:10px;background:linear-gradient(180deg,rgba(8,27,42,.44),rgba(1,7,12,.36));overflow:hidden}
.log{height:calc(100vh - 420px);min-height:330px;overflow:auto;padding:6px 0;scrollbar-color:var(--salmon) #111}.log::-webkit-scrollbar,.traces::-webkit-scrollbar{width:9px;height:9px}.log::-webkit-scrollbar-thumb,.traces::-webkit-scrollbar-thumb{background:var(--salmon);border-radius:8px}
.telemetry-row{display:grid;grid-template-columns:176px 170px 135px 105px minmax(180px,1fr) 92px;grid-template-rows:auto auto auto;gap:3px 10px;padding:9px 12px;border-bottom:1px solid var(--line-soft);align-items:center;font:13px/1.35 ui-monospace,SFMono-Regular,Consolas,monospace}.telemetry-row:nth-child(odd){background:#030a11}.telemetry-row:nth-child(even){background:#081421}.telemetry-row:hover{background:#0d2130}.telemetry-row>.time,.telemetry-row>.app,.telemetry-row>.flow,.telemetry-row>.outcome,.telemetry-row>.analyze{grid-row:1}.telemetry-row>.detail{grid-row:2;grid-column:1 / -1}.telemetry-row>.registration-id-cell{grid-row:3;grid-column:1 / -1}.telemetry-row button{background:none;border:0;color:inherit;text-align:left;font:inherit;cursor:pointer;padding:0}.time{color:var(--arctic-snow)}.app{color:var(--cyan)}.flow{color:#c7b8ff}.outcome{font-weight:900}.Succeeded{color:var(--green)}.Information,.Started{color:var(--cyan)}.Debug,.Trace{color:var(--muted)}.Warning{color:var(--yellow)}.Error,.Critical,.Failed{color:var(--red)}.Skipped{color:#8798a4}.detail,.registration-id-cell{white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.detail{padding-top:2px;color:var(--starship-white)}.registration-id-cell{color:var(--arctic-ice);font-size:11px;padding-top:3px;border-top:1px solid rgba(69,85,128,.32)}.registration-id-cell b{display:inline;margin-right:8px;color:var(--muted);font:800 9px/1.2 Arial,sans-serif;text-transform:uppercase;letter-spacing:.05em}.registration-id-cell code{font:11px/1.35 ui-monospace,SFMono-Regular,Consolas,monospace;user-select:all}.ids-hidden .registration-id-cell{display:none}.analyze{color:var(--title-gold)!important;text-align:right!important;font-weight:900!important}
.empty{padding:40px;text-align:center;color:var(--muted)}

.traces{height:calc(100vh - 420px);min-height:330px;overflow:auto;padding:12px}
.trace{border:1px solid var(--line-soft);border-top:2px solid var(--line);border-radius:10px;margin-bottom:12px;background:#040b11;overflow:hidden}
.trace-head{display:flex;gap:14px;align-items:center;padding:12px 14px;background:#091522}.trace-head b{font-size:15px}.trace-head .grow{flex:1}.trace-meta{color:var(--muted);font-size:12px}
.stages{display:flex;align-items:flex-start;gap:0;padding:22px 18px;overflow-x:auto;scrollbar-color:var(--salmon) #111}.stage{min-width:160px;max-width:220px;position:relative;text-align:center}.stage:not(:last-child):after{content:"";position:absolute;top:12px;left:59%;width:82%;height:3px;background:var(--night-rain)}
.lamp{position:relative;z-index:1;width:24px;height:24px;border-radius:50%;margin:0 auto 8px;background:#53646f;box-shadow:0 0 0 4px #101923}.lamp.Succeeded{background:var(--green)}.lamp.Warning{background:var(--yellow)}.lamp.Failed{background:var(--red)}.lamp.Started{background:var(--blue);animation:pulse 1.2s infinite}.lamp.Skipped{background:#8798a4}@keyframes pulse{50%{box-shadow:0 0 0 8px rgba(102,204,255,.18)}}.stage b{display:block;font-size:12px}.stage small{display:block;color:var(--muted);margin-top:4px}.trace-detail{padding:0 16px 15px;color:var(--arctic-snow)}

@media(max-width:1250px){.brand{width:280px}.live-log{left:410px;right:calc(225px + 50px + 14px)}.top-buttons{right:50px;width:225px}.filters{grid-template-columns:repeat(3,1fr)}}
@media(max-width:900px){.filters{grid-template-columns:repeat(2,1fr)}.telemetry-row{grid-template-columns:145px 1fr 100px 82px;grid-template-rows:auto auto auto}.telemetry-row .flow{display:none}.telemetry-row>.detail{display:block;grid-column:1 / -1;grid-row:2}.telemetry-row>.registration-id-cell{display:block;grid-column:1 / -1;grid-row:3}.ids-hidden .registration-id-cell{display:none}}
 .sound-toggle{margin-left:10px;margin-right:14px;border:0;border-left:3px solid #000;background:#071018;color:var(--title-gold);width:34px;height:26px;padding:4px 7px;cursor:pointer;z-index:6;display:grid;place-items:center}.sound-toggle svg{width:18px;height:18px;fill:currentColor}.sound-toggle:hover{background:#111d2a;color:var(--starship-white)}.sound-toggle:hover{background:#111d2a;color:var(--starship-white)}
</style>
</head>
<body>
<header class="lcars-top">
  <div class="left-vertical"></div><div class="bottom-arm"></div>
  <div class="brand"><div class="section">Operations</div><div class="org">Food For The Poor Jamaica</div><div class="datetime" id="brandDateTime"></div><div class="brand-user" id="brandUser">SIGNED IN</div></div>
  <div class="live-log"><div class="recent-title">Recent Activity</div><div class="recent-rows" id="liveLog"><div class="row"><span class="t"></span> <span class="inf">INF</span> <span class="src">[Operations]</span> Loading suite telemetry</div></div></div>
  <div class="top-buttons">
    <a class="blue" href="/">Operations</a>
    <a class="salmon" href="/handoff/diagnostics">Diagnostics</a>
    <a class="gold" href="/handoff/configuration">Configuration</a>
    <a class="gray" href="/handoff/registration">Registration</a>
  </div>
</header>

<section class="workspace-shell">
  <div class="workspace-arm"><div class="workspace-title">Activity & Flow Analyzer</div><div class="last-scan" id="lastScan">Last Scan: SCANNING</div></div>
  <div class="workspace-leg"></div>
  <nav class="workspace-nav">
    <a id="navActivity" class="active" data-mode="log">Activity</a>
    <a id="navLogs" data-mode="appLogs">App Logs</a>
    <a id="navFlow" data-mode="flow">Flow</a>
    <a href="/">Command</a>
  </nav>
  <button class="lcars-back" type="button" onclick="if(history.length>1){history.back()}else{location.href='/'}">Back</button>

  <main class="detail-scroll">
<section class="filters">
      <select id="application"><option value="">All applications</option></select>
      <select id="instance"><option value="">All instances</option></select>
      <select id="flow"><option value="">All flows</option></select>
      <select id="outcome"><option value="">All outcomes</option></select>
      <input id="correlation" placeholder="Correlation ID">
      <input id="sinceMinutes" type="number" min="1" max="10080" value="60" placeholder="Minutes">
      <input class="wide" id="text" placeholder="Search stage, code, detail, business ID…">
      <input id="stage" placeholder="Stage">
      <select id="logLevel"><option value="">All log levels</option><option>Trace</option><option>Debug</option><option>Information</option><option>Warning</option><option>Error</option><option>Critical</option></select>
      <input id="category" placeholder="Log category">
      <button class="action" id="apply">Apply Filter</button>
      <button class="action secondary" id="reset">Reset</button>
    </section>
    <div class="toolbar">
      <button class="action secondary" id="pause">Pause</button>
      <button class="action secondary" id="follow">Follow Tail: On</button>
      <button class="action secondary" id="clear">Clear View</button>
      <button class="action secondary" id="toggleIds">IDs: On</button>
      <span class="state" id="state">CONNECTING…</span>
    </div>
    <section class="telemetry-panel">
      <div id="logView" class="log"></div>
      <div id="appLogView" class="log" hidden></div>
      <div id="flowView" class="traces" hidden></div>
    </section>
  </main>
</section>

<script>
const $=id=>document.getElementById(id),esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#039;'}[c]));
let paused=false,followTail=true,mode='log',hiddenBefore=null;
const idsKey='aegis.engineering.showIds';
let showIds=localStorage.getItem(idsKey)!=='off';
function applyIdsVisibility(){
  $('logView')?.classList.toggle('ids-hidden',!showIds);
  const b=$('toggleIds');
  if(b)b.textContent='IDs: '+(showIds?'On':'Off');
}
function recentStamp(v){const d=v?new Date(v):new Date();if(Number.isNaN(d.getTime()))return '--- -- ---- --:--:--';const mon=d.toLocaleString('en-US',{month:'short'});return mon+' '+String(d.getDate()).padStart(2,'0')+' '+d.getFullYear()+' '+String(d.getHours()).padStart(2,'0')+':'+String(d.getMinutes()).padStart(2,'0')+':'+String(d.getSeconds()).padStart(2,'0')}
async function loadBrandUser(){try{const r=await fetch('/auth/me',{cache:'no-store'});if(!r.ok)return;const x=await r.json(),e=document.getElementById('brandUser');if(e)e.textContent='User: '+(x.displayName||x.userName||'Signed in')+' · IP: '+(x.clientIp||'unknown');}catch{}}loadBrandUser();
function updateClock(){const d=new Date(),t=$('brandDateTime');const mon=d.toLocaleString('en-US',{month:'short'}).toUpperCase();t.textContent=mon+' '+String(d.getDate()).padStart(2,'0')+' '+d.getFullYear()+' '+String(d.getHours()).padStart(2,'0')+':'+String(d.getMinutes()).padStart(2,'0')+' '+(d.getHours()>=12?'PM':'AM')}updateClock();setInterval(updateClock,1000);document.querySelector('#liveLog .t').textContent=recentStamp();
document.querySelectorAll('.top-buttons a,.workspace-nav a').forEach(a=>a.addEventListener('pointerdown',()=>{a.classList.add('nav-flash');setTimeout(()=>a.classList.remove('nav-flash'),180)}));
async function refreshRegisteredApplicationHealth(){const host=document.querySelector('.detail-scroll');if(!host)return;let banner=document.getElementById('registeredApplicationHealthBanner');if(!banner){banner=document.createElement('section');banner.id='registeredApplicationHealthBanner';banner.className='control-plane-health-banner';const own=document.getElementById('controlPlaneHealthBanner');own?.insertAdjacentElement('afterend',banner)??host.prepend(banner);}try{const r=await fetch('/api/operations/registered-applications',{cache:'no-store'});if(!r.ok)throw new Error('HTTP '+r.status);const x=await r.json(),issues=(x.applications||[]).filter(a=>String(a.state||'Unknown').toLowerCase()!=='healthy'||a.stale);if(!issues.length){banner.style.display='none';return;}banner.classList.toggle('critical',issues.some(a=>['unhealthy','failed','critical'].includes(String(a.state||'').toLowerCase())));banner.innerHTML='<strong>REGISTERED APPLICATION HEALTH · '+issues.length+' ISSUE'+(issues.length===1?'':'S')+'</strong><div>'+issues.slice(0,8).map(a=>esc(a.displayName||a.applicationId)+' — '+esc(a.state||'Unknown')+(a.stale?' (stale)':'')+(a.reason?' · '+esc(a.reason):'')).join('<br>')+'</div>';banner.style.display='block';}catch{banner.style.display='none';}}
refreshRegisteredApplicationHealth();setInterval(refreshRegisteredApplicationHealth,30000);
async function refreshControlPlaneHealthBanner(){const host=document.querySelector('.detail-scroll');if(!host)return;let banner=document.getElementById('controlPlaneHealthBanner');if(!banner){banner=document.createElement('section');banner.id='controlPlaneHealthBanner';banner.className='control-plane-health-banner';host.prepend(banner);}try{const response=await fetch('/health',{cache:'no-store'});if(!response.ok)throw new Error('HTTP '+response.status);const h=await response.json(),state=String(h.health||h.status||'Unknown'),summary=String(h.summary||'Health check returned no summary.').trim(),lower=state.toLowerCase();if(lower==='healthy'){banner.style.display='none';banner.classList.remove('critical','health-unavailable');return;}banner.classList.toggle('critical',lower==='unhealthy');banner.classList.remove('health-unavailable');banner.innerHTML='<strong>'+state.toUpperCase()+'</strong><div>'+esc(summary)+'</div>';banner.style.display='block';}catch(error){banner.classList.remove('critical');banner.classList.add('health-unavailable');banner.innerHTML='<strong>HEALTH CHECK UNAVAILABLE</strong><div>'+esc(error?.message||'Unable to read /health.')+'</div>';banner.style.display='block';}}
refreshControlPlaneHealthBanner();setInterval(refreshControlPlaneHealthBanner,30000);

function params(){const p=new URLSearchParams(),map={applicationId:'application',instanceId:'instance',flow:'flow',outcome:'outcome',correlationId:'correlation',stage:'stage',text:'text'};for(const[q,id]of Object.entries(map)){const v=$(id).value.trim();if(v)p.set(q,v)}const m=parseInt($('sinceMinutes').value||'0',10);if(m>0)p.set('since',new Date(Date.now()-m*60000).toISOString());p.set('take',mode==='flow'?'250':'1000');return p}
function setOptions(id,values,label){const el=$(id),current=el.value;el.innerHTML='<option value="">All '+label+'</option>'+values.map(v=>'<option>'+esc(v)+'</option>').join('');el.value=current}
async function loadFilters(){try{const r=await fetch('/api/operations/activity/filters',{cache:'no-store'});if(!r.ok)return;const x=await r.json();setOptions('application',x.applications||[],'applications');setOptions('instance',x.instances||[],'instances');setOptions('flow',x.flows||[],'flows');setOptions('outcome',x.outcomes||[],'outcomes')}catch{}}
function fmt(v){const d=new Date(v);return Number.isNaN(d.valueOf())?'—':recentStamp(d)+'.'+String(d.getMilliseconds()).padStart(3,'0')}
function detail(e){return[e.stage,e.code,e.detail].filter(Boolean).join(' · ')}
function levelClass(level){return String(level||'Information')}
function registrationId(e){return String(e.relatedBusinessId||e.correlationId||'')}
function updateRecent(events,type){const rows=(events||[]).slice(-5);$('liveLog').innerHTML=rows.length?rows.map(e=>{const when=e.occurredAtUtc||e.timestampUtc,app=e.applicationId||'Operations',level=type==='log'?(e.level||'Information'):(e.outcome==='Failed'?'ERR':e.outcome==='Warning'?'WRN':'INF'),cls=String(level).toLowerCase().startsWith('err')||level==='Critical'?'err':String(level).toLowerCase().startsWith('warn')?'wrn':'inf',message=e.detail||e.message||e.stage||e.flow||'Telemetry event';return '<div class="row"><span class="t">'+fmt(when)+'</span> <span class="'+cls+'">'+esc(level)+'</span> <span class="src">['+esc(app)+']</span> '+esc(message)+'</div>'}).join(''):'<div class="row"><span class="t">'+recentStamp()+'</span> <span class="inf">INF</span> <span class="src">[Operations]</span> No matching telemetry</div>'}

function analyze(c){$('correlation').value=c;mode='flow';tabs();refresh()}window.analyze=analyze;
async function loadLog(){const r=await fetch('/api/operations/activity?'+params(),{cache:'no-store'});if(!r.ok)throw new Error('HTTP '+r.status);const x=await r.json();let events=x.events||[];if(hiddenBefore)events=events.filter(e=>new Date(e.occurredAtUtc)>hiddenBefore);$('logView').innerHTML=events.length?events.map(e=>'<div class="telemetry-row"><span class="time">'+fmt(e.occurredAtUtc)+'</span><span class="app">'+esc(e.applicationId)+'</span><span class="flow">'+esc(e.flow)+'</span><span class="outcome '+esc(e.outcome)+'">'+esc(e.outcome)+'</span><button class="detail" title="'+esc(detail(e))+'" onclick="analyze(\''+esc(e.correlationId)+'\')">'+esc(detail(e))+'</button><button class="analyze" onclick="analyze(\''+esc(e.correlationId)+'\')">Analyze →</button></div>').join(''):'<div class="empty">No lifecycle events match this filter.</div>';updateRecent(events,'activity');$('state').textContent=events.length+' EVENTS · '+recentStamp();if(followTail){const el=$('logView');el.scrollTop=el.scrollHeight}}
async function loadAppLogs(){const p=params();p.delete('flow');p.delete('outcome');p.delete('stage');const level=$('logLevel').value.trim();if(level)p.set('level',level);const category=$('category').value.trim();if(category)p.set('category',category);const r=await fetch('/api/operations/logs?'+p,{cache:'no-store'});if(!r.ok)throw new Error('HTTP '+r.status);const x=await r.json();let events=x.events||[];if(hiddenBefore)events=events.filter(e=>new Date(e.timestampUtc)>hiddenBefore);$('appLogView').innerHTML=events.length?events.map(e=>'<div class="telemetry-row"><span class="time">'+fmt(e.timestampUtc)+'</span><span class="app">'+esc(e.applicationId)+'</span><span class="flow">'+esc(e.category)+'</span><span class="outcome '+levelClass(e.level)+'">'+esc(e.level)+'</span><button class="detail" title="'+esc(e.message)+'" '+(e.correlationId?'onclick="analyze(\''+esc(e.correlationId)+'\')"':'')+'>'+esc(e.message)+'</button><button class="analyze" '+(e.correlationId?'onclick="analyze(\''+esc(e.correlationId)+'\')"':'')+'>'+(e.correlationId?'Analyze →':'—')+'</button></div>').join(''):'<div class="empty">No application logs match this filter.</div>';updateRecent(events,'log');$('state').textContent=events.length+' LOG EVENTS · '+recentStamp();if(followTail){const el=$('appLogView');el.scrollTop=el.scrollHeight}}
async function loadFlows(){const r=await fetch('/api/operations/activity/flows?'+params(),{cache:'no-store'});if(!r.ok)throw new Error('HTTP '+r.status);const x=await r.json(),traces=x.traces||[];$('flowView').innerHTML=traces.length?traces.map(t=>'<article class="trace"><div class="trace-head"><b class="'+esc(t.status)+'">'+esc(t.status)+'</b><div class="grow"><b>'+esc(t.flow)+'</b><div class="trace-meta">'+esc(t.applicationId)+' / '+esc(t.instanceId)+' · '+esc(t.correlationId)+'</div></div><div class="trace-meta">'+t.durationMs+' ms · '+t.eventCount+' events</div></div><div class="stages">'+(t.stages||[]).map(s=>'<div class="stage"><div class="lamp '+esc(s.outcome)+'"></div><b>'+esc(s.stage)+'</b><small>'+esc(s.outcome)+' · '+fmt(s.occurredAtUtc)+'</small></div>').join('')+'</div>'+(t.relatedBusinessId?'<div class="trace-detail">Business ID: '+esc(t.relatedBusinessId)+'</div>':'')+'</article>').join(''):'<div class="empty">No correlated flows match this filter.</div>';$('state').textContent=traces.length+' FLOWS · '+recentStamp()}
async function refresh(){if(paused)return;try{if(mode==='log')await loadLog();else if(mode==='appLogs')await loadAppLogs();else await loadFlows();$('lastScan').textContent='Last Scan: '+new Date().toLocaleString()}catch(e){$('state').textContent='TELEMETRY UNAVAILABLE · '+e.message;$('lastScan').textContent='Last Scan: UNAVAILABLE'}}
function tabs(){$('navActivity').classList.toggle('active',mode==='log');$('navLogs').classList.toggle('active',mode==='appLogs');$('navFlow').classList.toggle('active',mode==='flow');$('logView').hidden=mode!=='log';$('appLogView').hidden=mode!=='appLogs';$('flowView').hidden=mode!=='flow';$('flow').disabled=mode==='appLogs';$('outcome').disabled=mode==='appLogs';$('stage').disabled=mode==='appLogs';$('logLevel').disabled=mode!=='appLogs';$('category').disabled=mode!=='appLogs'}
document.querySelectorAll('.workspace-nav [data-mode]').forEach(a=>a.addEventListener('click',e=>{e.preventDefault();mode=a.dataset.mode;tabs();refresh()}));
$('apply').onclick=refresh;$('reset').onclick=()=>{for(const id of ['application','instance','flow','outcome','correlation','stage','text','logLevel','category'])$(id).value='';$('sinceMinutes').value='60';hiddenBefore=null;refresh()};
$('pause').onclick=()=>{paused=!paused;$('pause').textContent=paused?'Resume':'Pause';$('state').textContent=paused?'PAUSED · INGESTION CONTINUES':'RESUMED';if(!paused)refresh()};
$('follow').onclick=()=>{followTail=!followTail;$('follow').textContent='Follow Tail: '+(followTail?'On':'Off')};
$('clear').onclick=()=>{hiddenBefore=new Date();$('logView').innerHTML='<div class="empty">View cleared. New events will appear here.</div>';$('appLogView').innerHTML='<div class="empty">View cleared. New logs will appear here.</div>';$('flowView').innerHTML='<div class="empty">View cleared. Adjust filters or wait for new flows.</div>'};
$('toggleIds').onclick=()=>{showIds=!showIds;localStorage.setItem(idsKey,showIds?'on':'off');applyIdsVisibility()};
tabs();applyIdsVisibility();loadFilters();refresh();setInterval(()=>{loadFilters();refresh()},2000);

function installAegisConsoleAudio(){
  const key='aegis.console.sound',enabled=()=>localStorage.getItem(key)!=='off';
  let ctx=null;
  function tone(freq,duration,volume=.045,delay=0){
    if(!enabled())return;
    try{
      ctx=ctx||new (window.AudioContext||window.webkitAudioContext)();
      if(ctx.state==='suspended')ctx.resume();
      const t=ctx.currentTime+delay,o=ctx.createOscillator(),g=ctx.createGain();
      o.type='sine';o.frequency.setValueAtTime(freq,t);
      g.gain.setValueAtTime(0,t);g.gain.linearRampToValueAtTime(volume,t+.004);g.gain.exponentialRampToValueAtTime(.0001,t+duration);
      o.connect(g);g.connect(ctx.destination);o.start(t);o.stop(t+duration+.02);
    }catch{}
  }
  const cues={
    navigation:()=>{tone(1046.5,.026,.035);tone(1318.5,.031,.03,.035)},
    success:()=>{tone(659.3,.035,.035);tone(987.8,.04,.038,.047);tone(1318.5,.043,.032,.097)},
    warning:()=>{tone(587.3,.06,.045);tone(493.9,.072,.045,.095)},
    critical:()=>{tone(392,.08,.055);tone(329.6,.085,.055,.11);tone(261.6,.115,.06,.225)},
    complete:()=>{tone(784,.045,.032);tone(932.3,.045,.032,.05);tone(1174.7,.035,.03,.105)}
  };
  window.aegisAudio={play:name=>cues[name]?.(),enabled};
  const arm=document.querySelector('.workspace-arm');
  if(arm&&!document.getElementById('soundToggle')){
    const b=document.createElement('button');b.id='soundToggle';b.type='button';b.className='sound-toggle';
    const paint=()=>{b.innerHTML=enabled()?'<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 9v6h4l5 4V5L8 9H4zm11.5 3a3.5 3.5 0 0 0-1.5-2.87v5.74A3.5 3.5 0 0 0 15.5 12zm0-7.1v2.06a7 7 0 0 1 0 10.08v2.06a9 9 0 0 0 0-14.2z"/></svg>':'<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 9v6h4l5 4V5L8 9H4zm11.2.8 1.4 1.4 1.4-1.4 1.4 1.4-1.4 1.4 1.4 1.4-1.4 1.4-1.4-1.4-1.4 1.4-1.4-1.4 1.4-1.4-1.4-1.4 1.4-1.4z"/></svg>';b.title=enabled()?'Sound on':'Sound off';b.setAttribute('aria-label',b.title);b.setAttribute('aria-pressed',String(enabled()))};paint();
    b.onclick=()=>{localStorage.setItem(key,enabled()?'off':'on');paint();if(enabled())cues.success()};
    arm.appendChild(b);
  }
  document.querySelectorAll('.top-buttons a,.workspace-nav a,.lcars-back').forEach(x=>x.addEventListener('pointerdown',()=>cues.navigation()));
}
installAegisConsoleAudio();
</script>
</body>
</html>
""";
    }
}
