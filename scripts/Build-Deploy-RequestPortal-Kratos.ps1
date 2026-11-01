[CmdletBinding()]
param(
    [string]$Root = $(if ($IsWindows) { "D:\Projects" } else { Join-Path $HOME "Projects" }),
    [string]$HostName = "kratos",
    [string]$UserName = "fwright@fftpj.local",
    [string]$IdentityFile,
    [switch]$SkipTests,
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

    if (-not $SkipTests -and [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable("REQUESTPORTAL_POSTGRES"))) {
        throw "REQUESTPORTAL_POSTGRES is required for integration tests. Configure an isolated test-only PostgreSQL database; refusing to deploy without tested prerequisites."
    }
    # Guard against accidentally running destructive integration tests on the live portal database.
    if (-not $SkipTests) {
        $testPg = [Environment]::GetEnvironmentVariable("REQUESTPORTAL_POSTGRES")
        if ($testPg -match '(?i)(database|initial catalog)\s*=\s*(requestportal|postgres|template0|template1)\s*(;|$)') {
            throw "Refusing generic/production-looking PostgreSQL database name in REQUESTPORTAL_POSTGRES. Supply a dedicated isolated test database."
        }
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
