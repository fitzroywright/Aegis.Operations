[CmdletBinding()]
param(
    [string]$Root = $(if ($IsWindows) { "D:\Projects" } else { Join-Path $HOME "Projects" }),
    [string]$HostName = "kratos",
    [string]$UserName = "fwright@fftpj.local",
    [string]$IdentityFile,
    [switch]$SkipTests,
    [string]$StudioTestPostgresVariable = "STUDIO_TEST_POSTGRES",
    [Parameter(Mandatory = $true)][uri]$BridgeBaseUrl,
    [switch]$KeepLocalBundle,
    [switch]$KeepRemoteBundle
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-Native {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath exited with code ${LASTEXITCODE}: $($Arguments -join ' ')"
    }
}

function Reset-DotNetBuildServers {
    Write-Host "Resetting .NET build servers..." -ForegroundColor DarkGray
    & dotnet build-server shutdown | Out-Host
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "dotnet build-server shutdown returned exit code $LASTEXITCODE; continuing with a fresh serialized invocation."
    }
}

function Get-MeaningfulGitChanges {
    param([Parameter(Mandatory = $true)][string]$Path)

    Set-Location $Path
    $statusLines = @(& git status --porcelain --untracked-files=all)
    if ($LASTEXITCODE -ne 0) { throw "git status failed in $Path" }

    return @($statusLines | Where-Object {
        if ([string]::IsNullOrWhiteSpace($_)) { return $false }

        $isUntracked = $_.StartsWith("?? ")
        $entryPath = if ($_.Length -gt 3) { $_.Substring(3).Trim() } else { "" }
        $normalized = $entryPath -replace '\\', '/'

        if ($isUntracked) {
            $isGeneratedBuildArtifact = $normalized -match '(^|/)(bin|obj)(/|$)'
            $isVisualStudioState = $normalized -match '^\.vs/'

            if ($isGeneratedBuildArtifact -or $isVisualStudioState) {
                return $false
            }
        }

        return $true
    })
}

function Assert-CleanMain {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Path
    )

    Write-Host ""
    Write-Host "----------------------------------------------------" -ForegroundColor DarkYellow
    Write-Host " Syncing dependency: $Name" -ForegroundColor DarkYellow
    Write-Host "----------------------------------------------------" -ForegroundColor DarkYellow

    if (-not (Test-Path (Join-Path $Path ".git"))) {
        throw "Git repository not found: $Path"
    }

    Set-Location $Path
    $dirty = Get-MeaningfulGitChanges -Path $Path
    if ($dirty) {
        throw "Repository has meaningful uncommitted changes and was not modified: $Path`n$($dirty -join [Environment]::NewLine)"
    }

    Invoke-Native "git" @("fetch", "origin", "--prune")
    Invoke-Native "git" @("switch", "main")
    Invoke-Native "git" @("pull", "--ff-only", "origin", "main")

    $branch = (& git branch --show-current).Trim()
    if ($LASTEXITCODE -ne 0 -or $branch -ne "main") {
        throw "Repository is not on authoritative main: $Path"
    }

    $sha = (& git rev-parse --short HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw "Could not read HEAD for $Path" }
    Write-Host "Authoritative main: $sha" -ForegroundColor DarkCyan
}

$requestPortalPath = Join-Path $Root "RequestPortal"
$requestPortalSolution = Join-Path $requestPortalPath "RequestPortal.slnx"
$deployScript = Join-Path $Root "Aegis.Operations\scripts\Deploy-Kratos.ps1"
$bridgeDeployScript = Join-Path $Root "Aegis.Operations\scripts\Build-Deploy-RequestPortalBridge-Kratos.ps1"

$dependencies = @(
    @{ Name = "Common.Diagnostics";   Path = Join-Path $Root "Common\Common.Diagnostics" },
    @{ Name = "Common.Messaging";     Path = Join-Path $Root "Common\Common.Messaging" },
    @{ Name = "Common.Registration";  Path = Join-Path $Root "Common\Common.Registration" },
    @{ Name = "Common.Secrets";       Path = Join-Path $Root "Common\Common.Secrets" },
    @{ Name = "Common.Security";      Path = Join-Path $Root "Common\Common.Security" },
    @{ Name = "Common.Storage";       Path = Join-Path $Root "Common\Common.Storage" },
    @{ Name = "RequestPortal";         Path = $requestPortalPath }
)

