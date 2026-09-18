WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("ops", client =>
{
    int timeoutSeconds = builder.Configuration.GetValue("Operations:RequestTimeoutSeconds", 3);
    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
});
builder.Services.AddSuiteSecurity();

WebApplication app = builder.Build();
ILogger startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Aegis.Operations.Startup");

string Resolve(string key, string fallback)
{
    string? configured = builder.Configuration[key];
    if (!string.IsNullOrWhiteSpace(configured))
    {
        startupLogger.LogInformation("Configuration {ConfigurationKey} resolved from configured value {ConfiguredValue}.", key, configured);
        return configured.Trim();
    }

    startupLogger.LogWarning("Configuration {ConfigurationKey} is not set; falling back to default value {FallbackValue}.", key, fallback);
    return fallback;
}

string configurationUrl = Resolve("Operations:ConfigurationUrl", "http://localhost:5101").TrimEnd('/');
string diagnosticsUrl = Resolve("Operations:DiagnosticsUrl", "http://localhost:5102").TrimEnd('/');
string site = Resolve("Operations:Site", "—");
string environment = Resolve("Operations:Environment", "—");

app.MapSuiteSecurity(configurationUrl, diagnosticsUrl);
app.UseSuiteSecurity();
app.MapOperationsEndpoints(configurationUrl, diagnosticsUrl, site, environment);
app.MapLifecycleRegistrationProxy(configurationUrl);
app.Run();
