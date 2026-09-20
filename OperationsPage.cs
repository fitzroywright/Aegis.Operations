using System.Reflection;
using System.Text.Encodings.Web;

internal static class OperationsPage
{
    internal static string Render(string configurationUrl, string diagnosticsUrl, string site, string environment)
    {
        string version = typeof(OperationsPage).Assembly.GetName().Version?.ToString() ?? "unknown";
        return Template.Value
            .Replace("__SITE__", Encode(site), StringComparison.Ordinal)
            .Replace("__ENVIRONMENT__", Encode(environment), StringComparison.Ordinal)
            .Replace("__VERSION__", Encode(version), StringComparison.Ordinal)
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
  --ink:#05070b;
  --lavender:#8d86f4;
  --lavender-2:#7069df;
  --gold:#ffbd3e;
  --blue:#49a9ff;
  --cyan:#30d7ff;
  --green:#16df86;
  --yellow:#ffc43d;
  --red:#ff4d59;
  --white:#f4f7fb;
  --muted:#aeb9c8;
}
*{box-sizing:border-box}
html,body{height:100%;margin:0;overflow:hidden;background:var(--black);color:var(--white);font-family:"Arial Narrow","Roboto Condensed","Segoe UI",Arial,sans-serif}
body{display:grid;grid-template-rows:176px minmax(0,1fr) 84px;padding:10px 12px 8px;gap:8px}
a{color:inherit;text-decoration:none}
button{font:inherit}

/* TOP L */
.top-l{
  position:relative;
  display:grid;
  grid-template-columns:240px minmax(360px,1fr) 300px 330px 140px;
  align-items:center;
  gap:0;
  min-width:0;
  padding:10px 0 26px 66px;
}
.top-l::before{
  content:"";
  position:absolute;
  left:0;
  top:0;
  bottom:20px;
  width:64px;
  background:var(--lavender);
  border-radius:30px 0 0 30px;
}
.top-l::after{
  content:"";
  position:absolute;
  left:54px;
  right:0;
  bottom:0;
  height:18px;
  background:var(--lavender);
  border-radius:0 10px 10px 0;
}
.brand{
  align-self:stretch;
  display:flex;
  flex-direction:column;
  justify-content:center;
  padding:0 16px;
  border-bottom:18px solid var(--gold);
}
.brand strong{font-size:31px;line-height:1;letter-spacing:.2px;color:var(--gold);text-transform:uppercase}
.brand b{font-size:27px;line-height:1.05;color:var(--blue);text-transform:uppercase}
.brand small{margin-top:6px;color:var(--cyan);font-weight:800;text-transform:uppercase}
.live-log{
  min-width:0;
  height:112px;
  overflow:hidden;
  padding:6px 14px;
  font:15px/1.42 ui-monospace,SFMono-Regular,Consolas,monospace;
  color:var(--cyan);
  border-right:2px solid #1679a7;
}
.log-line{white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.log-time{color:#aeb8ff}.log-level{display:inline-block;width:44px;font-weight:800}.log-level.inf{color:var(--green)}.log-level.wrn{color:var(--yellow)}.log-level.err{color:var(--red)}
.system-block,.top-actions,.time-block{height:112px;display:flex;align-items:center}
.system-block{gap:15px;padding:0 20px;border-right:2px solid #1679a7}
.system-icon{font-size:48px;color:var(--blue)}
.system-meta strong{display:block;font-size:22px;color:var(--gold)}
.system-meta span{display:block;color:#d9dee8}
.system-state{display:flex;align-items:center;gap:10px;margin-top:7px;color:var(--green);font-weight:800}
.state-dot,.status-dot{width:18px;height:18px;border-radius:50%;background:var(--green);display:inline-block}
.top-actions{padding:0 16px;border-right:2px solid #1679a7;display:grid;grid-template-columns:1fr 1fr;gap:10px}
.top-actions a,.top-actions button{
  min-height:42px;border:0;border-radius:12px;padding:8px 12px;text-align:center;font-weight:900;text-transform:uppercase;color:#071018;cursor:pointer
}
.top-actions .primary{background:var(--gold)}
.top-actions .blue{background:#4ca9ff}
.top-actions .purple{background:#9a7ff2}
.top-actions .more{background:#6d7e96}
.time-block{padding-left:18px;display:block;text-align:right;color:var(--blue);font-weight:800}
.time-block .date{font-size:18px}.time-block .time{font-size:31px;line-height:1.1}.time-block small{display:block;color:var(--cyan)}

/* MIRRORED WORKSPACE L */
.workspace-shell{
  min-height:0;
  display:grid;
  grid-template-columns:110px minmax(0,1fr);
  grid-template-rows:18px minmax(0,1fr);
  position:relative;
}
.workspace-arm{
  grid-column:1/3;
  grid-row:1;
  margin-left:54px;
  background:var(--lavender);
  border-radius:0 10px 10px 0;
  z-index:2;
}
.workspace-leg{
  grid-column:1;
  grid-row:1/3;
  background:var(--lavender);
  border-radius:28px 0 0 28px;
  padding-top:72px;
  overflow:hidden;
  z-index:1;
}
.workspace-leg nav{display:flex;flex-direction:column}
.workspace-leg a{
  display:flex;
  align-items:center;
  min-height:48px;
  padding:0 12px;
  color:#081020;
  font-weight:900;
  font-size:15px;
  text-transform:uppercase;
}
.workspace-leg a + a{border-top:3px solid #000}
.workspace-leg a.active{color:#000;font-size:16px}
.detail-scroll{
  grid-column:2;
  grid-row:2;
  min-width:0;
  min-height:0;
  overflow-y:auto;
  overflow-x:hidden;
  overscroll-behavior:contain;
  scrollbar-color:var(--lavender) #111;
  padding:22px 22px 40px;
  background:#000;
}
.detail-scroll::-webkit-scrollbar{width:10px}.detail-scroll::-webkit-scrollbar-track{background:#090909}.detail-scroll::-webkit-scrollbar-thumb{background:var(--lavender);border-radius:10px}
.page-head{display:flex;justify-content:space-between;gap:24px;align-items:flex-start;margin-bottom:18px}
.page-head h1{margin:0;color:var(--blue);font-size:34px}
.page-head p{margin:2px 0 0;color:#c8d2df;font-size:16px}
.scan-note{color:var(--cyan);text-align:right}
.metrics{display:grid;grid-template-columns:repeat(5,minmax(130px,1fr));gap:12px;margin-bottom:18px}
.metric{padding:14px 14px 12px;border-top:2px solid #174f72;min-height:98px}
.metric b{display:block;font-size:28px;color:var(--cyan)}.metric small{color:var(--muted)}.metric.good b{color:var(--green)}.metric.warn b{color:var(--yellow)}.metric.bad b{color:var(--red)}
.workspace{display:grid;grid-template-columns:1fr 1fr;gap:16px}
.panel{border-top:2px solid #18597f;padding:15px 0 8px;min-height:250px}
.panelhead{display:flex;align-items:center;gap:12px;margin-bottom:12px}
.panelhead h2{margin:0;font-size:24px;color:var(--blue)}.panelhead p{margin:2px 0;color:#c7d4e1}
.pill{margin-left:auto;border:1px solid currentColor;border-radius:12px;padding:5px 9px;font-weight:800}.available{color:var(--green)}.unavailable{color:var(--red)}
.facts{display:grid;grid-template-columns:repeat(2,1fr);gap:0}.facts div{padding:10px 12px 10px 0;border-bottom:1px solid #173848}.facts small,.facts b{display:block}.facts small{color:var(--muted)}
.action{display:inline-block;margin-top:20px;color:var(--blue);font-weight:900;text-transform:uppercase}
.activity,.journeys{margin-top:18px;border-top:2px solid #18597f;padding-top:12px}
.activity header,.journeys header{font-size:20px;color:var(--blue);font-weight:900}
.activityrow{display:grid;grid-template-columns:18px 180px 1fr;gap:10px;padding:10px 0;border-bottom:1px solid #122f3f}
.dot{width:12px;height:12px;border-radius:50%;background:var(--green)}
.journey{display:grid;grid-template-columns:190px 150px 1fr 100px;gap:12px;align-items:center;padding:10px 0;border-bottom:1px solid #173f58}
.track{display:flex;gap:7px}.light{width:12px;height:12px;border-radius:50%;background:#52636f}.light.ok{background:var(--green)}.light.warn{background:var(--yellow)}.light.fail{background:var(--red)}
.health.Healthy{color:var(--green)}.health.Warning,.health.Running{color:var(--yellow)}.health.Failed{color:var(--red)}

/* FIXED FOOTER */
.lcars-footer{
  display:grid;
  grid-template-columns:220px 285px 235px 1fr 155px 170px 60px;
  align-items:center;
  min-width:0;
  height:72px;
  border:10px solid #5d9cf5;
  border-radius:22px 0 0 22px;
  border-right:48px solid var(--gold);
  color:var(--white);
  background:#03070b;
}
.footer-link{height:100%;display:flex;align-items:center;gap:12px;padding:0 18px;border-right:2px solid #1b789f}
.footer-link strong{display:block;color:var(--blue);font-size:17px;text-transform:uppercase}.footer-link small{display:block;color:var(--cyan);text-transform:uppercase}
.footer-link.active{box-shadow:inset 0 0 0 2px #19a7ff;border-radius:14px}
.footer-icon{font-size:29px;color:var(--blue)}
.footer-status{height:100%;display:flex;align-items:center;justify-content:center;gap:18px;border-right:2px solid #1b789f;color:var(--cyan)}
.footer-status span{display:flex;align-items:center;gap:7px;white-space:nowrap}.footer-status .status-dot{width:16px;height:16px}.footer-status .warn{background:var(--yellow)}.footer-status .bad{background:var(--red)}
.footer-env,.footer-user,.footer-logout{height:100%;display:flex;align-items:center;justify-content:center;border-right:2px solid #1b789f}
.footer-env{color:var(--blue);font-weight:900}.footer-user{gap:10px}.footer-user b{display:block}.footer-user small{color:#b9c6d4}.footer-logout{font-size:26px;color:var(--blue)}
.footer-meta{position:absolute;left:54px;bottom:0;font-size:11px;color:#9cb3c6}
.footer-motto{position:absolute;right:78px;bottom:0;font-size:12px;color:#cfd8e3}
.footer-wrap{position:relative;height:84px;padding-bottom:12px}

@media(max-width:1100px){
  body{grid-template-rows:154px minmax(0,1fr) 84px}
  .top-l{grid-template-columns:190px 1fr 240px 280px;padding-left:56px}
  .time-block{display:none}.brand strong{font-size:24px}.brand b{font-size:22px}
  .metrics{grid-template-columns:repeat(3,1fr)}
  .workspace{grid-template-columns:1fr}
  .lcars-footer{grid-template-columns:1fr 1fr 1fr 1.3fr}.footer-env,.footer-user,.footer-logout{display:none}
}
</style>
</head>
<body>
<header class="top-l">
  <div class="brand">
    <strong>Aegis LCARS</strong>
    <b>Operations</b>
    <small>Food For The Poor Jamaica</small>
  </div>
  <div class="live-log" id="liveLog">
    <div class="log-line"><span class="log-time">--:--:--</span> <span class="log-level inf">INF</span> [Operations] Initializing management-plane status</div>
    <div class="log-line"><span class="log-time">--:--:--</span> <span class="log-level inf">INF</span> [Configuration] Waiting for current status</div>
    <div class="log-line"><span class="log-time">--:--:--</span> <span class="log-level inf">INF</span> [Diagnostics] Waiting for current status</div>
    <div class="log-line"><span class="log-time">--:--:--</span> <span class="log-level inf">INF</span> [Registration] Waiting for current status</div>
    <div class="log-line"><span class="log-time">--:--:--</span> <span class="log-level inf">INF</span> [Operations] Ready</div>
  </div>
  <div class="system-block">
    <div class="system-icon">⌁</div>
    <div class="system-meta">
      <strong>Aegis.Operations</strong>
      <span>v__VERSION__ · __ENVIRONMENT__</span>
      <span>__SITE__</span>
      <div class="system-state"><i class="state-dot" id="topStateDot"></i><span id="topStateText">SCANNING</span></div>
    </div>
  </div>
  <div class="top-actions">
    <button class="primary" onclick="load()">Refresh</button>
    <a class="blue" href="__CONFIGURATION_URL__">Config</a>
    <a class="purple" href="__DIAGNOSTICS_URL__">Diagnostics</a>
    <a class="more" href="/registration-lifecycle">Registration</a>
  </div>
  <div class="time-block">
    <div class="date" id="date"></div>
    <div class="time" id="time"></div>
    <small id="timezone"></small>
  </div>
</header>

<section class="workspace-shell">
  <div class="workspace-arm" aria-hidden="true"></div>
  <aside class="workspace-leg">
    <nav>
      <a class="active" href="#overview">Overview</a>
      <a href="#systems">Systems</a>
      <a href="#activity">Activity</a>
      <a href="#flow">Flow</a>
      <a href="#settings">Settings</a>
    </nav>
  </aside>

  <main class="detail-scroll" id="detailWorkspace">
    <section id="overview" class="page-head">
      <div>
        <h1>Operations Overview</h1>
        <p>Integrated operational status across the Aegis management plane</p>
      </div>
      <div class="scan-note">Detail workspace scrolls independently<br><span id="checked">SCANNING</span></div>
    </section>

    <section class="metrics">
      <div class="metric"><b id="managedCount">—</b><small>Control Plane Services</small></div>
      <div class="metric"><b id="registeredCount">—</b><small>Registered Applications</small></div>
      <div class="metric good"><b id="availableCount">—</b><small>Available</small></div>
      <div class="metric warn"><b id="attentionCount">—</b><small>Needs Attention</small></div>
      <div class="metric"><b>__ENVIRONMENT__</b><small>Environment</small></div>
    </section>

    <section id="systems" class="workspace">
      <article class="panel">
        <div class="panelhead">
          <div><h2>System Configuration</h2><p>Requirements · commissioning · runbooks</p></div>
          <span id="cfgStatus" class="pill">SCANNING</span>
        </div>
        <div class="facts">
          <div><small>Service</small><b>Aegis.Configuration</b></div>
          <div><small>Role</small><b>Configuration Authority</b></div>
          <div><small>Site</small><b>__SITE__</b></div>
          <div><small>Environment</small><b>__ENVIRONMENT__</b></div>
        </div>
        <a class="action" href="__CONFIGURATION_URL__">Open System Configuration →</a>
      </article>

      <article class="panel">
        <div class="panelhead">
          <div><h2>System Diagnostics</h2><p>Health · incidents · engineering levels</p></div>
          <span id="diagStatus" class="pill">SCANNING</span>
        </div>
        <div class="facts">
          <div><small>Service</small><b>Aegis.Diagnostics</b></div>
          <div><small>Role</small><b>Health Authority</b></div>
          <div><small>Site</small><b>__SITE__</b></div>
          <div><small>Environment</small><b>__ENVIRONMENT__</b></div>
        </div>
        <a class="action" href="__DIAGNOSTICS_URL__">Open System Diagnostics →</a>
      </article>
    </section>

    <section id="activity" class="activity">
      <header>Operational Activity</header>
      <div class="activityrow"><i class="dot"></i><span id="activityChecked">SCANNING</span><b>Management-plane status refresh</b></div>
    </section>

    <section id="flow" class="journeys">
      <header>Lifecycle / Flow Analyzer</header>
      <div class="journey"><b>Operations</b><small>Management Plane</small><div class="track"><i class="light ok"></i><i class="light ok"></i><i class="light"></i><i class="light"></i></div><span class="health Healthy">Healthy</span></div>
      <div class="journey"><b>Configuration</b><small>Registration</small><div class="track"><i class="light ok"></i><i class="light ok"></i><i class="light ok"></i><i class="light"></i></div><span id="cfgJourney" class="health Running">Scanning</span></div>
      <div class="journey"><b>Diagnostics</b><small>Health</small><div class="track"><i class="light ok"></i><i class="light ok"></i><i class="light"></i><i class="light"></i></div><span id="diagJourney" class="health Running">Scanning</span></div>
    </section>

    <section id="settings" class="activity">
      <header>Shell Behavior</header>
      <div class="activityrow"><i class="dot"></i><span>FIXED</span><b>Top LCARS L remains fixed</b></div>
      <div class="activityrow"><i class="dot"></i><span>FIXED</span><b>Workspace L and navigation remain fixed</b></div>
      <div class="activityrow"><i class="dot"></i><span>SCROLL</span><b>Only this detail workspace scrolls</b></div>
      <div class="activityrow"><i class="dot"></i><span>FIXED</span><b>Footer remains visible</b></div>
    </section>
  </main>
</section>

<div class="footer-wrap">
  <footer class="lcars-footer">
    <a class="footer-link active" href="/">
      <span class="footer-icon">◎</span><span><strong>Operations</strong><small>Monitor · Deploy · Manage</small></span>
    </a>
    <a class="footer-link" href="__CONFIGURATION_URL__">
      <span class="footer-icon">⚙</span><span><strong>Configuration</strong><small>Settings · Secrets · Registration</small></span>
    </a>
    <a class="footer-link" href="__DIAGNOSTICS_URL__">
      <span class="footer-icon">⌁</span><span><strong>Diagnostics</strong><small>Health · Logs · Analysis</small></span>
    </a>
    <div class="footer-status">
      <span><i class="status-dot"></i><b id="footerNominal">All Systems Nominal</b></span>
      <span><i class="status-dot warn"></i><b id="footerWarnings">0 Warnings</b></span>
      <span><i class="status-dot bad"></i><b>0 Critical</b></span>
    </div>
    <div class="footer-env">__ENVIRONMENT__⌄</div>
    <div class="footer-user"><span class="footer-icon">♟</span><span><b>Operator</b><small>Authenticated User</small></span></div>
    <div class="footer-logout">⇥</div>
  </footer>
  <div class="footer-meta">Aegis LCARS v__VERSION__ &nbsp;|&nbsp; Food For The Poor Jamaica</div>
  <div class="footer-motto">Own what makes us different.</div>
</div>

<script>
const detail=document.getElementById('detailWorkspace');
document.querySelectorAll('.workspace-leg a').forEach(a=>a.addEventListener('click',e=>{
  const id=a.getAttribute('href');
  if(!id?.startsWith('#')) return;
  e.preventDefault();
  const target=document.querySelector(id);
  if(target) detail.scrollTo({top:target.offsetTop-20,behavior:'smooth'});
  document.querySelectorAll('.workspace-leg a').forEach(x=>x.classList.remove('active'));
  a.classList.add('active');
}));

const formatTime=d=>d.toLocaleTimeString([], {hour12:false});
function tick(){
  const d=new Date();
  date.textContent=d.toLocaleDateString([], {day:'2-digit',month:'short',year:'numeric'}).toUpperCase();
  time.textContent=formatTime(d);
  timezone.textContent=Intl.DateTimeFormat().resolvedOptions().timeZone;
}
tick(); setInterval(tick,1000);

function addLog(level,source,message){
  const line=document.createElement('div');
  line.className='log-line';
  line.innerHTML='<span class="log-time">'+formatTime(new Date())+'</span> <span class="log-level '+level.toLowerCase()+'">'+level+'</span> ['+source+'] '+message;
  liveLog.appendChild(line);
  while(liveLog.children.length>5) liveLog.removeChild(liveLog.firstElementChild);
}
function setStatus(id,x){
  const e=document.getElementById(id);
  const state=String(x?.stateName??x?.state??(x?.available?'Healthy':'Unavailable'));
  e.textContent=state.toUpperCase();
  e.title=x?.reason??'';
  e.className='pill '+(state.toLowerCase()==='healthy'?'available':'unavailable');
  return state;
}
async function load(){
  try{
    const r=await fetch('/api/summary',{cache:'no-store'});
    if(!r.ok) throw new Error('HTTP '+r.status);
    const d=await r.json();
    const systems=[d.configuration,d.diagnostics].filter(Boolean);
    managedCount.textContent=systems.length;
    registeredCount.textContent=d.registeredApplications??0;
    const cfg=setStatus('cfgStatus',d.configuration);
    const diag=setStatus('diagStatus',d.diagnostics);
    const n=systems.filter(x=>x.available).length;
    const attention=(systems.length-n)+(d.registration?.attention??0);
    availableCount.textContent=n;
    attentionCount.textContent=attention;
    const stamp='LAST SCAN '+new Date(d.checkedAt).toLocaleString().toUpperCase();
    checked.textContent=stamp;
    activityChecked.textContent=stamp;
    const healthy=attention===0;
    topStateText.textContent=healthy?'HEALTHY':'ATTENTION';
    topStateDot.style.background=healthy?'var(--green)':'var(--yellow)';
    footerNominal.textContent=healthy?'All Systems Nominal':'Attention Required';
    footerWarnings.textContent=attention+' Warning'+(attention===1?'':'s');
    cfgJourney.textContent=cfg;
    cfgJourney.className='health '+(cfg.toLowerCase()==='healthy'?'Healthy':'Warning');
    diagJourney.textContent=diag;
    diagJourney.className='health '+(diag.toLowerCase()==='healthy'?'Healthy':'Warning');
    addLog('INF','Operations','Status refresh completed');
    addLog(cfg.toLowerCase()==='healthy'?'INF':'WRN','Configuration','State '+cfg);
    addLog(diag.toLowerCase()==='healthy'?'INF':'WRN','Diagnostics','State '+diag);
  }catch(err){
    managedCount.textContent='—';registeredCount.textContent='—';availableCount.textContent='—';attentionCount.textContent='—';
    checked.textContent='STATUS TELEMETRY UNAVAILABLE';activityChecked.textContent='STATUS TELEMETRY UNAVAILABLE';
    topStateText.textContent='UNAVAILABLE';topStateDot.style.background='var(--red)';
    footerNominal.textContent='Telemetry Unavailable';footerWarnings.textContent='1 Warning';
    addLog('ERR','Operations','Status telemetry unavailable');
  }
}
load(); setInterval(load,30000);
</script>
</body>
</html>
""";
    }
}
