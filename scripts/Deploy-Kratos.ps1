[CmdletBinding()]
param(
    [string]$HostName = "kratos",
    [string]$UserName = "root",
    [ValidateSet("operations","configuration","diagnostics")]
    [string[]]$Applications = @("operations","configuration","diagnostics"),
    [string]$ProjectsRoot = "D:\Projects",
    [string]$RemoteArchive = "/tmp/aegis-control-plane.tgz",
    [string]$RemoteWorkRoot = "/tmp/aegis-deploy",
    [string]$IdentityFile,
    [System.Management.Automation.PSCredential]$Credential,
    [switch]$SkipAuthoritativeBuild,
    [switch]$KeepLocalBundle,
    [switch]$KeepRemoteBundle
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-Native {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][string[]]$ArgumentList
    )
    & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath exited with code $LASTEXITCODE: $($ArgumentList -join ' ')"
    }
}

function ConvertTo-PlainText {
    param([Parameter(Mandatory)][Security.SecureString]$SecureString)
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureString)
    try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$serverScript = Join-Path $PSScriptRoot "Deploy-Kratos-Server.sh"
if (-not (Test-Path $serverScript)) {
    throw "Missing server deployment script: $serverScript"
}

$definitions = @{
    operations = @{
        Project = Join-Path $ProjectsRoot "Aegis.Operations\Aegis.Operations.csproj"
        Repo    = Join-Path $ProjectsRoot "Aegis.Operations"
    }
    configuration = @{
        Project = Join-Path $ProjectsRoot "Aegis.Configuration\Aegis.Configuration.Web\Aegis.Configuration.Web.csproj"
        Repo    = Join-Path $ProjectsRoot "Aegis.Configuration"
    }
    diagnostics = @{
        Project = Join-Path $ProjectsRoot "Aegis.Diagnostics\Aegis.Diagnostics.csproj"
        Repo    = Join-Path $ProjectsRoot "Aegis.Diagnostics"
    }
}

$Applications = $Applications | ForEach-Object { $_.ToLowerInvariant() } | Select-Object -Unique

if (-not $SkipAuthoritativeBuild) {
    Write-Host "`n========================================"
    Write-Host " AUTHORITATIVE AEGIS BUILD / TEST"
    Write-Host "========================================`n"
    $buildAll = Join-Path $PSScriptRoot "Build-All-Aegis.ps1"
    if (-not (Test-Path $buildAll)) { throw "Missing authoritative build script: $buildAll" }
    & $buildAll
    if ($LASTEXITCODE -ne 0) { throw "Authoritative build failed." }
}

$bundleRoot = Join-Path ([IO.Path]::GetTempPath()) ("aegis-kratos-deploy-" + [Guid]::NewGuid().ToString("N"))
$payloadRoot = Join-Path $bundleRoot "payload"
New-Item -ItemType Directory -Force -Path $payloadRoot | Out-Null

try {
    foreach ($app in $Applications) {
        $definition = $definitions[$app]
        if (-not $definition) { throw "Unsupported application: $app" }
        if (-not (Test-Path $definition.Project)) { throw "Project not found: $($definition.Project)" }

        $output = Join-Path $payloadRoot $app
        New-Item -ItemType Directory -Force -Path $output | Out-Null

        Write-Host "`n========================================"
        Write-Host " PUBLISH $app"
        Write-Host "========================================"
        Invoke-Native -FilePath "dotnet" -ArgumentList @(
            "publish", $definition.Project,
            "--configuration", "Release",
            "--output", $output,
            "--no-restore"
        )
    }

    Copy-Item $serverScript (Join-Path $bundleRoot "Deploy-Kratos-Server.sh") -Force

    $bundleFile = Join-Path ([IO.Path]::GetTempPath()) ("aegis-control-plane-" + [Guid]::NewGuid().ToString("N") + ".tgz")
    Push-Location $bundleRoot
    try {
        Invoke-Native -FilePath "tar.exe" -ArgumentList @("-czf", $bundleFile, ".")
    }
    finally { Pop-Location }

    $remote = "$UserName@$HostName"
    $appsCsv = ($Applications -join ",")
    $remoteCommand = @"
set -e
rm -rf '$RemoteWorkRoot'
mkdir -p '$RemoteWorkRoot'
tar -xzf '$RemoteArchive' -C '$RemoteWorkRoot'
chmod +x '$RemoteWorkRoot/Deploy-Kratos-Server.sh'
bash '$RemoteWorkRoot/Deploy-Kratos-Server.sh' --source '$RemoteWorkRoot/payload' --apps '$appsCsv'
"@
    if (-not $KeepRemoteBundle) {
        $remoteCommand += "`nrm -f '$RemoteArchive'`n"
    }

    if ($Credential) {
        $plink = Get-Command plink.exe -ErrorAction SilentlyContinue
        $pscp = Get-Command pscp.exe -ErrorAction SilentlyContinue
        if (-not $plink -or -not $pscp) {
            throw "-Credential requires PuTTY plink.exe and pscp.exe on PATH. Without -Credential, native OpenSSH will prompt interactively. For non-interactive auth, -IdentityFile is preferred."
        }

        if ($Credential.UserName) { $UserName = $Credential.UserName; $remote = "$UserName@$HostName" }
        $password = ConvertTo-PlainText $Credential.Password
        try {
            Write-Host "`nUploading deployment bundle to $remote ..."
            Invoke-Native -FilePath $pscp.Source -ArgumentList @("-batch", "-pw", $password, $bundleFile, "${remote}:$RemoteArchive")

            Write-Host "`nExecuting deployment remotely on $HostName ..."
            Invoke-Native -FilePath $plink.Source -ArgumentList @("-batch", "-pw", $password, $remote, $remoteCommand)
        }
        finally {
            $password = $null
        }
    }
    else {
        $scpArgs = @()
        $sshArgs = @()
        if ($IdentityFile) {
            $scpArgs += @("-i", $IdentityFile)
            $sshArgs += @("-i", $IdentityFile)
        }

        Write-Host "`nUploading deployment bundle to $remote ..."
        Write-Host "If SSH key authentication is not configured, SCP will prompt for the SSH password."
        Invoke-Native -FilePath "scp" -ArgumentList ($scpArgs + @($bundleFile, "${remote}:$RemoteArchive"))

        Write-Host "`nExecuting deployment remotely on $HostName ..."
        Write-Host "SSH may prompt for the password again when password authentication is used."
        Invoke-Native -FilePath "ssh" -ArgumentList ($sshArgs + @($remote, $remoteCommand))
    }

    Write-Host "`n========================================"
    Write-Host " KRATOS DEPLOYMENT COMPLETE"
    Write-Host "========================================"
    Write-Host "Applications: $appsCsv"
    Write-Host "Host:         $HostName"
}
finally {
    if (Test-Path $bundleRoot) { Remove-Item $bundleRoot -Recurse -Force -ErrorAction SilentlyContinue }
    if ($bundleFile -and -not $KeepLocalBundle -and (Test-Path $bundleFile)) {
        Remove-Item $bundleFile -Force -ErrorAction SilentlyContinue
    }
}
