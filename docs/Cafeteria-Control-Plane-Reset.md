# Reset Cafeteria control-plane registrations on Kratos

This resets registration identity and active control-plane state. It does not reinstall Cafeteria binaries or modify employee accounts, balances, transactions, allocation history, or the FFP Manager database. Registration audit history, old logs, retired contracts, resolved alerts and closed flows are retained.

## Install the control-plane fix

After the Operations and Configuration cleanup pull requests have been merged, run on the Mac:

```zsh
cd ~/Projects/Aegis.Configuration
git checkout main
git pull --ff-only origin main

cd ~/Projects/Aegis.Operations
git checkout main
git pull --ff-only origin main

pwsh -Command '& ./scripts/Deploy-Kratos.ps1 -Applications @("operations","configuration")'
```

The deployment script runs the control-plane build/test workflow and publishes the two changed services. No Cafeteria business database reset is involved.

Log out of Operations and sign in again. In the browser, /auth/me must include Configuration.Manage to perform cleanup or approve registrations. Configuration.View is sufficient to read the lifecycle and Actions list. If the signed-in account is intentionally read-only, use an authorized administrator. Existing SuiteSecurity:AdminUsers and the authentication plugin's administrative groups determine management access; cleanup does not grant permissions.

## Stop the applications

Close every Cafeteria POS on Windows, Linux and macOS, including development/debug runs, and stop any running Cafeteria Services process. Two running copies with the same logical Instance ID can create another identity conflict.

If Services is installed on Kratos, identify its actual unit first:

```zsh
ssh -l 'fwright@fftpj.local' kratos "systemctl list-unit-files '*cafeteria*'"
```

Stop that unit using sudo systemctl stop followed by the exact unit name shown. Do not stop unrelated control-plane services.

## Clean up using the UI

1. Open Operations → Registration → Actions.
2. Select **Clean up Cafeteria**.
3. Confirm all Cafeteria applications have been stopped.
4. Type **CAFETERIA**, then enter the reason.
5. Wait for the completion message. If cleanup reports a failure, keep the applications stopped and retry before registering again.

The server retires every active registration whose ApplicationId is exactly Aegis.Cafeteria or begins Aegis.Cafeteria. (including Services, POS and POS.UI), plus the legacy Cafeteria.Services identity, in any state including IdentityConflict. Credentials and runtime leases are invalidated, active contracts are retired, current Operations application rows are removed, active flows are closed, and active alerts are resolved. A marker prevents delayed telemetry from resurrecting an unregistered Cafeteria entry. Other application families are unchanged.

Historical Cafeteria logs remain visible by design. They are not proof that cleanup failed.

## Back up local registration identities

Do this on every host that will register again, while its Cafeteria processes remain stopped. Back up the identity files rather than deleting the Cafeteria application or database directories.

Default Services identity:
- Linux/Kratos: /var/lib/aegis/cafeteria-services/registration-identity.json
- Windows: %ProgramData%\Aegis\Cafeteria.Services\registration-identity.json
- Aegis:Registration:IdentityFile overrides either default; use the configured location when present.

On Kratos, after verifying the configured identity location is the default, run:

```bash
sudo sh -c 'p=/var/lib/aegis/cafeteria-services/registration-identity.json; if [ -f "$p" ]; then mv -- "$p" "$p.before-reset-$(date +%Y%m%d-%H%M%S)"; fi'
```

Default POS identity directories:
- Windows: %ProgramData%\Aegis\Cafeteria\Registration
- Linux/macOS: /var/lib/aegis-cafeteria/registration

Each POS directory contains separate ApplicationId/InstanceId subdirectories with registration-identity.json files for POS and POS.UI. Rename the Registration directory to a dated backup using an elevated file manager or shell. Do not rename or remove the surrounding Cafeteria data directory.

For Services running on a development Mac, the default is the Linux Services path above unless Aegis:Registration:IdentityFile was overridden.

## Register the applications again

1. Start Cafeteria Services on its intended host using its normal start command (or sudo systemctl start with its actual unit name).
2. Open Registration → Actions and approve the new Pending Aegis.Cafeteria.Services entry. It should claim its credential and publish its contract automatically.
3. Start one POS at a time. Approve both its Pending POS and POS.UI registrations.
4. Repeat for the remaining terminals. Give each terminal a distinct TerminalId; its POS Instance ID is derived from TerminalId and platform. Do not run a second copy with the same logical identity.
5. Confirm Registered state, current health and configuration contract publication.

If Pending entries do not appear, inspect the application's logs for its configured Configuration URL and identity file access. Do not copy registration identity files between machines.

## Validation before deployment

```zsh
dotnet test ~/Projects/Aegis.Configuration/Aegis.Configuration.Tests/Aegis.Configuration.Tests.csproj
dotnet test ~/Projects/Aegis.Operations/Aegis.Operations.Tests/Aegis.Operations.Tests.csproj
```

Regression coverage includes reset from every non-purged lifecycle state, credential/lease invalidation, fresh pending registration, unrelated application isolation, idempotent cleanup, and signed Operations identity precedence over stale Configuration cookies. The UI script is also checked for permission gating, cancellation, typed confirmation and failure reporting.

Live verification on Kratos is still required after deployment.
