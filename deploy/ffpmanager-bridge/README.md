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
Configure the approved bridge DNS name (Kratos commissioning uses HTTPS port 5440),
Portal HTTPS callback origin, Linux-mounted document directory and real status/document
mappings. `Bridge__SupportingDocumentRoot` is the Linux write directory;
`Bridge__SupportingDocumentManagerRoot` is the Windows UNC path to the same directory.
For this mount those are `/mnt/ffpmanager-data/RequestPortalBridge` and
`\\nas01.fftpj.local\ffpmanager_data\RequestPortalBridge`. Quote the UNC value with
single quotes in the environment file. SQL stores the UNC path so FFP Manager on
Windows can open the document. UNC strings do not mount Windows shares on Linux.

Configure the bridge's own OpenBao workload with provider-instance settings:
CommonSecrets__Providers__PrimarySecrets__Settings__*. Store the SQL connection and
integration key under Common.Secrets names FfpManagerBridge:SqlConnectionString and
FfpManagerIntegration:SharedSecret. Confirm resolved workload/base paths against
Common.Secrets and the deployed policy; do not guess vault paths.

Provision the protected AppRole SecretId at
/etc/aegis-ffpmanager-bridge/openbao-secret-id, root:service-group mode 0640.
The template preserves it for restarts (DeleteSecretIdFileAfterRead=false). The
AppRole must support that restart model with approved use/TTL limits and rotation.
For one-time credentials, implement reprovisioning before each restart instead.

Install the approved matching PEM chain and key at the template paths under
/etc/aegis-ffpmanager-bridge/tls. Use root:aegis-ffpmanager-bridge ownership and 0640
permissions for the private key. Configure renewal. Trust the issuing CA on both
hosts; never disable TLS validation. This bootstrap deliberately uses direct HTTPS
Kestrel listeners. An existing proxy topology requires explicit changes to the
configuration and validator; it must not silently open an HTTP listener.

Grant SQL and share permissions to the intended identity. Integrated SQL/SMB
authentication additionally requires separately managed Kerberos tickets and
renewal; the bridge unit does not acquire them. The `storage.conf` template requires
the persistent `/mnt/ffpmanager-data` mount and binds the service to its lifetime.
Review any preserved custom drop-in against that template. Apply SQL migrations
only when necessary after fresh JMDB01/FFP inspection and a verified backup. Verify that FFP
Manager enqueues events in the business-change transaction, following RequestPortal's
docs/FFP-MANAGER-INTEGRATION-COMMISSIONING.md. The bootstrap/deployer runs no SQL.

Verify /usr/bin/dotnet has Microsoft.AspNetCore.App 10.x; Python 3, curl, OpenSSH,
systemd and trusted CA certificates are required. No package installation,
certificate issuance, secret creation, database grants, firewall/DNS edits or
sudoers changes are automated.

## Preflight and deploy

```bash
sudo bash scripts/Initialize-RequestPortalBridge-Kratos.sh --check
# Or through the already authorized root-owned deployment entrypoint:
sudo -n /usr/local/sbin/aegis-deploy-requestportal-bridge --check
```

The checker prints setting names, not values. It rejects incomplete templates,
HTTP listeners/callbacks, missing files and destructive bootstrap settings.
Administrative preflight drops to the service UID/GID before checking file access.
The deployer's `--check` uses `--read-only`: it verifies the exact writable NAS01
CIFS source, directory read access and permission bits without writing a probe.
Direct checker invocation without `--read-only` creates, fsyncs, and deletes one
unique temporary storage probe; do not use that form during read-only inspection.
The existing unit repeats its configured checks with a bounded
startup timeout. Wrong/missing mounts and failed NAS writes fail closed; no existing
NAS documents or ownership are changed. Preflight does not establish SQL/OpenBao
connectivity, certificate validity, or Windows client access to the stored UNC path.
The SQL runtime rejects targets other than JMDB01/FFP before connecting and requires
verified TLS; it also verifies the connected database and server before querying data.

When configuration or startup safety remains unresolved, install a reviewed Linux
publish directory without starting or enabling the bridge:

```bash
sudo -n /usr/local/sbin/aegis-deploy-requestportal-bridge --install-only --source /path/to/publish
```

This requires the service to be stopped, reports incomplete configuration, backs
up existing binaries, and verifies installed checksums (when SHA256SUMS exists),
permissions, native dependencies and .NET 10 runtimes as the service identity.
It can be repeated while configuration is incomplete. It does not establish
application health or the effective SQL target. Preserve the publish provenance
and source manifest when deploying a checkout with uncommitted changes.

For a fixed-scope commissioning inventory without rebuilding or reinstalling:

```bash
sudo -n /usr/local/sbin/aegis-deploy-requestportal-bridge --commissioning-check
```

