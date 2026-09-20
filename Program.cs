using Common.Registration;
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
string operationsLogInstanceId = builder.Configuration["Operations:InstanceId"]?.Trim() ?? Environment.MachineName;
string operationsRegistrationDirectory = builder.Configuration["Operations:RegistrationDirectory"]?.Trim()
    ?? "/var/lib/aegis/operations/registration";
string operationsLogCredentialFile = builder.Configuration["Operations:RegistrationCredentialFile"]?.Trim()
    ?? Path.Combine(operationsRegistrationDirectory, "operations.key");
builder.Services.AddAegisControlPlaneOperationsLogging(builder.Configuration, "Aegis.Operations", operationsLogInstanceId, operationsLogCredentialFile, builder.Environment.EnvironmentName);

builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddHttpClient("ops", client =>
{
    int timeoutSeconds = builder.Configuration.GetValue("Operations:RequestTimeoutSeconds", 3);
    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
});
builder.Services.AddSuiteSecurity();
builder.Services.AddSingleton(sp => new OperationsV1Store(
    sp.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton(sp => new OperationsTelemetryAuthenticator(
    sp.GetRequiredService<IHttpClientFactory>(),
    sp.GetRequiredService<IConfiguration>()));

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
app.MapOperationsV1(configurationUrl, diagnosticsUrl, site, environment);
app.MapActivityTelemetry();
app.MapOperationsLogs();
app.MapOperationsSpecialized();
app.MapOperationsAdvisor();
app.Run();
