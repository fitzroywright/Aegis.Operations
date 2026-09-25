$variables = @{
    "COMMON_MESSAGING_POSTGRES" = "Host=axios;Port=5432;Database=common_messaging_tests;Username=sa;Password=sql_mis_sql"
    "REQUESTPORTAL_POSTGRES"    = "Host=axios;Port=5432;Database=requestportal_tests;Username=sa;Password=sql_mis_sql"
    "FFPMANAGER_TEST"           = "Integrated Security=SSPI;Pooling=false;Data Source=JMDB01;Initial Catalog=FFP;Encrypt=True;TrustServerCertificate=True"
}

try {
    foreach ($entry in $variables.GetEnumerator()) {
        $variableName = $entry.Key
        $connectionString = $entry.Value

        # Persist for the current Windows user.
        [Environment]::SetEnvironmentVariable(
            $variableName,
            $connectionString,
            "User"
        )

        # Make it immediately available in this PowerShell session.
        $resolvedValue = [Environment]::GetEnvironmentVariable(
            $variableName,
            "User"
        )

        Set-Item `
            -Path "Env:$variableName" `
            -Value $resolvedValue

        Write-Host ""
        Write-Host "$variableName has been set permanently for the current user."
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
