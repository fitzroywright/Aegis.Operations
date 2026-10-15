# Test-only environment validation. Never persist credentials or embed connection strings.
# Values must be supplied by the invoking process, CI secrets, or a secure local profile.
$ErrorActionPreference = "Stop"

$requiredVariables = @(
    "COMMON_MESSAGING_POSTGRES",
    "REQUESTPORTAL_POSTGRES",
    "FFPMANAGER_TEST"
)

$missing = @($requiredVariables | Where-Object {
    [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($_, "Process"))
})

if ($missing.Count -gt 0) {
    throw ("Missing test environment variable(s): {0}. Set these in the current process or load them from a protected local configuration before running authoritative integration tests." -f ($missing -join ", "))
}

Write-Host "Shared Aegis test environment validated (values redacted)." -ForegroundColor DarkCyan
