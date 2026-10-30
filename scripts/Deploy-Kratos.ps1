[CmdletBinding()]
param(
    [string]$HostName = "kratos",
    [string]$UserName = "fwright@fftpj.local",
    [ValidateSet("operations","configuration","diagnostics","hello","requestportal","studio","cafeteria","publicsite")]
    [string[]]$Applications = @("operations","configuration","diagnostics","hello"),
    [string]$ProjectsRoot = $(if ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) { "D:\Projects" } else { Join-Path $HOME "Projects" }),
    [string]$RemoteArchive = "/tmp/aegis-control-plane.tgz",
    [string]$RemoteWorkRoot = "/tmp/aegis-deploy",
    [string]$RemoteDeployCommand = "/usr/local/sbin/aegis-deploy",
    [string]$IdentityFile,
    [string]$CommonRoot,
    [string]$StudioDemoCutoverDirectory,
    [System.Management.Automation.PSCredential]$Credential,
    [switch]$SkipAuthoritativeBuild,
    [switch]$SkipPublishBuild,
    [string]$TargetRuntime = "linux-x64",
    [switch]$KeepLocalBundle,
    [switch]$KeepRemoteBundle
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Authoritative test setup belongs to Build-All-Aegis.ps1, not the deploy entrypoint.

function Invoke-Native {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][string[]]$ArgumentList
    )
    if ($FilePath -eq "dotnet" -and -not [string]::IsNullOrWhiteSpace($CommonRoot)) {
        $ArgumentList += "-p:CommonRoot=$CommonRoot"
    }
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
            "--runtime", $TargetRuntime,
            "--self-contained", "false",
            "--configuration", "Release",
            "--output", $Output,
            "--no-build",
            "--no-restore",
            "/p:BuildProjectReferences=false",
            "-m:1",
            "/nodeReuse:false",
            "/p:UseSharedCompilation=false"
        )
        return
    }

    Invoke-Native -FilePath "dotnet" -ArgumentList @(
        "restore", $Project,
        "--runtime", $TargetRuntime,
        "--disable-parallel",
        "-m:1",
        "/nodeReuse:false",
        "-p:RestoreDisableParallel=true",
        "/p:UseSharedCompilation=false"
    )

    Invoke-Native -FilePath "dotnet" -ArgumentList @(
        "publish", $Project,
        "--runtime", $TargetRuntime,
        "--self-contained", "false",
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
    cafeteria = @{
        Project = Join-Path $ProjectsRoot "Aegis.Cafeteria\Aegis.Cafeteria.Services\Aegis.Cafeteria.Services.csproj"
    }
    publicsite = @{
        Source = Join-Path (Join-Path $ProjectsRoot "Aegis.Studio") "Aegis.Studio.PublicSite"
    }
}

$Applications = $Applications | ForEach-Object { $_.ToLowerInvariant() } | Select-Object -Unique
if ($StudioDemoCutoverDirectory -and (@($Applications).Count -ne 1 -or $Applications[0] -ne "studio")) { throw "Demo cutover requires Studio-only deployment." }


