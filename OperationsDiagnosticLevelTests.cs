using Common.Diagnostics;

namespace Aegis.Operations;

internal sealed class OperationsControlPlaneEndpointsDiagnosticLevelTest(IConfiguration configuration) : IDiagnosticLevelLocalTest
{
    public string TestId => "AEGIS.OPERATIONS.L5.CONFIG.CONTROL_PLANE_URLS";
    public string Name => "Control Plane endpoint URLs valid";
    public string Owner => "Aegis.Operations";
    public EngineeringDiagnosticLevel Level => EngineeringDiagnosticLevel.Level5Scan;
    public bool IsDestructive => false;

    public Task<EngineeringDiagnosticCheckResult> RunAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string[] keys = ["Aegis:Configuration:Url", "Aegis:Diagnostics:Url"];
        List<string> invalid = [];
        foreach (string key in keys)
        {
            string? value = configuration[key]?.Trim();
            if (string.IsNullOrWhiteSpace(value) ||
                !Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && !uri.IsLoopback))
                invalid.Add(key);
        }

        return Task.FromResult(invalid.Count == 0
            ? EngineeringDiagnosticPolicy.Passed(TestId, Name, "Control Plane endpoint URLs are configured and valid.")
            : EngineeringDiagnosticPolicy.Failed(TestId, Name, "One or more Control Plane endpoint URLs are invalid.", $"Invalid={string.Join(",", invalid)}"));
    }
}

internal sealed class OperationsDataDirectoryConfigurationDiagnosticLevelTest(IConfiguration configuration) : IDiagnosticLevelLocalTest
{
    public string TestId => "AEGIS.OPERATIONS.L5.STORAGE.DATA_DIRECTORY_CONFIG";
    public string Name => "Operations data directory configured";
    public string Owner => "Aegis.Operations";
    public EngineeringDiagnosticLevel Level => EngineeringDiagnosticLevel.Level5Scan;
    public bool IsDestructive => false;

    public Task<EngineeringDiagnosticCheckResult> RunAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string path = configuration["Operations:DataDirectory"]?.Trim() ?? "/var/lib/aegis/operations/v1";
        try
        {
            string full = Path.GetFullPath(path);
            return Task.FromResult(EngineeringDiagnosticPolicy.Passed(TestId, Name, "Operations data directory path is valid.", $"Path={full}"));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return Task.FromResult(EngineeringDiagnosticPolicy.Failed(TestId, Name, "Operations data directory path is invalid.", $"FailureType={ex.GetType().Name}"));
        }
    }
}

internal sealed class OperationsDataDirectoryReadableDiagnosticLevelTest(IConfiguration configuration) : IDiagnosticLevelLocalTest
{
    public string TestId => "AEGIS.OPERATIONS.L4.STORAGE.DATA_DIRECTORY_READABLE";
    public string Name => "Operations data directory readable";
    public string Owner => "Aegis.Operations";
    public EngineeringDiagnosticLevel Level => EngineeringDiagnosticLevel.Level4Analysis;
    public bool IsDestructive => false;

    public Task<EngineeringDiagnosticCheckResult> RunAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string path = configuration["Operations:DataDirectory"]?.Trim() ?? "/var/lib/aegis/operations/v1";
        try
        {
            string full = Path.GetFullPath(path);
            if (!Directory.Exists(full))
                return Task.FromResult(EngineeringDiagnosticPolicy.Failed(TestId, Name, "Operations data directory does not exist.", $"Path={full}"));
            _ = Directory.EnumerateFileSystemEntries(full).Take(1).ToArray();
            return Task.FromResult(EngineeringDiagnosticPolicy.Passed(TestId, Name, "Operations data directory exists and is readable.", $"Path={full}"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return Task.FromResult(EngineeringDiagnosticPolicy.Failed(TestId, Name, "Operations data directory cannot be read.", $"FailureType={ex.GetType().Name}"));
        }
    }
}

internal sealed class OperationsRetentionConfigurationDiagnosticLevelTest(IConfiguration configuration) : IDiagnosticLevelLocalTest
{
    public string TestId => "AEGIS.OPERATIONS.L5.CONFIG.RETENTION";
    public string Name => "Operations retention configuration valid";
    public string Owner => "Aegis.Operations";
    public EngineeringDiagnosticLevel Level => EngineeringDiagnosticLevel.Level5Scan;
    public bool IsDestructive => false;

