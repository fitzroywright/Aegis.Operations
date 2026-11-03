using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Common.Secrets;
using FfpManager.Bridge;
using Microsoft.Extensions.Configuration;

Dictionary<string, object?> result = new();
try
{
    IConfiguration configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
    OpenBaoOptions options = configuration.GetSection("CommonSecrets:Providers:PrimarySecrets:Settings").Get<OpenBaoOptions>()
        ?? throw new InvalidOperationException();
    CommonSecretsOptions common = configuration.GetSection("CommonSecrets").Get<CommonSecretsOptions>()
        ?? throw new InvalidOperationException();
    if (common.Mode != SecretsEnvironmentMode.Production || !options.RequireHttps || !options.RequireSecretIdFileInProduction)
        throw new InvalidOperationException();
    using HttpClient http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(20) };
    await using OpenBaoAuthenticator authentication = new(options, common, http);
    using OpenBaoSecretProvider provider = new(options, http, authentication);
    string token = await authentication.GetTokenAsync();
    result["approle_authenticated"] = true;
    using HttpRequestMessage lookup = new(HttpMethod.Get, options.Address.TrimEnd('/')+"/v1/auth/token/lookup-self");
    lookup.Headers.Add("X-Vault-Token", token);
    using HttpResponseMessage response = await http.SendAsync(lookup);
    response.EnsureSuccessStatusCode();
    using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    string[] policies = document.RootElement.GetProperty("data").GetProperty("policies").EnumerateArray().Select(x => x.GetString()!).ToArray();
    if (!policies.ToHashSet().SetEquals(new[] { "default", "aegis-ffpmanager-bridge" })) throw new InvalidOperationException();
    result["token_policies"] = policies;
    string? sql = await provider.GetAsync(configuration["Bridge:SqlConnectionStringSecretName"] ?? "FfpManagerBridge:SqlConnectionString");
    string? shared = await provider.GetAsync(configuration["Bridge:SharedSecretSecretName"] ?? "FfpManagerIntegration:SharedSecret");
    result["sql_secret_resolved"] = !string.IsNullOrWhiteSpace(sql);
    result["shared_secret_resolved"] = !string.IsNullOrWhiteSpace(shared);
    result["common_secrets_assembly"] = typeof(OpenBaoSecretProvider).Assembly.GetName().Version?.ToString();
    result["sql_connection_attempted"] = false;
    if (!string.IsNullOrWhiteSpace(sql))
    {
        FfpSqlConnection.ValidateTarget(sql); // Must succeed before any SQL connection.
        result["effective_sql_target"] = "JMDB01/FFP";
        result["sql_connection_attempted"] = true;
        await using var connection = await FfpSqlConnection.OpenAsync(sql);
        result["sql_connected"] = true;
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT DB_NAME(), CONVERT(nvarchar(128),SERVERPROPERTY('MachineName')), ORIGINAL_LOGIN();";
        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            result["database"] = reader.GetString(0);
            result["server"] = reader.GetString(1);
            result["service_sql_principal"] = reader.GetString(2);
        }
    }
    result["status"] = !string.IsNullOrWhiteSpace(sql) && !string.IsNullOrWhiteSpace(shared) ? "verified" : "missing-managed-secret";
}
catch (Exception error)
{
    result["status"] = "blocked";
    result["failure_type"] = error.GetType().Name; // Provider exception text can contain credentials.
    Environment.ExitCode = 2;
}
Console.WriteLine(JsonSerializer.Serialize(result));
