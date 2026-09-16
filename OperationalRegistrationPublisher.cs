using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Common.Secrets;

internal sealed class OperationalRegistrationPublisher(
    IConfiguration configuration,
    IHostEnvironment environment,
    ISecretProvider secrets,
    ILogger<OperationalRegistrationPublisher> logger) : BackgroundService
{
    private const string ApplicationId = "Aegis.Operations";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string? configurationUrl = configuration["Operations:ConfigurationUrl"];
        if (string.IsNullOrWhiteSpace(configurationUrl)) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            if (await TryPublishAsync(configurationUrl, stoppingToken)) return;
            try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        }
    }

    private async Task<bool> TryPublishAsync(string configurationUrl, CancellationToken ct)
    {
        try
        {
            string? registrationKey = await secrets.GetAsync($"configuration/registration/{ApplicationId}", ct);
            if (string.IsNullOrWhiteSpace(registrationKey)) return false;
            string version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0";
            string siteId = configuration["Operations:Site"] ?? "";
            string instanceId = Environment.MachineName;
            string? diagnosticsUrl = configuration["Operations:DiagnosticsUrl"];

            object[] requirements =
            [
                Config("site", "Operations:Site", "Site", true, "Site identifier shown in the Operations portal."),
                Config("environment", "Operations:Environment", "Environment", true, "Environment label shown in the Operations portal."),
                Config("configuration-url", "Operations:ConfigurationUrl", "Configuration URL", true, "Aegis.Configuration service URL.", "http://localhost:5101"),
                Config("diagnostics-url", "Operations:DiagnosticsUrl", "Diagnostics URL", true, "Aegis.Diagnostics service URL.", "http://localhost:5102"),
                Config("timeout", "Operations:RequestTimeoutSeconds", "Request Timeout", false, "Timeout used when Operations probes suite services.", "3"),
                Secret("registration-key", "ConfigurationRegistrationKey", "Configuration Registration Credential", false, "Credential used only to publish Operations contracts.", true, $"configuration/registration/{ApplicationId}"),
                Secret("secret-zero", "Secret0", "OpenBao Secret Zero", true, "Bootstrap material used to establish OpenBao access.", SecretZeroDetected(), "OpenBao bootstrap")
            ];

            object[] dependencies =
            [
                Dependency("configuration", "Aegis.Configuration", true, "Feature", "Provides the suite Configuration application and receives Operations contracts.", "Operations still runs, but Configuration status/link and centralized contract visibility are unavailable.", "HTTP/HTTPS JSON + browser navigation", "Aegis.Configuration", configurationUrl, await ReachableAsync(configurationUrl, ct)),
                Dependency("diagnostics", "Aegis.Diagnostics", true, "Feature", "Provides suite diagnostics status and navigation.", "Operations still runs, but Diagnostics status/link is unavailable.", "HTTP/HTTPS JSON + browser navigation", "Aegis.Diagnostics", diagnosticsUrl, await ReachableAsync(diagnosticsUrl, ct)),
                Dependency("active-directory", "Active Directory", true, "Startup", "Authenticates Operations users through Common.Security.", "Users cannot sign in through the intended production authentication path.", "LDAP/LDAPS via Common.Security", "Active Directory", null, null),
                Dependency("openbao", "OpenBao", false, "Feature", "Provides the registration credential through Common.Secrets.", "Operations remains usable, but cannot publish its contracts.", "OpenBao HTTP API via Common.Secrets", "OpenBao", OpenBaoAddress(), await ReachableAsync(OpenBaoAddress(), ct))
            ];

            object registration = new
            {
                configuration = new { applicationId = ApplicationId, displayName = "Aegis Operations", version, siteId, instanceId, description = "Suite landing page and authenticated navigation for Aegis Configuration and Diagnostics.", requirements },
                dependencies = new { applicationId = ApplicationId, version, siteId, instanceId, dependencies },
                environment = new { applicationId = ApplicationId, version, siteId, instanceId, environment = await DetectEnvironmentAsync(ct) }
            };

            using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(10) };
            using HttpRequestMessage request = new(HttpMethod.Post, $"{configurationUrl.TrimEnd('/')}/api/registrations/register");
            request.Headers.TryAddWithoutValidation("X-Aegis-Registration-Key", registrationKey);
            request.Content = new StringContent(JsonSerializer.Serialize(registration), Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return false;
            logger.LogInformation("Aegis Operations published configuration, dependency and detected-environment contracts.");
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { logger.LogWarning(ex, "Unable to publish Aegis Operations operational contracts."); return false; }
    }

    private object Config(string id, string key, string name, bool required, string purpose, string? defaultValue = null)
    {
        string envKey = key.Replace(":", "__", StringComparison.Ordinal);
        string? envValue = Environment.GetEnvironmentVariable(envKey);
        string? configured = configuration[key];
        string? value = First(envValue, configured, defaultValue);
        return new { id, displayName = name, kind = "Configuration", required, purpose, configurationKey = key, isConfigured = !string.IsNullOrWhiteSpace(value), safeDisplayValue = value, sensitive = false, allowedSources = new[] { "Environment Variable", $"appsettings.{environment.EnvironmentName}.json", "appsettings.json", "Code Default" }, resolutionOrder = new[] { "Environment Variable", $"appsettings.{environment.EnvironmentName}.json", "appsettings.json", "Code Default" }, effectiveSource = !string.IsNullOrWhiteSpace(envValue) ? $"Environment Variable ({envKey})" : !string.IsNullOrWhiteSpace(configured) ? "Configuration Provider" : defaultValue is not null ? "Code Default" : "Unresolved", hasDefault = defaultValue is not null, defaultValue, redacted = false };
    }

    private static object Secret(string id, string key, string name, bool required, string purpose, bool configured, string path) => new { id, displayName = name, kind = "Secret", required, purpose, configurationKey = key, isConfigured = configured, safeDisplayValue = (string?)null, sensitive = true, allowedSources = new[] { "OpenBao", "approved Environment Variable override", "Secret0 bootstrap" }, resolutionOrder = new[] { "OpenBao", "approved Environment Variable override", "no plaintext fallback" }, effectiveSource = configured ? "OpenBao/Common.Secrets or approved secure override" : "Unresolved", hasDefault = false, defaultValue = (string?)null, secretPath = path, redacted = true };
    private static object Dependency(string id, string name, bool required, string criticality, string purpose, string degraded, string @interface, string? engine, string? endpoint, bool? reachable) => new { id, displayName = name, required, criticality, purpose, degradedBehavior = degraded, @interface, engine, endpoint, reachable, evidence = "Explicitly declared from Operations code." };

    private async Task<object> DetectEnvironmentAsync(CancellationToken ct)
    {
        Process process = Process.GetCurrentProcess(); GCMemoryInfo memory = GC.GetGCMemoryInfo();
        List<object> endpoints = [];
        foreach ((string name, string? endpoint) in new[] { ("Aegis.Configuration", configuration["Operations:ConfigurationUrl"]), ("Aegis.Diagnostics", configuration["Operations:DiagnosticsUrl"]), ("OpenBao", OpenBaoAddress()) }) if (!string.IsNullOrWhiteSpace(endpoint)) endpoints.Add(new { name, endpoint, reachable = await ReachableAsync(endpoint, ct) });
        string rootPath = Path.GetFullPath(environment.ContentRootPath); bool writable = await IsWritableAsync(rootPath, ct); List<object> disks = [];
        try { string root = Path.GetPathRoot(rootPath) ?? ""; if (root.Length > 0) { DriveInfo d = new(root); if (d.IsReady) disks.Add(new { path = root, freeBytes = d.AvailableFreeSpace, totalBytes = d.TotalSize }); } } catch { }
        return new { operatingSystem = RuntimeInformation.OSDescription, operatingSystemVersion = Environment.OSVersion.VersionString, cpuArchitecture = RuntimeInformation.ProcessArchitecture.ToString(), processorCount = Environment.ProcessorCount, availableMemoryBytes = memory.TotalAvailableMemoryBytes > 0 ? memory.TotalAvailableMemoryBytes : null, processWorkingSetBytes = process.WorkingSet64, runtimeVersion = RuntimeInformation.FrameworkDescription, networkAvailable = NetworkInterface.GetIsNetworkAvailable(), disks, paths = new[] { new { path = rootPath, exists = Directory.Exists(rootPath), writable } }, endpoints, hardware = Array.Empty<object>(), detectedAtUtc = DateTimeOffset.UtcNow };
    }

    private string? OpenBaoAddress() => First(configuration["CommonSecrets:OpenBao:Address"], Environment.GetEnvironmentVariable("OPENBAO_ADDR"), Environment.GetEnvironmentVariable("VAULT_ADDR"));
    private static bool SecretZeroDetected() => new[] { "OPENBAO_SECRET_ID", "OPENBAO_TOKEN", "VAULT_TOKEN" }.Any(x => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(x)));
    private static async Task<bool?> ReachableAsync(string? endpoint, CancellationToken ct) { if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri) || (uri.Scheme != "http" && uri.Scheme != "https")) return null; try { using HttpClient c = new() { Timeout = TimeSpan.FromSeconds(3) }; using HttpResponseMessage _ = await c.SendAsync(new HttpRequestMessage(HttpMethod.Head, uri), ct); return true; } catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; } catch { return false; } }
    private static async Task<bool> IsWritableAsync(string path, CancellationToken ct) { string probe = Path.Combine(path, $".aegis-write-{Guid.NewGuid():N}"); try { await File.WriteAllTextAsync(probe, "", ct); File.Delete(probe); return true; } catch { try { if (File.Exists(probe)) File.Delete(probe); } catch { } return false; } }
    private static string? First(params string?[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
}
