param(
    // Infer the sibling-repository root from this checkout on every PowerShell version.
    [string]$Root = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)),
    [switch]$ControlPlaneOnly,
    [string]$CommonMessagingPostgres = $env:COMMON_MESSAGING_POSTGRES
)

$ErrorActionPreference = "Stop"

$testEnvironmentScript = Join-Path $PSScriptRoot "Set-CommonMessagingPostgres.ps1"
if (-not (Test-Path $testEnvironmentScript)) {
    throw "Missing test environment setup script: $testEnvironmentScript"
}

Write-Host "Loading shared Aegis test environment..." -ForegroundColor DarkCyan
& $testEnvironmentScript

# Parameter defaults are evaluated before the setup script runs. Preserve an
# explicitly supplied value, otherwise refresh from the environment we just set.
if ([string]::IsNullOrWhiteSpace($CommonMessagingPostgres)) {
    $CommonMessagingPostgres = $env:COMMON_MESSAGING_POSTGRES
}

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

function Get-SafeRestoreArguments {
    param([Parameter(Mandatory = $true)][string]$Target)
    return @(
        "restore", $Target,
        "--disable-parallel",
        "-m:1",
        "/nodeReuse:false",
        "-p:RestoreDisableParallel=true",
        "/p:UseSharedCompilation=false",
        "--tl:off"
    )
}

function Get-SafeBuildArguments {
    param([Parameter(Mandatory = $true)][string]$Target)
    return @(
        "build", $Target,
        "--no-restore",
        "--configuration", "Release",
        "-m:1",
        "/nodeReuse:false",
        "/p:UseSharedCompilation=false",
        "--tl:off"
    )
}

function Get-SafeTestArguments {
    param([Parameter(Mandatory = $true)][string]$Target)
    return @(
        "test", $Target,
        "--no-restore",
        "--configuration", "Release",
        "-m:1",
        "/nodeReuse:false",
        "/p:UseSharedCompilation=false",
        "--tl:off"
    )
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

        # Only ignore known generated files when they are untracked. If any of these
        # paths are tracked and modified, they remain meaningful and stop the build.
        if ($isUntracked) {
            $isGeneratedBuildArtifact = $normalized -match '(^|/)(bin|obj)(/|$)'
            $isVisualStudioState = $normalized -match '^\.vs/'
            $isConfigurationContractSnapshot = $normalized -match '^Aegis\.Configuration\.Web/contracts/.*\.configuration-contract\.json$'
            $isConfigurationCommissioningHistory = $normalized -eq 'Aegis.Configuration.Web/data/commissioning-history.jsonl'

            if ($isGeneratedBuildArtifact -or $isVisualStudioState -or $isConfigurationContractSnapshot -or $isConfigurationCommissioningHistory) {
                return $false
            }
        }

        return $true
    })
}

function Assert-CleanMain {
    param([Parameter(Mandatory = $true)][string]$Path)

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

function Sync-AuthoritativeMain {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Path
    )

    Write-Host ""
    Write-Host "----------------------------------------------------" -ForegroundColor DarkYellow
    Write-Host " Syncing dependency: $Name" -ForegroundColor DarkYellow
    Write-Host "----------------------------------------------------" -ForegroundColor DarkYellow
    Assert-CleanMain $Path
}

function Build-Project {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$BuildTarget,
        [string]$TestTarget,
        [hashtable]$TestEnvironment
    )

    Write-Host "`n====================================================" -ForegroundColor Yellow
    Write-Host " $Name" -ForegroundColor Yellow
    Write-Host "====================================================" -ForegroundColor Yellow

    Assert-CleanMain $Path
    Reset-DotNetBuildServers

    Invoke-Native "dotnet" (Get-SafeRestoreArguments -Target $BuildTarget)
    Invoke-Native "dotnet" (Get-SafeBuildArguments -Target $BuildTarget)

    if ($TestTarget) {
        if (-not [string]::Equals($BuildTarget, $TestTarget, [StringComparison]::OrdinalIgnoreCase)) {
            Invoke-Native "dotnet" (Get-SafeRestoreArguments -Target $TestTarget)
        }

        $previousEnvironment = @{}
        try {
            if ($TestEnvironment) {
                foreach ($name in $TestEnvironment.Keys) {
                    $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, "Process")
                    [Environment]::SetEnvironmentVariable($name, [string]$TestEnvironment[$name], "Process")
                }
            }

            Invoke-Native "dotnet" (Get-SafeTestArguments -Target $TestTarget)
        }
        finally {
            if ($TestEnvironment) {
                foreach ($name in $TestEnvironment.Keys) {
                    [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], "Process")
                }
            }
        }
    }

    Write-Host "PASS: $Name" -ForegroundColor Green
}

