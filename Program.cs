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

string Resolve(string key, string fallback, string purpose)
{
    string? configured = builder.Configuration[key];
    if (!string.IsNullOrWhiteSpace(configured))
    {
        string value = configured.Trim();
        startupLogger.LogInformation(
            "{Purpose} endpoint {ConfigurationKey} resolved from configured value {ConfiguredValue}.",
            purpose,
            key,
            value);
        return value;
    }

    startupLogger.LogWarning(
        "{Purpose} endpoint {ConfigurationKey} is not configured. INTERNAL FALLBACK ACTIVE -> {FallbackValue}.",
        purpose,
        key,
        fallback);
    return fallback;
}

string configurationUrl = Resolve(
    "Aegis:Configuration:Url",
    "http://127.0.0.1:5200",
    "Internal Configuration").TrimEnd('/');
string diagnosticsUrl = Resolve(
    "Aegis:Diagnostics:Url",
    "http://127.0.0.1:5300",
    "Internal Diagnostics").TrimEnd('/');
string configurationPublicUrl = Resolve(
    "Aegis:Configuration:PublicUrl",
    "https://config.ffpja.org",
    "Public Configuration").TrimEnd('/');
string diagnosticsPublicUrl = Resolve(
    "Aegis:Diagnostics:PublicUrl",
    "https://diagnostics.ffpja.org",
    "Public Diagnostics").TrimEnd('/');
string site = Resolve("Operations:Site", "—", "Operations Site");
string environment = Resolve("Operations:Environment", "—", "Operations Environment");

startupLogger.LogInformation(
    "Operations control-plane endpoints: ConfigurationInternal={ConfigurationInternal}; DiagnosticsInternal={DiagnosticsInternal}; ConfigurationPublic={ConfigurationPublic}; DiagnosticsPublic={DiagnosticsPublic}.",
    configurationUrl,
    diagnosticsUrl,
    configurationPublicUrl,
    diagnosticsPublicUrl);

app.MapSuiteSecurity(configurationPublicUrl, diagnosticsPublicUrl);
app.UseSuiteSecurity();
app.MapOperationsEndpoints(configurationUrl, diagnosticsUrl, site, environment);
app.MapLifecycleRegistrationProxy(configurationUrl);
app.MapOperationsV1(configurationUrl, diagnosticsUrl, site, environment);
app.MapActivityTelemetry();
app.MapOperationsLogs();
app.MapOperationsSpecialized();
app.MapOperationsAdvisor();
app.Run();
