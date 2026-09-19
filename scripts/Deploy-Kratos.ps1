[CmdletBinding()]
param(
    [string]$HostName = "kratos",
    [string]$UserName = "fwright@fftpj.local",
    [ValidateSet("operations","configuration","diagnostics","hello","requestportal")]
    [string[]]$Applications = @("operations","configuration","diagnostics","hello"),
    [string]$ProjectsRoot = "D:\Projects",
    [string]$RemoteArchive = "/tmp/aegis-control-plane.tgz",
    [string]$RemoteWorkRoot = "/tmp/aegis-deploy",
    [string]$RemoteDeployCommand = "/usr/local/sbin/aegis-deploy",
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
        throw "$FilePath exited with code ${LASTEXITCODE}: $($ArgumentList -join ' ')"
    }
}

function ConvertTo-PlainText {
    param([Parameter(Mandatory)][Security.SecureString]$SecureString)
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureString)
    try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
}

$definitions = @{
    operations = @{
        Project = Join-Path $ProjectsRoot "Aegis.Operations\Aegis.Operations.csproj"
    }
    configuration = @{
        Project = Join-Path $ProjectsRoot "Aegis.Configuration\Aegis.Configuration.Web\Aegis.Configuration.Web.csproj"
    }
    diagnostics = @{
        Project = Join-Path $ProjectsRoot "Aegis.Diagnostics\Aegis.Diagnostics.csproj"
    }
    hello = @{
        Project = Join-Path $ProjectsRoot "Aegis.Hello\Aegis.Hello.csproj"
    }
    requestportal = @{
        Project = Join-Path $ProjectsRoot "RequestPortal\RequestPortal.Web\RequestPortal.Web.csproj"
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
}

$bundleRoot = Join-Path ([IO.Path]::GetTempPath()) ("aegis-kratos-deploy-" + [Guid]::NewGuid().ToString("N"))
$payloadRoot = Join-Path $bundleRoot "payload"
$bundleFile = $null
New-Item -ItemType Directory -Force -Path $payloadRoot | Out-Null

$serverDeployScript = Join-Path $PSScriptRoot "Deploy-Kratos-Server.sh"
if (-not (Test-Path $serverDeployScript)) {
    throw "Missing Kratos server deployer: $serverDeployScript"
}
Copy-Item -Force $serverDeployScript (Join-Path $bundleRoot "Deploy-Kratos-Server.sh")

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
            "--output", $output
        )
    }

    $bundleFile = Join-Path ([IO.Path]::GetTempPath()) ("aegis-control-plane-" + [Guid]::NewGuid().ToString("N") + ".tgz")
    Push-Location $bundleRoot
    try {
        Invoke-Native -FilePath "tar.exe" -ArgumentList @("-czf", $bundleFile, ".")
    }
    finally { Pop-Location }

    $remoteDisplay = "$UserName@$HostName"
    $appsCsv = ($Applications -join ",")

    # Build the remote shell program from individual lines joined with LF only.
    # PowerShell here-strings on Windows use CRLF, which causes bash to see tokens
    # such as `-e\r`. Keep this command explicitly Unix-normalized.
    $remoteLines = @(
        "set -e",
        "rm -rf '$RemoteWorkRoot'",
        "mkdir -p '$RemoteWorkRoot'",
        "tar -xzf '$RemoteArchive' -C '$RemoteWorkRoot'",
        "if sudo -n install -o root -g root -m 0755 '$RemoteWorkRoot/Deploy-Kratos-Server.sh' '$RemoteDeployCommand' 2>/dev/null; then",
        "  echo 'Updated Aegis server deployer.'",
        "elif sudo -n test -x '$RemoteDeployCommand' 2>/dev/null; then",
        "  echo 'NOTICE: Cannot update $RemoteDeployCommand without sudo password; reusing existing installed deployer.'",
        "else",
        "  echo 'ERROR: $RemoteDeployCommand is not installed and passwordless sudo is unavailable for installation.' >&2",
        "  echo 'Run the one-time sudoers setup for the Aegis deploy account, then retry.' >&2",
        "  exit 4",
        "fi",
        "if sudo -n '$RemoteDeployCommand' --source '$RemoteWorkRoot/payload' --apps '$appsCsv'; then",
        "  deploy_rc=0",
        "else",
        "  deploy_rc=`$?",
        "fi"
    )
    if (-not $KeepRemoteBundle) {
        $remoteLines += "rm -f '$RemoteArchive'"
    }
    $remoteLines += "exit `$deploy_rc"
    $remoteCommand = $remoteLines -join "`n"

    if ($Credential) {
        $plink = Get-Command plink.exe -ErrorAction SilentlyContinue
        $pscp = Get-Command pscp.exe -ErrorAction SilentlyContinue
        if (-not $plink -or -not $pscp) {
            throw "-Credential requires PuTTY plink.exe and pscp.exe on PATH. Without -Credential, native OpenSSH prompts interactively. For unattended auth, -IdentityFile is preferred."
        }

        if ($Credential.UserName) {
            $UserName = $Credential.UserName
            $remoteDisplay = "$UserName@$HostName"
        }

        $password = ConvertTo-PlainText $Credential.Password
        try {
            Write-Host "`nUploading deployment bundle to $remoteDisplay ..."
            Invoke-Native -FilePath $pscp.Source -ArgumentList @("-batch", "-pw", $password, "-l", $UserName, $bundleFile, "${HostName}:$RemoteArchive")

            Write-Host "`nExecuting deployment remotely on $HostName ..."
            Invoke-Native -FilePath $plink.Source -ArgumentList @("-batch", "-pw", $password, "-l", $UserName, $HostName, $remoteCommand)
        }
        finally {
            $password = $null
        }
    }
    else {
        $scpArgs = @("-o", "BatchMode=yes")
        $sshArgs = @("-o", "BatchMode=yes")
        if ($IdentityFile) {
            $scpArgs += @("-i", $IdentityFile)
            $sshArgs += @("-i", $IdentityFile)
        }

        Write-Host "`nUploading deployment bundle to $remoteDisplay ..."
        Write-Host "Using non-interactive SSH authentication (BatchMode=yes)."
        Invoke-Native -FilePath "scp" -ArgumentList ($scpArgs + @("-o", "User=$UserName", $bundleFile, "${HostName}:$RemoteArchive"))

        Write-Host "`nExecuting deployment remotely on $HostName ..."
        Invoke-Native -FilePath "ssh" -ArgumentList ($sshArgs + @("-l", $UserName, $HostName, $remoteCommand))
    }

    Write-Host "`n========================================"
    Write-Host " KRATOS DEPLOYMENT COMPLETE"
    Write-Host "========================================"
    Write-Host "Applications: $appsCsv"
    Write-Host "Host:         $HostName"
}
finally {
    if (Test-Path $bundleRoot) {
        Remove-Item $bundleRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
    if ($null -ne $bundleFile -and -not $KeepLocalBundle -and (Test-Path $bundleFile)) {
        Remove-Item $bundleFile -Force -ErrorAction SilentlyContinue
    }
}