$projects = @(
    @{ Name = "Common.Diagnostics";  Path = "$Root\Common\Common.Diagnostics";  Build = ".\Common.Diagnostics.csproj"; Test = ".\Common.Diagnostics.UnitTests\Common.Diagnostics.UnitTests.csproj" },
    @{ Name = "Common.Messaging";    Path = "$Root\Common\Common.Messaging";    Build = ".\Common.Messaging.csproj";   Test = ".\Common.Messaging.UnitTests\Common.Messaging.UnitTests.csproj"; RequiresCommonMessagingPostgres = $true },
    @{ Name = "Common.Registration"; Path = "$Root\Common\Common.Registration"; Build = ".\Common.Registration.csproj"; Test = ".\Common.Registration.Tests\Common.Registration.Tests.csproj" },
    @{ Name = "Common.Secrets";      Path = "$Root\Common\Common.Secrets";      Build = ".\Common.Secrets.slnx";       Test = ".\Common.Secrets.slnx" },
    @{ Name = "Common.Security";     Path = "$Root\Common\Common.Security";     Build = ".\Common.Security.slnx";      Test = ".\Common.Security.slnx" },
    @{ Name = "Common.Storage";      Path = "$Root\Common\Common.Storage";      Build = ".\Common.Storage.slnx";       Test = ".\Common.Storage.slnx" },
    @{ Name = "Aegis.Configuration"; Path = "$Root\Aegis.Configuration";        Build = ".\Aegis.Configuration.slnx";  Test = ".\Aegis.Configuration.slnx" },
    @{ Name = "Aegis.Diagnostics";   Path = "$Root\Aegis.Diagnostics";          Build = ".\Aegis.Diagnostics.slnx";    Test = ".\Aegis.Diagnostics.slnx" },
    @{ Name = "Aegis.Operations";    Path = "$Root\Aegis.Operations";           Build = ".\Aegis.Operations.slnx";     Test = ".\Aegis.Operations.slnx" },
    @{ Name = "Aegis.Hello";         Path = "$Root\Aegis.Hello";                Build = ".\Aegis.Hello.slnx";          Test = ".\Aegis.Hello.slnx" },
    @{ Name = "Aegis.Cafeteria";     Path = "$Root\Aegis.Cafeteria";            Build = ".\Aegis.Cafeteria.slnx";      Test = ".\Aegis.Cafeteria.slnx" },
    @{ Name = "Aegis.Studio";        Path = "$Root\Aegis.Studio";               Build = ".\Aegis.Studio.slnx";         Test = ".\Aegis.Studio.slnx" },
    @{ Name = "RequestPortal";       Path = "$Root\RequestPortal";              Build = ".\RequestPortal.slnx";        Test = ".\RequestPortal.slnx" },
    @{ Name = "Aegis.SensorNetwork"; Path = "$Root\Aegis.SensorNetwork";        Build = ".\Aegis.SensorNetwork.slnx";  Test = ".\Aegis.SensorNetwork.slnx"; UsesFfpManagerTest = $true },
    @{ Name = "Ebolito";             Path = "$Root\Ebolito";                    Build = ".\Ebolito.slnx";              Test = ".\Ebolito.slnx" }
)

if ($ControlPlaneOnly) {
    $controlPlaneNames = @("Common.Diagnostics", "Common.Messaging", "Common.Registration", "Common.Secrets", "Common.Security", "Common.Storage", "Aegis.Configuration", "Aegis.Diagnostics", "Aegis.Operations")
    $projects = @($projects | Where-Object { $_.Name -in $controlPlaneNames })
}

$originalLocation = Get-Location
try {
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host " AEGIS AUTHORITATIVE MAIN BUILD / TEST" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan

    # Synchronize every repository before any solution build starts. Some solutions
    # reference sibling repositories directly, so building before those siblings are
    # updated can compile stale source from disk.
    $synced = @{}
    foreach ($project in $projects) {
        if (-not $synced.ContainsKey($project.Path)) {
            Sync-AuthoritativeMain -Name $project.Name -Path $project.Path
            $synced[$project.Path] = $true
        }
    }

    if (-not $ControlPlaneOnly) {
        $ffpManagerProvisioning = Join-Path $PSScriptRoot "Initialize-FfpManagerTestIntegration.ps1"
        if (-not (Test-Path $ffpManagerProvisioning)) {
            throw "Missing FFP Manager test integration provisioning script: $ffpManagerProvisioning"
        }

        Write-Host ""
        Write-Host "Provisioning shared FFPMANAGER_TEST integration schema..." -ForegroundColor DarkCyan
        & $ffpManagerProvisioning -ProjectsRoot $Root

    } else {
        Write-Host "Control Plane scope: FFP Manager test integration excluded." -ForegroundColor DarkCyan
    }

    foreach ($project in $projects) {
        $testEnvironment = $null
        if ($project.ContainsKey("RequiresCommonMessagingPostgres") -and [bool]$project["RequiresCommonMessagingPostgres"]) {
            if ([string]::IsNullOrWhiteSpace($CommonMessagingPostgres)) {
                throw "Common.Messaging PostgreSQL integration tests require a connection string. Supply -CommonMessagingPostgres or set COMMON_MESSAGING_POSTGRES before running this script."
            }

            $testEnvironment = @{ COMMON_MESSAGING_POSTGRES = $CommonMessagingPostgres }
            Write-Host "Common.Messaging PostgreSQL integration environment configured (value redacted)." -ForegroundColor DarkCyan
        }

        if ($project.ContainsKey("UsesFfpManagerTest") -and [bool]$project["UsesFfpManagerTest"]) {
            if ($null -eq $testEnvironment) {
                $testEnvironment = @{}
            }

            $testEnvironment["FFPMANAGER_TEST"] = $env:FFPMANAGER_TEST
            $testEnvironment["SENSORNETWORK_RUN_PROVIDER_DB_TESTS"] = "1"
            Write-Host "FFP Manager provider integration tests enabled against FFPMANAGER_TEST." -ForegroundColor DarkCyan
        }

        Build-Project -Name $project.Name -Path $project.Path -BuildTarget $project.Build -TestTarget $project.Test -TestEnvironment $testEnvironment
    }

    Reset-DotNetBuildServers

    Write-Host "`n========================================" -ForegroundColor Green
    Write-Host " ALL AUTHORITATIVE MAIN BUILDS/TESTS PASSED" -ForegroundColor Green
    Write-Host "========================================`n" -ForegroundColor Green
}
finally {
    Set-Location $originalLocation
}
