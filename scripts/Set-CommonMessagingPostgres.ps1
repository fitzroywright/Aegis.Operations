$variables = @{
    "COMMON_MESSAGING_POSTGRES" = "Host=axios;Port=5432;Database=common_messaging_tests;Username=sa;Password=sql_mis_sql"
    "REQUESTPORTAL_POSTGRES"    = "Host=axios;Port=5432;Database=requestportal_tests;Username=sa;Password=sql_mis_sql"
    "FFPMANAGER_TEST"           = "Integrated Security=SSPI;Pooling=false;Data Source=JMDB01;Initial Catalog=FFP;Encrypt=True;TrustServerCertificate=True"
}

try {
    foreach ($entry in $variables.GetEnumerator()) {
        $variableName = $entry.Key
        $connectionString = $entry.Value

        # Preserve existing Windows behavior: persist the configured test value
        # for the current user and expose it to this process.
        if ($IsWindows) {
            [Environment]::SetEnvironmentVariable(
                $variableName,
                $connectionString,
                "User"
            )

            $resolvedValue = [Environment]::GetEnvironmentVariable(
                $variableName,
                "User"
            )
        }
        else {
            # User-level environment persistence is unsupported on macOS/Linux.
            # Respect values supplied by the invoking shell or CI environment.
            $resolvedValue = [Environment]::GetEnvironmentVariable(
                $variableName,
                "Process"
            )
            if ([string]::IsNullOrWhiteSpace($resolvedValue)) {
                $resolvedValue = $connectionString
            }
        }

        [Environment]::SetEnvironmentVariable(
            $variableName,
            $resolvedValue,
            "Process"
        )

        Write-Host ""
        if ($IsWindows) {
            Write-Host "$variableName has been set permanently for the current user."
        }
        else {
            Write-Host "$variableName is configured for this PowerShell process."
        }
        Write-Host "It is also available in this PowerShell session."
    }

    Write-Host ""
    Write-Host "Configured test environment variables:"
    Write-Host ""

    Write-Host "COMMON_MESSAGING_POSTGRES: configured"
    Write-Host "REQUESTPORTAL_POSTGRES: configured"
    Write-Host "FFPMANAGER_TEST: configured"

    Write-Host ""
    Write-Host "FFP Manager test target:"
    Write-Host "Server: JMDB01"
    Write-Host "Database: FFP"
    Write-Host "Authentication: Integrated Security (SSPI)"

    Write-Host ""
    Write-Host "Test environment variables are ready."
}
catch {
    Write-Error "Failed to configure test environment variables."
    Write-Error $_
    exit 1
}
