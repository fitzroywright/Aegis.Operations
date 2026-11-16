# Restore automatic loading of the shared test environment without committing secrets.
# Priority: existing process variables, protected local profile, Windows user/machine variables.
$ErrorActionPreference = "Stop"

$requiredVariables = @(
    "COMMON_MESSAGING_POSTGRES",
    "REQUESTPORTAL_POSTGRES",
    "FFPMANAGER_TEST"
)

# A developer-specific script outside the repository can populate process variables.
# Keep its credentials local; do not commit that script to source control.
$localProfile = Join-Path $HOME ".aegis\test-environment.ps1"
if (Test-Path -LiteralPath $localProfile -PathType Leaf) {
    Write-Host "Loading protected Aegis test environment profile..." -ForegroundColor DarkCyan
    . $localProfile
}

foreach ($name in $requiredVariables) {
    $current = [Environment]::GetEnvironmentVariable($name, "Process")
    if (-not [string]::IsNullOrWhiteSpace($current)) { continue }

    foreach ($scope in @("User", "Machine")) {
        $saved = [Environment]::GetEnvironmentVariable($name, $scope)
        if (-not [string]::IsNullOrWhiteSpace($saved)) {
            [Environment]::SetEnvironmentVariable($name, $saved, "Process")
            break
        }
    }
}

$missing = @($requiredVariables | Where-Object {
    [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($_, "Process"))
})
if ($missing.Count -gt 0) {
    throw ("Missing test environment variable(s): {0}. Restore existing user environment settings or configure the protected profile at {1}. Do not commit test credentials." -f ($missing -join ", "), $localProfile)
}

# The full authoritative build provisions the FFPMANAGER_TEST schema; reject known production databases.
$ffpConnection = [Environment]::GetEnvironmentVariable("FFPMANAGER_TEST", "Process")
$ffpDatabase = [regex]::Match($ffpConnection, '(?i)(?:^|;)\s*(?:Initial\s+Catalog|Database)\s*=\s*([^;]+)').Groups[1].Value.Trim().Trim('"', "'")
if ([string]::IsNullOrWhiteSpace($ffpDatabase)) {
    throw "FFPMANAGER_TEST must explicitly name an isolated test database in Initial Catalog or Database."
}
if ($ffpDatabase -in @("FFPJM", "master", "model", "msdb", "tempdb")) {
    throw "FFPMANAGER_TEST targets a production/system database. An isolated test database is required."
}
if ($ffpDatabase -eq "FFP") {
    throw "FFPMANAGER_TEST targets legacy database FFP on JMDB01. The full build provisions tables and triggers; explicitly configure a verified isolated test database instead."
}

Write-Host "Shared Aegis test environment loaded and validated (credentials redacted)." -ForegroundColor DarkCyan
