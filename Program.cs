WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("ops", client =>
{
    int timeoutSeconds = builder.Configuration.GetValue("Operations:RequestTimeoutSeconds", 3);
    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
});
builder.Services.AddSuiteSecurity();

WebApplication app = builder.Build();

string configurationUrl = (builder.Configuration["Operations:ConfigurationUrl"] ?? "http://localhost:5101").TrimEnd('/');
string diagnosticsUrl = (builder.Configuration["Operations:DiagnosticsUrl"] ?? "http://localhost:5102").TrimEnd('/');
string site = builder.Configuration["Operations:Site"] ?? "—";
string environment = builder.Configuration["Operations:Environment"] ?? "—";

app.MapSuiteSecurity(configurationUrl, diagnosticsUrl);
app.UseSuiteSecurity();
app.MapOperationsEndpoints(configurationUrl, diagnosticsUrl, site, environment);
app.Run();
