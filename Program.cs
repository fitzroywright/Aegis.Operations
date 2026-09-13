using System.Text.Encodings.Web;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient("ops", client =>
    client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("Operations:RequestTimeoutSeconds", 3)));

var app = builder.Build();

string configUrl = (builder.Configuration["Operations:ConfigurationUrl"] ?? "http://localhost:5101").TrimEnd('/');
string diagUrl = (builder.Configuration["Operations:DiagnosticsUrl"] ?? "http://localhost:5102").TrimEnd('/');
string site = builder.Configuration["Operations:Site"] ?? "—";
string environment = builder.Configuration["Operations:Environment"] ?? "—";

app.MapGet("/api/summary", async (IHttpClientFactory factory, CancellationToken cancellationToken) =>
{
    var client = factory.CreateClient("ops");
    return Results.Ok(new
    {
        site,
        environment,
        configuration = await ProbeAsync(client, configUrl, cancellationToken),
        diagnostics = await ProbeAsync(client, diagUrl, cancellationToken),
        checkedAt = DateTimeOffset.UtcNow
    });
});

app.MapGet("/", () => Results.Content(RenderPage(configUrl, diagUrl, site, environment), "text/html"));
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "Aegis.Operations",
    displayName = "System Operations"
}));

app.Run();

static async Task<object> ProbeAsync(HttpClient client, string url, CancellationToken cancellationToken)
{
    try
    {
        using var response = await client.GetAsync(url + "/health", cancellationToken);
        if (!response.IsSuccessStatusCode)
            return new { available = false, status = "Unavailable", url };

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        string status = document.RootElement.TryGetProperty("status", out var statusElement)
            ? statusElement.GetString() ?? "Available"
            : "Available";

        return new { available = true, status, url };
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
    {
        return new { available = false, status = "Unavailable", url };
    }
}

static string Html(string value) => HtmlEncoder.Default.Encode(value);

static string RenderPage(string configurationUrl, string diagnosticsUrl, string site, string environment)
{
    const string template = """
<!doctype html>
<html>
<head>
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>System Operations</title>
<style>
:root{--bg:#04070d;--panel:#101725;--line:#31415e;--ink:#f4f7ff;--muted:#aeb8ca;--orange:#f4a261;--blue:#64b5f6;--violet:#9d8cff;--green:#78e08f;--red:#ff5d73}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--ink);font-family:Arial,sans-serif}.shell{max-width:1200px;margin:auto;padding:18px}.mast{display:flex;gap:14px;align-items:center;border-bottom:7px solid var(--orange);padding:8px 0 14px}.mark{width:58px;height:58px;border-radius:50%;background:linear-gradient(135deg,var(--blue),var(--violet));box-shadow:inset 0 0 0 12px #0c111b}.mast h1{margin:0;font-size:clamp(24px,4vw,42px)}.mast p,.meta{color:var(--muted)}.meta{padding:14px 0}.grid{display:grid;grid-template-columns:1fr 1fr;gap:16px}.tile{background:linear-gradient(180deg,#121b2b,#0b1019);border:2px solid var(--line);border-radius:18px;padding:22px;min-height:260px;display:flex;flex-direction:column}.tile h2{font-size:28px;margin:10px 0}.icon{width:58px;height:58px;border-radius:50%;position:relative}.cfg{background:linear-gradient(135deg,var(--blue),#49d6c2);box-shadow:inset 0 0 0 12px #111827}.diag{background:linear-gradient(135deg,var(--blue),var(--violet));box-shadow:inset 0 0 0 12px #111827}.status{margin:12px 0;font-weight:900}.available{color:var(--green)}.unavailable{color:var(--red)}a.button{margin-top:auto;background:var(--blue);color:#07101a;text-align:center;padding:13px;border-radius:20px;text-decoration:none;font-weight:900}.footer{margin-top:18px;border-top:6px solid var(--violet);padding:12px;color:var(--muted)}@media(max-width:700px){.shell{padding:10px}.grid{grid-template-columns:1fr}.tile{min-height:220px}.mast{position:sticky;top:0;background:var(--bg);z-index:2}}:focus-visible{outline:3px solid white;outline-offset:2px}
</style>
</head>
<body>
<main class="shell">
<header class="mast"><div class="mark"></div><div><h1>SYSTEM OPERATIONS</h1><p>Configuration and diagnostics from one operational home.</p></div></header>
<div class="meta">SITE: __SITE__ &nbsp; • &nbsp; ENVIRONMENT: __ENVIRONMENT__ &nbsp; • &nbsp; <span id="checked">CHECKING SERVICES…</span></div>
<section class="grid">
<article class="tile"><div class="icon cfg"></div><h2>SYSTEM CONFIGURATION</h2><p>What applications require and whether they report those requirements configured.</p><div id="cfgStatus" class="status">CHECKING…</div><a class="button" href="__CONFIGURATION_URL__">OPEN SYSTEM CONFIGURATION</a></article>
<article class="tile"><div class="icon diag"></div><h2>SYSTEM DIAGNOSTICS</h2><p>Operational health, dependencies, evidence, history and engineering diagnostics.</p><div id="diagStatus" class="status">CHECKING…</div><a class="button" href="__DIAGNOSTICS_URL__">OPEN SYSTEM DIAGNOSTICS</a></article>
</section>
<footer class="footer">LIGHTWEIGHT • STATELESS • RESILIENT TO BACKEND UNAVAILABILITY</footer>
</main>
<script>
async function load(){try{const r=await fetch('/api/summary',{cache:'no-store'}),d=await r.json();setStatus('cfgStatus',d.configuration);setStatus('diagStatus',d.diagnostics);document.getElementById('checked').textContent='LAST CHECK: '+new Date(d.checkedAt).toLocaleString()}catch{document.getElementById('checked').textContent='STATUS CHECK UNAVAILABLE'}}
function setStatus(id,x){const e=document.getElementById(id);e.textContent=x.available?String(x.status).toUpperCase():'UNAVAILABLE';e.className='status '+(x.available?'available':'unavailable')}
load();setInterval(load,30000);
</script>
</body>
</html>
""";

    return template
        .Replace("__SITE__", Html(site), StringComparison.Ordinal)
        .Replace("__ENVIRONMENT__", Html(environment), StringComparison.Ordinal)
        .Replace("__CONFIGURATION_URL__", Html(configurationUrl), StringComparison.Ordinal)
        .Replace("__DIAGNOSTICS_URL__", Html(diagnosticsUrl), StringComparison.Ordinal);
}
