param(
    [string]$Root = "D:\Projects"
)

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

function Assert-CleanMain {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path (Join-Path $Path ".git"))) {
        throw "Git repository not found: $Path"
    }

    Set-Location $Path
    $dirty = (& git status --porcelain)
    if ($LASTEXITCODE -ne 0) { throw "git status failed in $Path" }
    if ($dirty) {
        throw "Repository has uncommitted changes and was not modified: $Path`n$($dirty -join [Environment]::NewLine)"
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

function Build-Project {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$BuildTarget,
        [string]$TestTarget
    )

    Write-Host "`n====================================================" -ForegroundColor Yellow
    Write-Host " $Name" -ForegroundColor Yellow
    Write-Host "====================================================" -ForegroundColor Yellow

    Assert-CleanMain $Path

    Invoke-Native "dotnet" @("restore", $BuildTarget)
    Invoke-Native "dotnet" @("build", $BuildTarget, "--no-restore", "--configuration", "Release")

    if ($TestTarget) {
        Invoke-Native "dotnet" @("restore", $TestTarget)
        Invoke-Native "dotnet" @("test", $TestTarget, "--no-restore", "--configuration", "Release")
    }

    Write-Host "PASS: $Name" -ForegroundColor Green
}

$projects = @(
    @{ Name = "Common.Diagnostics";  Path = "$Root\Common\Common.Diagnostics";  Build = ".\Common.Diagnostics.csproj"; Test = ".\Common.Diagnostics.UnitTests\Common.Diagnostics.UnitTests.csproj" },
    @{ Name = "Common.Messaging";    Path = "$Root\Common\Common.Messaging";    Build = ".\Common.Messaging.csproj";   Test = ".\Common.Messaging.UnitTests\Common.Messaging.UnitTests.csproj" },
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
    @{ Name = "Aegis.SensorNetwork"; Path = "$Root\Aegis.SensorNetwork";        Build = ".\Aegis.SensorNetwork.slnx";  Test = ".\Aegis.SensorNetwork.slnx" },
    @{ Name = "Ebolito";             Path = "$Root\Ebolito";                    Build = ".\Ebolito.slnx";              Test = ".\Ebolito.slnx" }
)

$originalLocation = Get-Location
try {
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host " AEGIS AUTHORITATIVE MAIN BUILD / TEST" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan

    foreach ($project in $projects) {
        Build-Project -Name $project.Name -Path $project.Path -BuildTarget $project.Build -TestTarget $project.Test
    }

    Write-Host "`n========================================" -ForegroundColor Green
    Write-Host " ALL AUTHORITATIVE MAIN BUILDS/TESTS PASSED" -ForegroundColor Green
    Write-Host "========================================`n" -ForegroundColor Green
}
finally {
    Set-Location $originalLocation
}