The root-owned inspector reports configuration shape and file metadata, public
certificate SANs, deployed checksums and provenance. It does not print credential
values, authenticate another application's AppRole, connect to SQL, or start a
service. It drops to the bridge UID before testing trusted OpenBao/Portal HTTPS,
Kerberos ticket presence, and NAS access. Unlike `--check`, this inventory creates,
fsyncs and removes one uniquely named NAS probe; run it only when that access test
is authorized. A healthy OpenBao endpoint does not establish AppRole access.

If no suitable bridge certificate exists, prepare an internal-CA request while
the service is stopped:

```bash
sudo -n /usr/local/sbin/aegis-deploy-requestportal-bridge --prepare-tls-request
```

This exclusively creates missing `tls/privatekey.pem` and
`tls/kratos.fftpj.local.csr` under `/etc/aegis-ffpmanager-bridge`, with
root:service-group 0640 permissions. Existing files are preserved. It verifies
the CSR signature and key match, then runs the commissioning inventory. The JSON
includes the public CSR; private key contents never leave Kratos. Have the
approved CA issue a server certificate with SAN `DNS:kratos.fftpj.local`, install
the approved chain at `tls/fullchain.pem`, and verify trust, dates, SAN, key match
and service-user access before startup. A CSR is not an issued certificate.

### Bridge OpenBao commissioning

Kratos uses `https://openbao.ffpja.org` on port 443. DNS points at Kratos's
existing Nginx proxy, whose upstream is `https://loki.fftpj.local:8200`.
The upstream certificate covers `openbao.ffpja.org`, so Nginx must use that
`proxy_ssl_name`, SNI, `proxy_ssl_verify on`, and the trusted CA bundle.
Opening Kratos port 8200 or changing DNS is unnecessary.

The root-owned deployment entrypoint provides these fixed-scope operations:

```bash
sudo -n /usr/local/sbin/aegis-deploy-requestportal-bridge --openbao-check
sudo -n /usr/local/sbin/aegis-deploy-requestportal-bridge --secure-openbao-proxy
sudo -n /usr/local/sbin/aegis-deploy-requestportal-bridge --configure-openbao
```

The proxy operation changes only the OpenBao site, validates Nginx configuration,
reloads, verifies HTTPS health, and restores the site on failure. Configuration
updates only confirmed public settings, preserves a protected environment backup,
and adds bridge-UID search-only ACL access through `/etc/aegis` to the dedicated
`/etc/aegis/ffpmanager-bridge` directory. It preserves unknown MountPath/BasePath
settings until an authorized operator verifies the existing policy and KV mount.
It does not create roles, issue SecretIds, consume credentials, or open SQL.

The confirmed AppRole name and expected policy are `aegis-ffpmanager-bridge`.
The supplied RoleId is configuration, not authentication. Verify the role's
actual policies, RoleId, SecretId use count/TTL, and approved rotation workflow
through an existing authorized OpenBao operator session. Persistent file retention
alone cannot establish that future restarts can authenticate.

The requested SecretId destination is
`/etc/aegis/ffpmanager-bridge/openbao-secret-id`, root:service-group mode 0640.
From an authorized OpenBao administration shell, after verifying the role's
restart/rotation policy and the trusted SSH destination:

```bash
set +x
set -o pipefail
export BAO_ADDR=https://openbao.ffpja.org
bao write -field=secret_id -f auth/approle/role/aegis-ffpmanager-bridge/secret-id |
  ssh -T -l 'fwright@fftpj.local' kratos.fftpj.local \
    'sudo -n /usr/local/sbin/aegis-deploy-requestportal-bridge --install-openbao-secretid'
```

The installer accepts the generated UUID via protected stdin, refuses active-service
rotation or an existing destination, and writes exclusively with root:service-group
0640 permissions. No credential value appears in arguments or JSON output. It
does not authenticate using that SecretId. Never add shell tracing, `tee`, or an
echo of the generated value to this transfer. The operator needs existing OpenBao
administrative authentication and SSH/sudo authorization; none are bypassed.

Common.Secrets reads KV v2 `data.data.value` at
`/v1/<MountPath>/data/<BasePath>/FfpManagerBridge/SqlConnectionString` and
`/v1/<MountPath>/data/<BasePath>/FfpManagerIntegration/SharedSecret`.
Verify the policy's actual narrow paths before configuring BasePath. Do not copy
another application's AppRole, guess paths, or replace the shared integration
secret without coordinating Request Portal. The effective SQL value must pass
the existing JMDB01/FFP guard before opening any connection.

Before using the normal deployment command below, resolve the actual OpenBao SQL
secret without printing it and verify JMDB01/FFP before opening any SQL connection.
Use only guarded read-only queries to inspect schema and pending/locked/retryable
outbox rows. Normal Production startup immediately runs the outbox dispatcher,
which claims rows with UPDATE, posts callbacks, and updates delivery state. Verify
that processing those rows is authorized and safe before starting. The name
Production and a passing configuration preflight do not authorize these writes.

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
