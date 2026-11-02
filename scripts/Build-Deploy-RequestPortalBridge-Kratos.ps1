[CmdletBinding()]
param(
    [uri]$BridgeBaseUrl,
    [string]$Root = $(if ($IsWindows) { 'D:\Projects' } else { Join-Path $HOME 'Projects' }),
    [string]$HostName = 'kratos',
    [string]$UserName = 'fwright@fftpj.local',
    [string]$IdentityFile,
    [string]$TargetRuntime = 'linux-x64',
    [switch]$SkipTests,
    [switch]$SkipPublishBuild,
    [switch]$KeepLocalBundle,
    [switch]$KeepRemoteBundle,
    [switch]$InitializeOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-Native {
    param([string]$FilePath, [string[]]$Arguments)
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$FilePath failed (exit $LASTEXITCODE)." }
}

function Quote-Shell {
    param([string]$Value)
    # Single-quote shell arguments; close/escape/reopen any embedded quote.
    return "'" + $Value.Replace("'", "'\''") + "'"
}

if (-not $InitializeOnly -and ($null -eq $BridgeBaseUrl -or -not $BridgeBaseUrl.IsAbsoluteUri -or $BridgeBaseUrl.Scheme -ne 'https' -or
    $BridgeBaseUrl.UserInfo -or $BridgeBaseUrl.Query -or $BridgeBaseUrl.Fragment -or
    $BridgeBaseUrl.AbsolutePath -ne '/')) {
    throw 'BridgeBaseUrl must be an absolute HTTPS origin, without credentials, path, query or fragment.'
}
if ($HostName -notmatch '^[A-Za-z0-9][A-Za-z0-9.-]*$') { throw 'Invalid SSH host name.' }

if ($InitializeOnly) {
    # Interactive first installation: sudo may prompt on the remote TTY.
    # Ordinary deployments still use the installed root-owned deployer only.
    $initScript = Join-Path $PSScriptRoot 'Initialize-RequestPortalBridge-Kratos.sh'
    $initAssets = Join-Path $PSScriptRoot '../deploy/ffpmanager-bridge'
    foreach ($required in @($initScript, $initAssets)) {
        if (-not (Test-Path -LiteralPath $required)) { throw "Missing bootstrap asset: $required" }
    }
    $initRoot = Join-Path ([IO.Path]::GetTempPath()) ('bridge-bootstrap-' + [guid]::NewGuid().ToString('N'))
    $initArchive = "$initRoot.tgz"
    $remoteInit = '/tmp/' + [IO.Path]::GetFileName($initRoot)
    $auth = @('-o', 'BatchMode=yes')
    if ($IdentityFile) { $auth += @('-i', $IdentityFile) }
    try {
        New-Item -ItemType Directory -Path (Join-Path $initRoot 'scripts') -Force | Out-Null
        New-Item -ItemType Directory -Path (Join-Path $initRoot 'deploy') -Force | Out-Null
        Copy-Item -LiteralPath $initScript -Destination (Join-Path $initRoot 'scripts')
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Deploy-RequestPortalBridge-Kratos-Server.sh') -Destination (Join-Path $initRoot 'scripts')
        Copy-Item -LiteralPath $initAssets -Destination (Join-Path $initRoot 'deploy/ffpmanager-bridge') -Recurse
        Invoke-Native 'tar' @('-czf', $initArchive, '-C', $initRoot, '.')
        Invoke-Native 'scp' ($auth + @('-o', "User=$UserName", $initArchive, "${HostName}:$remoteInit.tgz"))
        $qInit = Quote-Shell $remoteInit
        $qInitArchive = Quote-Shell "$remoteInit.tgz"
        $initCommand = @(
            'set -eu',
            ('trap ' + (Quote-Shell "rm -rf -- $qInit; rm -f -- $qInitArchive") + ' EXIT'),
            "mkdir -m 700 $qInit",
            "tar -xzf $qInitArchive -C $qInit",
            "sudo bash $qInit/scripts/Initialize-RequestPortalBridge-Kratos.sh"
        ) -join "`n"
        Invoke-Native 'ssh' ($auth + @('-t', '-l', $UserName, $HostName, $initCommand))
        Write-Host 'Bootstrap installed. Complete /etc/aegis/ffpmanager-bridge.env, TLS and secrets before normal deployment.'
    }
    finally {
        Remove-Item -LiteralPath $initRoot -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $initArchive -Force -ErrorAction SilentlyContinue
    }
    return
}

$project = Join-Path $Root 'RequestPortal/FfpManager.Bridge/FfpManager.Bridge.csproj'
$solution = Join-Path $Root 'RequestPortal/RequestPortal.slnx'
$serverScript = Join-Path $PSScriptRoot 'Deploy-RequestPortalBridge-Kratos-Server.sh'
foreach ($required in @($project, $solution, $serverScript)) {
    if (-not (Test-Path -LiteralPath $required)) { throw "Missing required file: $required" }
}
foreach ($command in @('dotnet', 'tar', 'ssh', 'scp')) {
    Get-Command $command -ErrorAction Stop | Out-Null
}

# Build the selected local checkout; never switch branches or pull over local work.
$bundleRoot = Join-Path ([IO.Path]::GetTempPath()) ('requestportal-bridge-' + [guid]::NewGuid().ToString('N'))
$archive = "$bundleRoot.tgz"
$remoteRoot = '/tmp/' + [IO.Path]::GetFileName($bundleRoot)
$remoteArchive = "$remoteRoot.tgz"
$sshArguments = @('-o', 'BatchMode=yes')
if ($IdentityFile) { $sshArguments += @('-i', $IdentityFile) }
# Fail before the expensive build and upload when root-owned deployment permission is absent.
# Ordinary deployments remain noninteractive; never attempt to bypass sudo.
Write-Host 'Checking Kratos bridge deployment authorization...' -ForegroundColor DarkCyan
$permissionProbe = 'test -x /usr/local/sbin/aegis-deploy-requestportal-bridge && sudo -n /usr/local/sbin/aegis-deploy-requestportal-bridge --help >/dev/null'
$identityProbe = 'printf "Remote Linux user: "; id -un; printf "Remote sudo authorization: "; sudo -n -l /usr/local/sbin/aegis-deploy-requestportal-bridge 2>&1'
& ssh @sshArguments -l $UserName $HostName $identityProbe
if ($LASTEXITCODE -ne 0) {
    throw "Unable to inspect remote SSH identity and sudo policy. Check the SSH user selection and effective sudoers rule."
}
& ssh @sshArguments -l $UserName $HostName $permissionProbe
if ($LASTEXITCODE -ne 0) {
    throw @'
Kratos has not authorized passwordless execution of the root-owned bridge deployer.
An administrator must first allow this EXACT deployment entrypoint via sudoers
(or arrange an equivalent privileged deployment agent). The script cannot safely
remove the remote sudo requirement. Bootstrap and TLS/OpenBao/SQL readiness are
separate requirements. Run sudo -l on Kratos to inspect granted permissions.
'@
}
# Fail early on a mismatched root-owned deployment helper. Do not spend time
# restoring and publishing a release that the guarded installer will reject.
$localHash = (Get-FileHash -LiteralPath $serverScript -Algorithm SHA256).Hash.ToLowerInvariant()
$remoteHash = & ssh @sshArguments -l $UserName $HostName 'sha256sum /usr/local/sbin/aegis-deploy-requestportal-bridge'
if ($LASTEXITCODE -ne 0) { throw 'Cannot verify installed root-owned bridge deployer.' }
$installedHash = ([string]($remoteHash | Select-Object -First 1)).Split(' ')[0].ToLowerInvariant()
if ($installedHash -ne $localHash) {
    throw @'
Installed bridge deployer does not match this source tree.
An administrator must back up and install the reviewed
Deploy-RequestPortalBridge-Kratos-Server.sh as
/usr/local/sbin/aegis-deploy-requestportal-bridge (owner root, mode 0755).
No build or release has been attempted.
'@
}

New-Item -ItemType Directory -Path (Join-Path $bundleRoot 'payload') -Force | Out-Null
try {
    Invoke-Native 'dotnet' @('restore', $solution, '--disable-parallel', '-m:1', '/nodeReuse:false')
    Invoke-Native 'dotnet' @('build', $solution, '-c', 'Release', '--no-restore', '-m:1', '/nodeReuse:false', '/p:UseSharedCompilation=false')
    if (-not $SkipTests) {
        Invoke-Native 'dotnet' @('test', $solution, '-c', 'Release', '--no-build', '--no-restore', '-m:1')
    } else { Write-Warning 'Tests skipped; deployment does not establish integration acceptance.' }

    $publishArguments = @('publish', $project, '-c', 'Release', '-r', $TargetRuntime,
        '--self-contained', 'false', '-o', (Join-Path $bundleRoot 'payload'), '-m:1',
        '/nodeReuse:false', '/p:UseSharedCompilation=false')
    if ($SkipPublishBuild) {
        # Requires a pre-existing Release build for this exact target runtime.
        $publishArguments += @('--no-build', '--no-restore', '/p:BuildProjectReferences=false')
    }
    Invoke-Native 'dotnet' $publishArguments
    Copy-Item -LiteralPath $serverScript -Destination (Join-Path $bundleRoot 'deploy.sh')
    Invoke-Native 'tar' @('-czf', $archive, '-C', $bundleRoot, '.')
    Invoke-Native 'scp' ($sshArguments + @('-o', "User=$UserName", $archive, "${HostName}:$remoteArchive"))

    # Installation uses a root-owned deployer provisioned by an administrator.
    # Never grant sudo permission to execute the uploaded, user-writable script.
    $qRoot = Quote-Shell $remoteRoot
    $qArchive = Quote-Shell $remoteArchive
    $qUrl = Quote-Shell $BridgeBaseUrl.AbsoluteUri.TrimEnd('/')
    $remoteLines = @(
        'set -eu',
        "mkdir -m 700 $qRoot",
        "tar -xzf $qArchive -C $qRoot",
        "cmp -s $qRoot/deploy.sh /usr/local/sbin/aegis-deploy-requestportal-bridge || { echo 'Install the matching root-owned bridge deployer before retrying.' >&2; exit 4; }",
        "sudo -n /usr/local/sbin/aegis-deploy-requestportal-bridge --source $qRoot/payload --probe-url $qUrl"
    )
    if (-not $KeepRemoteBundle) {
        # Preserve deployment exit status while cleaning up on both success/failure.
        $cleanup = "rm -rf -- $qRoot; rm -f -- $qArchive"
        $remoteLines = @('set -eu', ('trap ' + (Quote-Shell $cleanup) + ' EXIT')) + $remoteLines[1..($remoteLines.Count - 1)]
    }
    Invoke-Native 'ssh' ($sshArguments + @('-l', $UserName, $HostName, ($remoteLines -join "`n")))
    Write-Host 'Bridge deployment passed HTTPS health/readiness checks. End-to-end commissioning remains required.'
}
finally {
    if (-not $KeepLocalBundle) {
        Remove-Item -LiteralPath $bundleRoot -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $archive -Force -ErrorAction SilentlyContinue
    }
}
