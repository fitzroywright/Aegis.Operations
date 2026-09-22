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
<title>Logs & Flow Analyzer · Operations</title>
<style>
:root{
  --black:#000;--alpha-blue:#5588ee;--arctic-ice:#66ccff;--arctic-snow:#99ccff;
  --beta-blue:#7799dd;--night-cloud:#344470;--night-rain:#455580;--radioactive:#88eeff;
  --starship-white:#f3f3fc;--sunset-red:#ff3500;--peach:#99ccff;--salmon:#7799dd;
  --gold:#88eeff;--blue:#5588ee;--cyan:#66ccff;--green:#88eeff;--yellow:#99ccff;
  --red:#ff3500;--white:#f3f3fc;--muted:#99ccff;--line:#455580;--line-soft:#344470;
  --panel:#071020;--title-gold:#ffcc66;--rail-width:160px;--top-arm-height:14px;
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
.top-buttons .gold{background:var(--gold)}.top-buttons .blue{background:var(--blue)}.top-buttons .salmon{background:var(--salmon)}.top-buttons .gray{background:var(--peach)}.top-buttons .logout{background:var(--sunset-red);color:#fff}
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
.workspace-nav a{display:flex;align-items:center;gap:6px;height:48px;padding:0 6px 0 9px;color:#071018;font-weight:900;text-transform:uppercase;font-size:10px;line-height:1.05;white-space:normal;overflow-wrap:normal;word-break:normal;hyphens:none;border-bottom:3px solid #000;cursor:pointer}.nav-icon{width:15px;height:15px;flex:0 0 15px;fill:none;stroke:currentColor;stroke-width:1.8;stroke-linecap:round;stroke-linejoin:round}.workspace-nav a.active .nav-icon{stroke-width:2}
.workspace-nav a.active{font-size:12px;font-weight:800;background:var(--title-gold);color:#071018}.lcars-back{position:absolute;left:0;bottom:0;width:var(--rail-width);height:48px;z-index:7;border:0;border-top:3px solid #000;border-radius:0 0 0 28px;background:var(--alpha-blue);color:#071018;font:900 12px/1 "Arial Narrow","Roboto Condensed","Segoe UI",Arial,sans-serif;text-transform:uppercase;text-align:left;padding:0 0 0 11px;cursor:pointer}.lcars-back:hover,.lcars-back:focus-visible{background:var(--title-gold);outline:none}.lcars-back:active{background:var(--title-gold);filter:brightness(1.08)}
.detail-scroll{position:absolute;left:var(--rail-width);right:0;top:calc(var(--workspace-arm-height) + var(--scroll-arrow-space));bottom:var(--scroll-arrow-space);min-height:0;overflow:hidden;background:#000;padding:8px 10px 6px 10px;display:flex;flex-direction:column;gap:6px}

.control-plane-health-banner{display:none;margin:0 0 14px;padding:11px 13px;border:1px solid #c88a27;border-top:3px solid #e5a83f;border-radius:9px;background:#6d4911;color:#ffe1a3}.control-plane-health-banner strong{display:block;color:#ffd27a;font-size:12px;letter-spacing:.08em;text-transform:uppercase;margin-bottom:3px}.control-plane-health-banner.critical{background:#4a1711;border-color:#b94832;border-top-color:#e25b40;color:#ffd0c7}.control-plane-health-banner.critical strong{color:#ffb0a1}.control-plane-health-banner.health-unavailable{background:#30280e;border-color:#8f7620;color:#f0dfa0}
.page-head{display:flex;align-items:flex-start;justify-content:space-between;gap:20px;margin-bottom:14px}.page-head p{margin:0;color:var(--starship-white)}
.filter-toggle-row{display:flex;align-items:center;justify-content:flex-end;gap:8px;min-height:26px}.log-mode-tabs{display:flex;gap:5px;margin-right:auto}.log-mode-tabs .action.active{background:var(--title-gold)}.filters{display:grid;grid-template-columns:repeat(4,minmax(120px,1fr));gap:7px;padding:8px 10px;background:linear-gradient(180deg,rgba(8,27,42,.50),rgba(1,7,12,.40));border:0;margin:0;max-height:240px;opacity:1;overflow:hidden;transition:max-height .18s ease,opacity .14s ease,padding .18s ease}.filters.collapsed{max-height:0;opacity:0;padding-top:0;padding-bottom:0;pointer-events:none}.filters input,.filters select{width:100%;background:#02080d;color:var(--white);border:1px solid var(--line);border-radius:4px;padding:7px 8px;font:12px "Arial Narrow","Segoe UI",Arial,sans-serif}.filters .wide{grid-column:span 2}.filters [hidden]{display:none!important}
.toolbar{display:flex;align-items:center;justify-content:flex-end;gap:5px;margin-bottom:7px;min-height:28px}.toolbar .state{margin-left:0;margin-right:auto;order:-1;font-size:11px}.action{border:0;border-radius:5px;padding:5px 8px;background:var(--blue);color:#071018;font-weight:900;text-transform:uppercase;cursor:pointer;font-size:10px;line-height:1.1;min-height:24px;box-shadow:inset 0 1px 0 rgba(255,255,255,.18)}.action.secondary{background:var(--salmon)}.action.pause-action{background:#7799dd}.action.tail-action{background:#66ccff}.action.clear-action{background:#99ccff}.action.ids-action{background:#88eeff}.action:hover{filter:brightness(1.12)}.action:active{filter:brightness(.92)}.state{color:var(--muted);font-weight:800}
.telemetry-panel{border:0;border-radius:0;background:#000;overflow:hidden;flex:1;min-height:0;margin:0 2px;position:relative}.log{height:100%;min-height:0;overflow:auto;padding:2px 0;scrollbar-color:var(--salmon) #111}.log::-webkit-scrollbar,.traces::-webkit-scrollbar{width:9px;height:9px}.log::-webkit-scrollbar-thumb,.traces::-webkit-scrollbar-thumb{background:var(--salmon);border-radius:8px}
.telemetry-row{display:grid;grid-template-columns:176px 180px minmax(220px,1.1fr) 108px minmax(320px,2fr);gap:0;padding:0 8px;border-bottom:1px solid rgba(69,85,128,.24);align-items:center;min-height:34px;background:#03080d;font:12px/1.25 ui-monospace,SFMono-Regular,Consolas,monospace;outline:none;cursor:default}.telemetry-row:hover{background:#061019}.telemetry-row.active,.telemetry-row[aria-selected="true"],.telemetry-row:focus{background:#061019;color:inherit;box-shadow:none}.telemetry-row>.time,.telemetry-row>.app,.telemetry-row>.flow,.telemetry-row>.outcome,.telemetry-row>.detail{min-width:0;padding:6px 10px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;border-left:1px solid rgba(69,85,128,.18)}.telemetry-row>.time{border-left:0;padding-left:2px}.telemetry-row>.registration-id-cell{display:none}.telemetry-row button{background:none;border:0;color:inherit;text-align:left;font:inherit;cursor:pointer;padding:0}.time{color:var(--arctic-snow)}.app{color:var(--cyan)}.flow{color:#c7b8ff}.outcome{font-weight:900}.Succeeded{color:var(--green)}.Information,.Started{color:var(--cyan)}.Debug,.Trace{color:var(--muted)}.Warning{color:var(--yellow)}.Error,.Critical,.Failed{color:var(--red)}.Skipped{color:#8798a4}.detail,.registration-id-cell{white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.detail{color:var(--starship-white)}.registration-id-cell{display:none}.app,.flow{white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.outcome{white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.empty{padding:40px;text-align:center;color:var(--muted)}.empty .action{margin-top:12px}.filter-summary{flex:0 0 auto;min-height:28px;display:flex;align-items:center;gap:8px;padding:4px 8px;margin:0 2px;background:#071018;border-top:2px solid var(--title-gold);color:var(--arctic-snow);font:800 10px/1.2 Arial,sans-serif;text-transform:uppercase;letter-spacing:.04em;white-space:nowrap;overflow:hidden}.filter-summary b{color:var(--title-gold);flex:0 0 auto}.filter-summary span{overflow:hidden;text-overflow:ellipsis}.flow-overlay{position:absolute;z-index:40;right:26px;top:calc(var(--workspace-arm-height) + var(--scroll-arrow-space) + 12px);width:min(780px,calc(100% - var(--rail-width) - 52px));max-height:calc(100% - var(--workspace-arm-height) - var(--scroll-arrow-space) - 28px);display:flex;flex-direction:column;background:#02080d;border:2px solid var(--title-gold);box-shadow:0 12px 34px rgba(0,0,0,.78);overflow:hidden}.flow-overlay[hidden]{display:none}.flow-overlay-head{display:flex;align-items:center;gap:12px;background:var(--title-gold);color:#071018;padding:8px 10px;font-weight:900;text-transform:uppercase;cursor:move;user-select:none;touch-action:none}.flow-overlay-head .grow{flex:1;min-width:0;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.flow-overlay-head button{border:0;background:#071018;color:var(--title-gold);font-weight:900;padding:5px 9px;cursor:pointer}.flow-overlay-body{overflow:auto;min-height:140px;padding:10px}.flow-overlay .trace{margin:0 0 10px}.overlay-empty{padding:22px;color:var(--muted);text-align:center}

.traces{height:100%;min-height:0;overflow:auto;padding:12px}
.trace{border:1px solid var(--line-soft);border-top:2px solid var(--line);border-radius:10px;margin-bottom:12px;background:#040b11;overflow:hidden}
.trace-head{display:flex;gap:14px;align-items:center;padding:12px 14px;background:#091522}.trace-head b{font-size:15px}.trace-head .grow{flex:1}.trace-meta{color:var(--muted);font-size:12px}
.stages{display:flex;align-items:flex-start;gap:0;padding:22px 18px;overflow-x:auto;scrollbar-color:var(--salmon) #111}.stage{min-width:160px;max-width:220px;position:relative;text-align:center}.stage:not(:last-child):after{content:"";position:absolute;top:12px;left:59%;width:82%;height:3px;background:var(--night-rain)}
.lamp{position:relative;z-index:1;width:24px;height:24px;border-radius:50%;margin:0 auto 8px;background:#53646f;box-shadow:0 0 0 4px #101923}.lamp.Succeeded{background:var(--green)}.lamp.Warning{background:var(--yellow)}.lamp.Failed{background:var(--red)}.lamp.Started{background:var(--blue);animation:pulse 1.2s infinite}.lamp.Skipped{background:#8798a4}@keyframes pulse{50%{box-shadow:0 0 0 8px rgba(102,204,255,.18)}}.stage b{display:block;font-size:12px}.stage small{display:block;color:var(--muted);margin-top:4px}.trace-detail{padding:0 16px 15px;color:var(--arctic-snow)}

@media(max-width:1250px){.brand{width:280px}.live-log{left:410px;right:calc(225px + 50px + 14px)}.top-buttons{right:50px;width:225px}.filters{grid-template-columns:repeat(3,1fr)}}
@media(max-width:900px){.filters{grid-template-columns:repeat(2,1fr)}.flow-overlay{right:12px;width:calc(100% - var(--rail-width) - 24px)}.telemetry-row{grid-template-columns:150px minmax(150px,1fr) 92px 70px}.telemetry-row .flow,.telemetry-row .detail{display:none}.telemetry-row>.time,.telemetry-row>.app,.telemetry-row>.outcome{padding:6px 8px}}
 .sound-toggle{margin-left:10px;margin-right:14px;border:0;border-left:3px solid #000;background:#071018;color:var(--title-gold);width:34px;height:26px;padding:4px 7px;cursor:pointer;z-index:6;display:grid;place-items:center}.sound-toggle svg{width:18px;height:18px;fill:currentColor}.sound-toggle:hover{background:#111d2a;color:var(--starship-white)}.sound-toggle:hover{background:#111d2a;color:var(--starship-white)}
__CONTROL_PLANE_HEADER_CSS__
</style>
</head>
<body>
<header class="lcars-top">
  <div class="left-vertical"></div><div class="bottom-arm"></div>
  <div class="brand"><div class="section">Operations</div><div class="org">Food For The Poor Jamaica</div><div class="datetime" id="brandDateTime"></div><div class="brand-user" id="brandUser">SIGNED IN</div></div>
  <div class="live-log"><div class="recent-title">Recent Logs</div><div class="recent-rows" id="liveLog"><div class="row"><span class="t"></span> <span class="inf">INF</span> <span class="src">[Operations]</span> Loading suite telemetry</div></div></div>
  <div class="top-buttons">
    <a class="blue" href="/">Operations</a>
    <a class="salmon" href="/handoff/diagnostics">Diagnostics</a>
    <a class="gold" href="/handoff/configuration">Configuration</a>
    <a class="logout" href="/auth/logout">Logout</a>
  </div>
</header>

<section class="workspace-shell">
  <div class="workspace-arm"><div class="workspace-title">Logs</div><div class="last-scan" id="lastScan">Last Scan: SCANNING</div></div>
  <div class="workspace-leg"></div>
  <nav class="workspace-nav">
    <a href="/"><svg class="nav-icon" viewBox="0 0 24 24" aria-hidden="true"><rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/><rect x="14" y="14" width="7" height="7" rx="1"/></svg><span>Overview</span></a>
    <a href="/#systems"><svg class="nav-icon" viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="5" r="2.5"/><circle cx="6" cy="18" r="2.5"/><circle cx="18" cy="18" r="2.5"/><path d="M12 7.5v4M10.5 12.5 7.5 15.5M13.5 12.5l3 3"/></svg><span>Applications</span></a>
    <a href="/#deployments"><svg class="nav-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M4 9 12 4l8 5-8 5-8-5Z"/><path d="M4 9v7l8 5 8-5V9M12 14v7"/></svg><span>Deployments</span></a>
    <a href="/handoff/registration"><svg class="nav-icon" viewBox="0 0 24 24" aria-hidden="true"><rect x="3" y="5" width="18" height="14" rx="2"/><circle cx="8" cy="11" r="2"/><path d="M5.5 16c.8-1.7 4.2-1.7 5 0M14 11l2 2 4-4"/></svg><span>Registration</span></a>
    <a class="active" href="/logs"><svg class="nav-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M5 6h14M5 12h14M5 18h10"/><path d="M3 6h.01M3 12h.01M3 18h.01"/></svg><span>Logs</span></a>
  </nav>
  <button class="lcars-back" type="button" onclick="if(history.length>1){history.back()}else{location.href='/'}">Back</button>

  <main class="detail-scroll">
    <div class="filter-toggle-row">
      <div class="log-mode-tabs" role="tablist" aria-label="Log views">
        <button class="action active" id="navActivity" type="button" data-mode="log">Lifecycle Logs</button>
        <button class="action" id="navLogs" type="button" data-mode="appLogs">Application Logs</button>
        <button class="action" id="navFlow" type="button" data-mode="flow">Flow Analyzer</button>
      </div>
      <button class="action" id="filterToggle" type="button" aria-expanded="false" aria-controls="filtersPanel">Show Filters</button>
    </div>
<section class="filters collapsed" id="filtersPanel" aria-hidden="true">
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
      <button class="action pause-action" id="pause" title="Pause screen refresh">Pause</button>
      <button class="action tail-action" id="follow" title="Keep view pinned to newest events">Tail: On</button>
      <button class="action clear-action" id="clear" title="Clear the current view only">Clear</button>
      <button class="action ids-action" id="toggleIds" title="Show or hide registration IDs">IDs: On</button>
      <span class="state" id="state">CONNECTING…</span>
    </div>
    <section class="telemetry-panel">
      <div id="logView" class="log" tabindex="0" aria-label="Log results"></div>
      <div id="appLogView" class="log" tabindex="0" aria-label="Application log results" hidden></div>
      <div id="flowView" class="traces" hidden></div>
    </section>
    <div class="filter-summary" id="filterSummary"><b>FILTER</b><span>None · showing all logs from the last 60 minutes</span></div>
  </main>
  <aside class="flow-overlay" id="flowOverlay" hidden role="dialog" aria-modal="false" aria-labelledby="flowOverlayTitle">
    <div class="flow-overlay-head"><span id="flowOverlayTitle">Flow Analyzer</span><span class="grow" id="flowOverlayContext"></span><button id="flowOverlayClose" type="button" aria-label="Close Flow Analyzer">Close</button></div>
    <div class="flow-overlay-body" id="flowOverlayBody"></div>
  </aside>
</section>

<script>
const $=id=>document.getElementById(id),esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#039;'}[c]));
const initialQuery=new URLSearchParams(location.search);let paused=false,followTail=true,mode=['log','appLogs','flow'].includes(initialQuery.get('mode'))?initialQuery.get('mode'):'log',hiddenBefore=null;
let selectedKey=null,selectedEvent=null,overlayOpen=false,lastFocusedList=null,analyzerEvent=null;
let currentActivityEvents=[],currentAppLogEvents=[];
const idsKey='aegis.engineering.showIds';
let showIds=localStorage.getItem(idsKey)!=='off';
function applyIdsVisibility(){
  $('logView')?.classList.toggle('ids-hidden',!showIds);
  const b=$('toggleIds');
  if(b)b.textContent='IDs: '+(showIds?'On':'Off');
}
function recentStamp(v){const d=v?new Date(v):new Date();if(Number.isNaN(d.getTime()))return '--- -- ---- --:-- --';const mon=d.toLocaleString('en-US',{month:'short'});let h=d.getHours(),tt=h>=12?'PM':'AM';h=h%12||12;return mon+' '+String(d.getDate()).padStart(2,'0')+' '+d.getFullYear()+' '+String(h).padStart(2,'0')+':'+String(d.getMinutes()).padStart(2,'0')+' '+tt}
async function loadBrandUser(){try{const r=await fetch('/auth/me',{cache:'no-store'});if(!r.ok)return;const x=await r.json(),e=document.getElementById('brandUser');if(e)e.textContent='User: '+(x.displayName||x.userName||'Signed in')+' · IP: '+(x.clientIp||'unknown');}catch{}}loadBrandUser();
function updateClock(){const d=new Date(),t=$('brandDateTime');const mon=d.toLocaleString('en-US',{month:'short'}).toUpperCase();let h=d.getHours(),tt=h>=12?'PM':'AM';h=h%12||12;t.textContent=mon+' '+String(d.getDate()).padStart(2,'0')+' '+d.getFullYear()+' '+String(h).padStart(2,'0')+':'+String(d.getMinutes()).padStart(2,'0')+' '+tt}updateClock();setInterval(updateClock,1000);document.querySelector('#liveLog .t').textContent=recentStamp();
document.querySelectorAll('.top-buttons a,.workspace-nav a').forEach(a=>a.addEventListener('pointerdown',()=>{a.classList.add('nav-flash');setTimeout(()=>a.classList.remove('nav-flash'),180)}));
async function refreshRegisteredApplicationHealth(){const host=document.querySelector('.detail-scroll');if(!host)return;let banner=document.getElementById('registeredApplicationHealthBanner');if(!banner){banner=document.createElement('section');banner.id='registeredApplicationHealthBanner';banner.className='control-plane-health-banner';const own=document.getElementById('controlPlaneHealthBanner');own?.insertAdjacentElement('afterend',banner)??host.prepend(banner);}try{const r=await fetch('/api/operations/registered-applications',{cache:'no-store'});if(!r.ok)throw new Error('HTTP '+r.status);const x=await r.json(),issues=(x.applications||[]).filter(a=>String(a.state||'Unknown').toLowerCase()!=='healthy'||a.stale);if(!issues.length){banner.style.display='none';return;}banner.classList.toggle('critical',issues.some(a=>['unhealthy','failed','critical'].includes(String(a.state||'').toLowerCase())));banner.innerHTML='<strong>REGISTERED APPLICATION HEALTH · '+issues.length+' ISSUE'+(issues.length===1?'':'S')+'</strong><div>'+issues.slice(0,8).map(a=>esc(a.displayName||a.applicationId)+' — '+esc(a.state||'Unknown')+(a.stale?' (stale)':'')+(a.reason?' · '+esc(a.reason):'')).join('<br>')+'</div>';banner.style.display='block';}catch{banner.style.display='none';}}
refreshRegisteredApplicationHealth();setInterval(refreshRegisteredApplicationHealth,30000);
async function refreshControlPlaneHealthBanner(){const host=document.querySelector('.detail-scroll');if(!host)return;let banner=document.getElementById('controlPlaneHealthBanner');if(!banner){banner=document.createElement('section');banner.id='controlPlaneHealthBanner';banner.className='control-plane-health-banner';host.prepend(banner);}try{const response=await fetch('/health',{cache:'no-store'});if(!response.ok)throw new Error('HTTP '+response.status);const h=await response.json(),state=String(h.health||h.status||'Unknown'),summary=String(h.summary||'Health check returned no summary.').trim(),lower=state.toLowerCase();if(lower==='healthy'){banner.style.display='none';banner.classList.remove('critical','health-unavailable');return;}banner.classList.toggle('critical',lower==='unhealthy');banner.classList.remove('health-unavailable');banner.innerHTML='<strong>'+state.toUpperCase()+'</strong><div>'+esc(summary)+'</div>';banner.style.display='block';}catch(error){banner.classList.remove('critical');banner.classList.add('health-unavailable');banner.innerHTML='<strong>HEALTH CHECK UNAVAILABLE</strong><div>'+esc(error?.message||'Unable to read /health.')+'</div>';banner.style.display='block';}}
refreshControlPlaneHealthBanner();setInterval(refreshControlPlaneHealthBanner,30000);

function params(){const p=new URLSearchParams(),map={applicationId:'application',instanceId:'instance',flow:'flow',outcome:'outcome',correlationId:'correlation',stage:'stage',text:'text'};for(const[q,id]of Object.entries(map)){const v=$(id).value.trim();if(v)p.set(q,v)}const m=parseInt($('sinceMinutes').value||'0',10);if(m>0)p.set('since',new Date(Date.now()-m*60000).toISOString());p.set('take',mode==='flow'?'250':'1000');return p}
function setOptions(id,values,label){const el=$(id),current=el.value;el.innerHTML='<option value="">All '+label+'</option>'+values.map(v=>'<option>'+esc(v)+'</option>').join('');el.value=current}
async function loadFilters(){try{const r=await fetch('/api/operations/activity/filters',{cache:'no-store'});if(!r.ok)return;const x=await r.json();setOptions('application',x.applications||[],'applications');setOptions('instance',x.instances||[],'instances');setOptions('flow',x.flows||[],'flows');setOptions('outcome',x.outcomes||[],'outcomes');for(const[q,id]of Object.entries({applicationId:'application',instanceId:'instance',flow:'flow',outcome:'outcome',correlationId:'correlation',stage:'stage',text:'text',level:'logLevel',category:'category'})){const v=initialQuery.get(q);if(v&&$(id))$(id).value=v}const since=initialQuery.get('sinceMinutes');if(since&&$('sinceMinutes'))$('sinceMinutes').value=since}catch{}}
function fmt(v){const d=new Date(v);return Number.isNaN(d.valueOf())?'—':recentStamp(d)+'.'+String(d.getMilliseconds()).padStart(3,'0')}
function detail(e){return[e.stage,e.code,e.detail].filter(Boolean).join(' · ')}
function levelClass(level){return String(level||'Information')}
function registrationId(e){return String(e.relatedBusinessId||e.correlationId||'')}
function newestFirst(events,type){const field=type==='log'?'timestampUtc':'occurredAtUtc';return [...(events||[])].sort((a,b)=>new Date(b[field]||0)-new Date(a[field]||0))}
function updateRecent(events,type){const rows=newestFirst(events,type).slice(0,5);$('liveLog').innerHTML=rows.length?rows.map(e=>{const when=e.occurredAtUtc||e.timestampUtc,app=e.applicationId||'Operations',level=type==='log'?(e.level||'Information'):(e.outcome==='Failed'?'ERR':e.outcome==='Warning'?'WRN':'INF'),cls=String(level).toLowerCase().startsWith('err')||level==='Critical'?'err':String(level).toLowerCase().startsWith('warn')?'wrn':'inf',message=e.detail||e.message||e.stage||e.flow||'Telemetry event';return '<div class="row"><span class="t">'+fmt(when)+'</span> <span class="'+cls+'">'+esc(level)+'</span> <span class="src">['+esc(app)+']</span> '+esc(message)+'</div>'}).join(''):'<div class="row"><span class="t">'+recentStamp()+'</span> <span class="inf">INF</span> <span class="src">[Operations]</span> No matching telemetry</div>'}

function filterLabel(){
  const parts=[];
  const add=(id,label)=>{const e=$(id),v=e?.value?.trim();if(v)parts.push(label+'='+v)};
  add('application','App');add('instance','Instance');if(mode!=='appLogs'){add('flow','Flow');add('outcome','Outcome');add('stage','Stage')}add('correlation','Correlation');add('text','Text');
  if(mode==='appLogs'){add('logLevel','Level');add('category','Category')}
  const m=parseInt($('sinceMinutes').value||'0',10);if(m>0)parts.push('Last '+m+'m');
  return parts.length?parts.join(' · '):'None';
}
function updateFilterSummary(count){
  const kind=mode==='appLogs'?'logs':mode==='flow'?'flows':'events';
  $('filterSummary').innerHTML='<b>FILTER</b><span>'+esc(filterLabel())+' · '+count+' '+kind+'</span>';
}
function setFiltersOpen(open){
  $('filtersPanel').classList.toggle('collapsed',!open);$('filtersPanel').setAttribute('aria-hidden',String(!open));
  $('filterToggle').setAttribute('aria-expanded',String(open));$('filterToggle').textContent=open?'Hide Filters':'Show Filters';
}
$('filterToggle').onclick=()=>setFiltersOpen($('filtersPanel').classList.contains('collapsed'));
function eventKey(e,type){return type==='log'?String(e.eventId||[e.timestampUtc,e.applicationId,e.instanceId,e.category,e.message].join('|')):String(e.eventId||[e.occurredAtUtc,e.applicationId,e.instanceId,e.correlationId,e.stage,e.code].join('|'))}
function currentEvents(){return mode==='appLogs'?currentAppLogEvents:currentActivityEvents}
function selectedIndex(events,type){return events.findIndex(e=>eventKey(e,type)===selectedKey)}
function selectEvent(e,type,viewId,focusRow=false,refreshOverlay=false){
  if(!e)return;
  const nextKey=eventKey(e,type),changed=nextKey!==selectedKey;
  selectedKey=nextKey;selectedEvent=e;
  const view=$(viewId);
  view.querySelectorAll('.telemetry-row').forEach(row=>{const selected=row.dataset.key===selectedKey;row.classList.toggle('active',selected);row.setAttribute('aria-selected',String(selected));row.tabIndex=selected?0:-1;});
  const active=view.querySelector('.telemetry-row[aria-selected="true"]');if(active){active.scrollIntoView({block:'nearest'});if(focusRow)active.focus({preventScroll:true})}
  // Row selection is independent from Flow Analyzer. Analyze pins an explicit event until closed or another Analyze is clicked.
}
function restoreSelection(events,type,viewId){
  if(!events.length){selectedKey=null;selectedEvent=null;return}
  const existing=events.find(e=>eventKey(e,type)===selectedKey)||events[0];
  selectEvent(existing,type,viewId,false);
}
function bindRows(viewId,events,type){
  const view=$(viewId);
  view.querySelectorAll('.telemetry-row').forEach(row=>{
    row.onclick=()=>{const item=events.find(x=>eventKey(x,type)===row.dataset.key);selectEvent(item,type,viewId,true,false)};
    row.ondblclick=e=>{e.preventDefault();const item=events.find(x=>eventKey(x,type)===row.dataset.key);if(item)openAnalyzer(item.correlationId||'',row.dataset.key)};
    row.onfocus=()=>{const item=events.find(x=>eventKey(x,type)===row.dataset.key);if(item)selectEvent(item,type,viewId,false,false)};
  });
  view.onkeydown=e=>{
    if(e.key!=='ArrowDown'&&e.key!=='ArrowUp')return;
    e.preventDefault();if(!events.length)return;
    let i=selectedIndex(events,type);if(i<0)i=0;else i=Math.max(0,Math.min(events.length-1,i+(e.key==='ArrowDown'?1:-1)));
    selectEvent(events[i],type,viewId,true,true);
  };
}
function rowMarkup(e,type){
  const key=esc(eventKey(e,type)),when=type==='log'?e.timestampUtc:e.occurredAtUtc,category=type==='log'?e.category:e.flow,outcome=type==='log'?e.level:e.outcome,message=type==='log'?e.message:detail(e),corr=e.correlationId||'';
  return '<div class="telemetry-row" role="row" aria-selected="false" tabindex="-1" data-key="'+key+'" data-has-flow="'+(corr?'true':'false')+'" title="'+(corr?'Double-click to analyze flow':'No correlation ID for Flow Analyzer')+'"><span class="time">'+fmt(when)+'</span><span class="app">'+esc(e.applicationId)+'</span><span class="flow">'+esc(category)+'</span><span class="outcome '+esc(type==='log'?levelClass(outcome):outcome)+'">'+esc(outcome)+'</span><button class="detail" type="button" title="'+esc(message)+'">'+esc(message)+'</button></div>';
}
function emptyMarkup(noun){return '<div class="empty">No '+noun+' match the active filters.<br><button class="action secondary" type="button" onclick="resetFilters()">Reset Filters</button></div>'}
async function refreshFlowOverlay(){
  if(!overlayOpen)return;
  const e=analyzerEvent,c=e?.correlationId;
  $('flowOverlayContext').textContent=e?[(e.applicationId||''),(e.flow||e.category||''),(c||'No correlation ID')].filter(Boolean).join(' · '):'No event selected';
  if(!c){$('flowOverlayBody').innerHTML='<div class="overlay-empty">The selected event has no correlation ID, so no system flow can be resolved.</div>';return}
  $('flowOverlayBody').innerHTML='<div class="overlay-empty">Loading observed flow…</div>';
  try{
    const p=new URLSearchParams();p.set('correlationId',c);p.set('take','250');
    const r=await fetch('/api/operations/activity/flows?'+p,{cache:'no-store'});if(!r.ok)throw new Error('HTTP '+r.status);
    const x=await r.json(),traces=x.traces||[];
    $('flowOverlayBody').innerHTML=traces.length?traces.map(t=>'<article class="trace"><div class="trace-head"><b class="'+esc(t.status)+'">'+esc(t.status)+'</b><div class="grow"><b>'+esc(t.flow)+'</b><div class="trace-meta">'+esc(t.applicationId)+' / '+esc(t.instanceId)+' · '+esc(t.correlationId)+'</div></div><div class="trace-meta">'+t.durationMs+' ms · '+t.eventCount+' events</div></div><div class="stages">'+(t.stages||[]).map(s=>'<div class="stage"><div class="lamp '+esc(s.outcome)+'"></div><b>'+esc(s.stage)+'</b><small>'+esc(s.outcome)+' · '+fmt(s.occurredAtUtc)+'</small></div>').join('')+'</div>'+(t.relatedBusinessId?'<div class="trace-detail">Business ID: '+esc(t.relatedBusinessId)+'</div>':'')+'</article>').join(''):'<div class="overlay-empty">No correlated lifecycle flow has been observed for this event.</div>';
  }catch(err){$('flowOverlayBody').innerHTML='<div class="overlay-empty">Flow unavailable · '+esc(err.message)+'</div>'}
}
function openAnalyzer(c,key){
  const events=currentEvents(),type=mode==='appLogs'?'log':'activity';
  const match=events.find(e=>key?eventKey(e,type)===key:e.correlationId===c);
  if(!match)return;
  selectEvent(match,type,mode==='appLogs'?'appLogView':'logView',false,false);
  analyzerEvent={...match};
  overlayOpen=true;lastFocusedList=document.activeElement;$('flowOverlay').hidden=false;refreshFlowOverlay();
}
function closeAnalyzer(){overlayOpen=false;analyzerEvent=null;$('flowOverlay').hidden=true;if(lastFocusedList?.focus)lastFocusedList.focus({preventScroll:true})}
$('flowOverlayClose').onclick=closeAnalyzer;document.addEventListener('keydown',e=>{if(e.key==='Escape'&&overlayOpen)closeAnalyzer()});
(function enableFlowDrag(){const box=$('flowOverlay'),head=box?.querySelector('.flow-overlay-head'),shell=document.querySelector('.workspace-shell');if(!box||!head||!shell)return;let drag=null;head.addEventListener('pointerdown',e=>{if(e.target.closest('button'))return;const br=box.getBoundingClientRect(),sr=shell.getBoundingClientRect();box.style.left=(br.left-sr.left)+'px';box.style.top=(br.top-sr.top)+'px';box.style.right='auto';drag={id:e.pointerId,dx:e.clientX-br.left,dy:e.clientY-br.top};head.setPointerCapture(e.pointerId)});head.addEventListener('pointermove',e=>{if(!drag||e.pointerId!==drag.id)return;const sr=shell.getBoundingClientRect(),br=box.getBoundingClientRect(),maxX=Math.max(0,sr.width-br.width),maxY=Math.max(0,sr.height-br.height);box.style.left=Math.max(0,Math.min(maxX,e.clientX-sr.left-drag.dx))+'px';box.style.top=Math.max(0,Math.min(maxY,e.clientY-sr.top-drag.dy))+'px'});const stop=e=>{if(drag&&e.pointerId===drag.id)drag=null};head.addEventListener('pointerup',stop);head.addEventListener('pointercancel',stop)})();
function analyze(c){openAnalyzer(c)}window.analyze=analyze;
async function loadLog(){const r=await fetch('/api/operations/activity?'+params(),{cache:'no-store'});if(!r.ok)throw new Error('HTTP '+r.status);const x=await r.json();let events=x.events||[];if(hiddenBefore)events=events.filter(e=>new Date(e.occurredAtUtc)>hiddenBefore);events=newestFirst(events,'activity');currentActivityEvents=events;$('logView').innerHTML=events.length?events.map(e=>rowMarkup(e,'activity')).join(''):emptyMarkup('lifecycle events');bindRows('logView',events,'activity');restoreSelection(events,'activity','logView');updateRecent(events,'activity');$('state').textContent=events.length+' EVENTS · '+recentStamp();updateFilterSummary(events.length);if(followTail&&!selectedKey){$('logView').scrollTop=0}}
async function loadAppLogs(){const p=params();p.delete('flow');p.delete('outcome');p.delete('stage');const level=$('logLevel').value.trim();if(level)p.set('level',level);const category=$('category').value.trim();if(category)p.set('category',category);const r=await fetch('/api/operations/logs?'+p,{cache:'no-store'});if(!r.ok)throw new Error('HTTP '+r.status);const x=await r.json();let events=x.events||[];if(hiddenBefore)events=events.filter(e=>new Date(e.timestampUtc)>hiddenBefore);events=newestFirst(events,'log');currentAppLogEvents=events;$('appLogView').innerHTML=events.length?events.map(e=>rowMarkup(e,'log')).join(''):emptyMarkup('application logs');bindRows('appLogView',events,'log');restoreSelection(events,'log','appLogView');updateRecent(events,'log');$('state').textContent=events.length+' LOG EVENTS · '+recentStamp();updateFilterSummary(events.length);if(followTail&&!selectedKey){$('appLogView').scrollTop=0}}
async function loadFlows(){const r=await fetch('/api/operations/activity/flows?'+params(),{cache:'no-store'});if(!r.ok)throw new Error('HTTP '+r.status);const x=await r.json(),traces=x.traces||[];$('flowView').innerHTML=traces.length?traces.map(t=>'<article class="trace"><div class="trace-head"><b class="'+esc(t.status)+'">'+esc(t.status)+'</b><div class="grow"><b>'+esc(t.flow)+'</b><div class="trace-meta">'+esc(t.applicationId)+' / '+esc(t.instanceId)+' · '+esc(t.correlationId)+'</div></div><div class="trace-meta">'+t.durationMs+' ms · '+t.eventCount+' events</div></div><div class="stages">'+(t.stages||[]).map(s=>'<div class="stage"><div class="lamp '+esc(s.outcome)+'"></div><b>'+esc(s.stage)+'</b><small>'+esc(s.outcome)+' · '+fmt(s.occurredAtUtc)+'</small></div>').join('')+'</div>'+(t.relatedBusinessId?'<div class="trace-detail">Business ID: '+esc(t.relatedBusinessId)+'</div>':'')+'</article>').join(''):emptyMarkup('correlated flows');$('state').textContent=traces.length+' FLOWS · '+recentStamp();updateFilterSummary(traces.length)}
async function refresh(){if(paused)return;try{if(mode==='log')await loadLog();else if(mode==='appLogs')await loadAppLogs();else await loadFlows();$('lastScan').textContent='Last Scan: '+recentStamp()}catch(e){$('state').textContent='TELEMETRY UNAVAILABLE · '+e.message;$('lastScan').textContent='Last Scan: UNAVAILABLE'}}
function tabs(){$('navActivity').classList.toggle('active',mode==='log');$('navLogs').classList.toggle('active',mode==='appLogs');$('navFlow').classList.toggle('active',mode==='flow');$('logView').hidden=mode!=='log';$('appLogView').hidden=mode!=='appLogs';$('flowView').hidden=mode!=='flow';for(const id of ['flow','outcome','stage']){$(id).disabled=mode==='appLogs';$(id).hidden=mode==='appLogs'}for(const id of ['logLevel','category']){$(id).disabled=mode!=='appLogs';$(id).hidden=mode!=='appLogs'}updateFilterSummary(mode==='appLogs'?currentAppLogEvents.length:mode==='log'?currentActivityEvents.length:0)}
document.querySelectorAll('[data-mode]').forEach(a=>a.addEventListener('click',e=>{e.preventDefault();mode=a.dataset.mode;tabs();refresh()}));
async function applyFilters(){await refresh();setFiltersOpen(false)}$('apply').onclick=applyFilters;function resetFilters(){for(const id of ['application','instance','flow','outcome','correlation','stage','text','logLevel','category'])$(id).value='';$('sinceMinutes').value='60';hiddenBefore=null;selectedKey=null;selectedEvent=null;setFiltersOpen(false);refresh()}window.resetFilters=resetFilters;$('reset').onclick=resetFilters;
$('pause').onclick=()=>{paused=!paused;$('pause').textContent=paused?'Resume':'Pause';$('state').textContent=paused?'PAUSED · INGESTION CONTINUES':'RESUMED';if(!paused)refresh()};
$('follow').onclick=()=>{followTail=!followTail;$('follow').textContent='Tail: '+(followTail?'On':'Off')};
$('clear').onclick=()=>{hiddenBefore=new Date();$('logView').innerHTML='<div class="empty">View cleared. New logs will appear here.</div>';$('appLogView').innerHTML='<div class="empty">View cleared. New logs will appear here.</div>';$('flowView').innerHTML='<div class="empty">View cleared. Adjust filters or wait for new flows.</div>'};
$('toggleIds').onclick=()=>{showIds=!showIds;localStorage.setItem(idsKey,showIds?'on':'off');applyIdsVisibility()};
tabs();applyIdsVisibility();loadFilters().then(refresh);setInterval(()=>{loadFilters();refresh()},2000);

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
""".Replace("__CONTROL_PLANE_HEADER_CSS__", ControlPlaneHeader.Css, StringComparison.Ordinal);
    }
}
