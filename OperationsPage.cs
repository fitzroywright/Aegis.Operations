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
            .Replace("__DIAGNOSTICS_URL__", "/handoff/diagnostics", StringComparison.Ordinal)
            .Replace("__CONTROL_PLANE_HEADER_CSS__", ControlPlaneHeader.Css, StringComparison.Ordinal);
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
<title>Operations</title>
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
  --title-gold:#ffcc66;
  --rail-width:160px;
  --top-arm-height:14px;
  --workspace-arm-height:48px;
  --elbow-radius:18px;
  --scroll-arrow-space:18px;
  --workspace-right-inset:18px;
  --top-right-inset:18px;
  --header-gap:18px;
}
*{box-sizing:border-box}
html,body{width:100%;height:100%;margin:0;overflow:hidden;background:#000;color:var(--white);font-family:"Arial Narrow","Roboto Condensed","Segoe UI",Arial,sans-serif}
body{position:relative}
a{color:inherit;text-decoration:none}

/* --- fixed TOP L: deliberately open on top/right --- */
.lcars-top{position:fixed;left:8px;right:var(--top-right-inset);top:8px;height:140px;z-index:20;background:#000}
.lcars-top .left-vertical{position:absolute;left:0;top:0;width:var(--rail-width);height:140px;background:var(--alpha-blue);border-radius:28px 0 0 28px}
.lcars-top .left-gold{display:none}
.lcars-top .bottom-arm{position:absolute;left:var(--rail-width);right:0;top:126px;height:var(--top-arm-height);background:var(--alpha-blue);z-index:1;border-radius:0 9px 9px 0;box-shadow:none}
.lcars-top .join-dot{display:none}
.lcars-top::after{content:"";position:absolute;left:var(--rail-width);top:calc(126px - var(--elbow-radius));width:var(--elbow-radius);height:var(--elbow-radius);background:radial-gradient(circle at 100% 0,transparent 0 calc(var(--elbow-radius) - 1px),var(--alpha-blue) var(--elbow-radius));z-index:4;pointer-events:none}

.brand{position:absolute;left:calc(var(--rail-width) + 18px);top:8px;width:320px;height:96px;z-index:3}
.brand .aegis{display:none}
.brand .section{font-size:36px;line-height:1;color:var(--arctic-ice);font-weight:900;text-transform:uppercase;margin-top:10px}
.brand .org{font-size:10px;color:var(--arctic-snow);font-weight:800;text-transform:uppercase;margin-top:5px;max-width:220px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.brand .datetime{margin-top:8px;font-weight:900;text-transform:uppercase;color:var(--arctic-ice);letter-spacing:.04em;font-size:14px;white-space:nowrap}.brand .brand-user{margin-top:4px;color:var(--arctic-snow);font:800 10px/1 Arial,sans-serif;letter-spacing:.04em;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;max-width:260px}

.live-log{position:absolute;left:470px;top:8px;right:calc(250px + 50px + var(--header-gap));height:108px;overflow:hidden;font:13px/1.35 ui-monospace,SFMono-Regular,Consolas,monospace;padding:0}
.live-log .recent-title{display:flex;align-items:center;width:100%;height:18px;background:var(--title-gold);color:#071018;font:900 10px/18px Arial,sans-serif;letter-spacing:.12em;text-transform:uppercase;padding:0 8px;border-radius:3px 3px 0 0}
.live-log .recent-rows{height:90px;overflow:hidden;padding:3px 10px 0 10px;scrollbar-width:none;-ms-overflow-style:none}
.live-log .recent-rows::-webkit-scrollbar{display:none;width:0;height:0}
.live-log .row{white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.live-log .t{display:inline-block;min-width:176px;color:var(--arctic-snow)}.live-log .inf{color:var(--green);font-weight:900}.live-log .wrn{color:var(--yellow);font-weight:900}.live-log .src{color:var(--cyan)}


.top-buttons{position:absolute;right:50px;top:18px;width:250px;height:72px;display:grid;grid-template-columns:1fr 1fr;gap:8px 10px;padding-left:12px}
.top-buttons a,.top-buttons button{border:0;border-radius:8px;font-weight:900;text-transform:uppercase;color:#071018;cursor:pointer;font-size:12px;display:flex;align-items:center;justify-content:center;line-height:1;padding:0 8px;box-shadow:inset 0 1px 0 rgba(255,255,255,.28),0 1px 0 rgba(0,0,0,.55)}
.top-buttons .gold{background:var(--gold)}.top-buttons .blue{background:var(--blue)}.top-buttons .salmon{background:var(--salmon)}.top-buttons .gray{background:var(--peach)}.top-buttons .logout{background:var(--sunset-red);color:#fff}


/* --- fixed mirrored workspace L --- */
.workspace-shell{position:fixed;left:8px;right:14px;top:calc(148px + var(--scroll-arrow-space));bottom:var(--scroll-arrow-space);min-height:0;overflow:hidden;z-index:10}
.workspace-leg{position:absolute;left:0;top:0;bottom:0;width:var(--rail-width);background:var(--alpha-blue);border-radius:28px 0 0 28px}
.workspace-arm{position:absolute;left:var(--rail-width);right:0;top:0;height:var(--workspace-arm-height);background:var(--title-gold);display:flex;align-items:center;padding:0;z-index:3;border-radius:0 9px 9px 0}
.workspace-arm::before{content:"";position:absolute;left:0;top:0;width:var(--elbow-radius);height:100%;background:var(--alpha-blue);z-index:0}
.workspace-arm .workspace-title{position:relative;z-index:1;height:100%;display:flex;align-items:center;font-weight:900;text-transform:uppercase;color:#071018;letter-spacing:.055em;font-size:30px;line-height:1;margin-left:var(--elbow-radius);padding:0 1ch;background:none;border-left:6px solid #000;border-right:6px solid #000;box-shadow:none;text-shadow:none}
.workspace-arm .last-scan{margin-left:auto;margin-right:14px;color:#3b2f16;font:700 9px/1 Arial,sans-serif;letter-spacing:.06em;text-transform:uppercase;white-space:nowrap;opacity:.9}
.workspace-leg::before{display:none}
.workspace-leg::after{display:none}
.workspace-shell::after{content:"";position:absolute;left:var(--rail-width);top:var(--workspace-arm-height);width:var(--elbow-radius);height:var(--elbow-radius);background:radial-gradient(circle at 100% 100%,transparent 0 calc(var(--elbow-radius) - 1px),var(--alpha-blue) var(--elbow-radius));z-index:5;pointer-events:none}

.workspace-nav{position:absolute;left:0;top:calc(var(--workspace-arm-height) + var(--scroll-arrow-space));width:var(--rail-width);z-index:2}
.workspace-nav a{display:flex;align-items:center;gap:6px;height:48px;padding:0 6px 0 9px;color:#071018;font-weight:900;text-transform:uppercase;font-size:10px;line-height:1.05;white-space:normal;overflow-wrap:normal;word-break:normal;hyphens:none;border-bottom:3px solid #000}
.workspace-nav a:last-child{border-bottom:0}.nav-icon{width:15px;height:15px;flex:0 0 15px;fill:none;stroke:currentColor;stroke-width:1.8;stroke-linecap:round;stroke-linejoin:round}.workspace-nav a.active .nav-icon{stroke-width:2}
.workspace-nav a.active{font-size:12px;font-weight:800;background:var(--title-gold);color:#071018}.lcars-back{position:absolute;left:0;bottom:0;width:var(--rail-width);height:48px;z-index:7;border:0;border-top:3px solid #000;border-radius:0 0 0 28px;background:var(--alpha-blue);color:#071018;font:900 12px/1 "Arial Narrow","Roboto Condensed","Segoe UI",Arial,sans-serif;text-transform:uppercase;text-align:left;padding:0 0 0 11px;cursor:pointer}.lcars-back:hover,.lcars-back:focus-visible{background:var(--title-gold);outline:none}.lcars-back:active{background:var(--title-gold);filter:brightness(1.08)}
.top-buttons a,.workspace-nav a{transition:background .12s ease,color .12s ease,filter .12s ease}
.top-buttons a.nav-flash,.workspace-nav a.nav-flash{background:var(--title-gold)!important;color:#071018!important;filter:brightness(1.08)}

.detail-scroll{position:absolute;left:var(--rail-width);right:0;top:calc(var(--workspace-arm-height) + var(--scroll-arrow-space));bottom:var(--scroll-arrow-space);min-height:0;overflow-y:auto;overflow-x:hidden;overscroll-behavior:contain;background:#000;padding:18px 22px 24px 24px;scrollbar-color:var(--salmon) #111}
.detail-scroll::-webkit-scrollbar{width:10px}.detail-scroll::-webkit-scrollbar-track{background:#090909}.detail-scroll::-webkit-scrollbar-thumb{background:var(--salmon);border-radius:10px}

/* --- current Operations content, intentionally inside the L only --- */
.control-plane-health-banner{display:none;margin:0 0 14px;padding:11px 13px;border:1px solid #c88a27;border-top:3px solid #e5a83f;border-radius:9px;background:#6d4911;color:#ffe1a3}.control-plane-health-banner strong{display:block;color:#ffd27a;font-size:12px;letter-spacing:.08em;text-transform:uppercase;margin-bottom:3px}.control-plane-health-banner.critical{background:#4a1711;border-color:#b94832;border-top-color:#e25b40;color:#ffd0c7}.control-plane-health-banner.critical strong{color:#ffb0a1}.control-plane-health-banner.health-unavailable{background:#30280e;border-color:#8f7620;color:#f0dfa0}
.page-head{display:flex;justify-content:space-between;align-items:flex-start;gap:14px;margin-bottom:6px}
.page-head h1{margin:0;color:var(--blue);font-size:31px}.page-head p{margin:4px 0 0;color:var(--starship-white)}
.scan{color:var(--cyan);text-align:right}
.suite-status{display:grid;grid-template-columns:repeat(5,minmax(110px,1fr));gap:0;margin:4px 0 10px;border:1px solid var(--line-soft);border-top:2px solid var(--line);border-radius:10px;overflow:hidden;background:linear-gradient(180deg,rgba(12,34,52,.58),rgba(2,10,16,.70));box-shadow:inset 0 1px 0 rgba(96,183,230,.07)}.status-cell{position:relative;min-height:44px;padding:6px 22px 6px 9px;border-right:1px solid var(--line-soft);color:var(--white);cursor:pointer;transition:background .12s ease}.status-cell:last-child{border-right:0}.status-cell::after{content:"›";position:absolute;right:8px;top:50%;transform:translateY(-50%);color:var(--arctic-ice);font-size:17px;font-weight:900}.status-cell:hover,.status-cell:focus-visible{background:#0b1b29;outline:none}.status-cell b{display:block;font-size:17px;line-height:1;color:var(--cyan)}.status-cell small{display:block;color:var(--muted);font-size:9px;line-height:1.1;margin-top:3px;text-transform:uppercase;letter-spacing:.03em}.status-cell.good b{color:var(--green)}.status-cell.issue-card{background:#5f4010}.status-cell.issue-card b{color:#ffd27a}.status-cell.issue-card small{color:#ffe1a3}
.content-grid{display:grid;grid-template-columns:1fr 1fr;gap:10px}.landscape{margin:0 0 8px;border:1px solid var(--line-soft);border-top:2px solid var(--line);border-radius:10px;background:linear-gradient(180deg,rgba(8,27,42,.46),rgba(1,7,12,.36));overflow:hidden}.landscape-head{display:flex;align-items:center;justify-content:space-between;padding:6px 9px;border-bottom:1px solid var(--line-soft)}.landscape-head h2{margin:0;color:var(--blue);font-size:15px}.landscape-head a{font-size:9px;color:var(--arctic-ice);font-weight:800;text-transform:uppercase}.landscape-grid{display:grid;grid-template-columns:repeat(5,minmax(145px,1fr));gap:0}.landscape-app,.authority-mini,.runtime-mini{padding:4px 7px;border-right:1px solid var(--line-soft);border-bottom:1px solid var(--line-soft);min-height:42px;align-content:center}.landscape-top{display:flex;align-items:center;justify-content:space-between;gap:8px;min-width:0}.landscape-app b,.authority-mini b,.runtime-mini b{display:block;min-width:0;font-size:10px;line-height:1.15;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.landscape-app small,.authority-mini small,.runtime-mini small{display:block;color:var(--muted);font-size:8.5px;line-height:1.15;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;margin-top:2px}.landscape-state{display:inline-block;flex:0 0 auto;margin:0;font-weight:900;font-size:8.5px;line-height:1.1;white-space:nowrap}.landscape-state.good{color:var(--green)}.landscape-state.warn{color:var(--title-gold)}.landscape-state.bad{color:var(--red)}.landscape-state.offline{color:#9a7f7f}.landscape-state.neutral{color:var(--muted)}.authority-mini{background:#06101a}.authority-mini:hover{background:#0a1926}.authority-mini .authority-role{color:var(--arctic-ice);text-transform:uppercase;letter-spacing:.04em}.runtime-mini{background:#071018}.runtime-mini .runtime-meta{color:var(--muted)}@media(max-width:1180px){.landscape-grid{grid-template-columns:repeat(4,minmax(145px,1fr))}}@media(max-width:900px){.landscape-grid{grid-template-columns:repeat(2,minmax(145px,1fr))}}
.panel{border:1px solid var(--line-soft);border-top:2px solid var(--line);padding:9px 12px 8px;min-height:150px;background:linear-gradient(180deg,rgba(8,27,42,.50),rgba(1,7,12,.40));border-radius:10px;box-shadow:inset 0 1px 0 rgba(96,183,230,.07)}.panel h2{margin:0;color:var(--blue)}.panel p{color:var(--muted)}
.status-pill{display:inline-block;padding:4px 9px;border:1px solid;border-radius:12px;font-weight:900}.ok{color:var(--green)}.bad{color:var(--red)}
.facts{display:grid;grid-template-columns:1fr 1fr;margin-top:6px}.facts div{padding:5px 10px 5px 0;border-bottom:1px solid #173747}.facts small,.facts b{display:block}.facts small{color:var(--muted)}
.activity,.flow{margin-top:10px;border:1px solid var(--line-soft);border-top:2px solid var(--line);padding:8px 12px 5px;background:linear-gradient(180deg,rgba(8,27,42,.40),rgba(1,7,12,.34));border-radius:10px;box-shadow:inset 0 1px 0 rgba(96,183,230,.06)}.activity h2,.flow h2{color:var(--blue);margin:0 0 10px}
.activity-row{display:grid;grid-template-columns:14px 145px 1fr;gap:8px;padding:6px 0;border-bottom:1px solid var(--line-soft);align-items:center}.activity-row:last-child{border-bottom:0}.dot{width:12px;height:12px;border-radius:50%;background:var(--green);display:block;justify-self:center}
.flow-row{display:grid;grid-template-columns:160px 120px 1fr minmax(120px,auto);gap:8px;align-items:center;padding:6px 0;border-bottom:1px solid var(--line-soft)}.flow-row:last-child{border-bottom:0}.lights{display:flex;gap:7px;align-items:center}.lights i{width:11px;height:11px;border-radius:50%;background:#53646f;display:block}.lights i.on{background:var(--green)}.lights i.warn{background:var(--yellow)}

@media(max-width:1250px){
  .brand{width:280px}
  .live-log{left:410px;right:calc(225px + 50px + 14px)}
  .top-buttons{right:50px;width:225px}
}
 .op-appicon{width:28px;height:28px;border-radius:7px;background:#173d58;border:1px solid #3f7ca3;display:grid;place-items:center;font:800 11px Arial,sans-serif;overflow:hidden;flex:none}.op-appicon img,.op-appicon>span{width:100%;height:100%;object-fit:contain;place-items:center} .sound-toggle{margin-left:10px;margin-right:14px;border:0;border-left:3px solid #000;background:#071018;color:var(--title-gold);width:34px;height:26px;padding:4px 7px;cursor:pointer;z-index:6;display:grid;place-items:center}.sound-toggle svg{width:18px;height:18px;fill:currentColor}.sound-toggle:hover{background:#111d2a;color:var(--starship-white)}.sound-toggle:hover{background:#111d2a;color:var(--starship-white)}
__CONTROL_PLANE_HEADER_CSS__
</style>
</head>
<body>

<header class="lcars-top">
  <div class="left-vertical"></div>
  <div class="left-gold"></div>
  <div class="bottom-arm"></div>
  <div class="join-dot"></div>

  <div class="brand">
    <div class="section">Operations</div>
    <div class="org">Food For The Poor Jamaica</div>
    <div class="datetime" id="brandDateTime"></div><div class="brand-user" id="brandUser">SIGNED IN</div>
  </div>

  <div class="live-log"><div class="recent-title">Recent Logs</div><div class="recent-rows" id="liveLog">
    <div class="row"><span class="t">--:--:--</span> <span class="inf">INF</span> <span class="src">[Operations]</span> Loading control-plane state</div>
    <div class="row"><span class="t">--:--:--</span> <span class="inf">INF</span> <span class="src">[Configuration]</span> Awaiting status</div>
    <div class="row"><span class="t">--:--:--</span> <span class="inf">INF</span> <span class="src">[Diagnostics]</span> Awaiting status</div>
    <div class="row"><span class="t">--:--:--</span> <span class="inf">INF</span> <span class="src">[Registration]</span> Awaiting status</div>
    <div class="row"><span class="t">--:--:--</span> <span class="inf">INF</span> <span class="src">[Operations]</span> Ready</div>
  </div></div>

  <div class="top-buttons">
    <a class="blue" href="/">Operations</a>
    <a class="salmon" href="__DIAGNOSTICS_URL__">Diagnostics</a>
    <a class="gold" href="__CONFIGURATION_URL__">Configuration</a>
    <a class="logout" href="/auth/logout">Logout</a>
  </div>
</header>

<section class="workspace-shell">
  <div class="workspace-arm">
    <div class="workspace-title" id="workspaceTitle">Overview</div>
    <div class="last-scan" id="checked">Last Scan: SCANNING</div>
  </div>
  <div class="workspace-leg"></div>
  <nav class="workspace-nav">
    <a class="active" href="#overview"><svg class="nav-icon" viewBox="0 0 24 24" aria-hidden="true"><rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/><rect x="14" y="14" width="7" height="7" rx="1"/></svg><span>Overview</span></a>
    <a href="/applications"><svg class="nav-icon" viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="5" r="2.5"/><circle cx="6" cy="18" r="2.5"/><circle cx="18" cy="18" r="2.5"/><path d="M12 7.5v4M10.5 12.5 7.5 15.5M13.5 12.5l3 3"/></svg><span>Applications</span></a>
    <a href="/deployments"><svg class="nav-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M4 9 12 4l8 5-8 5-8-5Z"/><path d="M4 9v7l8 5 8-5V9M12 14v7"/></svg><span>Deployments</span></a>
    <a href="/handoff/registration"><svg class="nav-icon" viewBox="0 0 24 24" aria-hidden="true"><rect x="3" y="5" width="18" height="14" rx="2"/><circle cx="8" cy="11" r="2"/><path d="M5.5 16c.8-1.7 4.2-1.7 5 0M14 11l2 2 4-4"/></svg><span>Registration</span></a>
    <a href="/logs"><svg class="nav-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M5 6h14M5 12h14M5 18h10"/><path d="M3 6h.01M3 12h.01M3 18h.01"/></svg><span>Logs</span></a>
  </nav>
  <button class="lcars-back" type="button" onclick="if(history.length>1){history.back()}else{location.href='/'}">Back</button>

  <main class="detail-scroll" id="detail">
    <section class="page-head" id="overview"></section>

    <section class="suite-status" aria-label="Operations status">
      <a class="status-cell" href="/applications"><b id="registeredCount">—</b><small>Registered</small></a>
      <a class="status-cell good" href="/applications"><b id="healthyCount">—</b><small>Healthy</small></a>
      <a class="status-cell" id="warningCard" href="/applications"><b id="warningCount">—</b><small>Warnings</small></a>
      <a class="status-cell" id="activeAlertCard" href="#attention"><b id="activeAlertCount">—</b><small>Alerts</small></a>
      <a class="status-cell" href="#flow"><b id="activeFlowCount">—</b><small>Flows</small></a>
    </section>

    <section class="landscape">
      <div class="landscape-head"><h2>Installation Landscape</h2><a href="/applications">Open Applications →</a></div>
      <div class="landscape-grid" id="installationLandscape"><div class="landscape-app"><b>Loading installation landscape…</b></div></div>
    </section>

    <section class="flow" id="flow">
      <h2>Active Flow Analyzer</h2>
      <div id="flowList"><div class="flow-row"><b>Loading</b><span>Telemetry</span><div class="lights"><i></i><i></i><i></i><i></i></div><span>—</span></div></div>
    </section>

    <section class="content-grid" id="attention" style="margin-top:10px">
      <article class="panel" style="min-height:0">
        <h2>Attention</h2><p>Active alerts and conditions that require operator review.</p>
        <div id="alertList"><div class="activity-row"><i class="dot"></i><span>LOADING</span><b>Reading active alerts…</b></div></div>
      </article>
      <article class="panel" style="min-height:0">
        <h2>Recent Logs</h2><p>Latest control-plane and application lifecycle events.</p>
        <div id="activityList"><div class="activity-row"><i class="dot"></i><span>LOADING</span><b>Reading operational history…</b></div></div>
      </article>
    </section>


  </main>
</section>

<script>
const detail=document.getElementById('detail');
function recentStamp(v){const d=v?new Date(v):new Date();if(Number.isNaN(d.getTime()))return '--- -- ---- --:--:--';const mon=d.toLocaleString('en-US',{month:'short'});return mon+' '+String(d.getDate()).padStart(2,'0')+' '+d.getFullYear()+' '+String(d.getHours()).padStart(2,'0')+':'+String(d.getMinutes()).padStart(2,'0')+':'+String(d.getSeconds()).padStart(2,'0')}
function seedRecentTimestamps(){document.querySelectorAll('#liveLog .t').forEach(x=>{if(!x.textContent||x.textContent.includes('--:--:--'))x.textContent=recentStamp();});}
seedRecentTimestamps();
async function loadBrandUser(){try{const r=await fetch('/auth/me',{cache:'no-store'});if(!r.ok)return;const x=await r.json();const e=document.getElementById('brandUser');if(e)e.textContent='User: '+(x.displayName||x.userName||'Signed in')+' · IP: '+(x.clientIp||'unknown');}catch{}}
loadBrandUser();
function updateBrandClock(){
  const d=new Date();
  const target=document.getElementById('brandDateTime');
  if(!target) return;
  const month=d.toLocaleString('en-US',{month:'short'}).toUpperCase();
  const day=String(d.getDate()).padStart(2,'0');
  const year=d.getFullYear();
  const hours=String(d.getHours()).padStart(2,'0');
  const minutes=String(d.getMinutes()).padStart(2,'0');
  const tt=d.getHours()>=12?'PM':'AM';
  target.textContent=month+' '+day+' '+year+' '+hours+':'+minutes+' '+tt;
}
updateBrandClock();
setInterval(updateBrandClock,1000);
document.querySelectorAll('.top-buttons a,.workspace-nav a').forEach(a=>a.addEventListener('pointerdown',()=>{a.classList.add('nav-flash');setTimeout(()=>a.classList.remove('nav-flash'),180)}));
function showRegisteredApplicationHealthIssues(apps){const host=document.querySelector('.detail-scroll');if(!host)return;let banner=document.getElementById('registeredApplicationHealthBanner');if(!banner){banner=document.createElement('section');banner.id='registeredApplicationHealthBanner';banner.className='control-plane-health-banner';const own=document.getElementById('controlPlaneHealthBanner');own?.insertAdjacentElement('afterend',banner)??host.prepend(banner);}const issues=(apps||[]).filter(a=>String(a.state||'Unknown').toLowerCase()!=='healthy'||a.stale);if(!issues.length){banner.style.display='none';return;}const critical=issues.some(a=>['unhealthy','failed','critical'].includes(String(a.state||'').toLowerCase()));banner.classList.toggle('critical',critical);banner.innerHTML='<strong>REGISTERED APPLICATION HEALTH · '+issues.length+' ISSUE'+(issues.length===1?'':'S')+'</strong><div>'+issues.slice(0,8).map(a=>esc(a.displayName||a.applicationId)+' — '+esc(a.state||'Unknown')+(a.stale?' (no recent heartbeat)':'')+(a.reason?' · '+esc(a.reason):'')).join('<br>')+(issues.length>8?'<br>+'+(issues.length-8)+' more':'')+'</div>';banner.style.display='block';}
async function refreshControlPlaneHealthBanner(){const host=document.querySelector('.detail-scroll');if(!host)return;let banner=document.getElementById('controlPlaneHealthBanner');if(!banner){banner=document.createElement('section');banner.id='controlPlaneHealthBanner';banner.className='control-plane-health-banner';host.prepend(banner);}try{const response=await fetch('/health',{cache:'no-store'});if(!response.ok)throw new Error('HTTP '+response.status);const h=await response.json(),state=String(h.health||h.status||'Unknown'),summary=String(h.summary||'Health check returned no summary.').trim(),lower=state.toLowerCase();if(lower==='healthy'){banner.style.display='none';banner.classList.remove('critical','health-unavailable');return;}banner.classList.toggle('critical',lower==='unhealthy');banner.classList.remove('health-unavailable');banner.innerHTML='<strong>'+state.toUpperCase()+'</strong><div>'+esc(summary)+'</div>';banner.style.display='block';}catch(error){banner.classList.remove('critical');banner.classList.add('health-unavailable');banner.innerHTML='<strong>HEALTH CHECK UNAVAILABLE</strong><div>'+esc(error?.message||'Unable to read /health.')+'</div>';banner.style.display='block';}}
refreshControlPlaneHealthBanner();setInterval(refreshControlPlaneHealthBanner,30000);
const workspaceLinks=[...document.querySelectorAll('.workspace-nav a[href^="#"]')];
function setActiveWorkspaceLink(link){
  workspaceLinks.forEach(x=>x.classList.toggle('active',x===link));
  const title=document.getElementById('workspaceTitle');
  if(title&&link)title.textContent=(link.textContent||'').trim();
}
workspaceLinks.forEach(a=>a.addEventListener('click',e=>{
  const id=a.getAttribute('href'); if(!id?.startsWith('#')) return; e.preventDefault();
  const target=document.querySelector(id);
  if(target) detail.scrollTo({top:Math.max(0,target.offsetTop-24),behavior:'smooth'});
  setActiveWorkspaceLink(a);
}));
function syncWorkspaceNav(){
  const anchor=detail.scrollTop+80;
  let active=workspaceLinks[0]??null;
  for(const link of workspaceLinks){
    const target=document.querySelector(link.getAttribute('href'));
    if(target && target.offsetTop<=anchor) active=link;
  }
  if(active) setActiveWorkspaceLink(active);
}
detail.addEventListener('scroll',syncWorkspaceNav,{passive:true});
syncWorkspaceNav();

function esc(v){return String(v??'').replace(/[&<>"']/g,m=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[m]))}
function addLog(level,source,message){
  const row=document.createElement('div'); row.className='row';
  row.innerHTML='<span class="t">'+recentStamp()+'</span> <span class="'+level.toLowerCase()+'">'+esc(level)+'</span> <span class="src">['+esc(source)+']</span> '+esc(message);
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
function flowIsActive(flow){
  const stages=flow?.stages??[];
  if(!stages.length)return false;
  const states=stages.map(s=>String(s.state??'').toLowerCase());
  if(states.some(x=>x==='active'||x==='warning'))return true;
  const terminal=new Set(['completed','failed','timedout']);
  return states.some(x=>!terminal.has(x));
}
function appInitials(a){const src=String(a?.shortName||a?.displayName||a?.applicationId||'AE').replace(/^Aegis[. ]/i,'');const parts=src.split(/[ ._-]+/).filter(Boolean);return parts.length>1?(parts[0][0]+parts[1][0]).toUpperCase():src.slice(0,2).toUpperCase()}
function safeIconUrl(v){if(!v)return'';try{if(v.startsWith('/')&&!v.startsWith('//')&&!v.includes('..'))return v;const u=new URL(v,location.origin);return u.protocol==='https:'?u.href:''}catch{return''}}
function appIcon(a){const initials=esc(appInitials(a)),url=safeIconUrl(String(a?.iconUrl||''));return url?'<span class="op-appicon"><img src="'+esc(url)+'" alt="" onerror="this.style.display=\'none\';this.nextElementSibling.style.display=\'grid\'"><span style="display:none">'+initials+'</span></span>':'<span class="op-appicon">'+initials+'</span>'}
function renderApplications(data){
  const apps=data?.applications??[];
  registrationAuthority.textContent=data?.authorityAvailable?'Configuration registration authority online':'Registration authority unavailable'+(data?.authorityError?': '+data.authorityError:'');
  if(!apps.length){applicationsList.innerHTML='<div class="activity-row"><i class="dot"></i><span>0</span><b>No registered applications returned.</b></div>';return}
  applicationsList.innerHTML=apps.slice(0,12).map(a=>{
    const state=String(a.state??'Unknown'); const cls=state.toLowerCase()==='healthy'?'ok':'bad';
    return '<div class="activity-row"><i class="dot" style="background:'+(cls==='ok'?'var(--green)':'var(--yellow)')+'"></i><span style="display:flex;align-items:center;gap:8px">'+appIcon(a)+esc(a.applicationId)+'</span><b>'+esc(a.instanceId)+' · '+esc(a.stale?'Offline':state)+(a.stale?' · NO RECENT HEARTBEAT':'')+'</b></div>'
  }).join('');
}
function renderLandscape(apps,summary,self){
  const rows=apps??[];
  const authorityTile=(name,role,stateObj,href)=>{
    const state=stateName(stateObj),lower=state.toLowerCase(),cls=lower==='healthy'?'good':(['unhealthy','failed','critical'].includes(lower)?'bad':'warn'),reason=stateObj?.reason||stateObj?.url||role;
    return '<a class="authority-mini" href="'+href+'" title="'+esc(reason)+'"><div class="landscape-top"><b>'+esc(name)+'</b><span class="landscape-state '+cls+'">'+esc(state.toUpperCase())+'</span></div><small class="authority-role">'+esc(role)+'</small></a>';
  };
  const runtimeState=String(self?.state??'Unknown'),runtimeLower=runtimeState.toLowerCase(),runtimeCls=runtimeLower==='healthy'||runtimeLower==='running'?'good':(['unhealthy','failed','critical'].includes(runtimeLower)?'bad':'warn');
  const runtime='<div class="runtime-mini" title="Operations Runtime"><div class="landscape-top"><b>Operations Runtime</b><span class="landscape-state '+runtimeCls+'">'+esc(runtimeState.toUpperCase())+'</span></div><small class="runtime-meta">'+esc(String(self?.storage??'Unknown').toUpperCase())+' · __VERSION__ · '+esc(location.hostname)+'</small></div>';
  const infrastructure=
    authorityTile('Configuration','Configuration Authority',summary?.configuration,'/handoff/configuration')+
    authorityTile('Diagnostics','Health Authority',summary?.diagnostics,'/handoff/diagnostics')+
    runtime;
  const applications=rows.length?rows.map(a=>{
    const state=String(a.stale?'Offline':(a.state??'Unknown')),lower=state.toLowerCase(),cls=lower==='healthy'?'good':(['unhealthy','failed','critical'].includes(lower)?'bad':'warn');
    const flowLabel=String(a.registrationFlowLabel||a.registrationStatus||'Unknown'),flowTone=String(a.registrationFlowTone||cls),displayName=String(a.displayName||a.applicationId||'Application'),applicationId=String(a.applicationId||''),sameName=displayName.localeCompare(applicationId,undefined,{sensitivity:'accent'})===0,identityBits=[sameName?'':applicationId,String(a.instanceId||'—'),String(a.environment||'—')].filter(Boolean);return '<a class="landscape-app" href="/applications"><div class="landscape-top"><b>'+esc(displayName)+'</b><span class="landscape-state '+esc(flowTone)+'">'+esc(flowLabel.toUpperCase())+'</span></div><small>'+identityBits.map(esc).join(' · ')+'</small><small>'+esc(a.reason||a.version||'')+'</small></a>'
  }).join(''):'<div class="landscape-app"><b>No registered applications</b><small>Nothing is currently reporting into Operations.</small></div>';
  installationLandscape.innerHTML=infrastructure+applications;
}
function renderHistory(items){
  const rows=(items??[]).slice(0,8);
  if(!rows.length){activityList.innerHTML='<div class="activity-row"><i class="dot"></i><span>—</span><b>No operational history available.</b></div>';liveLog.innerHTML='<div class="row"><span class="t">'+recentStamp()+'</span> <span class="inf">INF</span> <span class="src">[Operations]</span> No operational history available</div>';return}
  activityList.innerHTML=rows.map(x=>'<div class="activity-row"><i class="dot"></i><span>'+fmtTime(x.at)+'</span><b>'+esc(x.application||x.type)+' · '+esc(x.text)+'</b></div>').join('');
  liveLog.innerHTML=rows.slice(0,5).reverse().map(x=>'<div class="row"><span class="t">'+fmtTime(x.at)+'</span> <span class="'+levelFromType(x.type).toLowerCase()+'">'+levelFromType(x.type)+'</span> <span class="src">['+esc(x.application||x.type)+']</span> '+esc(x.text)+'</div>').join('');
}
function renderAlerts(items){
  const rows=(items??[]).filter(a=>String(a.state??'').toLowerCase()!=='resolved').slice(0,6);
  if(!rows.length){alertList.innerHTML='<div class="activity-row"><i class="dot"></i><span>0</span><b>No active alerts.</b></div>';return}
  alertList.innerHTML=rows.map(a=>'<div class="activity-row"><i class="dot" style="background:var(--yellow)"></i><span>'+esc(a.applicationId||a.application||a.type||'Alert')+'</span><b>'+esc(a.message||a.text||a.title||a.state||'Attention required')+'</b></div>').join('');
}
function renderFlows(flows){
  const rows=(flows??[]).filter(flowIsActive).slice(0,8);
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
    warningCount.textContent=warnings;warningCard.classList.toggle('issue-card',warnings>0);
    activeAlertCount.textContent=activeAlerts;
    const activeFlows=(flows??[]).filter(flowIsActive);
    activeFlowCount.textContent=activeFlows.length;

    const cfg=stateName(summary.configuration), diag=stateName(summary.diagnostics);
    checked.textContent='Last Scan: '+new Date(summary.checkedAt).toLocaleString();

    const attention=warnings+activeAlerts+(summary.registration?.attention??0)+(cfg.toLowerCase()==='healthy'?0:1)+(diag.toLowerCase()==='healthy'?0:1);
    const healthy=attention===0;
    document.title='Operations · '+(healthy?'Nominal':'Attention');

    renderLandscape(apps,summary,self);
    renderFlows(flows);
    renderAlerts(alerts);
    renderHistory(history);


  }catch(err){
    document.title='Operations · Telemetry Unavailable';
    const message=err?.message??'Status telemetry unavailable';
    addLog('ERR','Operations',message);
    console.error('Operations overview load failed',err);
  }
}
async function refreshRecentLogs(){
  try{
    const r=await fetch('/api/operations/activity?take=5',{cache:'no-store'});if(!r.ok)return;
    const x=await r.json(),events=x.events||[];
    if(!events.length)return;
    liveLog.innerHTML=events.slice(-5).map(e=>{
      const outcome=String(e.outcome||e.level||'Information'),lower=outcome.toLowerCase(),level=(lower.includes('fail')||lower.includes('error')||lower.includes('critical'))?'ERR':(lower.includes('warn')?'WRN':'INF');
      return '<div class="row"><span class="t">'+fmtTime(e.occurredAtUtc||e.atUtc||e.at)+'</span> <span class="'+level.toLowerCase()+'">'+level+'</span> <span class="src">['+esc(e.applicationId||e.application||'Operations')+']</span> '+esc(e.detail||e.stage||e.flow||e.code||outcome)+'</div>'
    }).join('');
  }catch{}
}
load();setInterval(load,30000);refreshRecentLogs();setInterval(refreshRecentLogs,5000);

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
    navigation:()=>{tone(1174.7,.025,.032);tone(1568,.032,.028,.032)},
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