if (-not $SkipAuthoritativeBuild) {
    Write-Host "`n========================================"
    Write-Host " AUTHORITATIVE AEGIS BUILD / TEST"
    Write-Host "========================================`n"
    $buildAll = Join-Path $PSScriptRoot "Build-All-Aegis.ps1"
    if (-not (Test-Path $buildAll)) { throw "Missing authoritative build script: $buildAll" }
    $controlPlaneApps = @("operations", "configuration", "diagnostics")
    $controlPlaneOnly = @($Applications | Where-Object { $_ -notin $controlPlaneApps }).Count -eq 0
    if ($controlPlaneOnly) {
        & $buildAll -Root $ProjectsRoot -ControlPlaneOnly
    } else {
        & $buildAll -Root $ProjectsRoot
    }
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
Copy-Item -Force (Join-Path $PSScriptRoot "Studio-Demo-Cutover.py") (Join-Path $bundleRoot "Studio-Demo-Cutover.py")

try {
    foreach ($app in $Applications) {
        $definition = $definitions[$app]
        if (-not $definition) { throw "Unsupported application: $app" }
        $output = Join-Path $payloadRoot $app
        New-Item -ItemType Directory -Force -Path $output | Out-Null

        Write-Host "`n========================================"
        Write-Host " STAGE $app"
        Write-Host "========================================"

        if ($app -eq "publicsite") {
            if (-not (Test-Path (Join-Path $definition.Source "package.json"))) {
                throw "Public Site package.json not found: $($definition.Source)"
            }
            Get-ChildItem -LiteralPath $definition.Source -Force |
                Where-Object { $_.Name -notin @("node_modules", ".env", "data", ".git") } |
                ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $output -Recurse -Force }
            foreach ($required in @("server.mjs", "intake-api.mjs", "intake-store.mjs", "public", "package.json")) {
                if (-not (Test-Path (Join-Path $output $required))) { throw "Public Site staging is missing $required" }
            }
        }
        else {
            if (-not (Test-Path $definition.Project)) { throw "Project not found: $($definition.Project)" }
            # --no-build is only safe when explicitly requested. An authoritative suite build
            # can validate the source, but project references may carry NoBuild state into
            # publish under .NET 10 and fail with NETSDK1085. Publish normally by default.
            Invoke-SafePublish -Project $definition.Project -Output $output -NoBuild:$SkipPublishBuild
        }
    }

    if ($StudioDemoCutoverDirectory) {
        $cutoverOutput = Join-Path $payloadRoot "studio-demo-cutover"
        New-Item -ItemType Directory -Path $cutoverOutput | Out-Null
        foreach ($name in @("appsettings.Demo.json", "studio-demo.env", "plan.json")) {
            $inputFile = Join-Path $StudioDemoCutoverDirectory $name
            if (-not (Test-Path -LiteralPath $inputFile -PathType Leaf)) { throw "Protected Demo cutover input is missing." }
            if (-not $IsWindows -and (([IO.File]::GetUnixFileMode($inputFile) -band 63) -ne 0)) { throw "Demo cutover inputs must be owner-only." }
            Copy-Item -LiteralPath $inputFile -Destination (Join-Path $cutoverOutput $name)
        }
        $planPath = Join-Path $cutoverOutput "plan.json"
        $plan = Get-Content -LiteralPath $planPath -Raw | ConvertFrom-Json
        $commit = (& git -C (Join-Path $ProjectsRoot "Aegis.Studio") rev-parse HEAD).Trim()
        if ($LASTEXITCODE -ne 0 -or $commit -notmatch '^[a-f0-9]{40}$') { throw "Cannot bind Demo maintenance to the published source." }
        $plan.expectedSourceCommit = $commit
        $plan | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $planPath -Encoding utf8NoBOM
        if (-not $IsWindows) { [IO.File]::SetUnixFileMode($planPath, [IO.UnixFileMode]::UserRead -bor [IO.UnixFileMode]::UserWrite) }
        $RemoteArchive = "/tmp/aegis-studio-demo-" + [Guid]::NewGuid().ToString("N") + ".tgz"
        $RemoteWorkRoot = "/tmp/aegis-studio-demo-" + [Guid]::NewGuid().ToString("N")
    }

    $bundleFile = Join-Path ([IO.Path]::GetTempPath()) ("aegis-control-plane-" + [Guid]::NewGuid().ToString("N") + ".tgz")
    # Create the archive owner-only before tar writes any protected environment data.
    $archive = [IO.File]::Open($bundleFile, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
    $archive.Dispose()
    if (-not $IsWindows) { [IO.File]::SetUnixFileMode($bundleFile, [IO.UnixFileMode]::UserRead -bor [IO.UnixFileMode]::UserWrite) }
    Push-Location $bundleRoot
    try {
        Invoke-Native -FilePath "tar" -ArgumentList @("-czf", $bundleFile, ".")
    }
    finally { Pop-Location }

    $remoteDisplay = "$UserName@$HostName"
    $appsCsv = ($Applications -join ",")
    $demoFlag = if ($StudioDemoCutoverDirectory) { " --studio-demo-cutover" } else { "" }

    # Build the remote shell program from individual lines joined with LF only.
    # PowerShell here-strings on Windows use CRLF, which causes bash to see tokens
    # such as `-e\r`. Keep this command explicitly Unix-normalized.
    $remoteLines = @(
        "set -e",
        "umask 077",
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
        "if sudo -n '$RemoteDeployCommand' --source '$RemoteWorkRoot/payload' --apps '$appsCsv'$demoFlag; then",
        "  deploy_rc=0",
        "else",
        "  deploy_rc=`$?",
        "fi"
    )
    if (-not $KeepRemoteBundle) {
        $remoteLines += "rm -f '$RemoteArchive'"
    }
    if ($StudioDemoCutoverDirectory -and -not $KeepRemoteBundle) { $remoteLines += "rm -rf '$RemoteWorkRoot'" }
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
    if ($SkipPublishBuild) {
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
