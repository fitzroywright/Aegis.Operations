using System.Reflection;
using System.Text.Encodings.Web;

internal static class OperationsPage
{
    internal static string Render(string configurationUrl, string diagnosticsUrl, string site, string environment, string? userName = null)
    {
        string version = typeof(OperationsPage).Assembly.GetName().Version?.ToString() ?? "unknown";
        return Template.Value
            .Replace("__SITE__", Encode(site), StringComparison.Ordinal)
            .Replace("__ENVIRONMENT__", Encode(environment), StringComparison.Ordinal)
            .Replace("__VERSION__", Encode(version), StringComparison.Ordinal)
            .Replace("__USER__", Encode(string.IsNullOrWhiteSpace(userName) ? "Operator" : userName), StringComparison.Ordinal)
            .Replace("__CONFIGURATION_URL__", "/handoff/configuration", StringComparison.Ordinal)
            .Replace("__DIAGNOSTICS_URL__", "/handoff/diagnostics", StringComparison.Ordinal);
    }

    private static string Encode(string value) => HtmlEncoder.Default.Encode(value);

    private static class Template
    {
        internal const string Value = """
<!doctype html>
<html>
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Aegis LCARS · Operations</title>
<style>
:root{
  --black:#000;
  --alpha-blue:#5588ee;
  --arctic-ice:#66ccff;
  --arctic-snow:#99ccff;
  --beta-blue:#7799dd;
  --night-cloud:#344470;
  --night-rain:#455580;
  --radioactive:#88eeff;
  --starship-white:#f3f3fc;
  --sunset-red:#ff3500;
  --orange:#ff3500;
  --peach:#99ccff;
  --salmon:#7799dd;
  --gold:#88eeff;
  --blue:#5588ee;
  --cyan:#66ccff;
  --green:#88eeff;
  --yellow:#99ccff;
  --red:#ff3500;
  --white:#f3f3fc;
  --muted:#99ccff;
  --line:#455580;
  --line-soft:#344470;
  --panel:#071020;
}
*{box-sizing:border-box}
html,body{width:100%;height:100%;margin:0;overflow:hidden;background:#000;color:var(--white);font-family:"Arial Narrow","Roboto Condensed","Segoe UI",Arial,sans-serif}
body{position:relative}
a{color:inherit;text-decoration:none}

/* --- fixed TOP L: deliberately open on top/right --- */
.lcars-top{position:fixed;left:8px;right:14px;top:8px;height:146px;z-index:20;background:#000}
.lcars-top .left-vertical{position:absolute;left:0;top:0;width:58px;height:140px;background:var(--alpha-blue);border-radius:28px 0 0 28px}
.lcars-top .left-gold{display:none}
.lcars-top .bottom-arm{position:absolute;left:58px;right:0;bottom:0;height:14px;background:var(--alpha-blue);z-index:1;border-radius:0 9px 9px 0;box-shadow:none}
.lcars-top .join-dot{display:none}

.brand{position:absolute;left:76px;top:8px;width:180px;height:82px;z-index:3}
.brand .aegis{font-size:27px;line-height:1;color:var(--gold);font-weight:900;text-transform:uppercase}
.brand .section{font-size:23px;line-height:1.05;color:var(--blue);font-weight:900;text-transform:uppercase;margin-top:5px}
.brand .org{font-size:10px;color:var(--arctic-snow);font-weight:800;text-transform:uppercase;margin-top:5px;max-width:165px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}

.live-log{position:absolute;left:270px;top:10px;width:calc(100% - 861px);height:94px;overflow:hidden;font:15px/1.4 ui-monospace,SFMono-Regular,Consolas,monospace;padding:4px 14px;border-right:1px solid var(--line)}
.live-log .row{white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.live-log .t{color:var(--arctic-snow)}.live-log .inf{color:var(--green);font-weight:900}.live-log .wrn{color:var(--yellow);font-weight:900}.live-log .src{color:var(--cyan)}

.current-system{position:absolute;right:455px;top:12px;width:260px;height:94px;border-right:1px solid var(--line);display:grid;grid-template-columns:58px 1fr;align-items:center;padding:0 16px}
.wave{width:48px;height:48px;color:var(--blue);display:flex;align-items:center;justify-content:center}.wave svg{width:44px;height:44px;display:block;stroke:currentColor;fill:none;stroke-width:2.4;stroke-linecap:round;stroke-linejoin:round}
.sys-meta strong{display:block;color:var(--gold);font-size:23px}.sys-meta span{display:block;font-size:14px}.health{display:flex;align-items:center;gap:9px;color:var(--green);font-size:18px;font-weight:900;margin-top:4px}.health i{width:19px;height:19px;border-radius:50%;background:var(--green)}

.top-buttons{position:absolute;right:150px;top:14px;width:290px;height:88px;display:grid;grid-template-columns:1fr 1fr;gap:10px 12px;padding-left:16px;border-right:1px solid var(--line)}
.top-buttons a,.top-buttons button{border:0;border-radius:10px;font-weight:900;text-transform:uppercase;color:#071018;cursor:pointer;font-size:13px;display:flex;align-items:center;justify-content:center;line-height:1;padding:0 12px;box-shadow:inset 0 1px 0 rgba(255,255,255,.32),0 1px 0 rgba(0,0,0,.55)}
.top-buttons .gold{background:var(--gold)}.top-buttons .blue{background:var(--blue)}.top-buttons .salmon{background:var(--salmon)}.top-buttons .gray{background:var(--peach)}

.clock{position:absolute;right:0;top:12px;width:138px;text-align:right;color:var(--blue);font-weight:900}
.clock .date{font-size:16px}.clock .time{font-size:30px;line-height:1}.clock small{display:block;color:var(--cyan);font-size:12px;margin-top:4px}

/* --- fixed mirrored workspace L --- */
.workspace-shell{position:fixed;left:8px;right:14px;top:162px;bottom:10px;z-index:10}
.workspace-leg{position:absolute;left:0;top:0;bottom:0;width:96px;background:var(--alpha-blue);border-radius:28px 0 0 28px}
.workspace-arm{position:absolute;left:96px;right:0;top:0;height:48px;background:var(--alpha-blue);display:flex;align-items:center;padding:0 14px;z-index:3;border-radius:0 9px 9px 0}
.workspace-arm .workspace-title{font-weight:900;text-transform:uppercase;color:var(--night-cloud);letter-spacing:.055em;font-size:28px;line-height:1;padding:0 18px;background:transparent;text-shadow:none}
.workspace-leg::before{display:none}
.workspace-leg::after{display:none}

.workspace-nav{position:absolute;left:0;top:72px;width:96px;z-index:2}
.workspace-nav a{display:flex;align-items:center;height:48px;padding-left:11px;color:#071018;font-weight:900;text-transform:uppercase;font-size:12px;border-bottom:3px solid #000}
.workspace-nav a:last-child{border-bottom:0}
.workspace-nav a.active{font-size:13px}

.detail-scroll{position:absolute;left:96px;right:0;top:48px;bottom:0;overflow-y:auto;overflow-x:hidden;overscroll-behavior:contain;background:#000;padding:18px 22px 34px 24px;scrollbar-color:var(--salmon) #111}
.detail-scroll::-webkit-scrollbar{width:10px}.detail-scroll::-webkit-scrollbar-track{background:#090909}.detail-scroll::-webkit-scrollbar-thumb{background:var(--salmon);border-radius:10px}

/* --- current Operations content, intentionally inside the L only --- */
.page-head{display:flex;justify-content:space-between;align-items:flex-start;gap:20px;margin-bottom:16px}
.page-head h1{margin:0;color:var(--blue);font-size:31px}.page-head p{margin:4px 0 0;color:var(--starship-white)}
.scan{color:var(--cyan);text-align:right}
.metrics{display:grid;grid-template-columns:repeat(5,minmax(120px,1fr));gap:14px;margin:10px 0 22px}
.metric{padding:14px 14px 15px;border:1px solid var(--line-soft);border-top:2px solid var(--line);background:linear-gradient(180deg,rgba(12,34,52,.68),rgba(2,10,16,.78));border-radius:10px;box-shadow:inset 0 1px 0 rgba(96,183,230,.08)}.metric b{display:block;font-size:29px;color:var(--cyan)}.metric small{color:var(--muted)}.metric.good b{color:var(--green)}.metric.warn b{color:var(--yellow)}
.content-grid{display:grid;grid-template-columns:1fr 1fr;gap:20px}
.panel{border:1px solid var(--line-soft);border-top:2px solid var(--line);padding:16px 16px 12px;min-height:250px;background:linear-gradient(180deg,rgba(8,27,42,.50),rgba(1,7,12,.40));border-radius:10px;box-shadow:inset 0 1px 0 rgba(96,183,230,.07)}.panel h2{margin:0;color:var(--blue)}.panel p{color:var(--muted)}
.status-pill{display:inline-block;padding:4px 9px;border:1px solid;border-radius:12px;font-weight:900}.ok{color:var(--green)}.bad{color:var(--red)}
.facts{display:grid;grid-template-columns:1fr 1fr;margin-top:12px}.facts div{padding:9px 12px 9px 0;border-bottom:1px solid #173747}.facts small,.facts b{display:block}.facts small{color:var(--muted)}
.activity,.flow{margin-top:22px;border:1px solid var(--line-soft);border-top:2px solid var(--line);padding:14px 16px 8px;background:linear-gradient(180deg,rgba(8,27,42,.40),rgba(1,7,12,.34));border-radius:10px;box-shadow:inset 0 1px 0 rgba(96,183,230,.06)}.activity h2,.flow h2{color:var(--blue);margin:0 0 10px}
.activity-row{display:grid;grid-template-columns:16px 170px 1fr;gap:10px;padding:10px 0;border-bottom:1px solid var(--line-soft);align-items:center}.activity-row:last-child{border-bottom:0}.dot{width:12px;height:12px;border-radius:50%;background:var(--green);display:block;justify-self:center}
.flow-row{display:grid;grid-template-columns:180px 140px 1fr minmax(140px,auto);gap:12px;align-items:center;padding:10px 0;border-bottom:1px solid var(--line-soft)}.flow-row:last-child{border-bottom:0}.lights{display:flex;gap:7px;align-items:center}.lights i{width:11px;height:11px;border-radius:50%;background:#53646f;display:block}.lights i.on{background:var(--green)}.lights i.warn{background:var(--yellow)}

@media(max-width:1250px){
  .live-log{width:calc(100% - 790px)}
  .current-system{right:450px;width:250px}
  .top-buttons{right:145px;width:295px}
}
</style>
</head>
<body>

<header class="lcars-top">
  <div class="left-vertical"></div>
  <div class="left-gold"></div>
  <div class="bottom-arm"></div>
  <div class="join-dot"></div>

  <div class="brand">
    <div class="aegis">Aegis LCARS</div>
    <div class="section">Operations</div>
    <div class="org">Food For The Poor Jamaica</div>
  </div>

  <div class="live-log" id="liveLog">
    <div class="row"><span class="t">--:--:--</span> <span class="inf">INF</span> <span class="src">[Operations]</span> Loading control-plane state</div>
    <div class="row"><span class="t">--:--:--</span> <span class="inf">INF</span> <span class="src">[Configuration]</span> Awaiting status</div>
    <div class="row"><span class="t">--:--:--</span> <span class="inf">INF</span> <span class="src">[Diagnostics]</span> Awaiting status</div>
    <div class="row"><span class="t">--:--:--</span> <span class="inf">INF</span> <span class="src">[Registration]</span> Awaiting status</div>
    <div class="row"><span class="t">--:--:--</span> <span class="inf">INF</span> <span class="src">[Operations]</span> Ready</div>
  </div>

  <div class="current-system">
    <div class="wave" aria-hidden="true"><svg viewBox="0 0 48 48"><path d="M3 25h9l4-12 6 24 6-19 5 12 3-5h9"/></svg></div>
    <div class="sys-meta">
      <strong>Aegis.Operations</strong>
      <span>v__VERSION__ | __ENVIRONMENT__</span>
      <span>__SITE__</span>
      <div class="health"><i id="healthDot"></i><span id="healthText">SCANNING</span></div>
    </div>
  </div>

  <div class="top-buttons">
    <a class="blue" href="/">Operations</a>
    <a class="salmon" href="__DIAGNOSTICS_URL__">Diagnostics</a>
    <a class="gold" href="__CONFIGURATION_URL__">Configuration</a>
    <a class="gray" href="/registration-lifecycle">Registration</a>
  </div>

  <div class="clock">
    <div class="date" id="date"></div>
    <div class="time" id="time"></div>
    <small>JAMAICA (EST)</small>
    <small>UTC-05:00</small>
  </div>
</header>

<section class="workspace-shell">
  <div class="workspace-arm">
    <div class="workspace-title">Operations Overview</div>
  </div>
  <div class="workspace-leg"></div>
  <nav class="workspace-nav">
    <a class="active" href="#overview">Command</a>
    <a href="#systems">Apps</a>
    <a href="#flow">Flows</a>
    <a href="#attention">Attention</a>
    <a href="#controlplane">Control</a>
    <a href="#settings">Runtime</a>
  </nav>

  <main class="detail-scroll" id="detail">
    <section class="page-head" id="overview">
      <div><p>Command view: suite state → applications → flows → attention → control-plane health</p></div>
      <div class="scan" id="checked">SCANNING</div>
    </section>

    <section class="metrics">
      <div class="metric"><b id="registeredCount">—</b><small>Registered Applications</small></div>
      <div class="metric good"><b id="healthyCount">—</b><small>Healthy Applications</small></div>
      <div class="metric warn"><b id="warningCount">—</b><small>Warnings / Unknown</small></div>
      <div class="metric"><b id="activeAlertCount">—</b><small>Active Alerts</small></div>
      <div class="metric"><b id="activeFlowCount">—</b><small>Recent Flows</small></div>
    </section>

    <section class="content-grid" id="systems">
      <article class="panel" style="grid-column:1 / -1;min-height:0">
        <h2>Applications</h2><p id="registrationAuthority">Loading authoritative registration inventory…</p>
        <div id="applicationsList"></div>
      </article>
    </section>

    <section class="flow" id="flow">
      <h2>Active Flow Analyzer</h2>
      <div id="flowList"><div class="flow-row"><b>Loading</b><span>Telemetry</span><div class="lights"><i></i><i></i><i></i><i></i></div><span>—</span></div></div>
    </section>

    <section class="content-grid" id="attention" style="margin-top:18px">
      <article class="panel" style="min-height:0">
        <h2>Attention</h2><p>Active alerts and conditions that require operator review.</p>
        <div id="alertList"><div class="activity-row"><i class="dot"></i><span>LOADING</span><b>Reading active alerts…</b></div></div>
      </article>
      <article class="panel" style="min-height:0">
        <h2>Recent Activity</h2><p>Latest control-plane and application lifecycle events.</p>
        <div id="activityList"><div class="activity-row"><i class="dot"></i><span>LOADING</span><b>Reading operational history…</b></div></div>
      </article>
    </section>

    <section class="content-grid" id="controlplane" style="margin-top:18px">
      <article class="panel" style="min-height:0">
        <h2>Configuration Authority</h2><p id="cfgReason">Configuration authority status</p>
        <span id="cfgStatus" class="status-pill">SCANNING</span>
        <div class="facts"><div><small>Service</small><b>Aegis.Configuration</b></div><div><small>Role</small><b>Configuration Authority</b></div></div>
      </article>
      <article class="panel" style="min-height:0">
        <h2>Diagnostics Authority</h2><p id="diagReason">Diagnostics authority status</p>
        <span id="diagStatus" class="status-pill">SCANNING</span>
        <div class="facts"><div><small>Service</small><b>Aegis.Diagnostics</b></div><div><small>Role</small><b>Health Authority</b></div></div>
      </article>
    </section>

    <section class="activity" id="settings">
      <h2>Operations Runtime</h2>
      <div class="activity-row"><i class="dot"></i><span id="runtimeState">LOADING</span><b>Operations service state</b></div>
      <div class="activity-row"><i class="dot"></i><span id="runtimeStorage">LOADING</span><b>Telemetry storage</b></div>
      <div class="activity-row"><i class="dot"></i><span>__VERSION__</span><b>Application version</b></div>
      <div class="activity-row"><i class="dot"></i><span id="runtimeHost">—</span><b>Current host</b></div>
    </section>
  </main>
</section>

<script>
const detail=document.getElementById('detail');
document.querySelectorAll('.workspace-nav a').forEach(a=>a.addEventListener('click',e=>{
  const id=a.getAttribute('href'); if(!id?.startsWith('#')) return; e.preventDefault();
  const target=document.querySelector(id); if(target) detail.scrollTo({top:target.offsetTop-24,behavior:'smooth'});
  document.querySelectorAll('.workspace-nav a').forEach(x=>x.classList.remove('active')); a.classList.add('active');
}));

function clockTick(){const d=new Date();date.textContent=d.toLocaleDateString('en-GB',{day:'2-digit',month:'short',year:'numeric'}).toUpperCase();time.textContent=d.toLocaleTimeString('en-GB',{hour12:false});}
clockTick();setInterval(clockTick,1000);

function esc(v){return String(v??'').replace(/[&<>"']/g,m=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[m]))}
function addLog(level,source,message){
  const row=document.createElement('div'); row.className='row';
  row.innerHTML='<span class="t">'+new Date().toLocaleTimeString('en-GB',{hour12:false})+'</span> <span class="'+level.toLowerCase()+'">'+esc(level)+'</span> <span class="src">['+esc(source)+']</span> '+esc(message);
  liveLog.appendChild(row); while(liveLog.children.length>5) liveLog.removeChild(liveLog.firstElementChild);
}
function stateName(x){return String(x?.stateName??x?.state??(x?.available?'Healthy':'Unknown'))}
function setPill(id,x){const e=document.getElementById(id);const s=stateName(x);e.textContent=s.toUpperCase();e.className='status-pill '+(s.toLowerCase()==='healthy'?'ok':'bad');return s}
async function json(url){const r=await fetch(url,{cache:'no-store'});if(!r.ok)throw new Error(url+' HTTP '+r.status);return r.json()}
function fmtTime(v){if(!v)return '—';const d=new Date(v);return Number.isNaN(d.valueOf())?'—':d.toLocaleTimeString([], {hour:'2-digit',minute:'2-digit',second:'2-digit'})}
function levelFromType(t){const s=String(t??'').toLowerCase();return s.includes('fail')||s.includes('critical')?'ERR':s.includes('warn')||s.includes('alert')?'WRN':'INF'}
function flowHealth(flow){
  const stages=flow?.stages??[];
  if(stages.some(s=>String(s.state).toLowerCase()==='failed'||String(s.state).toLowerCase()==='timedout'))return 'Failed';
  if(stages.some(s=>String(s.state).toLowerCase()==='warning'))return 'Warning';
  if(stages.some(s=>String(s.state).toLowerCase()==='active'))return 'Running';
  return 'Healthy';
}
function renderApplications(data){
  const apps=data?.applications??[];
  registrationAuthority.textContent=data?.authorityAvailable?'Configuration registration authority online':'Registration authority unavailable'+(data?.authorityError?': '+data.authorityError:'');
  if(!apps.length){applicationsList.innerHTML='<div class="activity-row"><i class="dot"></i><span>0</span><b>No registered applications returned.</b></div>';return}
  applicationsList.innerHTML=apps.slice(0,12).map(a=>{
    const state=String(a.state??'Unknown'); const cls=state.toLowerCase()==='healthy'?'ok':'bad';
    return '<div class="activity-row"><i class="dot" style="background:'+(cls==='ok'?'var(--green)':'var(--yellow)')+'"></i><span>'+esc(a.applicationId)+'</span><b>'+esc(a.instanceId)+' · '+esc(state)+(a.stale?' · STALE':'')+'</b></div>'
  }).join('');
}
function renderHistory(items){
  const rows=(items??[]).slice(0,8);
  if(!rows.length){activityList.innerHTML='<div class="activity-row"><i class="dot"></i><span>—</span><b>No operational history available.</b></div>';liveLog.innerHTML='<div class="row"><span class="t">'+new Date().toLocaleTimeString('en-GB',{hour12:false})+'</span> <span class="inf">INF</span> <span class="src">[Operations]</span> No operational history available</div>';return}
  activityList.innerHTML=rows.map(x=>'<div class="activity-row"><i class="dot"></i><span>'+fmtTime(x.at)+'</span><b>'+esc(x.application||x.type)+' · '+esc(x.text)+'</b></div>').join('');
  liveLog.innerHTML=rows.slice(0,5).reverse().map(x=>'<div class="row"><span class="t">'+fmtTime(x.at)+'</span> <span class="'+levelFromType(x.type).toLowerCase()+'">'+levelFromType(x.type)+'</span> <span class="src">['+esc(x.application||x.type)+']</span> '+esc(x.text)+'</div>').join('');
}
function renderAlerts(items){
  const rows=(items??[]).filter(a=>String(a.state??'').toLowerCase()!=='resolved').slice(0,6);
  if(!rows.length){alertList.innerHTML='<div class="activity-row"><i class="dot"></i><span>0</span><b>No active alerts.</b></div>';return}
  alertList.innerHTML=rows.map(a=>'<div class="activity-row"><i class="dot" style="background:var(--yellow)"></i><span>'+esc(a.applicationId||a.application||a.type||'Alert')+'</span><b>'+esc(a.message||a.text||a.title||a.state||'Attention required')+'</b></div>').join('');
}
function renderFlows(flows){
  const rows=(flows??[]).slice(0,8);
  if(!rows.length){flowList.innerHTML='<div class="flow-row"><b>No flows</b><span>Telemetry</span><div class="lights"><i></i><i></i><i></i><i></i></div><span>—</span></div>';return}
  flowList.innerHTML=rows.map(f=>{
    const health=flowHealth(f), stages=f.stages??[], current=f.currentStage??stages.find(s=>String(s.state).toLowerCase()==='active')?.name??stages.at(-1)?.name??'—';
    const lights=stages.slice(0,6).map(s=>'<i class="'+(String(s.state).toLowerCase()==='completed'?'on':String(s.state).toLowerCase()==='warning'?'warn':'')+'"></i>').join('');
    return '<div class="flow-row"><b>'+esc(f.applicationId)+'</b><span>'+esc(f.flowType)+'</span><div class="lights">'+lights+'</div><span class="'+(health==='Healthy'?'ok':'')+'">'+esc(current)+' · '+esc(health)+'</span></div>'
  }).join('');
}
async function load(){
  try{
    const [summary,registered,flows,alerts,history,self]=await Promise.all([
      json('/api/summary'),
      json('/api/operations/registered-applications').catch(()=>({applications:[],authorityAvailable:false})),
      json('/api/operations/flows').catch(()=>[]),
      json('/api/operations/alerts').catch(()=>[]),
      json('/api/operations/history').catch(()=>[]),
      json('/api/operations/self').catch(()=>({state:'Unknown',storage:'Unknown'}))
    ]);
    const apps=registered?.applications??[];
    const healthyApps=apps.filter(a=>String(a.state).toLowerCase()==='healthy'&&!a.stale).length;
    const warnings=apps.filter(a=>String(a.state).toLowerCase()!=='healthy'||a.stale).length;
    const activeAlerts=(alerts??[]).filter(a=>String(a.state).toLowerCase()!=='resolved').length;
    registeredCount.textContent=apps.length||summary.registeredApplications||0;
    healthyCount.textContent=healthyApps;
    warningCount.textContent=warnings;
    activeAlertCount.textContent=activeAlerts;
    activeFlowCount.textContent=(flows??[]).length;

    const cfg=setPill('cfgStatus',summary.configuration), diag=setPill('diagStatus',summary.diagnostics);
    cfgReason.textContent=summary.configuration?.reason||summary.configuration?.url||'Configuration health endpoint responded.';
    diagReason.textContent=summary.diagnostics?.reason||summary.diagnostics?.url||'Diagnostics health endpoint responded.';
    checked.textContent='LAST SCAN '+new Date(summary.checkedAt).toLocaleString();

    const attention=warnings+activeAlerts+(summary.registration?.attention??0)+(cfg.toLowerCase()==='healthy'?0:1)+(diag.toLowerCase()==='healthy'?0:1);
    const healthy=attention===0;
    healthText.textContent=healthy?'HEALTHY':'ATTENTION';
    healthDot.style.background=healthy?'var(--green)':'var(--yellow)';
    document.title='Aegis LCARS · Operations · '+(healthy?'Nominal':'Attention');

    renderApplications(registered);
    renderFlows(flows);
    renderAlerts(alerts);
    renderHistory(history);

    runtimeState.textContent=String(self.state??'Unknown').toUpperCase();
    runtimeStorage.textContent=String(self.storage??'Unknown').toUpperCase();
    runtimeHost.textContent=location.hostname;

  }catch(err){
    healthText.textContent='UNAVAILABLE';healthDot.style.background='var(--red)';
    document.title='Aegis LCARS · Operations · Telemetry Unavailable';
    addLog('ERR','Operations',err?.message??'Status telemetry unavailable');
  }
}
load();setInterval(load,30000);
</script>
</body>
</html>
""";
    }
}
