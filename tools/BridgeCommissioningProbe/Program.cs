using System.Data.Common;
using System.Text.Json;
using Common.Secrets;
using FfpManager.Bridge;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using RequestPortal.Infrastructure.Persistence;

// Fixed read-only commissioning commands. Never submit, dispatch, migrate or send.
Dictionary<string, object?> result = new();
try
{
    if (args.Length != 1 || args[0] is not ("--ffpjm" or "--portal"))
        throw new InvalidOperationException("Select --ffpjm or --portal.");
    IConfiguration configuration = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: true)
        .AddJsonFile("appsettings.Production.json", optional: true)
        .AddEnvironmentVariables().Build();
    await using var secrets = CommonSecretsBootstrapRuntime.Create(configuration);
    result["sharedSecretResolved"] = !string.IsNullOrWhiteSpace(await secrets.Provider.GetAsync(
        configuration[args[0] == "--portal" ? "FfpManagerIntegration:SharedSecretName" : "Bridge:SharedSecretSecretName"]
        ?? "FfpManagerIntegration:SharedSecret"));
    if (result["sharedSecretResolved"] is not true) throw new InvalidOperationException("Managed shared secret unavailable.");
    if (args[0] == "--portal")
    {
        string connectionString = await secrets.Provider.GetAsync("ConnectionStrings:RequestPortal")
            ?? throw new InvalidOperationException("Managed PostgreSQL secret unavailable.");
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction))
            await readOnly.ExecuteNonQueryAsync();
        var options = new DbContextOptionsBuilder<RequestPortalDbContext>().UseNpgsql(connection).Options;
        await using var db = new RequestPortalDbContext(options);
        await db.Database.UseTransactionAsync(transaction);
        result["database"] = connection.Database;
        result["appliedMigrations"] = await db.Database.GetAppliedMigrationsAsync();
        result["pendingMigrations"] = await db.Database.GetPendingMigrationsAsync();
        result["portalOutbox"] = await db.OutboxMessages.AsNoTracking()
            .GroupBy(x => new { x.MessageType, Processed = x.ProcessedUtc != null, x.AttemptCount })
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        result["integrationLinks"] = await db.IntegrationLinks.AsNoTracking().GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key.ToString(), Count = g.Count() }).ToListAsync();
        result["notificationHistory"] = await db.NotificationHistory.AsNoTracking().GroupBy(x => new { x.Channel, x.Status })
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        await transaction.RollbackAsync();
    }
    else
    {
        string value = await secrets.Provider.GetAsync(configuration["Bridge:SqlConnectionStringSecretName"]
            ?? "FfpManagerBridge:SqlConnectionString") ?? throw new InvalidOperationException("Managed SQL secret unavailable.");
        // Never rewrite the secret catalog and never fall back to FFP.
        FfpSqlConnection.ValidateTarget(value, "Production");
        await using var connection = await FfpSqlConnection.OpenAsync(value, "Production");
        result["identity"] = await Query(connection, "SELECT DB_NAME() AS DatabaseName, CONVERT(nvarchar(128),SERVERPROPERTY('MachineName')) AS ServerName, ORIGINAL_LOGIN() AS ServiceIdentity, CONVERT(nvarchar(128),CONNECTIONPROPERTY('auth_scheme')) AS Authentication, IS_SRVROLEMEMBER('sysadmin') AS IsSysadmin, IS_MEMBER('db_owner') AS IsDbOwner");
        result["permissions"] = await Query(connection, "SELECT permission_name FROM fn_my_permissions(NULL,'DATABASE')");
        result["schema"] = await Query(connection, "SELECT o.name,o.type_desc,c.name AS ColumnName,TYPE_NAME(c.user_type_id) AS DataType,c.max_length,c.is_nullable FROM sys.objects o LEFT JOIN sys.columns c ON c.object_id=o.object_id WHERE SCHEMA_NAME(o.schema_id)='dbo' AND o.name IN ('ApplicationLetters','ItemsRequested','SupportingDocument','ApplicationNotes','ApplicationStatusTypes','DocumentTypes','UnitsOfMeasure','Parishes','IDGeneratorTable','RequestPortalIntegrationOutbox','RequestPortal_EnqueueApplicationStatusChanged','RequestPortal_EnqueueInformationRequest') ORDER BY o.name,c.column_id");
        result["correlationIndexes"] = await Query(connection, "SELECT name,is_unique,has_filter,filter_definition FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.ApplicationLetters') AND name LIKE '%ExternalRequestId%'");
        result["statuses"] = await Query(connection, "SELECT ID,Name FROM dbo.ApplicationStatusTypes ORDER BY ID");
        result["documentTypes"] = await Query(connection, "SELECT ID,Name FROM dbo.DocumentTypes ORDER BY ID");
        result["referenceCounterSchema"] = await Query(connection, "SELECT name,TYPE_NAME(user_type_id) AS DataType FROM sys.columns WHERE object_id=OBJECT_ID('dbo.IDGeneratorTable')");
        result["bridgeOutbox"] = await Query(connection, "SELECT EventType, COUNT_BIG(*) AS EventCount FROM dbo.RequestPortalIntegrationOutbox GROUP BY EventType");
        result["statusEventHooks"] = await Query(connection, "SELECT OBJECT_SCHEMA_NAME(object_id) AS SchemaName,OBJECT_NAME(object_id) AS ObjectName FROM sys.sql_modules WHERE definition LIKE '%RequestPortal_EnqueueApplicationStatusChanged%' AND object_id <> OBJECT_ID('dbo.RequestPortal_EnqueueApplicationStatusChanged')");
        try
        {
            result["backupHistory"] = await Query(connection, "SELECT TOP (3) database_name,type,backup_start_date,backup_finish_date,is_copy_only,has_backup_checksums,is_damaged FROM msdb.dbo.backupset WHERE database_name='FFPJM' ORDER BY backup_finish_date DESC");
        }
        catch (SqlException) { result["backupHistory"] = "Not accessible to runtime identity"; }
    }
    result["readOnly"] = true;
    result["status"] = "verified";
}
catch (Exception error)
{
    result["status"] = "blocked";
    result["failureType"] = error.GetType().Name;
    if (error is SqlException sql) result["sqlErrorNumber"] = sql.Number;
    if (error is PostgresException postgres) result["sqlState"] = postgres.SqlState;
    Environment.ExitCode = 2;
}
Console.WriteLine(JsonSerializer.Serialize(result));

static async Task<List<Dictionary<string, object?>>> Query(DbConnection connection, string sql)
{
    await using var command = connection.CreateCommand();
    command.CommandText = sql;
    command.CommandTimeout = 15;
    await using var reader = await command.ExecuteReaderAsync();
    List<Dictionary<string, object?>> rows = [];
    while (await reader.ReadAsync())
    {
        Dictionary<string, object?> row = new();
        for (int i = 0; i < reader.FieldCount; i++) row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        rows.Add(row);
    }
    return rows;
}