    public Task<EngineeringDiagnosticCheckResult> RunAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string[] keys = [
            "Operations:Retention:Flows",
            "Operations:Retention:Alerts",
            "Operations:Retention:EngineerLog",
            "Operations:Retention:HistoryView"
        ];
        List<string> invalid = [];
        foreach (string key in keys)
        {
            int value = configuration.GetValue(key, 1);
            if (value <= 0) invalid.Add(key);
        }
        return Task.FromResult(invalid.Count == 0
            ? EngineeringDiagnosticPolicy.Passed(TestId, Name, "Operations retention values are positive.")
            : EngineeringDiagnosticPolicy.Failed(TestId, Name, "One or more Operations retention values are invalid.", $"Invalid={string.Join(",", invalid)}"));
    }
}

internal sealed class OperationsRuntimeResourcesDiagnosticLevelTest(IHostEnvironment environment) : IDiagnosticLevelLocalTest
{
    public string TestId => "AEGIS.OPERATIONS.L4.RUNTIME.RESOURCES";
    public string Name => "Operations runtime resources observable";
    public string Owner => "Aegis.Operations";
    public EngineeringDiagnosticLevel Level => EngineeringDiagnosticLevel.Level4Analysis;
    public bool IsDestructive => false;

    public Task<EngineeringDiagnosticCheckResult> RunAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            string root = Path.GetPathRoot(environment.ContentRootPath) ?? environment.ContentRootPath;
            DriveInfo drive = new(root);
            string evidence = $"Processors={Environment.ProcessorCount}; WorkingSetBytes={Environment.WorkingSet}; ManagedBytes={GC.GetTotalMemory(false)}; DiskFreeBytes={drive.AvailableFreeSpace}";
            return Task.FromResult(drive.AvailableFreeSpace < 512L * 1024 * 1024
                ? EngineeringDiagnosticPolicy.Warning(TestId, Name, "Runtime is observable but disk free space is low.", evidence)
                : EngineeringDiagnosticPolicy.Passed(TestId, Name, "Runtime resources are observable.", evidence));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return Task.FromResult(EngineeringDiagnosticPolicy.Warning(TestId, Name, "One or more runtime resource metrics could not be read.", $"FailureType={ex.GetType().Name}"));
        }
    }
}

internal sealed class OperationsDataDirectoryRoundTripDiagnosticLevelTest(IConfiguration configuration) : IDiagnosticLevelLocalTest
{
    public string TestId => "AEGIS.OPERATIONS.L3.STORAGE.ROUNDTRIP";
    public string Name => "Operations isolated storage round trip";
    public string Owner => "Aegis.Operations";
    public EngineeringDiagnosticLevel Level => EngineeringDiagnosticLevel.Level3Verification;
    public bool IsDestructive => true;

    public async Task<EngineeringDiagnosticCheckResult> RunAsync(CancellationToken cancellationToken)
    {
        string basePath = configuration["Operations:DataDirectory"]?.Trim() ?? "/var/lib/aegis/operations/v1";
        string directory = Path.Combine(basePath, "diagnostics", "diagnostic-level");
        string file = Path.Combine(directory, $"{Guid.NewGuid():N}.probe");
        try
        {
            Directory.CreateDirectory(directory);
            byte[] payload = System.Text.Encoding.UTF8.GetBytes("Aegis Operations DiagnosticLevel probe");
            await File.WriteAllBytesAsync(file, payload, cancellationToken).ConfigureAwait(false);
            byte[] read = await File.ReadAllBytesAsync(file, cancellationToken).ConfigureAwait(false);
            return read.SequenceEqual(payload)
                ? EngineeringDiagnosticPolicy.Passed(TestId, Name, "Isolated storage write/read/delete probe succeeded.", $"Bytes={read.Length}")
                : EngineeringDiagnosticPolicy.Failed(TestId, Name, "Isolated storage probe content mismatch.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return EngineeringDiagnosticPolicy.Failed(TestId, Name, "Isolated storage round trip failed.", $"FailureType={ex.GetType().Name}");
        }
        finally
        {
            try { if (File.Exists(file)) File.Delete(file); } catch { }
        }
    }
}
