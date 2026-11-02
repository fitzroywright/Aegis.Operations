# Request Portal Bridge: bootstrap and deployment

Run from PowerShell 7 on Windows/macOS/Linux. The bridge is a separate application
in RequestPortal/FfpManager.Bridge. Use an explicit `Bridge__SqlTarget` (`Production` = JMDB01/FFPJM; `Rehearsal` = JMDB01/FFP) and a mounted
test document directory for commissioning. No application secrets belong in source.

## First install on Kratos

Preferred entrypoint, from Aegis.Operations on the Mac (PowerShell 7 must be on PATH):

```bash
pwsh ./scripts/Build-Deploy-RequestPortalBridge-Kratos.ps1 -Root "$HOME/Projects" -InitializeOnly
ssh -t -l 'fwright@fftpj.local' kratos 'sudoedit /etc/aegis/ffpmanager-bridge.env'
```

The first command uploads the bootstrap assets and invokes interactive remote sudo.
It creates the prerequisites but deliberately does not build or start the service.
Repeat it after bootstrap/deployer updates; existing environment and unit customizations
are preserved. Complete the dependencies below before the normal build/deploy command.

If performing installation manually on Kratos instead:

Copy these repository assets to Kratos together, retaining directory layout:
scripts/Initialize-RequestPortalBridge-Kratos.sh,
scripts/Deploy-RequestPortalBridge-Kratos-Server.sh,
deploy/ffpmanager-bridge/.

Run as administrator from the copied repository root:

```bash
sudo bash scripts/Initialize-RequestPortalBridge-Kratos.sh
sudoedit /etc/aegis/ffpmanager-bridge.env
```

Bootstrap creates the system user/group, configuration/TLS/state directories,
root-owned deployer, validator, systemd unit and mode-0600 environment template.
It reloads systemd without starting/enabling the service. Repeated runs preserve
existing configuration and customized units; they update the deployer/validator.
Review differences in a preserved unit, especially the ExecStartPre validator.

Complete every CHANGE_ME, including mapping key suffixes, with real agreed values.
Configure the approved bridge DNS name (template HTTPS port 5443), Portal HTTPS
callback origin, Linux-mounted test document directory and real status/document
mappings. UNC strings do not mount Windows shares on Linux.

Configure the bridge's own OpenBao workload with provider-instance settings:
CommonSecrets__Providers__PrimarySecrets__Settings__*. Store the SQL connection and
integration key under Common.Secrets names FfpManagerBridge:SqlConnectionString and
FfpManagerIntegration:SharedSecret. Confirm resolved workload/base paths against
Common.Secrets and the deployed policy; do not guess vault paths.

Provision the protected AppRole SecretId at
/etc/aegis/ffpmanager-bridge/openbao-secret-id, root:service-group mode 0640.
The template preserves it for restarts (DeleteSecretIdFileAfterRead=false). The
AppRole must support that restart model with approved use/TTL limits and rotation.
For one-time credentials, implement reprovisioning before each restart instead.

Install the approved matching PEM chain and key at the template paths under
/etc/aegis/ffpmanager-bridge/tls. Use root:aegis-ffpmanager-bridge ownership and 0640
permissions for the private key. Configure renewal. Trust the issuing CA on both
hosts; never disable TLS validation. This bootstrap deliberately uses direct HTTPS
Kestrel listeners. An existing proxy topology requires explicit changes to the
configuration and validator; it must not silently open an HTTP listener.

Grant SQL and share permissions to the intended identity. Integrated SQL/SMB
authentication additionally requires separately managed Kerberos tickets and
renewal; the bridge unit does not acquire them. Add mount dependencies to systemd
if needed. Install the required outbox/correlation SQL scripts and verify that FFP
Manager enqueues events in the business-change transaction, following RequestPortal's
docs/FFP-MANAGER-INTEGRATION-COMMISSIONING.md. The bootstrap/deployer runs no SQL.

Verify /usr/bin/dotnet has Microsoft.AspNetCore.App 10.x; Python 3, curl, OpenSSH,
systemd and trusted CA certificates are required. No package installation,
certificate issuance, secret creation, database grants, firewall/DNS edits or
sudoers changes are automated.

## Preflight and deploy

```bash
sudo bash scripts/Initialize-RequestPortalBridge-Kratos.sh --check
```

The checker prints setting names, not values. It rejects incomplete templates,
HTTP listeners/callbacks, missing files and destructive bootstrap settings.
The unit checks again as the runtime identity. Root preflight cannot establish
runtime permissions, SQL/OpenBao connectivity, certificate validity or share writes.

An administrator must authorize the deployment account to sudo only the root-owned
/usr/local/sbin/aegis-deploy-requestportal-bridge if using the PowerShell entrypoint.
Use visudo and validate with visudo -c. Never allow uploaded scripts to run as root
through passwordless sudo. The client requires an exact installed deployer match;
rerun bootstrap after updating its source.

From the development computer's Aegis.Operations checkout:

```powershell
$BridgeBaseUrl = Read-Host 'Actual HTTPS bridge origin, including port if not 443'
pwsh ./scripts/Build-Deploy-RequestPortalBridge-Kratos.ps1 -Root "$HOME/Projects" -BridgeBaseUrl $BridgeBaseUrl
```

Use -Root D:\Projects on Windows. The selected local checkouts must contain the
compatible RequestPortal and Common dependencies. The entrypoint does not pull,
switch or reset them. It restores/builds/tests, publishes Linux x64, uploads via
tar/scp and invokes the installed deployer. Default behavior does not skip tests.
-SkipPublishBuild requires existing runtime-specific Release outputs.

The deployer preserves environment/secrets/state/database/documents, restarts only
the bridge and checks HTTPS /health and /ready JSON in SQL mode without redirects
or certificate bypasses. It restores old binaries and prior active/stopped state
on failure; a failed initial install remains stopped. Backups stay under
/opt/aegis/backups for separate retention management.

## Acceptance

Configure Portal's bridge URL as HTTPS and its matching secret reference. Test
submission with attachment, one Application Letter, duplicate retries, callbacks,
information request/response and restart/network recovery. Inspect logs locally:

```bash
sudo systemctl status aegis-ffpmanager-bridge.service
sudo journalctl -u aegis-ffpmanager-bridge.service -n 80 --no-pager
```

Redact sensitive data before sharing logs. Only after acceptance, enable boot startup:

```bash
sudo systemctl enable aegis-ffpmanager-bridge.service
```

A configuration-contract file and diagnostics endpoints do not prove control-plane
registration. Verify registration/publication separately. HTTPS readiness does not
prove callbacks, storage writes, event wiring or complete acceptance.

## Script verification

```bash
bash -n scripts/Initialize-RequestPortalBridge-Kratos.sh scripts/Deploy-RequestPortalBridge-Kratos-Server.sh
python3 tests/BridgeBootstrapSmokeTests.py
python3 tests/BridgeDeploymentSmokeTests.py
```

Fixture checks do not prove actual certificates, credentials or external services.
PowerShell execution, .NET build/tests and Kratos deployment remain target checks.
