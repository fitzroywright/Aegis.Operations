[CmdletBinding()]
param(
    [Parameter(Mandatory)][uri]$BridgeBaseUrl,
    [string]$Root = $(if ($IsWindows) { 'D:\Projects' } else { Join-Path $HOME 'Projects' }),
    [string]$HostName = 'kratos',
    [string]$UserName = 'fwright@fftpj.local',
    [string]$IdentityFile,
    [string]$TargetRuntime = 'linux-x64',
    [switch]$SkipTests,
    [switch]$SkipPublishBuild,
    [switch]$KeepLocalBundle,
    [switch]$KeepRemoteBundle
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

if (-not $BridgeBaseUrl.IsAbsoluteUri -or $BridgeBaseUrl.Scheme -ne 'https' -or
    $BridgeBaseUrl.UserInfo -or $BridgeBaseUrl.Query -or $BridgeBaseUrl.Fragment -or
    $BridgeBaseUrl.AbsolutePath -ne '/') {
    throw 'BridgeBaseUrl must be an absolute HTTPS origin, without credentials, path, query or fragment.'
}
if ($HostName -notmatch '^[A-Za-z0-9][A-Za-z0-9.-]*$') { throw 'Invalid SSH host name.' }

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
