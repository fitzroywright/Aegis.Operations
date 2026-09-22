using System.Text.Encodings.Web;

internal static class RegistrationWorkspacePage
{
    internal static string Render(string section, string? userName = null)
    {
        section = Normalize(section);
        string title = section switch
        {
            "applications" => "Applications",
            "lifecycle" => "Lifecycle",
            "actions" => "Actions",
            "logs" => "Logs",
            _ => "Overview"
        };

        string html = """
<!doctype html>
<html>
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>__TITLE__ · Operations → Registration</title>
<style>
:root{--blue:#5588ee;--ice:#66ccff;--snow:#99ccff;--gold:#ffcc66;--red:#ff3500;--salmon:#7799dd;--text:#f3f3fc;--line:#344470;--rail:160px}
*{box-sizing:border-box}html,body{margin:0;width:100%;height:100%;overflow:hidden;background:#000;color:var(--text);font:14px/1.4 "Segoe UI",Arial,sans-serif}a{color:inherit;text-decoration:none}button,input,select{font:inherit}
.lcars-top{position:fixed;left:8px;right:18px;top:8px;height:140px;background:#000}.left-vertical{position:absolute;left:0;top:0;width:var(--rail);height:140px;background:var(--blue);border-radius:28px 0 0 28px}.bottom-arm{position:absolute;left:var(--rail);right:0;top:126px;height:14px;background:var(--blue);border-radius:0 9px 9px 0}
.brand{position:absolute;left:calc(var(--rail) + 18px);top:8px;width:285px}.brand .section{font-size:34px;font-weight:900;text-transform:uppercase;color:var(--ice)}.brand .sub{font-size:12px;font-weight:900;color:var(--gold);text-transform:uppercase;margin-top:4px}.brand-user{font-size:10px;color:var(--snow);margin-top:8px}
.top-buttons{position:absolute;right:50px;top:18px;width:250px;height:72px;display:grid;grid-template-columns:1fr 1fr;gap:8px 10px}.top-buttons a{border-radius:8px;font-weight:900;text-transform:uppercase;color:#071018;font-size:12px;display:flex;align-items:center;justify-content:center}.top-buttons .blue{background:var(--blue)}.top-buttons .salmon{background:var(--salmon)}.top-buttons .gold{background:#88eeff}.top-buttons .gray{background:#99ccff}.top-buttons a.module-active{box-shadow:0 0 0 2px var(--gold),0 0 14px 3px rgba(255,204,102,.72)!important;filter:brightness(1.06)}.top-buttons a.nav-flash{animation:moduleFlash .18s ease-out}@keyframes moduleFlash{0%{filter:brightness(1);transform:scale(1)}45%{filter:brightness(1.38);transform:scale(1.035)}100%{filter:brightness(1.06);transform:scale(1)}}.lcars-logout{position:absolute;right:0;top:126px;height:14px;min-width:92px;padding:0 12px;background:var(--red);color:#fff;border:0;border-radius:7px 9px 9px 7px;z-index:8;display:flex;align-items:center;justify-content:center;font:900 9px/14px Arial,sans-serif;letter-spacing:.08em;text-transform:uppercase;text-decoration:none;box-shadow:none}
.workspace-shell{position:fixed;left:8px;right:14px;top:166px;bottom:18px;overflow:hidden}.workspace-leg{position:absolute;left:0;top:0;bottom:0;width:var(--rail);background:var(--blue);border-radius:28px 0 0 28px}.workspace-arm{position:absolute;left:var(--rail);right:0;top:0;height:48px;background:var(--gold);display:flex;align-items:center;border-radius:0 9px 9px 0}.workspace-title{height:100%;display:flex;align-items:center;margin-left:18px;padding:0 12px;border-left:6px solid #000;border-right:6px solid #000;color:#071018;font-size:25px;font-weight:900;text-transform:uppercase}.crumb{margin-left:12px;color:#3b2f16;font:800 10px/1 Arial,sans-serif;text-transform:uppercase}.last-scan{margin-left:auto;margin-right:14px;color:#3b2f16;font:700 9px/1 Arial,sans-serif;text-transform:uppercase}
.workspace-nav{position:absolute;left:0;top:66px;width:var(--rail);z-index:2}.workspace-nav a{display:flex;align-items:center;height:48px;padding:0 10px;color:#071018;font-weight:900;text-transform:uppercase;font-size:11px;white-space:nowrap;border-bottom:3px solid #000}.workspace-nav a.active{background:var(--gold);font-size:12px}.detail-scroll{position:absolute;left:var(--rail);right:0;top:66px;bottom:0;overflow:auto;padding:12px 18px 22px 20px;background:#000}
.panel{border:1px solid var(--line);border-top:2px solid #455580;border-radius:9px;background:#071020;padding:12px;margin-bottom:10px}.panel h2{margin:0 0 8px;color:var(--ice);font-size:18px}.muted{color:var(--snow)}.metrics{display:grid;grid-template-columns:repeat(4,minmax(120px,1fr));gap:8px;margin-bottom:10px}.metric{border:1px solid var(--line);border-radius:8px;background:#071020;padding:9px}.metric b{display:block;color:var(--ice);font-size:22px}.metric small{text-transform:uppercase;color:var(--snow)}
table{width:100%;border-collapse:collapse}th{text-align:left;color:var(--ice);background:#071b29;padding:9px;font-size:11px;position:sticky;top:0}td{padding:9px;border-bottom:1px solid var(--line);vertical-align:top}.table-scroll{overflow:auto;max-height:58vh}.badge{font-weight:900}.good{color:#55dd88}.warn{color:var(--gold)}.bad{color:var(--red)}.actions{display:flex;gap:6px;flex-wrap:wrap}.actions button{border:1px solid #557099;border-radius:6px;background:#102c40;color:var(--text);padding:5px 8px;cursor:pointer}.actions button.danger{border-color:#8b352d;background:#40120f}.log-row{display:grid;grid-template-columns:190px 150px 120px 1fr;gap:8px;padding:7px 4px;border-bottom:1px solid var(--line);font:12px/1.35 ui-monospace,Consolas,monospace}.empty{color:var(--snow);padding:12px}
@media(max-width:900px){.metrics{grid-template-columns:repeat(2,1fr)}.log-row{grid-template-columns:1fr}.top-buttons{width:220px}}
__CONTROL_PLANE_HEADER_CSS__
.aegis-busy-overlay{position:absolute;inset:0;z-index:80;display:grid;place-items:center;background:rgba(0,5,12,.82);backdrop-filter:blur(2px);opacity:0;pointer-events:none;transition:opacity .16s ease}.aegis-busy-overlay.visible{opacity:1;pointer-events:auto}.aegis-busy-scan{position:relative;width:min(680px,72vw);height:220px;display:grid;place-items:center;overflow:hidden}.aegis-busy-grid{position:absolute;inset:0;background-image:linear-gradient(rgba(85,136,238,.10) 1px,transparent 1px),linear-gradient(90deg,rgba(85,136,238,.10) 1px,transparent 1px);background-size:24px 24px;mask-image:linear-gradient(90deg,transparent,#000 15%,#000 85%,transparent)}.aegis-busy-wave{position:absolute;left:4%;right:4%;top:50%;height:2px;background:#66ccff;box-shadow:0 0 8px #66ccff,0 0 22px #5588ee}.aegis-busy-wave:before,.aegis-busy-wave:after{content:"";position:absolute;left:50%;top:50%;width:38px;height:38px;border:1px solid rgba(102,204,255,.65);border-radius:50%;transform:translate(-50%,-50%);animation:subspacePulse 1.35s ease-out infinite}.aegis-busy-wave:after{animation-delay:.45s}.aegis-busy-sweep{position:absolute;top:14%;bottom:14%;width:9%;background:linear-gradient(90deg,transparent,rgba(102,204,255,.10),rgba(102,204,255,.85),rgba(255,204,102,.65),transparent);animation:subspaceSweep 1.55s linear infinite}.aegis-busy-core{position:relative;z-index:2;width:64px;height:72px;border:2px solid #66ccff;border-radius:7px;background:#071b29;box-shadow:0 0 18px rgba(102,204,255,.7);display:grid;align-content:center;gap:7px;padding:10px}.aegis-busy-core i{display:block;height:5px;border-radius:2px;background:#99ccff;box-shadow:0 0 5px rgba(153,204,255,.7)}.aegis-busy-copy{position:absolute;z-index:3;left:0;right:0;bottom:4px;text-align:center;text-transform:uppercase;font:900 13px/1.25 Arial,sans-serif;letter-spacing:.13em;color:#66ccff}.aegis-busy-copy b{display:block;color:#f3f3fc;font-size:18px;margin-bottom:6px}.aegis-busy-copy span{color:#ffcc66}@keyframes subspaceSweep{0%{left:2%;opacity:.15}15%{opacity:1}85%{opacity:1}100%{left:89%;opacity:.15}}@keyframes subspacePulse{0%{width:20px;height:20px;opacity:.9}100%{width:360px;height:150px;opacity:0}}@media(prefers-reduced-motion:reduce){.aegis-busy-sweep,.aegis-busy-wave:before,.aegis-busy-wave:after{animation:none}}</style>
</head>
<body>
<header class="lcars-top">
<div class="left-vertical"></div><div class="bottom-arm"></div>
<div class="brand"><div class="section">Operations</div><div class="sub">Registration Workspace</div><div class="brand-user" id="brandUser">__USER__</div></div>
<div class="top-buttons"><a class="blue" href="/">Operations</a><a class="gray module-active" href="/registration">Registration</a><a class="gold" href="/handoff/configuration">Configuration</a><a class="salmon" href="/handoff/diagnostics">Diagnostics</a></div><a class="lcars-logout" href="/auth/logout">Logout</a>
</header>
<section class="workspace-shell">
<div class="workspace-arm"><div class="workspace-title">__TITLE__</div><div class="crumb">Operations → Registration</div><div class="last-scan" id="lastScan">Last Scan: SCANNING</div></div>
<div class="workspace-leg"></div>
<nav class="workspace-nav">
<a class="__OVERVIEW__" href="/registration">Overview</a>
<a class="__APPLICATIONS__" href="/registration/applications">Applications</a>
<a class="__LIFECYCLE__" href="/registration/lifecycle">Lifecycle</a>
<a class="__ACTIONS__" href="/registration/actions">Actions</a>
<a class="__LOGS__" href="/registration/logs">Logs</a>
</nav>
<main class="detail-scroll">__BODY__<div class="aegis-busy-overlay" id="aegisBusyOverlay" aria-hidden="true"><div class="aegis-busy-scan" role="status" aria-live="polite"><div class="aegis-busy-grid"></div><div class="aegis-busy-wave"></div><div class="aegis-busy-sweep"></div><div class="aegis-busy-core"><i></i><i></i><i></i><i></i></div><div class="aegis-busy-copy"><b>Processing</b><span id="aegisBusyText">Analyzing system state</span></div></div></div></main>
</section>
<script>
document.querySelectorAll('.top-buttons a').forEach(a=>a.addEventListener('pointerdown',()=>{a.classList.add('nav-flash');setTimeout(()=>a.classList.remove('nav-flash'),180)}));
const esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#039;'}[c]));
const tone=s=>{const x=String(s||'').toLowerCase();return /healthy|registered|running|ready|success/.test(x)?'good':/failed|unhealthy|revoked|rejected|stale|offline|error/.test(x)?'bad':'warn'};
const fmt=v=>{const d=new Date(v);if(Number.isNaN(d.getTime()))return '—';let h=d.getHours(),ap=h>=12?'PM':'AM';h=h%12||12;return d.toLocaleString('en-US',{month:'short'})+' '+String(d.getDate()).padStart(2,'0')+' '+d.getFullYear()+' '+String(h).padStart(2,'0')+':'+String(d.getMinutes()).padStart(2,'0')+':'+String(d.getSeconds()).padStart(2,'0')+'.'+String(d.getMilliseconds()).padStart(3,'0')+' '+ap};
async function loadUser(){try{const r=await fetch('/auth/me',{cache:'no-store'});if(!r.ok)return;const x=await r.json();document.getElementById('brandUser').textContent='User: '+(x.displayName||x.userName||'Signed in')+' · IP: '+(x.clientIp||'unknown')}catch{}}loadUser();
function scan(){document.getElementById('lastScan').textContent='Last Scan: '+new Date().toLocaleTimeString()} 
__SCRIPT__
(()=>{const overlay=document.getElementById('aegisBusyOverlay'),label=document.getElementById('aegisBusyText');if(!overlay)return;let pending=0,timer=0;const show=(text='Analyzing system state')=>{pending++;if(label)label.textContent=text;clearTimeout(timer);timer=setTimeout(()=>{if(pending>0){overlay.classList.add('visible');overlay.setAttribute('aria-hidden','false')}},300)};const hide=()=>{pending=Math.max(0,pending-1);if(!pending){clearTimeout(timer);overlay.classList.remove('visible');overlay.setAttribute('aria-hidden','true')}};window.AegisBusy={show,hide,run:async(p,text)=>{show(text);try{return await p}finally{hide()}}};document.querySelectorAll('.top-buttons a,.workspace-nav a,.lcars-back').forEach(a=>a.addEventListener('click',()=>show('Routing control plane')));})();</script>
</body>
</html>
""";

        return html
            .Replace("__TITLE__", HtmlEncoder.Default.Encode(title), StringComparison.Ordinal)
            .Replace("__USER__", HtmlEncoder.Default.Encode(userName ?? "SIGNED IN"), StringComparison.Ordinal)
            .Replace("__OVERVIEW__", section == "overview" ? "active" : "", StringComparison.Ordinal)
            .Replace("__APPLICATIONS__", section == "applications" ? "active" : "", StringComparison.Ordinal)
            .Replace("__LIFECYCLE__", section == "lifecycle" ? "active" : "", StringComparison.Ordinal)
            .Replace("__ACTIONS__", section == "actions" ? "active" : "", StringComparison.Ordinal)
            .Replace("__LOGS__", section == "logs" ? "active" : "", StringComparison.Ordinal)
            .Replace("__BODY__", Body(section), StringComparison.Ordinal)
            .Replace("__SCRIPT__", Script(section), StringComparison.Ordinal)
            .Replace("__CONTROL_PLANE_HEADER_CSS__", ControlPlaneHeader.Css, StringComparison.Ordinal);
    }

