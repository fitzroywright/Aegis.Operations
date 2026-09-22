using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Common.Security.Authenticators;
using Common.Security.Models;
using Common.Security.Options;

internal sealed record SuiteIdentity(string UserName,string DisplayName,string Title,string[] Permissions);
internal sealed record SuiteTicket(string Sub,string Name,string Title,string[] Permissions,string Audience,long Expires,string Nonce);
internal sealed class SuiteSecurity(IConfiguration config,IHostEnvironment environment)
{
    const string Cookie="Aegis.Operations.Session";
    const string DevelopmentSigningKey="Aegis-Suite-Development-Handoff-Key-v1-Operations-Configuration-Diagnostics";
    readonly byte[] key=Encoding.UTF8.GetBytes(GetSigningKey(config,environment));
    static string GetSigningKey(IConfiguration config,IHostEnvironment environment){string? configured=config["SuiteSecurity:SigningKey"];if(!string.IsNullOrWhiteSpace(configured))return configured;if(environment.IsDevelopment())return DevelopmentSigningKey;throw new InvalidOperationException("SuiteSecurity:SigningKey is required outside Development. Supply it through protected deployment configuration/OpenBao.");}
    public string Mode=>config["SuiteSecurity:Mode"]??"Mock";
    public int IdleTimeoutMinutes=>Math.Clamp(config.GetValue("SuiteSecurity:IdleTimeoutMinutes",30),5,60);
    public SuiteIdentity? Authenticate(string user,string password){if(Mode.Equals("ActiveDirectory",StringComparison.OrdinalIgnoreCase))return AuthenticateAd(user,password);if(Mode.Equals("Internal",StringComparison.OrdinalIgnoreCase))return AuthenticateInternal(user,password);if(Mode.Equals("Mock",StringComparison.OrdinalIgnoreCase))return AuthenticateMock(user,password);return null;}
    SuiteIdentity? AuthenticateAd(string user,string password){var o=new ActiveDirectoryAuthenticationOptions{Domain=config["SuiteSecurity:ActiveDirectory:Domain"]??"",SearchBase=config["SuiteSecurity:ActiveDirectory:SearchBase"]??"",Servers=config.GetSection("SuiteSecurity:ActiveDirectory:Servers").Get<string[]>()??[]};AuthenticationResult r=new ActiveDirectory(o).Authenticate(user,password);return r.IsAuthenticated?FromResult(user,r):null;}
    SuiteIdentity? AuthenticateMock(string user,string password)
    {
        if(!config.GetValue("SuiteSecurity:Mock:Enabled",false)||string.IsNullOrWhiteSpace(user))return null;
        if(environment.IsProduction()&&!config.GetValue("SuiteSecurity:Mock:AllowInProduction",false))return null;
        string configuredPassword=config["SuiteSecurity:Mock:Password"]??string.Empty;
        bool allowAny=config.GetValue("SuiteSecurity:Mock:AllowAnyUser",true);
        string? allowed=config["SuiteSecurity:Mock:AllowedUsers"];
        string? admins=config["SuiteSecurity:Mock:AdminUsers"];
        if(configuredPassword.Length==0&&allowAny)
        {
            bool admin=ParseUsers(admins).Contains(user)||IsConfiguredAdmin(user);
            return new SuiteIdentity(user,$"Mock {user}",admin?"Administrator":"Mock User",admin?AdminPermissions():DefaultPermissions());
        }
        var o=new MockAuthenticationOptions{Password=configuredPassword,AllowAnyUser=allowAny,AllowedUsers=allowed,AdminUsers=admins};
        AuthenticationResult r=new MockAuthenticator(o).Authenticate(user,password??string.Empty);
        return r.IsAuthenticated?FromResult(user,r):null;
    }
    static HashSet<string> ParseUsers(string? users)=>string.IsNullOrWhiteSpace(users)?new(StringComparer.OrdinalIgnoreCase):users.Split([',',';'],StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
    SuiteIdentity? AuthenticateInternal(string user,string password){var section=config.GetSection("SuiteSecurity:Internal:Users").GetChildren().FirstOrDefault(x=>x.Key.Equals(user,StringComparison.OrdinalIgnoreCase));if(section is null)return null;string? hash=section["PasswordHash"];if(hash is null||!Verify(password,hash))return null;string[] permissions=IsConfiguredAdmin(user)?AdminPermissions():(section.GetSection("Permissions").Get<string[]>()??DefaultPermissions());return new(user,section["DisplayName"]??user,section["Title"]??"User",permissions);}
    SuiteIdentity FromResult(string user,AuthenticationResult r){bool admin=r.IsAdministrative||IsConfiguredAdmin(user);return new(user,r.UserInfo?.DisplayName??user,r.UserInfo?.Title??"User",admin?AdminPermissions():DefaultPermissions());}
    bool IsConfiguredAdmin(string user){string normalized=NormalizeUser(user);return ParseUsers(config["SuiteSecurity:AdminUsers"]).Any(x=>NormalizeUser(x).Equals(normalized,StringComparison.OrdinalIgnoreCase));}
    static string NormalizeUser(string user){string value=(user??string.Empty).Trim();int slash=value.LastIndexOf('\\');if(slash>=0)value=value[(slash+1)..];int at=value.IndexOf('@');if(at>0)value=value[..at];return value.Trim();}
    static string[] DefaultPermissions()=>["Operations.View","Configuration.View","Diagnostics.View"];
    static string[] AdminPermissions()=>[
        "Operations.View",
        "Operations.Telemetry.Publish",
        "Operations.Alerts.Acknowledge",
        "Operations.Alerts.Resolve",
        "Operations.EngineerLog.Create",
        "Operations.Runbooks.Manage",
        "Operations.Audit.View",
        "Configuration.View",
        "Configuration.Manage",
        "Diagnostics.View",
        "Diagnostics.Run",
        "Diagnostics.Repair",
        "Diagnostics.Critical",
        "Security.Manage"
    ];
    static bool Verify(string password,string encoded){try{var p=encoded.Split('$');if(p.Length!=4||p[0]!="PBKDF2")return false;int iterations=int.Parse(p[1]);byte[] salt=Convert.FromBase64String(p[2]),expected=Convert.FromBase64String(p[3]);byte[] actual=Rfc2898DeriveBytes.Pbkdf2(password,salt,iterations,HashAlgorithmName.SHA256,expected.Length);return CryptographicOperations.FixedTimeEquals(actual,expected);}catch{return false;}}
    public string CreateSession(SuiteIdentity i)=>Sign(new SuiteTicket(i.UserName,i.DisplayName,i.Title,i.Permissions,"Aegis.Operations",DateTimeOffset.UtcNow.AddMinutes(IdleTimeoutMinutes).ToUnixTimeSeconds(),Guid.NewGuid().ToString("N")));
    public string CreateHandoff(SuiteIdentity i,string audience)=>Sign(new SuiteTicket(i.UserName,i.DisplayName,i.Title,i.Permissions,audience,DateTimeOffset.UtcNow.AddSeconds(45).ToUnixTimeSeconds(),Guid.NewGuid().ToString("N")));
    public SuiteIdentity? AcceptHandoff(string token)=>Validate(token,"Aegis.Operations");
    public SuiteIdentity? ReadSession(HttpRequest req)=>req.Cookies.TryGetValue(Cookie,out var v)?Validate(v,"Aegis.Operations"):null;
    SuiteIdentity? Validate(string token,string audience){var parts=token.Split('.');if(parts.Length!=2)return null;try{byte[] data=WebEncoders.Base64UrlDecode(parts[0]),sig=WebEncoders.Base64UrlDecode(parts[1]);using var h=new HMACSHA256(key);if(!CryptographicOperations.FixedTimeEquals(h.ComputeHash(data),sig))return null;var t=JsonSerializer.Deserialize<SuiteTicket>(data);if(t is null||t.Audience!=audience||t.Expires<DateTimeOffset.UtcNow.ToUnixTimeSeconds())return null;return new(t.Sub,t.Name,t.Title,t.Permissions);}catch{return null;}}
    string Sign(SuiteTicket ticket){byte[] data=JsonSerializer.SerializeToUtf8Bytes(ticket);using var h=new HMACSHA256(key);return WebEncoders.Base64UrlEncode(data)+"."+WebEncoders.Base64UrlEncode(h.ComputeHash(data));}
    public void SetSession(HttpResponse response,SuiteIdentity i)=>response.Cookies.Append(Cookie,CreateSession(i),new CookieOptions{HttpOnly=true,Secure=!environment.IsDevelopment(),SameSite=SameSiteMode.Lax,MaxAge=TimeSpan.FromMinutes(IdleTimeoutMinutes)});
    public void Clear(HttpResponse response)=>response.Cookies.Delete(Cookie);
}
internal static class SuiteSecurityExtensions
{
    public static IServiceCollection AddSuiteSecurity(this IServiceCollection s)=>s.AddSingleton<SuiteSecurity>();
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
             ctx.Request.Path.Equals("/api/operations/logs/filters", StringComparison.OrdinalIgnoreCase));

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
        var id=sec.ReadSession(ctx.Request);
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
        sec.SetSession(ctx.Response,id);
        await next();
    });
    public static void MapSuiteSecurity(this WebApplication app,string configurationPublicUrl,string diagnosticsPublicUrl)
    {
        app.MapGet("/login",(SuiteSecurity s,string? returnUrl,string? username,string? error)=>Results.Content(LoginPage.Render(s.Mode,returnUrl,username,!string.IsNullOrWhiteSpace(error)),"text/html; charset=utf-8"));
        app.MapPost("/auth/login",async(HttpContext c,SuiteSecurity s)=>{var f=await c.Request.ReadFormAsync();string user=f["username"].ToString();string password=f["password"].ToString();string r=f["returnUrl"].ToString();var id=s.Authenticate(user,password);if(id is null)return Results.Redirect("/login?error=1&username="+Uri.EscapeDataString(user)+"&returnUrl="+Uri.EscapeDataString(SafeLocal(r)?r:"/"));s.SetSession(c.Response,id);return Results.Redirect(SafeLocal(r)?r:"/");});
        app.MapPost("/auth/handoff",async(HttpContext c,SuiteSecurity s)=>{var f=await c.Request.ReadFormAsync();var id=s.AcceptHandoff(f["token"].ToString());if(id is null)return Results.Redirect("/login?error=handoff");s.SetSession(c.Response,id);c.Response.Headers.CacheControl="no-store";string r=f["returnUrl"].ToString();return Results.Redirect(SafeLocal(r)?r:"/");});
        app.MapGet("/auth/me",(HttpContext c,SuiteSecurity s)=>{var id=s.ReadSession(c.Request);if(id is null)return Results.Unauthorized();s.SetSession(c.Response,id);return Results.Ok(new { id.UserName, id.DisplayName, id.Title, id.Permissions, clientIp=c.Connection.RemoteIpAddress?.ToString() ?? "unknown", idleTimeoutMinutes=s.IdleTimeoutMinutes });});
        app.MapGet("/auth/logout",(HttpContext c,SuiteSecurity s)=>{s.Clear(c.Response);return Results.Redirect("/login");});
        app.MapGet("/handoff/{target}",(string target,string? returnUrl,HttpContext c,SuiteSecurity s)=>
        {
            var id=s.ReadSession(c.Request);
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
