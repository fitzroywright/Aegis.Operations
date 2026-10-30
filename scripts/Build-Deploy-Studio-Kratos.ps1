[CmdletBinding()]
param(
    [string]$Root,
    [string]$HostName = "kratos",
    [string]$UserName = "fwright@fftpj.local",
    [string]$IdentityFile,
    [string]$ExpectedStudioCommit,
    [string]$StudioDemoCutoverDirectory,
    [switch]$SkipTests,
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

    if ($FilePath -eq "dotnet" -and -not [string]::IsNullOrWhiteSpace($script:studioPinnedCommonRoot)) {
        $Arguments += "-p:CommonRoot=$script:studioPinnedCommonRoot"
    }
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

if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
}
if (-not (Test-Path -LiteralPath $Root -PathType Container)) {
    throw "Projects root does not exist: $Root"
}
$Root = (Resolve-Path -LiteralPath $Root).Path

$studioPath = Join-Path $Root "Aegis.Studio"
$studioSolution = Join-Path $studioPath "Aegis.Studio.slnx"
$deployScript = Join-Path $Root "Aegis.Operations\scripts\Deploy-Kratos.ps1"

$dependencies = @(
    @{ Name = "Common.Diagnostics";   Path = Join-Path $Root "Common\Common.Diagnostics" },
    @{ Name = "Common.Messaging";     Path = Join-Path $Root "Common\Common.Messaging" },
    @{ Name = "Common.Registration";  Path = Join-Path $Root "Common\Common.Registration" },
    @{ Name = "Common.Secrets";       Path = Join-Path $Root "Common\Common.Secrets" },
    @{ Name = "Common.Security";      Path = Join-Path $Root "Common\Common.Security" },
    @{ Name = "Common.Storage";       Path = Join-Path $Root "Common\Common.Storage" },
    @{ Name = "Aegis.Studio";         Path = $studioPath }
)

$script:studioPinnedCommonRoot = $null
$pinnedDependencies = $null
$originalLocation = Get-Location
try {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host " AEGIS.STUDIO BUILD / TEST / DEPLOY" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan

    foreach ($dependency in $dependencies) {
        Assert-CleanMain -Name $dependency.Name -Path $dependency.Path
    }

    if ($StudioDemoCutoverDirectory -and -not $ExpectedStudioCommit) { throw "Demo cutover requires the exact reviewed Studio main commit." }
    if ($ExpectedStudioCommit) {
        if ($ExpectedStudioCommit -notmatch '^[a-f0-9]{40}$') { throw "ExpectedStudioCommit must be a full Git SHA." }
        $actualCommit = (& git -C $studioPath rev-parse HEAD).Trim()
        if ($LASTEXITCODE -ne 0 -or $actualCommit -ne $ExpectedStudioCommit) { throw "Studio main changed after approval/preparation; deployment stopped." }
    }
    . (Join-Path $PSScriptRoot "Studio-PinnedDependencies.ps1")
    $pinnedDependencies = New-StudioPinnedDependencies -ProjectsRoot $Root -StudioPath $studioPath
    $script:studioPinnedCommonRoot = $pinnedDependencies.CommonRoot
    $studioSolution = $pinnedDependencies.Solution

    if (-not (Test-Path $studioSolution)) {
        throw "Studio solution not found: $studioSolution"
    }

    Set-Location $studioPath
    Reset-DotNetBuildServers

    Write-Host ""
    Write-Host "====================================================" -ForegroundColor Yellow
    Write-Host " RESTORE Aegis.Studio" -ForegroundColor Yellow
    Write-Host "====================================================" -ForegroundColor Yellow
    Invoke-Native "dotnet" @(
        "restore", $studioSolution,
        "--disable-parallel",
        "-m:1",
        "/nodeReuse:false",
        "-p:RestoreDisableParallel=true",
        "/p:UseSharedCompilation=false",
        "--tl:off"
    )

    Write-Host ""
    Write-Host "====================================================" -ForegroundColor Yellow
    Write-Host " BUILD Aegis.Studio" -ForegroundColor Yellow
    Write-Host "====================================================" -ForegroundColor Yellow
    Invoke-Native "dotnet" @(
        "build", $studioSolution,
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
        Write-Host " TEST Aegis.Studio" -ForegroundColor Yellow
        Write-Host "====================================================" -ForegroundColor Yellow
        Invoke-Native "dotnet" @(
            "test", $studioSolution,
            "--no-restore",
            "--configuration", "Release",
            "-m:1",
            "/nodeReuse:false",
            "/p:UseSharedCompilation=false",
            "--tl:off"
        )
    }
    else {
        Write-Warning "Studio tests were skipped by request."
    }

    Reset-DotNetBuildServers

    if (-not (Test-Path $deployScript)) {
        throw "Kratos deployment script not found: $deployScript"
    }

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host " DEPLOY Aegis.Studio TO KRATOS" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Publishing framework-dependent linux-x64 assets for Kratos after the validated build/tests." -ForegroundColor DarkCyan

    $deployArguments = @{
        Applications = @("studio")
        ProjectsRoot = $Root
        CommonRoot = $script:studioPinnedCommonRoot
        StudioDemoCutoverDirectory = $StudioDemoCutoverDirectory
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
    if ($LASTEXITCODE -ne 0) {
        throw "Studio deployment failed with exit code $LASTEXITCODE."
    }

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host " AEGIS.STUDIO BUILD / DEPLOY COMPLETE" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
}
finally {
    Set-Location $originalLocation
    if ($null -ne $pinnedDependencies) { Remove-StudioPinnedDependencies -Dependencies $pinnedDependencies }
    $script:studioPinnedCommonRoot = $null
}
