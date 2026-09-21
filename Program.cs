using Microsoft.AspNetCore.HttpOverrides;
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
builder.Services.AddHttpClient("registration-authority", client =>
{
    int timeoutSeconds = builder.Configuration.GetValue("Operations:RegistrationAuthorityTimeoutSeconds", 12);
    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 5, 30));
});
builder.Services.AddSuiteSecurity();
builder.Services.AddSingleton(sp => new OperationsV1Store(
    sp.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton(sp => new OperationsTelemetryAuthenticator(
    sp.GetRequiredService<IHttpClientFactory>(),
    sp.GetRequiredService<IConfiguration>(),
    sp.GetRequiredService<ILogger<OperationsTelemetryAuthenticator>>()));

WebApplication app = builder.Build();
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
ILogger startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Aegis.Operations.Startup");

string configurationUrl = AegisControlPlaneEndpoints.ResolveInternal(
    builder.Configuration,
    AegisControlPlaneService.Configuration,
    startupLogger);
string diagnosticsUrl = AegisControlPlaneEndpoints.ResolveInternal(
    builder.Configuration,
    AegisControlPlaneService.Diagnostics,
    startupLogger);
string configurationPublicUrl = AegisControlPlaneEndpoints.ResolvePublic(
    builder.Configuration,
    AegisControlPlaneService.Configuration,
    startupLogger);
string diagnosticsPublicUrl = AegisControlPlaneEndpoints.ResolvePublic(
    builder.Configuration,
    AegisControlPlaneService.Diagnostics,
    startupLogger);

string ResolveValue(string key, string fallback)
{
    string? configured = builder.Configuration[key]?.Trim();
    if (!string.IsNullOrWhiteSpace(configured))
    {
        startupLogger.LogInformation(
            "Configuration {ConfigurationKey} resolved from configured value {ConfiguredValue}.",
            key,
            configured);
        return configured;
    }

    startupLogger.LogWarning(
        "Configuration {ConfigurationKey} is not set; falling back to {FallbackValue}.",
        key,
        fallback);
    return fallback;
}

string site = ResolveValue("Operations:Site", "—");
string environment = ResolveValue("Operations:Environment", "—");

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
