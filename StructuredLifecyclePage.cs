using Common.Diagnostics;

public static class StructuredLifecyclePage
{
    public static void MapLifecycleVisualizer(this WebApplication app)
    {
        app.MapGet("/workflow-visualizer", (HttpContext context) =>
        {
            if (!(context.User?.Identity?.IsAuthenticated ?? false)) return Results.Unauthorized();
            return Results.Content(Html, "text/html; charset=utf-8");
        });
    }

    private const string Html = """
<!doctype html>
<html>
<head>
<meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Aegis Workflow Visualizer</title>
<style>
body{font:14px Segoe UI,Arial,sans-serif;margin:0;background:#071522;color:#eef6ff}
main{max-width:1400px;margin:26px auto;padding:0 20px}a{color:#66bfff}
.panel{background:#0d2030;border:1px solid #28597b;border-radius:8px;padding:16px;margin:14px 0}
.flow{display:grid;grid-template-columns:180px 1fr 120px 170px;gap:10px;padding:9px 0;border-bottom:1px solid #29475b;align-items:center}
.stages{display:flex;gap:7px;flex-wrap:wrap}.stage{padding:5px 9px;border-radius:999px;background:#34495a}.succeeded{background:#145b40}.failed{background:#7d2f2f}.warning{background:#72531b}.started{background:#245a7a}
small{color:#9fb7c8}.mono{font-family:Consolas,monospace;font-size:12px}.empty{color:#9fb7c8;padding:20px}
</style></head><body><main>
<p><a href="/">← Operations</a></p><h1>Workflow Visualizer</h1>
<p>Structured lifecycle events grouped by application, flow and correlation ID. No secrets or credentials are accepted by the lifecycle contract.</p>
<section class="panel"><div id="flows" class="empty">Loading lifecycle events…</div></section>
<script>
const e=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
function render(items){
 const groups=new Map();
 for(const x of items){
  const key=[x.applicationId,x.flow,x.correlationId].join('|');
  if(!groups.has(key))groups.set(key,{applicationId:x.applicationId,flow:x.flow,correlationId:x.correlationId,events:[]});
  groups.get(key).events.push(x);
 }
 const rows=[...groups.values()].sort((a,b)=>new Date(b.events[0].occurredAtUtc)-new Date(a.events[0].occurredAtUtc));
 document.getElementById('flows').innerHTML=rows.length?rows.map(g=>{
   g.events.sort((a,b)=>new Date(a.occurredAtUtc)-new Date(b.occurredAtUtc));
   const latest=g.events[g.events.length-1];
   const stages=g.events.map(x=>'<span class="stage '+e(String(x.outcome).toLowerCase())+'">'+e(x.stage)+' · '+e(x.outcome)+'</span>').join('');
   return '<div class="flow"><div><b>'+e(g.applicationId)+'</b><br><small>'+e(g.flow)+'</small></div><div class="stages">'+stages+'</div><div>'+e(latest.outcome)+'</div><div class="mono">'+e(g.correlationId)+'</div></div>';
 }).join(''):'<div class="empty">No lifecycle events have been received.</div>';
}
async function load(){try{const r=await fetch('/api/operations/lifecycle/events?take=1000',{cache:'no-store'});if(!r.ok)throw new Error('HTTP '+r.status);render(await r.json())}catch(err){document.getElementById('flows').textContent='Lifecycle telemetry unavailable: '+err.message}}
load();setInterval(load,3000);
</script></main></body></html>
""";
}
