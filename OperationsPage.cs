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
  --lav:#8f88f2;
  --lav-dark:#736ce0;
  --gold:#ffb83e;
  --blue:#3da9ff;
  --cyan:#24d5ff;
  --green:#14df80;
  --yellow:#ffc43b;
  --red:#ff4d56;
  --white:#f4f7fb;
  --muted:#b5c2d0;
}
*{box-sizing:border-box}
html,body{width:100%;height:100%;margin:0;overflow:hidden;background:#000;color:var(--white);font-family:"Arial Narrow","Roboto Condensed","Segoe UI",Arial,sans-serif}
body{position:relative}
a{color:inherit;text-decoration:none}

/* --- fixed TOP L: deliberately open on top/right --- */
.lcars-top{position:fixed;left:8px;right:14px;top:10px;height:166px;z-index:20;background:#000}
.lcars-top .left-vertical{position:absolute;left:0;top:0;width:58px;height:160px;background:var(--lav);border-radius:28px 0 0 28px}
.lcars-top .left-gold{position:absolute;left:58px;top:118px;width:195px;height:42px;background:var(--gold);border-radius:0 0 0 22px}
.lcars-top .bottom-arm{position:absolute;left:253px;right:0;bottom:0;height:18px;background:var(--lav);border-radius:0 9px 9px 0}
.lcars-top .join-dot{position:absolute;left:255px;bottom:0;width:18px;height:18px;border-radius:50%;background:var(--gold);transform:translateX(-8px)}

.brand{position:absolute;left:70px;top:14px;width:180px;height:100px}
.brand .aegis{font-size:31px;line-height:1;color:var(--gold);font-weight:900;text-transform:uppercase}
.brand .section{font-size:28px;line-height:1.05;color:var(--blue);font-weight:900;text-transform:uppercase;margin-top:5px}
.brand .org{font-size:12px;color:var(--cyan);font-weight:900;text-transform:uppercase;margin-top:8px}

.live-log{position:absolute;left:255px;top:13px;width:calc(100% - 900px);height:108px;overflow:hidden;font:15px/1.4 ui-monospace,SFMono-Regular,Consolas,monospace;padding:2px 10px;border-right:2px solid #0c6e97}
.live-log .row{white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.live-log .t{color:#c8c9ff}.live-log .inf{color:var(--green);font-weight:900}.live-log .wrn{color:var(--yellow);font-weight:900}.live-log .src{color:var(--cyan)}

.current-system{position:absolute;right:515px;top:15px;width:290px;height:106px;border-right:2px solid #0c6e97;display:grid;grid-template-columns:58px 1fr;align-items:center;padding:0 16px}
.wave{font-size:47px;color:var(--blue);line-height:1}
.sys-meta strong{display:block;color:var(--gold);font-size:23px}.sys-meta span{display:block;font-size:14px}.health{display:flex;align-items:center;gap:9px;color:var(--green);font-size:18px;font-weight:900;margin-top:4px}.health i{width:19px;height:19px;border-radius:50%;background:var(--green)}

.top-buttons{position:absolute;right:158px;top:18px;width:340px;height:102px;display:grid;grid-template-columns:1fr 1fr;gap:10px 12px;padding-left:16px;border-right:2px solid #0c6e97}
.top-buttons a,.top-buttons button{border:0;border-radius:10px;font-weight:900;text-transform:uppercase;color:#071018;cursor:pointer;font-size:13px}
.top-buttons .gold{background:var(--gold)}.top-buttons .blue{background:#53aaf4}.top-buttons .purple{background:#9a85ef}.top-buttons .gray{background:#6b7d94}

.clock{position:absolute;right:0;top:15px;width:145px;text-align:right;color:var(--blue);font-weight:900}
.clock .date{font-size:16px}.clock .time{font-size:30px;line-height:1}.clock small{display:block;color:var(--cyan);font-size:12px;margin-top:4px}

/* --- fixed mirrored workspace L --- */
.workspace-shell{position:fixed;left:8px;right:14px;top:184px;bottom:108px;z-index:10}
.workspace-arm{position:absolute;left:253px;right:0;top:0;height:18px;background:var(--lav);border-radius:0 9px 9px 0}
.workspace-leg{position:absolute;left:0;top:0;bottom:0;width:78px;background:var(--lav);border-radius:28px 0 0 28px}
.workspace-leg::before{content:"";position:absolute;left:58px;top:0;width:195px;height:36px;background:var(--gold);border-radius:0 0 0 22px}
.workspace-leg::after{content:"";position:absolute;left:58px;bottom:0;width:130px;height:34px;background:var(--gold);border-radius:0 0 0 20px}

.workspace-nav{position:absolute;left:0;top:73px;width:78px;z-index:2}
.workspace-nav a{display:flex;align-items:center;height:58px;padding-left:12px;color:#071018;font-weight:900;text-transform:uppercase;font-size:12px;border-bottom:3px solid #000}
.workspace-nav a:last-child{border-bottom:0}
.workspace-nav a.active{font-size:13px}

.detail-scroll{position:absolute;left:78px;right:0;top:18px;bottom:0;overflow-y:auto;overflow-x:hidden;overscroll-behavior:contain;background:#000;padding:26px 28px 44px 32px;scrollbar-color:var(--lav) #111}
.detail-scroll::-webkit-scrollbar{width:10px}.detail-scroll::-webkit-scrollbar-track{background:#090909}.detail-scroll::-webkit-scrollbar-thumb{background:var(--lav);border-radius:10px}

/* --- current Operations content, intentionally inside the L only --- */
.page-head{display:flex;justify-content:space-between;align-items:flex-start;gap:20px;margin-bottom:16px}
.page-head h1{margin:0;color:var(--blue);font-size:31px}.page-head p{margin:4px 0 0;color:#cad4df}
.scan{color:var(--cyan);text-align:right}
.metrics{display:grid;grid-template-columns:repeat(5,minmax(120px,1fr));gap:14px;margin:10px 0 22px}
.metric{padding:12px 12px 14px;border-top:2px solid #174f72}.metric b{display:block;font-size:29px;color:var(--cyan)}.metric small{color:var(--muted)}.metric.good b{color:var(--green)}.metric.warn b{color:var(--yellow)}
.content-grid{display:grid;grid-template-columns:1fr 1fr;gap:20px}
.panel{border-top:2px solid #174f72;padding-top:15px;min-height:250px}.panel h2{margin:0;color:var(--blue)}.panel p{color:var(--muted)}
.status-pill{display:inline-block;padding:4px 9px;border:1px solid;border-radius:12px;font-weight:900}.ok{color:var(--green)}.bad{color:var(--red)}
.facts{display:grid;grid-template-columns:1fr 1fr;margin-top:12px}.facts div{padding:9px 12px 9px 0;border-bottom:1px solid #173747}.facts small,.facts b{display:block}.facts small{color:var(--muted)}
.activity,.flow{margin-top:22px;border-top:2px solid #174f72;padding-top:12px}.activity h2,.flow h2{color:var(--blue);margin:0 0 10px}
.activity-row{display:grid;grid-template-columns:16px 170px 1fr;gap:10px;padding:10px 0;border-bottom:1px solid #122f3e}.dot{width:12px;height:12px;border-radius:50%;background:var(--green)}
.flow-row{display:grid;grid-template-columns:180px 140px 1fr 90px;gap:12px;align-items:center;padding:10px 0;border-bottom:1px solid #122f3e}.lights{display:flex;gap:7px}.lights i{width:11px;height:11px;border-radius:50%;background:#53646f}.lights i.on{background:var(--green)}.lights i.warn{background:var(--yellow)}

/* --- fixed footer copied from approved shell geometry --- */
.footer-wrap{position:fixed;left:8px;right:14px;bottom:8px;height:92px;z-index:30}
.footer-bar{position:absolute;left:0;right:0;top:0;height:70px;border:9px solid #5da0f5;border-right:48px solid var(--gold);border-radius:22px 0 0 22px;background:#02060a;display:grid;grid-template-columns:225px 285px 235px 1fr 150px 170px 58px;align-items:center;overflow:hidden}
.footer-item{height:100%;display:flex;align-items:center;gap:12px;padding:0 18px;border-right:2px solid #14759d}
.footer-item strong{display:block;color:var(--blue);font-size:16px;text-transform:uppercase}.footer-item small{display:block;color:var(--cyan);font-size:10px;text-transform:uppercase}.footer-icon{font-size:28px;color:var(--blue)}
.footer-status{display:flex;align-items:center;justify-content:center;gap:18px;height:100%;border-right:2px solid #14759d;color:var(--cyan);font-size:12px}.footer-status span{display:flex;gap:6px;align-items:center;white-space:nowrap}.footer-status i{width:16px;height:16px;border-radius:50%;background:var(--green)}.footer-status .warn{background:var(--yellow)}.footer-status .crit{background:var(--red)}
.footer-env,.footer-user,.footer-logout{height:100%;display:flex;align-items:center;justify-content:center;border-right:2px solid #14759d}.footer-env{color:var(--blue);font-weight:900}.footer-user{gap:8px}.footer-user small{display:block;color:#b9c4d0}.footer-logout{font-size:24px;color:var(--blue)}
.footer-meta{position:absolute;left:50px;bottom:0;color:#aebdca;font-size:11px}.footer-motto{position:absolute;right:72px;bottom:0;color:#cdd7e1;font-size:12px}

@media(max-width:1250px){
  .live-log{width:calc(100% - 790px)}
  .current-system{right:450px;width:250px}
  .top-buttons{right:145px;width:295px}
  .footer-bar{grid-template-columns:190px 235px 200px 1fr 130px}.footer-user,.footer-logout{display:none}
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
    <div class="wave">⌁</div>
    <div class="sys-meta">
      <strong>Aegis.Operations</strong>
      <span>v__VERSION__ | __ENVIRONMENT__</span>
      <span>__SITE__</span>
      <div class="health"><i id="healthDot"></i><span id="healthText">SCANNING</span></div>
    </div>
  </div>

  <div class="top-buttons">
    <button class="gold" onclick="load()">Refresh</button>
    <a class="blue" href="__CONFIGURATION_URL__">Config</a>
    <a class="purple" href="__DIAGNOSTICS_URL__">Diagnostics</a>
    <a class="gray" href="/registration-lifecycle">More</a>
  </div>

  <div class="clock">
    <div class="date" id="date"></div>
    <div class="time" id="time"></div>
    <small>JAMAICA (EST)</small>
    <small>UTC-05:00</small>
  </div>
</header>

<section class="workspace-shell">
  <div class="workspace-arm"></div>
  <div class="workspace-leg"></div>
  <nav class="workspace-nav">
    <a class="active" href="#overview">Overview</a>
    <a href="#systems">Systems</a>
    <a href="#activity">Activity</a>
    <a href="#flow">Flow</a>
    <a href="#settings">Settings</a>
  </nav>

  <main class="detail-scroll" id="detail">
    <section class="page-head" id="overview">
      <div><h1>Operations Overview</h1><p>Integrated operational status across the Aegis management plane</p></div>
      <div class="scan" id="checked">SCANNING</div>
    </section>

    <section class="metrics">
      <div class="metric"><b id="managedCount">—</b><small>Control Plane Services</small></div>
      <div class="metric"><b id="registeredCount">—</b><small>Registered Applications</small></div>
      <div class="metric good"><b id="availableCount">—</b><small>Available</small></div>
      <div class="metric warn"><b id="attentionCount">—</b><small>Needs Attention</small></div>
      <div class="metric"><b>__ENVIRONMENT__</b><small>Environment</small></div>
    </section>

    <section class="content-grid" id="systems">
      <article class="panel">
        <h2>System Configuration</h2><p>Requirements · commissioning · runbooks</p>
        <span id="cfgStatus" class="status-pill">SCANNING</span>
        <div class="facts"><div><small>Service</small><b>Aegis.Configuration</b></div><div><small>Role</small><b>Configuration Authority</b></div><div><small>Site</small><b>__SITE__</b></div><div><small>Environment</small><b>__ENVIRONMENT__</b></div></div>
      </article>
      <article class="panel">
        <h2>System Diagnostics</h2><p>Health · incidents · engineering levels</p>
        <span id="diagStatus" class="status-pill">SCANNING</span>
        <div class="facts"><div><small>Service</small><b>Aegis.Diagnostics</b></div><div><small>Role</small><b>Health Authority</b></div><div><small>Site</small><b>__SITE__</b></div><div><small>Environment</small><b>__ENVIRONMENT__</b></div></div>
      </article>
    </section>

    <section class="activity" id="activity">
      <h2>Operational Activity</h2>
      <div class="activity-row"><i class="dot"></i><span id="activityChecked">SCANNING</span><b>Management-plane status refresh</b></div>
    </section>

    <section class="flow" id="flow">
      <h2>Flow Analyzer</h2>
      <div class="flow-row"><b>Operations</b><span>Management Plane</span><div class="lights"><i class="on"></i><i class="on"></i><i></i><i></i></div><span class="ok">Healthy</span></div>
      <div class="flow-row"><b>Configuration</b><span>Registration</span><div class="lights"><i class="on"></i><i class="on"></i><i class="on"></i><i></i></div><span id="cfgJourney">Scanning</span></div>
      <div class="flow-row"><b>Diagnostics</b><span>Health</span><div class="lights"><i class="on"></i><i class="on"></i><i></i><i></i></div><span id="diagJourney">Scanning</span></div>
    </section>

    <section class="activity" id="settings">
      <h2>Shell Behavior</h2>
      <div class="activity-row"><i class="dot"></i><span>FIXED</span><b>Top L stays fixed</b></div>
      <div class="activity-row"><i class="dot"></i><span>FIXED</span><b>Lavender workspace L stays fixed</b></div>
      <div class="activity-row"><i class="dot"></i><span>SCROLL</span><b>Only this black detail workspace scrolls</b></div>
      <div class="activity-row"><i class="dot"></i><span>FIXED</span><b>Footer stays fixed</b></div>
    </section>
  </main>
</section>

<div class="footer-wrap">
  <footer class="footer-bar">
    <a class="footer-item" href="/"><span class="footer-icon">◎</span><span><strong>Operations</strong><small>Monitor · Deploy · Manage</small></span></a>
    <a class="footer-item" href="__CONFIGURATION_URL__"><span class="footer-icon">⚙</span><span><strong>Configuration</strong><small>Settings · Secrets · Registration</small></span></a>
    <a class="footer-item" href="__DIAGNOSTICS_URL__"><span class="footer-icon">⌁</span><span><strong>Diagnostics</strong><small>Health · Logs · Analysis</small></span></a>
    <div class="footer-status"><span><i></i><b id="nominal">All Systems Nominal</b></span><span><i class="warn"></i><b id="warnings">0 Warnings</b></span><span><i class="crit"></i><b>0 Critical</b></span></div>
    <div class="footer-env">__ENVIRONMENT__⌄</div>
    <div class="footer-user"><span class="footer-icon">♟</span><span><b>FWright</b><small>IT Manager</small></span></div>
    <div class="footer-logout">⇥</div>
  </footer>
  <div class="footer-meta">Aegis LCARS v__VERSION__ | Food For The Poor Jamaica</div>
  <div class="footer-motto">Own what makes us different.</div>
</div>

<script>
const detail=document.getElementById('detail');
document.querySelectorAll('.workspace-nav a').forEach(a=>a.addEventListener('click',e=>{
  const id=a.getAttribute('href'); if(!id?.startsWith('#')) return; e.preventDefault();
  const target=document.querySelector(id); if(target) detail.scrollTo({top:target.offsetTop-24,behavior:'smooth'});
  document.querySelectorAll('.workspace-nav a').forEach(x=>x.classList.remove('active')); a.classList.add('active');
}));

function clockTick(){const d=new Date();date.textContent=d.toLocaleDateString('en-GB',{day:'2-digit',month:'short',year:'numeric'}).toUpperCase();time.textContent=d.toLocaleTimeString('en-GB',{hour12:false});}
clockTick();setInterval(clockTick,1000);

function addLog(level,source,message){
  const row=document.createElement('div'); row.className='row';
  row.innerHTML='<span class="t">'+new Date().toLocaleTimeString('en-GB',{hour12:false})+'</span> <span class="'+level.toLowerCase()+'">'+level+'</span> <span class="src">['+source+']</span> '+message;
  liveLog.appendChild(row); while(liveLog.children.length>5) liveLog.removeChild(liveLog.firstElementChild);
}
function stateName(x){return String(x?.stateName??x?.state??(x?.available?'Healthy':'Unavailable'))}
function setPill(id,x){const e=document.getElementById(id);const s=stateName(x);e.textContent=s.toUpperCase();e.className='status-pill '+(s.toLowerCase()==='healthy'?'ok':'bad');return s}
async function load(){
  try{
    const r=await fetch('/api/summary',{cache:'no-store'});if(!r.ok)throw new Error('HTTP '+r.status);const d=await r.json();
    const systems=[d.configuration,d.diagnostics].filter(Boolean);managedCount.textContent=systems.length;registeredCount.textContent=d.registeredApplications??0;
    const cfg=setPill('cfgStatus',d.configuration),diag=setPill('diagStatus',d.diagnostics);const n=systems.filter(x=>x.available).length;const attention=(systems.length-n)+(d.registration?.attention??0);
    availableCount.textContent=n;attentionCount.textContent=attention;const stamp='LAST SCAN '+new Date(d.checkedAt).toLocaleString();checked.textContent=stamp;activityChecked.textContent=stamp;
    const healthy=attention===0;healthText.textContent=healthy?'HEALTHY':'ATTENTION';healthDot.style.background=healthy?'var(--green)':'var(--yellow)';nominal.textContent=healthy?'All Systems Nominal':'Attention Required';warnings.textContent=attention+' Warning'+(attention===1?'':'s');
    cfgJourney.textContent=cfg;diagJourney.textContent=diag;addLog('INF','Operations','Status refresh completed');addLog(cfg.toLowerCase()==='healthy'?'INF':'WRN','Configuration','State '+cfg);addLog(diag.toLowerCase()==='healthy'?'INF':'WRN','Diagnostics','State '+diag);
  }catch{
    healthText.textContent='UNAVAILABLE';healthDot.style.background='var(--red)';nominal.textContent='Telemetry Unavailable';warnings.textContent='1 Warning';addLog('WRN','Operations','Status telemetry unavailable');
  }
}
load();setInterval(load,30000);
</script>
</body>
</html>
""";
    }
}
