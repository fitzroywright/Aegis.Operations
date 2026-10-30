using System.Text.Encodings.Web;
using Common.Security.Hosting;
using Common.Security.Models;
using Common.Secrets;
using Microsoft.Data.Sqlite;
internal static class SuiteSecurityExtensions
{
    public static IServiceCollection AddSuiteSecurity(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var definition = new SuiteApplication("Aegis.Operations", "Aegis.Operations.Session", new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Administrator"] = ["Operations.View","Operations.Telemetry.Publish","Operations.Alerts.Acknowledge","Operations.Alerts.Resolve","Operations.EngineerLog.Create","Operations.Runbooks.Manage","Operations.Audit.View","Configuration.View","Configuration.Manage","Diagnostics.View","Diagnostics.Run","Diagnostics.Repair","Diagnostics.Critical","Security.Manage"],
            ["Operator"] = ["Operations.View","Operations.Telemetry.Publish","Operations.Alerts.Acknowledge","Operations.EngineerLog.Create"],
            ["Reader"] = ["Operations.View"],
            ["Diagnostics"] = ["Diagnostics.View","Diagnostics.Run"]
        });
        return services.AddCommonSuiteSecurity(definition, provider =>
        {
            string? path = configuration["SuiteSecurity:AccountStorePath"];
            if (string.IsNullOrWhiteSpace(path))
            {
                if (!environment.IsDevelopment()) throw new InvalidOperationException("SuiteSecurity:AccountStorePath must point to the same protected identity database for all Control Plane applications.");
                path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aegis", "ControlPlane", "security.db");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            return new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, ForeignKeys = true }.ToString());
        }, configuration.GetValue("SuiteSecurity:Entra:Enabled", false) ? provider => SuiteSecurityRegistration.EntraOptions(
            configuration, provider.GetRequiredService<ISecretProvider>().GetRequiredAsync("SuiteSecurity:Entra:ClientSecret").GetAwaiter().GetResult()) : null);
    }
    public static void UseSuiteSecurity(this WebApplication app)=>app.Use(async(ctx,next)=>{
        bool publicRegistrationEndpoint =
            ctx.Request.Path.Equals("/api/registration/contracts", StringComparison.OrdinalIgnoreCase) ||
            ctx.Request.Path.Equals("/api/registration/validate", StringComparison.OrdinalIgnoreCase) ||
            ctx.Request.Path.Equals("/api/registration/control-plane/auto", StringComparison.OrdinalIgnoreCase);

        bool lifecycleAuthenticatedTelemetry =
            HttpMethods.IsPost(ctx.Request.Method) &&
            (ctx.Request.Path.Equals("/api/operations/activity/observe", StringComparison.OrdinalIgnoreCase) ||
             ctx.Request.Path.Equals("/api/operations/applications/observe", StringComparison.OrdinalIgnoreCase) ||
             ctx.Request.Path.Equals("/api/operations/flows/observe", StringComparison.OrdinalIgnoreCase) ||
             ctx.Request.Path.Equals("/api/operations/flow-definitions", StringComparison.OrdinalIgnoreCase) ||
             ctx.Request.Path.Equals("/api/operations/alerts", StringComparison.OrdinalIgnoreCase) ||
             ctx.Request.Path.Equals("/api/operations/diagnostics/observe", StringComparison.OrdinalIgnoreCase) ||
             ctx.Request.Path.Equals("/api/operations/messaging/observe", StringComparison.OrdinalIgnoreCase) ||
             ctx.Request.Path.Equals("/api/operations/messages/trace", StringComparison.OrdinalIgnoreCase) ||
             ctx.Request.Path.Equals("/api/operations/synchronization/observe", StringComparison.OrdinalIgnoreCase) ||
             ctx.Request.Path.Equals("/api/operations/logs/observe", StringComparison.OrdinalIgnoreCase));

        bool machineActivityRead =
            HttpMethods.IsGet(ctx.Request.Method) &&
            ctx.Request.Headers.ContainsKey("Authorization") &&
            ctx.Request.Headers.ContainsKey("X-Aegis-Application-Id") &&
            (ctx.Request.Path.Equals("/api/operations/activity", StringComparison.OrdinalIgnoreCase) ||
             ctx.Request.Path.Equals("/api/operations/activity/filters", StringComparison.OrdinalIgnoreCase) ||
             ctx.Request.Path.Equals("/api/operations/activity/flows", StringComparison.OrdinalIgnoreCase) ||
             ctx.Request.Path.Equals("/api/operations/logs", StringComparison.OrdinalIgnoreCase) ||
             ctx.Request.Path.Equals("/api/operations/logs/filters", StringComparison.OrdinalIgnoreCase) ||
             ctx.Request.Path.Equals("/api/operations/registered-applications", StringComparison.OrdinalIgnoreCase));

        if(ctx.Request.Path.StartsWithSegments("/health")||
           ctx.Request.Path.StartsWithSegments("/login")||
           ctx.Request.Path.StartsWithSegments("/auth")||
           publicRegistrationEndpoint||
           lifecycleAuthenticatedTelemetry||
           machineActivityRead)
        {
            await next();
            return;
        }

        var sec=ctx.RequestServices.GetRequiredService<SuiteSecurity>();
        var id=sec.Read(ctx.Request);
        if(id is null)
        {
            if(ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode=StatusCodes.Status401Unauthorized;
                return;
            }

            ctx.Response.Redirect("/login?returnUrl="+Uri.EscapeDataString(ctx.Request.Path+ctx.Request.QueryString));
            return;
        }

        if(!id.Permissions.Contains("Operations.View",StringComparer.OrdinalIgnoreCase))
        {
            ctx.Response.StatusCode=StatusCodes.Status403Forbidden;
            return;
        }

        ctx.Items["SuiteIdentity"]=id;
        await sec.SignInAsync(ctx,id);
        await next();
    });
    public static void MapSuiteSecurity(this WebApplication app,string configurationPublicUrl,string diagnosticsPublicUrl)
    {
        app.MapGet("/login",(SuiteSecurity s,string? returnUrl,string? username,string? error)=>Results.Content(LoginPage.Render(s.Mode,returnUrl,username,!string.IsNullOrWhiteSpace(error),s.EntraEnabled),"text/html; charset=utf-8"));
        app.MapPost("/auth/login",async(HttpContext c,SuiteSecurity s,ILogger<SuiteSecurity> logger)=>
        {
            var f=await c.Request.ReadFormAsync();
            string user=f["username"].ToString();
            string password=f["password"].ToString();
            string r=f["returnUrl"].ToString();
            try
            {
                var id=s.Authenticate(user,password);
                if(id is null)
                    return Results.Redirect("/login?error=1&username="+Uri.EscapeDataString(user)+"&returnUrl="+Uri.EscapeDataString(SafeLocal(r)?r:"/"));
                await s.SignInAsync(c,id);
                return Results.Redirect(SafeLocal(r)?r:"/");
            }
            catch(Exception ex)
            {
                logger.LogError(ex,"Operations authentication backend failed for user {UserName}.",user);
                return Results.Redirect("/login?error=backend&username="+Uri.EscapeDataString(user)+"&returnUrl="+Uri.EscapeDataString(SafeLocal(r)?r:"/"));
            }
        }).RequireRateLimiting(AccountSecurityHosting.LoginRateLimit);
        app.MapPost("/auth/handoff",async(HttpContext c,SuiteSecurity s)=>{var f=await c.Request.ReadFormAsync();var id=s.AcceptHandoff(f["token"].ToString());if(id is null)return Results.Redirect("/login?error=handoff");await s.SignInAsync(c,id);c.Response.Headers.CacheControl="no-store";string r=f["returnUrl"].ToString();return Results.Redirect(SafeLocal(r)?r:"/");});
        app.MapGet("/auth/me",async(HttpContext c,SuiteSecurity s)=>{var id=s.Read(c.Request);if(id is null)return Results.Unauthorized();await s.SignInAsync(c,id);return Results.Ok(new { id.UserName, id.DisplayName, id.Title, id.Permissions, clientIp=c.Connection.RemoteIpAddress?.ToString() ?? "unknown", idleTimeoutMinutes=s.IdleTimeoutMinutes });});
        app.MapGet("/auth/logout",async(HttpContext c,SuiteSecurity s)=>{await s.SignOutAsync(c);return Results.Redirect("/login");});
        app.MapGet("/handoff/{target}",(string target,string? returnUrl,HttpContext c,SuiteSecurity s)=>
        {
            var id=s.Read(c.Request);
            if(id is null)
            {
                string pending=c.Request.Path+c.Request.QueryString;
                return Results.Redirect("/login?returnUrl="+Uri.EscapeDataString(pending));
            }
            if(target.Equals("registration",StringComparison.OrdinalIgnoreCase)) return Results.Redirect("/registration");
            string audience,url,permission;
            string destination=SafeLocal(returnUrl??string.Empty)?returnUrl!:"/";
            if(target.Equals("configuration",StringComparison.OrdinalIgnoreCase)){audience="Aegis.Configuration";url=configurationPublicUrl;permission="Configuration.View";}
            else if(target.Equals("diagnostics",StringComparison.OrdinalIgnoreCase)){audience="Aegis.Diagnostics";url=diagnosticsPublicUrl;permission="Diagnostics.View";}
            else return Results.BadRequest();
            if(!id.Permissions.Contains(permission,StringComparer.OrdinalIgnoreCase))return Results.StatusCode(StatusCodes.Status403Forbidden);
            string action=HtmlEncoder.Default.Encode(url.TrimEnd('/')+"/auth/handoff");
            string token=HtmlEncoder.Default.Encode(s.CreateHandoff(id,audience));
            string html=$"""<!doctype html><html><head><meta charset="utf-8"><meta name="referrer" content="no-referrer"><title>Opening {HtmlEncoder.Default.Encode(target)}</title></head><body><form id="handoff" method="post" action="{action}"><input type="hidden" name="token" value="{token}"><input type="hidden" name="returnUrl" value="{HtmlEncoder.Default.Encode(destination)}"></form><script>history.replaceState(null,'','/');document.getElementById('handoff').submit();</script><noscript><button form="handoff" type="submit">Continue</button></noscript></body></html>""";
            c.Response.Headers.CacheControl="no-store";
            c.Response.Headers.Pragma="no-cache";
            c.Response.Headers["Referrer-Policy"]="no-referrer";
            return Results.Content(html,"text/html; charset=utf-8");
        });
    }
    static bool SafeLocal(string value)=>!string.IsNullOrWhiteSpace(value)&&value.StartsWith('/')&&!value.StartsWith("//",StringComparison.Ordinal)&&!value.Contains("\\",StringComparison.Ordinal);
}
