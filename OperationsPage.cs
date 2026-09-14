using System.Text.Encodings.Web;

internal static class OperationsPage
{
    internal static string Render(string configurationUrl, string diagnosticsUrl, string site, string environment)
    {
        string page = Template.Value;
        return page
            .Replace("__GLOSSY__", GlossyCss.Value, StringComparison.Ordinal)
            .Replace("__SITE__", Encode(site), StringComparison.Ordinal)
            .Replace("__ENVIRONMENT__", Encode(environment), StringComparison.Ordinal)
            .Replace("__CONFIGURATION_URL__", Encode(configurationUrl), StringComparison.Ordinal)
            .Replace("__DIAGNOSTICS_URL__", Encode(diagnosticsUrl), StringComparison.Ordinal);
    }

    private static string Encode(string value) => HtmlEncoder.Default.Encode(value);

    private static class Template
    {
        internal const string Value = """
<!doctype html>
<html>
<head>
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>System Operations</title>
<style>
:root{--space:#000;--ink:#f8f7f2;--lav:#cc99cc;--vio:#9999ff;--blue:#99ccff;--peach:#ff9966;--orange:#ff9900;--sand:#ffcc99;--red:#cc6666;--green:#99cc99}*{box-sizing:border-box}body{margin:0;background:var(--space);color:var(--ink);font-family:Arial,Helvetica,sans-serif;font-weight:700;letter-spacing:.04em}a{color:inherit;text-decoration:none}.lcars{min-height:100vh;padding:10px;display:grid;grid-template-columns:190px 1fr;grid-template-rows:112px 1fr 72px;gap:0 12px}.elbow{background:var(--peach);border-radius:58px 0 0 0;position:relative}.elbow:after{content:"";position:absolute;background:#000;right:0;bottom:0;width:54px;height:54px;border-radius:28px 0 0 0}.brand{background:var(--peach);color:#000;display:flex;align-items:end;justify-content:flex-end;padding:0 18px 12px;font-size:clamp(28px,4vw,54px);font-weight:900;border-radius:0 36px 36px 0}.rail{background:linear-gradient(var(--peach) 0 18%,#000 18% 20%,var(--vio) 20% 36%,#000 36% 38%,var(--blue) 38% 54%,#000 54% 56%,var(--orange) 56% 72%,#000 72% 74%,var(--lav) 74%);border-radius:0 0 0 48px;padding-top:20px}.rail .tag{display:block;background:#000;margin:12px 0 0 28px;padding:8px 10px;color:var(--sand);font-size:12px}.main{padding:18px 4px}.subbar{display:grid;grid-template-columns:2fr .7fr .45fr;gap:8px;margin-bottom:18px}.seg{height:28px;border-radius:16px;background:var(--vio);color:#000;padding:5px 14px;font-size:12px}.seg:nth-child(2){background:var(--blue)}.seg:nth-child(3){background:var(--orange)}.readout{font-size:12px;color:var(--sand);margin:10px 0 24px}.grid{display:grid;grid-template-columns:1fr 1fr;gap:16px}.panel{border-top:14px solid var(--lav);border-right:3px solid var(--lav);border-bottom:3px solid var(--lav);padding:20px;min-height:330px;position:relative}.panel:before{content:"";position:absolute;left:0;top:-14px;width:42%;height:14px;background:var(--orange);border-radius:0 12px 12px 0}.panel h2{font-size:clamp(24px,3vw,40px);margin:12px 0}.code{font-size:11px;color:var(--blue)}.status{font-size:28px;margin:28px 0}.available{color:var(--green)}.unavailable{color:var(--red)}.button{display:block;margin-top:36px;background:var(--sand);color:#000;border-radius:22px;padding:12px 18px;text-align:right;font-weight:900}.diag .button{background:var(--vio)}.footerElbow{background:var(--blue);border-radius:0 0 0 46px}.footer{display:flex;gap:8px;align-items:center}.footer span{height:28px;flex:1;background:var(--blue);border-radius:14px}.footer span:nth-child(2){flex:.35;background:var(--orange)}.footer span:nth-child(3){flex:.2;background:var(--lav)}@media(max-width:720px){.lcars{grid-template-columns:72px 1fr;grid-template-rows:88px 1fr 54px;gap:0 7px}.brand{font-size:24px;padding:0 12px 9px}.grid{grid-template-columns:1fr}.rail .tag{display:none}.panel{min-height:250px}.elbow:after{width:28px;height:36px}.subbar{grid-template-columns:1fr .35fr}.subbar .seg:last-child{display:none}}
__GLOSSY__
</style>
</head>
<body>
<main class="lcars">
<div class="elbow"></div>
<header class="brand">SYSTEM OPERATIONS</header>
<aside class="rail"><span class="tag">OPS 47-A</span><span class="tag">SITE __SITE__</span><span class="tag">ENV __ENVIRONMENT__</span></aside>
<section class="main">
<div class="subbar"><div class="seg">AEGIS OPERATIONS CONTROL</div><div class="seg">SYSTEM STATUS</div><div class="seg">ONLINE</div></div>
<div class="readout">SITE __SITE__ &nbsp; // &nbsp; ENVIRONMENT __ENVIRONMENT__ &nbsp; // &nbsp; <span id="checked">SCANNING SUBSYSTEMS</span></div>
<div class="grid">
<article class="panel"><div class="code">CONFIGURATION CONTROL // CFG-01</div><h2>SYSTEM<br>CONFIGURATION</h2><p>APPLICATION REQUIREMENTS · COMMISSIONING · RUNBOOKS</p><div id="cfgStatus" class="status">SCANNING…</div><a class="button" href="__CONFIGURATION_URL__">OPEN CONFIGURATION →</a></article>
<article class="panel diag"><div class="code">ENGINEERING CONTROL // DIA-01</div><h2>SYSTEM<br>DIAGNOSTICS</h2><p>HEALTH · INCIDENTS · DEPENDENCIES · ENGINEERING LEVELS</p><div id="diagStatus" class="status">SCANNING…</div><a class="button" href="__DIAGNOSTICS_URL__">OPEN DIAGNOSTICS →</a></article>
</div>
</section>
<div class="footerElbow"></div><footer class="footer"><span></span><span></span><span></span></footer>
</main>
<script>
async function load(){try{const r=await fetch('/api/summary',{cache:'no-store'}),d=await r.json();setStatus('cfgStatus',d.configuration);setStatus('diagStatus',d.diagnostics);document.getElementById('checked').textContent='LAST SCAN '+new Date(d.checkedAt).toLocaleString().toUpperCase()}catch{document.getElementById('checked').textContent='STATUS TELEMETRY UNAVAILABLE'}}
function setStatus(id,x){const e=document.getElementById(id);e.textContent=x.available?String(x.status).toUpperCase():'UNAVAILABLE';e.className='status '+(x.available?'available':'unavailable')}
load();setInterval(load,30000)
</script>
</body>
</html>
""";
    }
}
