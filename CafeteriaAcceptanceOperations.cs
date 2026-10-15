using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Common.Diagnostics;

internal sealed record CafeteriaAcceptanceRequest(string Reason);
internal sealed record CafeteriaAcceptanceExecution(Guid Id, string RequestedBy, string Reason,
    DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, int? ExitCode);

internal sealed class CafeteriaAcceptanceOperations(IConfiguration configuration, ILogger<CafeteriaAcceptanceOperations> logger)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private CafeteriaAcceptanceExecution? execution;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter() } };
    public string? Root => configuration["CafeteriaAcceptance:RootDirectory"];
    public bool Configured => !string.IsNullOrWhiteSpace(Root)
        && File.Exists(configuration["CafeteriaAcceptance:ServicesAssembly"])
        && File.Exists(configuration["CafeteriaAcceptance:ManifestPath"])
        && IsAcceptanceAssembly(configuration["CafeteriaAcceptance:ServicesAssembly"]!, configuration["CafeteriaAcceptance:RunnerSha256"]);
    public CafeteriaAcceptanceExecution? Current => execution;

    internal static bool IsAcceptanceAssembly(string path, string? expectedHash)
    {
        if (string.IsNullOrWhiteSpace(expectedHash)) return false;
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (!string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), expectedHash, StringComparison.OrdinalIgnoreCase)) return false;
            using var stream = new MemoryStream(bytes); using var pe = new PEReader(stream);
            MetadataReader metadata = pe.GetMetadataReader();
            var types = metadata.TypeDefinitions.Select(handle => metadata.GetTypeDefinition(handle))
                .Where(type => metadata.GetString(type.Namespace) == "Aegis.Cafeteria.Services.Acceptance")
                .Select(type => metadata.GetString(type.Name)).ToHashSet(StringComparer.Ordinal);
            return types.Contains("CafeteriaAcceptanceCommand") && types.Contains("AcceptanceDatabaseSafety") && types.Contains("CafeteriaAcceptanceSuite");
        }
        catch (Exception e) when (e is IOException or BadImageFormatException or UnauthorizedAccessException) { return false; }
    }

    public async Task<IReadOnlyList<AcceptanceEvidence>> ReadHistoryAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Root) || !Directory.Exists(Root)) return [];
        List<AcceptanceEvidence> history = [];
        foreach (string directory in Directory.EnumerateDirectories(Root))
        {
            if (!Guid.TryParseExact(Path.GetFileName(directory), "N", out _)) continue;
            string path = Path.Combine(directory, "acceptance.json");
            if (!File.Exists(path)) continue;
            try
            {
                var evidence = JsonSerializer.Deserialize<AcceptanceEvidence>(await File.ReadAllTextAsync(path, ct), JsonOptions);
                if (evidence is not null && evidence.Diagnostic.Environment == "Acceptance") history.Add(evidence);
            }
            catch (JsonException) { logger.LogWarning("Acceptance evidence is incomplete in {RunDirectory}", directory); }
        }
        return history.OrderByDescending(x => x.Diagnostic.StartedAt).Take(100).ToArray();
    }

    public async Task<CafeteriaAcceptanceExecution?> StartAsync(string actor, string reason, CancellationToken stopping)
    {
        if (!Configured || !await gate.WaitAsync(0, stopping)) return null;
        try
        {
            string manifest = Path.GetFullPath(configuration["CafeteriaAcceptance:ManifestPath"]!);
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(manifest, stopping));
            string manifestRoot = doc.RootElement.GetProperty("rootDirectory").GetString()!;
            if (!doc.RootElement.TryGetProperty("level", out var level) || level.GetInt32() != 1)
                throw new InvalidOperationException("The complete acceptance action requires a Level 1 manifest.");
            if (!Path.IsPathFullyQualified(Root!) || !string.Equals(Path.GetFullPath(Root!), Path.GetFullPath(manifestRoot), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Operations and acceptance manifest roots disagree.");
            execution = new(Guid.NewGuid(), actor, reason, DateTimeOffset.UtcNow, null, null);
            _ = ExecuteAsync(manifest, actor, reason, stopping);
            return execution;
        }
        catch { gate.Release(); throw; }
    }

    private async Task ExecuteAsync(string manifest, string actor, string reason, CancellationToken stopping)
    {
        int code = -1;
        try
        {
            string assembly = Path.GetFullPath(configuration["CafeteriaAcceptance:ServicesAssembly"]!);
            ProcessStartInfo start = new(configuration["CafeteriaAcceptance:DotnetExecutable"] ?? "dotnet")
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = Path.GetDirectoryName(assembly)! };
            start.ArgumentList.Add(assembly);
            start.ArgumentList.Add("--cafeteria-acceptance");
            start.ArgumentList.Add(manifest);
            start.Environment["CAFETERIA_ACCEPTANCE_REQUESTED_BY"] = actor;
            start.Environment["CAFETERIA_ACCEPTANCE_REASON"] = reason;
            using Process process = Process.Start(start) ?? throw new InvalidOperationException("Acceptance process could not start.");
            // Drain both pipes concurrently to avoid blocking the child on a full log pipe.
            Task<string> stdout = process.StandardOutput.ReadToEndAsync(stopping);
            Task<string> stderr = process.StandardError.ReadToEndAsync(stopping);
            try { await process.WaitForExitAsync(stopping); }
            catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw; }
            string output = await stdout; string error = await stderr;
            code = process.ExitCode;
            Directory.CreateDirectory(Root!);
            await File.WriteAllTextAsync(Path.Combine(Root!, $"execution-{execution!.Id:N}.log"), output + "\n" + error, CancellationToken.None);
            logger.LogInformation("Cafeteria acceptance requested by {Actor} completed with exit code {Code}", actor, code);
        }
        catch (Exception exception) { logger.LogError(exception, "Cafeteria acceptance execution failed before completion"); }
        finally
        {
            execution = execution! with { CompletedAt = DateTimeOffset.UtcNow, ExitCode = code };
            gate.Release();
        }
    }

    internal static bool CanRun(SuiteIdentity? identity) => identity is not null
        && identity.Permissions.Contains("Diagnostics.Run", StringComparer.OrdinalIgnoreCase)
        && identity.Permissions.Contains("Diagnostics.Critical", StringComparer.OrdinalIgnoreCase);

    public static void Map(WebApplication app, string configurationUrl, string diagnosticsUrl)
    {
        app.MapGet("/cafeteria-acceptance", () => Results.Redirect("/diagnostics/level-1/acceptance"));
        app.MapGet("/diagnostics/level-1/acceptance", (HttpContext context) => Results.Content(
            OperationsSectionPage.RenderCafeteriaAcceptance(configurationUrl, diagnosticsUrl, (context.Items["SuiteIdentity"] as SuiteIdentity)?.UserName), "text/html; charset=utf-8"));
        app.MapGet("/api/operations/acceptance/{applicationId}", async (string applicationId, HttpContext context, CafeteriaAcceptanceOperations service, CancellationToken ct) =>
        {
            if (!string.Equals(applicationId, "Aegis.Cafeteria.Services", StringComparison.Ordinal)) return Results.NotFound();
            var history = await service.ReadHistoryAsync(ct);
            var latest = history.FirstOrDefault();
            var required = Enumerable.Range(1, 20).Select(i => new AcceptanceProofDefinition($"CAF-{i:D2}", "", 1)).ToArray();
            bool integration = latest?.IntegrationProven(required) == true;
            AcceptanceQualification? qualification = null;
            string? qualificationPath = app.Configuration["CafeteriaAcceptance:QualificationPath"];
            if (!string.IsNullOrWhiteSpace(qualificationPath) && File.Exists(qualificationPath))
                qualification = JsonSerializer.Deserialize<AcceptanceQualification>(await File.ReadAllTextAsync(qualificationPath, ct), JsonOptions);
            bool reviewed = qualification is not null && latest is not null
                && qualification.ApplicationVersion == latest.ApplicationVersion
                && !string.IsNullOrWhiteSpace(qualification.ReviewedBy)
                && qualification.ReviewedAtUtc <= DateTimeOffset.UtcNow;
            // No review or deployment evidence is inferred from successful test creation.
            return Results.Ok(new { history, execution = service.Current,
                canRun = service.Configured && CanRun(context.Items["SuiteIdentity"] as SuiteIdentity),
                release = AcceptanceReleaseStatus.Evaluate(reviewed, reviewed && qualification!.BuildAndTestsPassed,
                    integration, reviewed && qualification!.DeploymentVerified,
                    reviewed ? qualification!.ReleaseBlockingDefects.Count : 1) });
        });
        app.MapPost("/api/operations/acceptance/{applicationId}/run", async (string applicationId, HttpContext context, CafeteriaAcceptanceRequest request,
            CafeteriaAcceptanceOperations service, IHostApplicationLifetime lifetime) =>
        {
            if (!string.Equals(applicationId, "Aegis.Cafeteria.Services", StringComparison.Ordinal)) return Results.NotFound();
            var identity = context.Items["SuiteIdentity"] as SuiteIdentity;
            if (!CanRun(identity)) return Results.StatusCode(403);
            if (context.Request.Headers["X-Aegis-Action"] != "cafeteria-acceptance") return Results.BadRequest("An explicit acceptance action is required.");
            if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 500) return Results.BadRequest("An engineering reason of 1–500 characters is required.");
            try
            {
                var execution = await service.StartAsync(identity!.UserName, request.Reason.Trim(), lifetime.ApplicationStopping);
                return execution is null ? Results.Conflict("Acceptance is unavailable or already executing.")
                    : Results.Accepted("/api/operations/acceptance/Aegis.Cafeteria.Services", execution);
            }
            catch (Exception e) when (e is InvalidOperationException or JsonException or IOException)
            { return Results.Conflict("Acceptance configuration is invalid; inspect the manifest, output root and runner fingerprint."); }
        });
    }
}