$originalLocation = Get-Location
try {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host " REQUESTPORTAL BUILD / TEST / DEPLOY" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan

    foreach ($dependency in $dependencies) {
        Assert-CleanMain -Name $dependency.Name -Path $dependency.Path
    }

    if (-not (Test-Path $requestPortalSolution)) {
        throw "RequestPortal solution not found: $requestPortalSolution"
    }

    if (-not $SkipTests) {
        $pg = [Environment]::GetEnvironmentVariable("REQUESTPORTAL_POSTGRES", "Process")
        if ([string]::IsNullOrWhiteSpace($pg)) {
            if ($StudioTestPostgresVariable -notmatch '^[A-Za-z_][A-Za-z0-9_]*$') {
                throw "Invalid Studio test environment variable name."
            }
            $studioPg = [Environment]::GetEnvironmentVariable($StudioTestPostgresVariable, "Process")
            if (-not [string]::IsNullOrWhiteSpace($studioPg)) {
                $studioFields = @{}
                foreach ($fragment in ($studioPg -split ';')) {
                    $pair = $fragment.Split('=', 2)
                    if ($pair.Count -eq 2) {
                        $studioFields[$pair[0].Trim().ToLowerInvariant()] = $pair[1].Trim()
                    }
                }
                $studioHost = if ($studioFields.ContainsKey('host')) { $studioFields['host'] } elseif ($studioFields.ContainsKey('server')) { $studioFields['server'] } else { '' }
                $studioDb = if ($studioFields.ContainsKey('database')) { $studioFields['database'] } else { '' }
                if ($studioHost -ne '10.10.0.226' -or $studioDb -notmatch '(?i)(^|[_-])test(s|ing)?($|[_-])') {
                    throw "Studio test source must be an isolated test database on host 10.10.0.226."
                }
                $pg = [regex]::Replace($studioPg, '(?i)(^|;)\s*database\s*=\s*[^;]*', '${1}Database=requestportal_tests')
                [Environment]::SetEnvironmentVariable("REQUESTPORTAL_POSTGRES", $pg, "Process")
            }
        }
        if ([string]::IsNullOrWhiteSpace($pg)) {
            # Explicitly requested demo PostgreSQL target. Never substitute a production DB.
            # Password can be provided through REQUESTPORTAL_DEMO_PG_PASSWORD;
            # when absent, ask interactively rather than committing credentials to Git.
            $demoPassword = [Environment]::GetEnvironmentVariable("REQUESTPORTAL_DEMO_PG_PASSWORD", "Process")
            if ([string]::IsNullOrWhiteSpace($demoPassword)) {
                if ([Console]::IsInputRedirected) {
                    throw "Set REQUESTPORTAL_DEMO_PG_PASSWORD in the current process for unattended demo integration tests."
                }
                $securePassword = Read-Host "PostgreSQL demo password for sa at 10.10.0.226" -AsSecureString
                $demoPassword = [System.Net.NetworkCredential]::new("", $securePassword).Password
            }
            if ($demoPassword.Contains(';')) {
                throw "Demo PostgreSQL password contains an unsupported connection-string delimiter; supply a correctly escaped REQUESTPORTAL_POSTGRES instead."
            }
            $pg = "Host=10.10.0.226;Port=5432;Database=requestportal_tests;Username=sa;Password=$demoPassword;Timeout=10"
            [Environment]::SetEnvironmentVariable("REQUESTPORTAL_POSTGRES", $pg, "Process")
            Write-Host "Using requested PostgreSQL demo host 10.10.0.226, user sa, isolated database requestportal_tests." -ForegroundColor DarkCyan
        }
        if ([string]::IsNullOrWhiteSpace($pg)) {
            throw "Missing REQUESTPORTAL_POSTGRES. Supply a dedicated test-only PostgreSQL connection on 10.10.0.226 or a verified Studio test connection through StudioTestPostgresVariable."
        }
        $parts = @{}
        foreach ($fragment in ($pg -split ';')) {
            $pair = $fragment.Split('=', 2)
            if ($pair.Count -eq 2) {
                $parts[$pair[0].Trim().ToLowerInvariant()] = $pair[1].Trim()
            }
        }
        $pgHost = if ($parts.ContainsKey('host')) { $parts['host'] } elseif ($parts.ContainsKey('server')) { $parts['server'] } else { '' }
        $pgDb = if ($parts.ContainsKey('database')) { $parts['database'] } else { '' }
        if ($pgHost -notin @('localhost', '127.0.0.1', '10.10.0.226') -or $pgDb -ne 'requestportal_tests') {
            throw "Request Portal tests require the isolated requestportal_tests database on localhost or approved host 10.10.0.226."
        }
        Write-Host "Verified Request Portal test database target (credentials redacted)." -ForegroundColor DarkCyan
    }
    Set-Location $requestPortalPath
    Reset-DotNetBuildServers

    Write-Host ""
    Write-Host "====================================================" -ForegroundColor Yellow
    Write-Host " RESTORE RequestPortal" -ForegroundColor Yellow
    Write-Host "====================================================" -ForegroundColor Yellow
    Invoke-Native "dotnet" @(
        "restore", ".\RequestPortal.slnx",
        "--disable-parallel",
        "-m:1",
        "/nodeReuse:false",
        "-p:RestoreDisableParallel=true",
        "/p:UseSharedCompilation=false",
        "--tl:off"
    )

    Write-Host ""
    Write-Host "====================================================" -ForegroundColor Yellow
    Write-Host " BUILD RequestPortal" -ForegroundColor Yellow
    Write-Host "====================================================" -ForegroundColor Yellow
    Invoke-Native "dotnet" @(
        "build", ".\RequestPortal.slnx",
        "--no-restore",
        "--configuration", "Release",
        "-m:1",
        "/nodeReuse:false",
        "/p:UseSharedCompilation=false",
        "--tl:off"
    )

    if (-not $SkipTests) {
        Write-Host ""
        Write-Host "====================================================" -ForegroundColor Yellow
        Write-Host " TEST RequestPortal" -ForegroundColor Yellow
        Write-Host "====================================================" -ForegroundColor Yellow
        Invoke-Native "dotnet" @(
            "test", ".\RequestPortal.slnx",
            "--no-restore",
            "--configuration", "Release",
            "-m:1",
            "/nodeReuse:false",
            "/p:UseSharedCompilation=false",
            "--tl:off"
        )
    }
    else {
        Write-Warning "RequestPortal tests were skipped by request."
    }

    Reset-DotNetBuildServers

    if (-not (Test-Path $deployScript)) {
        throw "Kratos deployment script not found: $deployScript"
    }

    if (-not (Test-Path $bridgeDeployScript)) {
        throw "Bridge deployment script not found: $bridgeDeployScript"
    }
    if (-not $BridgeBaseUrl.IsAbsoluteUri -or $BridgeBaseUrl.Scheme -ne "https" -or
        $BridgeBaseUrl.UserInfo -or $BridgeBaseUrl.Query -or $BridgeBaseUrl.Fragment -or
        $BridgeBaseUrl.AbsolutePath -ne "/") {
        throw "BridgeBaseUrl must be a credential-free HTTPS origin without path/query/fragment."
    }

    # Bridge must be healthy before the live Request Portal is replaced.
    # Reuse the existing guarded, rollback-capable bridge deployment script.
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host " DEPLOY FFP MANAGER BRIDGE TO KRATOS" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    $bridgeArguments = @{
        BridgeBaseUrl = $BridgeBaseUrl
        Root = $Root
        HostName = $HostName
        UserName = $UserName
        SkipTests = $SkipTests
        KeepLocalBundle = $KeepLocalBundle
        KeepRemoteBundle = $KeepRemoteBundle
    }
    if (-not [string]::IsNullOrWhiteSpace($IdentityFile)) {
        $bridgeArguments.IdentityFile = $IdentityFile
    }
    & $bridgeDeployScript @bridgeArguments
    if (-not $?) {
        throw "Bridge deployment failed; Request Portal deployment was not started."
    }

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host " DEPLOY RequestPortal TO KRATOS" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Publishing RequestPortal for Linux x64 using the shared deployment script." -ForegroundColor DarkCyan

    $deployArguments = @{
        Applications = @("requestportal")
        ProjectsRoot = $Root
        HostName = $HostName
        UserName = $UserName
        SkipAuthoritativeBuild = $true
        SkipPublishBuild = $false
        KeepLocalBundle = $KeepLocalBundle
        KeepRemoteBundle = $KeepRemoteBundle
    }
    if (-not [string]::IsNullOrWhiteSpace($IdentityFile)) {
        $deployArguments.IdentityFile = $IdentityFile
    }

    & $deployScript @deployArguments
    if (-not $? -or $LASTEXITCODE -ne 0) {
        throw "RequestPortal deployment failed with exit code $LASTEXITCODE."
    }

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host " REQUESTPORTAL + FFP BRIDGE BUILD / DEPLOY COMPLETE" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
}
finally {
    Set-Location $originalLocation
}
