[CmdletBinding()]
param(
    [string]$ProjectsRoot = "D:\Projects"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($env:FFPMANAGER_TEST)) {
    throw "FFPMANAGER_TEST is not configured. Run Set-CommonMessagingPostgres.ps1 first."
}

function Invoke-SqlBatch {
    param(
        [Parameter(Mandatory)][System.Data.SqlClient.SqlConnection]$Connection,
        [Parameter(Mandatory)][string]$Sql
    )

    $batches = [regex]::Split(
        $Sql,
        "(?im)^\s*GO\s*(?:--.*)?$"
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    foreach ($batch in $batches) {
        $command = $Connection.CreateCommand()
        try {
            $command.CommandTimeout = 120
            $command.CommandText = $batch
            [void]$command.ExecuteNonQuery()
        }
        finally {
            $command.Dispose()
        }
    }
}

function Invoke-SqlFile {
    param(
        [Parameter(Mandatory)][System.Data.SqlClient.SqlConnection]$Connection,
        [Parameter(Mandatory)][string]$Path
    )

    if (-not (Test-Path $Path)) {
        throw "Required SQL file not found: $Path"
    }

    Write-Host "Applying $Path" -ForegroundColor DarkCyan
    Invoke-SqlBatch -Connection $Connection -Sql (Get-Content -Raw -Path $Path)
}

$connection = New-Object System.Data.SqlClient.SqlConnection $env:FFPMANAGER_TEST
try {
    Write-Host "Connecting to FFPMANAGER_TEST..." -ForegroundColor Cyan
    $connection.Open()

    $requestPortalSql = @(
        (Join-Path $ProjectsRoot "RequestPortal\FfpManager.Bridge\Sql\000_ApplicationLetterExternalRequestId.sql"),
        (Join-Path $ProjectsRoot "RequestPortal\FfpManager.Bridge\Sql\001_RequestPortalIntegrationOutbox.sql")
    )

    foreach ($file in $requestPortalSql) {
        Invoke-SqlFile -Connection $connection -Path $file
    }

    $sensorNetworkSql = @"
IF OBJECT_ID(N'dbo.SensorNetworkChangeOutbox', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SensorNetworkChangeOutbox
    (
        ChangeId bigint IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_SensorNetworkChangeOutbox PRIMARY KEY,
        EntityName nvarchar(64) NOT NULL,
        ChangedAtUtc datetime2(3) NOT NULL
            CONSTRAINT DF_SensorNetworkChangeOutbox_ChangedAtUtc DEFAULT SYSUTCDATETIME(),
        ProcessedAtUtc datetime2(3) NULL
    );
END;

IF OBJECT_ID(N'dbo.TR_Employees_SensorNetworkChangeOutbox', N'TR') IS NULL
EXEC(N'
CREATE TRIGGER dbo.TR_Employees_SensorNetworkChangeOutbox
ON dbo.Employees
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    INSERT dbo.SensorNetworkChangeOutbox(EntityName) VALUES (N''Employees'');
END;');

IF OBJECT_ID(N'dbo.TR_DoorPermissions_SensorNetworkChangeOutbox', N'TR') IS NULL
EXEC(N'
CREATE TRIGGER dbo.TR_DoorPermissions_SensorNetworkChangeOutbox
ON dbo.DoorPermissions
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    INSERT dbo.SensorNetworkChangeOutbox(EntityName) VALUES (N''DoorPermissions'');
END;');

IF OBJECT_ID(N'dbo.TR_Holidays_SensorNetworkChangeOutbox', N'TR') IS NULL
EXEC(N'
CREATE TRIGGER dbo.TR_Holidays_SensorNetworkChangeOutbox
ON dbo.Holidays
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    INSERT dbo.SensorNetworkChangeOutbox(EntityName) VALUES (N''Holidays'');
END;');
"@

    Write-Host "Ensuring Sensor Network durable change feed..." -ForegroundColor DarkCyan
    Invoke-SqlBatch -Connection $connection -Sql $sensorNetworkSql

    $validationSql = @"
SELECT
    CASE WHEN COL_LENGTH(N'dbo.ApplicationLetters', N'ExternalRequestId') IS NOT NULL THEN 1 ELSE 0 END AS ExternalRequestIdReady,
    CASE WHEN OBJECT_ID(N'dbo.RequestPortalIntegrationOutbox', N'U') IS NOT NULL THEN 1 ELSE 0 END AS RequestPortalOutboxReady,
    CASE WHEN OBJECT_ID(N'dbo.SensorNetworkChangeOutbox', N'U') IS NOT NULL THEN 1 ELSE 0 END AS SensorNetworkOutboxReady;
"@

    $command = $connection.CreateCommand()
    $command.CommandText = $validationSql
    $reader = $command.ExecuteReader()
    try {
        if (-not $reader.Read()) {
            throw "FFPMANAGER_TEST integration validation returned no row."
        }

        $externalRequestIdReady = $reader.GetInt32(0) -eq 1
        $requestPortalOutboxReady = $reader.GetInt32(1) -eq 1
        $sensorNetworkOutboxReady = $reader.GetInt32(2) -eq 1

        if (-not ($externalRequestIdReady -and $requestPortalOutboxReady -and $sensorNetworkOutboxReady)) {
            throw "FFPMANAGER_TEST integration schema validation failed."
        }
    }
    finally {
        $reader.Close()
        $command.Dispose()
    }

    Write-Host ""
    Write-Host "FFPMANAGER_TEST integration schema is ready." -ForegroundColor Green
    Write-Host "  Request Portal ExternalRequestId: ready"
    Write-Host "  Request Portal durable outbox:   ready"
    Write-Host "  Sensor Network durable outbox:   ready"
}
finally {
    if ($connection.State -ne [System.Data.ConnectionState]::Closed) {
        $connection.Close()
    }
    $connection.Dispose()
}
