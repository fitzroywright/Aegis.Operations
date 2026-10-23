[CmdletBinding()]
param(
    [string]$ProjectsRoot = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)),
    [string]$HostName = "kratos",
    [string]$UserName = "fwright@fftpj.local",
    [string]$IdentityFile,
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
        throw "$FilePath exited with code $($LASTEXITCODE): $($Arguments -join ' ')"
    }
}

if (-not (Test-Path -LiteralPath $ProjectsRoot -PathType Container)) {
    throw "Projects root does not exist: $ProjectsRoot"
}
$ProjectsRoot = (Resolve-Path -LiteralPath $ProjectsRoot).Path
$publicSitePath = Join-Path (Join-Path $ProjectsRoot "Aegis.Studio") "Aegis.Studio.PublicSite"
$deployScript = Join-Path (Join-Path (Join-Path $ProjectsRoot "Aegis.Operations") "scripts") "Deploy-Kratos.ps1"

if (-not (Test-Path (Join-Path $publicSitePath "package.json"))) {
    throw "Bryan's Public Site project was not found: $publicSitePath"
}
if (-not (Test-Path $deployScript)) {
    throw "Shared Kratos deployment script was not found: $deployScript"
}

$node = Get-Command node -ErrorAction SilentlyContinue
$npm = Get-Command npm -ErrorAction SilentlyContinue
if (-not $node -or -not $npm) {
    throw "Install Node.js 24+ on the Mac and ensure node and npm are on PATH."
}
$nodeVersionText = (& $node.Source --version).Trim()
if ($LASTEXITCODE -ne 0 -or $nodeVersionText -notmatch '^v(\d+)\.') {
    throw "Could not determine Node.js version from '$nodeVersionText'."
}
if ([int]$Matches[1] -lt 24) {
    throw "Bryan's Public Site requires Node.js 24+. Found $nodeVersionText."
}

$originalLocation = Get-Location
try {
    Set-Location $publicSitePath
    Write-Host "Installing Public Site test dependencies..." -ForegroundColor DarkCyan
    Invoke-Native $npm.Source @("install", "--no-audit", "--no-fund")

    Write-Host ""
    Write-Host "Running Bryan's Public Site tests on this Mac..." -ForegroundColor Cyan
    Invoke-Native $npm.Source @("test")

    Write-Host ""
    Write-Host "Deploying only the Public Site to Kratos..." -ForegroundColor Cyan
    $deployArguments = @{
        Applications = @("publicsite")
        ProjectsRoot = $ProjectsRoot
        HostName = $HostName
        UserName = $UserName
        SkipAuthoritativeBuild = $true
        SkipPublishBuild = $true
        KeepLocalBundle = $KeepLocalBundle
        KeepRemoteBundle = $KeepRemoteBundle
    }
    if (-not [string]::IsNullOrWhiteSpace($IdentityFile)) {
        $deployArguments.IdentityFile = $IdentityFile
    }

    & $deployScript @deployArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Bryan's Public Site deployment failed with exit code $($LASTEXITCODE)."
    }

    Write-Host ""
    Write-Host "Deployment and local Kratos health check completed." -ForegroundColor Green
    Write-Host "Service: aegis-public-site (Node 24 at /opt/nodejs/current)"
    Write-Host "API host: https://api.bryanstudiosltd.com (uses the existing pfSense wildcard certificate)"
}
finally {
    Set-Location $originalLocation
}