    private static string Normalize(string? section) => section?.Trim().ToLowerInvariant() switch
    {
        "applications" => "applications",
        "lifecycle" => "lifecycle",
        "actions" => "actions",
        "logs" => "logs",
        _ => "overview"
    };

    private static string Body(string section) => section switch
    {
        "applications" => """<section class="panel"><h2>Registered Applications</h2><p class="muted">Registration-scoped inventory. Configuration remains the authority; Operations overlays observed runtime state.</p><div class="table-scroll"><table><thead><tr><th>Application</th><th>Instance</th><th>Environment</th><th>Registration</th><th>Runtime</th><th>Registration ID</th></tr></thead><tbody id="rows"><tr><td colspan="6">Loading…</td></tr></tbody></table></div></section>""",
        "lifecycle" => """<section class="metrics"><div class="metric"><b id="journeys">—</b><small>Journeys</small></div><div class="metric"><b id="events">—</b><small>Events</small></div><div class="metric"><b id="registered">—</b><small>Registered</small></div><div class="metric"><b id="attention">—</b><small>Attention</small></div></section><section class="panel"><h2>Registration Lifecycle</h2><p class="muted">Canonical lifecycle view for Operations → Registration.</p><div class="table-scroll"><table><thead><tr><th>Application</th><th>Instance</th><th>State</th><th>Stage</th><th>Observed</th><th>Registration ID</th></tr></thead><tbody id="rows"><tr><td colspan="6">Loading…</td></tr></tbody></table></div></section>""",
        "actions" => """<section class="panel"><h2>Registration Actions</h2><p class="muted">Only lifecycle actions belong here. Actions remain backed by the Registration authority through the Operations proxy.</p><div class="table-scroll"><table><thead><tr><th>Application</th><th>Instance</th><th>State</th><th>Registration ID</th><th>Available Actions</th></tr></thead><tbody id="rows"><tr><td colspan="5">Loading…</td></tr></tbody></table></div></section>""",
        "logs" => """<section class="panel"><h2>Registration Logs</h2><p class="muted">Registration-scoped lifecycle telemetry only. General Operations logs remain at /logs.</p><div id="rows"><div class="empty">Loading…</div></div></section>""",
        _ => """<section class="metrics"><div class="metric"><b id="total">—</b><small>Registrations</small></div><div class="metric"><b id="active">—</b><small>Registered</small></div><div class="metric"><b id="pending">—</b><small>Pending</small></div><div class="metric"><b id="issues">—</b><small>Attention</small></div></section><section class="panel"><h2>Registration Overview</h2><p class="muted">This workspace contains Registration only: inventory, lifecycle, operator actions, and registration logs. It does not redirect its left navigation into general Operations, Diagnostics, or Configuration pages.</p><div id="summary">Loading authoritative registration state…</div></section>"""
    };

