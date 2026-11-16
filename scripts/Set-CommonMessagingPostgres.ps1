#Requires -Version 5.1
# Interactive, test-only Aegis environment setup. No embedded or persisted passwords.
$ErrorActionPreference = "Stop"

$requiredVariables = @("COMMON_MESSAGING_POSTGRES", "REQUESTPORTAL_POSTGRES", "FFPMANAGER_TEST")

function Get-EnvironmentSetting {
    param([Parameter(Mandatory)][string]$Name)
    $value = [Environment]::GetEnvironmentVariable($Name, "Process")
    if (-not [string]::IsNullOrWhiteSpace($value)) { return $value }

    foreach ($scope in @("User", "Machine")) {
        $value = [Environment]::GetEnvironmentVariable($Name, $scope)
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            [Environment]::SetEnvironmentVariable($Name, $value, "Process")
            return $value
        }
    }
    return $null
}

# Optional protected local profile; never store test credentials in the repository.
$localProfile = Join-Path $HOME ".aegis\test-environment.ps1"
if (Test-Path -LiteralPath $localProfile -PathType Leaf) {
    Write-Host "Loading protected Aegis test profile..." -ForegroundColor DarkCyan
    . $localProfile
}

$commonPg = Get-EnvironmentSetting "COMMON_MESSAGING_POSTGRES"
$requestPg = Get-EnvironmentSetting "REQUESTPORTAL_POSTGRES"

if ([string]::IsNullOrWhiteSpace($commonPg) -or [string]::IsNullOrWhiteSpace($requestPg)) {
    if ([Console]::IsInputRedirected) {
        throw "Missing PostgreSQL test settings. Supply process environment variables or a protected test profile for noninteractive runs."
    }

    Write-Host "PostgreSQL test settings are missing. Enter the credentials for the isolated test server axios." -ForegroundColor Yellow
    $pgCredential = Get-Credential -UserName "sa" -Message "PostgreSQL TEST credentials for axios"
    if ($null -eq $pgCredential) { throw "PostgreSQL test credentials were not provided." }
    $pgPassword = $pgCredential.GetNetworkCredential().Password
    if ([string]::IsNullOrWhiteSpace($pgPassword)) { throw "PostgreSQL test password cannot be empty." }

    function New-TestPostgresConnection {
        param([Parameter(Mandatory)][string]$Database)
        $builder = [System.Data.Common.DbConnectionStringBuilder]::new()
        $builder["Host"] = "axios"
        $builder["Port"] = 5432
        $builder["Database"] = $Database
        $builder["Username"] = $pgCredential.UserName
        $builder["Password"] = $pgPassword
        $builder["Timeout"] = 10
        return $builder.ConnectionString
    }

    if ([string]::IsNullOrWhiteSpace($commonPg)) {
        $env:COMMON_MESSAGING_POSTGRES = New-TestPostgresConnection -Database "common_messaging_tests"
    }
    if ([string]::IsNullOrWhiteSpace($requestPg)) {
        $env:REQUESTPORTAL_POSTGRES = New-TestPostgresConnection -Database "requestportal_tests"
    }
    $pgPassword = $null
    $pgCredential = $null
}

$ffpConnection = Get-EnvironmentSetting "FFPMANAGER_TEST"
if ([string]::IsNullOrWhiteSpace($ffpConnection)) {
    if ([Console]::IsInputRedirected) {
        throw "FFPMANAGER_TEST is missing. Supply an explicitly approved isolated test SQL Server connection."
    }

    $ffpDatabase = Read-Host "Enter the VERIFIED ISOLATED FFP Manager test database name on JMDB01"
    if ($ffpDatabase -notmatch '^[A-Za-z0-9_]+$') {
        throw "Invalid FFP Manager test database name."
    }

    $sqlBuilder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new()
    $sqlBuilder.DataSource = "JMDB01"
    $sqlBuilder.InitialCatalog = $ffpDatabase
    $sqlBuilder.IntegratedSecurity = $true
    $sqlBuilder.Pooling = $false
    $sqlBuilder.Encrypt = $true
    $sqlBuilder.TrustServerCertificate = $true
    $ffpConnection = $sqlBuilder.ConnectionString
}

# Full Build All executes schema changes. Never reuse legacy FFP or production FFPJM.
$sqlTarget = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($ffpConnection)
$dbName = $sqlTarget.InitialCatalog
if ([string]::IsNullOrWhiteSpace($dbName) -or
    $dbName -in @("FFP", "FFPJM", "master", "model", "msdb", "tempdb") -or
    $dbName -notmatch '(?i)test') {
    throw "FFPMANAGER_TEST must target an explicitly named isolated test database, never FFP/FFPJM or a system database."
}

if ($sqlTarget.DataSource -ne "JMDB01") {
    Write-Host "FFP Manager test server: $($sqlTarget.DataSource)" -ForegroundColor Yellow
}
Write-Host "FFP Manager test database: $dbName" -ForegroundColor Yellow
if ([Console]::IsInputRedirected) {
    throw "Cannot confirm schema-changing FFP Manager test target noninteractively."
}
$confirmation = Read-Host "Confirm that this database is isolated and safe for schema changes (type YES)"
if ($confirmation -cne "YES") { throw "FFP Manager test target not confirmed." }
[Environment]::SetEnvironmentVariable("FFPMANAGER_TEST", $ffpConnection, "Process")

$missing = @($requiredVariables | Where-Object {
    [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($_, "Process"))
})
if ($missing.Count -gt 0) { throw "Missing test environment variable(s): $($missing -join ', ')" }

Write-Host "Shared Aegis test environment configured (credentials redacted)." -ForegroundColor Green
