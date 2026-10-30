# Build complete Studio bundles from the reviewed manifest without changing shared checkouts.
Set-StrictMode -Version Latest

function Remove-StudioPinnedDependencies {
    param([Parameter(Mandatory)]$Dependencies)
    foreach ($entry in $Dependencies.Worktrees) {
        & git -C $entry.Repository worktree remove --force $entry.Path | Out-Host
        if ($LASTEXITCODE -ne 0) { Write-Warning "Could not clean generated pinned worktree: $($entry.Path)" }
    }
    if (Test-Path $Dependencies.CommonRoot) {
        if (@(Get-ChildItem -LiteralPath $Dependencies.CommonRoot -Force).Count -eq 0) {
            Remove-Item -LiteralPath $Dependencies.CommonRoot
        }
    }
}

function New-StudioPinnedDependencies {
    param([Parameter(Mandatory)][string]$ProjectsRoot, [Parameter(Mandatory)][string]$StudioPath)
    $manifest = Get-Content -LiteralPath (Join-Path $StudioPath "security-baseline.json") -Raw | ConvertFrom-Json
    $commonRoot = Join-Path ([IO.Path]::GetTempPath()) ("studio-pinned-common-" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $commonRoot | Out-Null
    $worktrees = [Collections.Generic.List[object]]::new()
    $result = [pscustomobject]@{ CommonRoot = $commonRoot; Worktrees = $worktrees }
    try {
        $required = @("Common.Diagnostics", "Common.Messaging", "Common.Registration", "Common.Secrets", "Common.Security", "Common.Storage")
        $entries = @($manifest.commonLibraries.PSObject.Properties)
        if ($entries.Count -ne $required.Count) { throw "Studio dependency manifest is incomplete." }
        foreach ($entry in $entries) {
            if ($entry.Name -notin $required -or $entry.Value -notmatch '^[a-f0-9]{40}$') { throw "Invalid pinned dependency." }
            $repository = Join-Path (Join-Path $ProjectsRoot "Common") $entry.Name
            # The outer deployer has already synchronized clean authoritative main.
            # Unmerged coordinated source is never a deployable dependency.
            & git -C $repository merge-base --is-ancestor $entry.Value HEAD
            if ($LASTEXITCODE -ne 0) { throw "The approved pin is not merged in authoritative main: $($entry.Name)" }
            $path = Join-Path $commonRoot $entry.Name
            & git -C $repository worktree add --detach $path $entry.Value | Out-Host
            if ($LASTEXITCODE -ne 0) { throw "Could not prepare pinned dependency: $($entry.Name)" }
            $worktrees.Add([pscustomobject]@{ Repository = $repository; Path = $path })
        }
        $verify = Join-Path $commonRoot "Common.Security/scripts/verify-source-baseline.py"
        & python3 $verify (Join-Path $StudioPath "security-baseline.json") --common-root $commonRoot | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "Studio source pins failed verification." }
        return $result
    }
    catch {
        Remove-StudioPinnedDependencies -Dependencies $result
        throw
    }
}