    private static string Script(string section) => section switch
    {
        "applications" => """
async function load(){try{const r=await fetch('/api/operations/registered-applications',{cache:'no-store'});if(!r.ok)throw new Error('HTTP '+r.status);const x=await r.json(),apps=x.applications||[];rows.innerHTML=apps.length?apps.map(a=>'<tr><td><b>'+esc(a.displayName||a.applicationId)+'</b><br><small>'+esc(a.applicationId)+'</small></td><td>'+esc(a.instanceId)+'</td><td>'+esc(a.environment||'—')+'</td><td class="'+tone(a.registrationFlowLabel||a.registrationStatus)+'">'+esc(a.registrationFlowLabel||a.registrationStatus||'Unknown')+'</td><td class="'+tone(a.state)+'">'+esc(a.state||'Unknown')+'</td><td>'+esc(a.registrationId||'—')+'</td></tr>').join(''):'<tr><td colspan="6">No registered applications.</td></tr>';scan()}catch(e){rows.innerHTML='<tr><td colspan="6">Registration inventory unavailable · '+esc(e.message)+'</td></tr>'}}load();setInterval(load,10000);
""",
        "lifecycle" => """
async function load(){try{const [fr,rr]=await Promise.all([fetch('/api/lifecycle-registrations/flow',{cache:'no-store'}),fetch('/api/lifecycle-registrations',{cache:'no-store'})]);if(!fr.ok||!rr.ok)throw new Error('Lifecycle source unavailable');const f=await fr.json(),regs=await rr.json(),js=f.journeys||[],list=Array.isArray(regs)?regs:(regs.registrations||[]);journeys.textContent=js.length;events.textContent=(f.events||[]).length;registered.textContent=list.filter(x=>String(x.state).toLowerCase()==='registered').length;attention.textContent=list.filter(x=>!['registered','purged'].includes(String(x.state).toLowerCase())).length;rows.innerHTML=js.length?js.map(x=>'<tr><td><b>'+esc(x.applicationId)+'</b></td><td>'+esc(x.instanceId)+'</td><td class="'+tone(x.state)+'">'+esc(x.state||'Unknown')+'</td><td>'+esc(x.currentStage||'—')+'</td><td>'+fmt(x.lastEventAtUtc||x.observedAtUtc)+'</td><td>'+esc(x.registrationId||'—')+'</td></tr>').join(''):'<tr><td colspan="6">No active registration journeys.</td></tr>';scan()}catch(e){rows.innerHTML='<tr><td colspan="6">Lifecycle unavailable · '+esc(e.message)+'</td></tr>'}}load();setInterval(load,5000);
""",
        "actions" => """
function actionButtons(x){const id=encodeURIComponent(x.registrationId||''),s=String(x.state||'').toLowerCase(),b=[];if(s==='pending'){b.push(['approve','Approve','']);b.push(['reject','Reject','danger'])}if(s==='registered')b.push(['revoke','Revoke','danger']);if(s==='conflict'){b.push(['conflict/retain-existing','Retain Existing','']);b.push(['conflict/replace-with-incoming','Replace Incoming','danger'])}if(['revoked','rejected'].includes(s))b.push(['purge','Remove','danger']);return b.map(z=>'<button class="'+z[2]+'" onclick="act(\''+id+'\',\''+z[0]+'\')">'+z[1]+'</button>').join('')||'<span class="muted">No operator action</span>'}
async function act(id,action){if(!confirm('Apply '+action+' to this registration?'))return;const url='/api/lifecycle-registrations/'+id+(action==='purge'?'':'/'+action),opt={method:action==='purge'?'DELETE':'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({reason:'Operator action from Operations → Registration'})};const r=await fetch(url,opt);if(!r.ok){alert('Registration action failed: HTTP '+r.status);return}await load()}
async function load(){try{const r=await fetch('/api/lifecycle-registrations',{cache:'no-store'});if(!r.ok)throw new Error('HTTP '+r.status);const x=await r.json(),list=Array.isArray(x)?x:(x.registrations||[]);rows.innerHTML=list.length?list.map(x=>'<tr><td><b>'+esc(x.applicationId)+'</b></td><td>'+esc(x.instanceId)+'</td><td class="'+tone(x.state)+'">'+esc(x.state||'Unknown')+'</td><td>'+esc(x.registrationId||'—')+'</td><td><div class="actions">'+actionButtons(x)+'</div></td></tr>').join(''):'<tr><td colspan="5">No registrations.</td></tr>';scan()}catch(e){rows.innerHTML='<tr><td colspan="5">Actions unavailable · '+esc(e.message)+'</td></tr>'}}load();setInterval(load,10000);
""",
        "logs" => """
async function load(){try{const p=new URLSearchParams({take:'500',text:'Registration'}),r=await fetch('/api/operations/activity?'+p,{cache:'no-store'});if(!r.ok)throw new Error('HTTP '+r.status);const x=await r.json(),ev=(x.events||[]).filter(e=>/registration/i.test([e.flow,e.stage,e.code,e.detail].filter(Boolean).join(' ')));rows.innerHTML=ev.length?ev.map(e=>'<div class="log-row"><span>'+fmt(e.occurredAtUtc)+'</span><b>'+esc(e.applicationId||'—')+'</b><span class="'+tone(e.outcome)+'">'+esc(e.outcome||'—')+'</span><span>'+esc([e.flow,e.stage,e.code,e.detail].filter(Boolean).join(' · '))+'</span></div>').join(''):'<div class="empty">No registration telemetry matches.</div>';scan()}catch(e){rows.innerHTML='<div class="empty">Registration logs unavailable · '+esc(e.message)+'</div>'}}load();setInterval(load,3000);
""",
        _ => """
async function load(){try{const [r,q]=await Promise.all([fetch('/api/lifecycle-registrations',{cache:'no-store'}),fetch('/api/lifecycle-registrations/reconciliation',{cache:'no-store'})]);if(!r.ok)throw new Error('HTTP '+r.status);const x=await r.json(),list=Array.isArray(x)?x:(x.registrations||[]);total.textContent=list.length;active.textContent=list.filter(a=>String(a.state).toLowerCase()==='registered').length;pending.textContent=list.filter(a=>String(a.state).toLowerCase()==='pending').length;issues.textContent=list.filter(a=>!['registered','purged'].includes(String(a.state).toLowerCase())).length;let rec='Reconciliation unavailable';if(q.ok){const z=await q.json();rec='Reconciliation: '+esc(z.status||z.health||'Available')}summary.innerHTML='<b>Authority:</b> Configuration Registration · <b>'+rec+'</b>';scan()}catch(e){summary.textContent='Registration overview unavailable · '+e.message}}load();setInterval(load,10000);
"""
    };
}
