[CmdletBinding()]
param(
    [string]$HostName = "kratos",
    [string]$UserName = "fwright@fftpj.local",
    [ValidateSet("operations","configuration","diagnostics","hello","requestportal","studio")]
    [string[]]$Applications = @("operations","configuration","diagnostics","hello"),
    [string]$ProjectsRoot = "D:\Projects",
    [string]$RemoteArchive = "/tmp/aegis-control-plane.tgz",
    [string]$RemoteWorkRoot = "/tmp/aegis-deploy",
    [string]$RemoteDeployCommand = "/usr/local/sbin/aegis-deploy",
    [string]$IdentityFile,
    [System.Management.Automation.PSCredential]$Credential,
    [switch]$SkipAuthoritativeBuild,
    [switch]$SkipPublishBuild,
    [switch]$KeepLocalBundle,
    [switch]$KeepRemoteBundle
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$testEnvironmentScript = Join-Path $PSScriptRoot "Set-CommonMessagingPostgres.ps1"
if (-not (Test-Path $testEnvironmentScript)) {
    throw "Missing test environment setup script: $testEnvironmentScript"
}

Write-Host "Loading shared Aegis test environment..." -ForegroundColor DarkCyan
& $testEnvironmentScript

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

function Reset-DotNetBuildServers {
    Write-Host "Resetting .NET build servers..." -ForegroundColor DarkGray
    & dotnet build-server shutdown | Out-Host
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "dotnet build-server shutdown returned exit code $LASTEXITCODE; continuing."
    }
}

function Invoke-SafePublish {
    param(
        [Parameter(Mandatory)][string]$Project,
        [Parameter(Mandatory)][string]$Output,
        [switch]$NoBuild
    )

    Reset-DotNetBuildServers

    if ($NoBuild) {
        Invoke-Native -FilePath "dotnet" -ArgumentList @(
            "publish", $Project,
            "--configuration", "Release",
            "--output", $Output,
            "--no-build",
            "--no-restore",
            "-m:1",
            "/nodeReuse:false",
            "/p:UseSharedCompilation=false"
        )
        return
    }

    Invoke-Native -FilePath "dotnet" -ArgumentList @(
        "restore", $Project,
        "--disable-parallel",
        "-m:1",
        "/nodeReuse:false",
        "-p:RestoreDisableParallel=true",
        "/p:UseSharedCompilation=false"
    )

    Invoke-Native -FilePath "dotnet" -ArgumentList @(
        "publish", $Project,
        "--configuration", "Release",
        "--output", $Output,
        "--no-restore",
        "-m:1",
        "/nodeReuse:false",
        "/p:UseSharedCompilation=false"
    )
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
    studio = @{
        Project = Join-Path $ProjectsRoot "Aegis.Studio\Aegis.Studio.Web\Aegis.Studio.Web.csproj"
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
        # --no-build is only safe when explicitly requested. An authoritative suite build
        # can validate the source, but project references may carry NoBuild state into
        # publish under .NET 10 and fail with NETSDK1085. Publish normally by default.
        Invoke-SafePublish -Project $definition.Project -Output $output -NoBuild:$SkipPublishBuild
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
        "elif [ -x '$RemoteDeployCommand' ] && cmp -s '$RemoteWorkRoot/Deploy-Kratos-Server.sh' '$RemoteDeployCommand'; then",
        "  echo 'Installed Aegis server deployer already matches the uploaded version.'",
        "else",
        "  echo 'ERROR: Cannot install the uploaded Aegis server deployer with passwordless sudo.' >&2",
        "  echo 'The installed deployer is missing or differs from the uploaded version; refusing to run stale deployment logic.' >&2",
        "  echo 'Install the uploaded Deploy-Kratos-Server.sh as $RemoteDeployCommand with root privileges, then retry.' >&2",
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
    if ($SkipPublishBuild -or (-not $SkipAuthoritativeBuild)) {
        Write-Host "Publish:      reused existing validated Release build outputs"
    } else {
        Write-Host "Publish:      serialized restore/publish"
    }
}
finally {
    if (Test-Path $bundleRoot) {
        Remove-Item $bundleRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
    if ($null -ne $bundleFile -and -not $KeepLocalBundle -and (Test-Path $bundleFile)) {
        Remove-Item $bundleFile -Force -ErrorAction SilentlyContinue
    }
}
